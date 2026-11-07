using System.Threading.Channels;
using fernien.Core;

namespace fernien.Transfer;

/// <summary>
/// Aktarımları sırayla yürüten kuyruk. Tek kablo üzerinde aynı anda tek iş yürütmek
/// en yüksek toplam hızı verir. Yönteme (ADB, Wi‑Fi...) bağımlı değildir; yalnızca IDeviceSession kullanır.
/// </summary>
public sealed class TransferQueue : IDisposable
{
    private const int MaxRemoteDepth = 64;

    private static readonly EnumerationOptions LocalEnumeration = new()
    {
        RecurseSubdirectories = true,
        IgnoreInaccessible = true,
        AttributesToSkip = FileAttributes.System
    };

    private static readonly char[] InvalidWindowsChars = Path.GetInvalidFileNameChars();

    private static readonly HashSet<string> ReservedWindowsNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
    };

    private readonly Channel<TransferJob> _channel =
        Channel.CreateUnbounded<TransferJob>(new UnboundedChannelOptions { SingleReader = true });
    private readonly CancellationTokenSource _shutdown = new();

    public TransferQueue() => _ = Task.Run(RunWorkerAsync);

    /// <summary>Kuyruğa yeni iş eklendiğinde (çağıran iş parçacığında).</summary>
    public event EventHandler<TransferJob>? JobAdded;

    /// <summary>İşin durumu değiştiğinde (arka plan iş parçacığında). İlerleme için periyodik olarak iş nesnesi okunmalıdır.</summary>
    public event EventHandler<TransferJob>? JobUpdated;

    public TransferJob Enqueue(IDeviceSession session, TransferDirection direction, IReadOnlyList<string> sources,
        string destinationDirectory, ConflictPolicy policy, bool openWhenDone = false)
    {
        var job = new TransferJob(session, direction, sources, destinationDirectory, policy) { OpenWhenDone = openWhenDone };
        JobAdded?.Invoke(this, job);
        _channel.Writer.TryWrite(job);
        return job;
    }

    public void Cancel(TransferJob job)
    {
        if (job.RequestCancel()) Raise(job);
    }

    private void Raise(TransferJob job) => JobUpdated?.Invoke(this, job);

    private async Task RunWorkerAsync()
    {
        try
        {
            await foreach (var job in _channel.Reader.ReadAllAsync(_shutdown.Token).ConfigureAwait(false))
            {
                if (!job.TryTransition(TransferStatus.Queued, TransferStatus.Preparing)) continue;
                Raise(job);
                await RunJobAsync(job).ConfigureAwait(false);
                Raise(job);
            }
        }
        catch (OperationCanceledException)
        {
            // Uygulama kapanıyor.
        }
    }

    private async Task RunJobAsync(TransferJob job)
    {
        var ct = job.Token;
        job.StartedAt = DateTime.Now;
        try
        {
            var plan = job.Direction == TransferDirection.ToDevice
                ? await PlanUploadAsync(job, ct).ConfigureAwait(false)
                : await PlanDownloadAsync(job, ct).ConfigureAwait(false);

            job.SetPlan(plan.Count, plan.Sum(item => item.Size));
            job.FirstOutputPath = plan.Count > 0 ? plan[0].Destination : null;
            job.StartedAt = DateTime.Now;
            job.SetStatus(TransferStatus.Running);
            Raise(job);

            foreach (var item in plan)
            {
                ct.ThrowIfCancellationRequested();
                job.CurrentFile = item.DisplayName;
                long itemTransferred = 0;
                void OnProgress(long bytes)
                {
                    itemTransferred += bytes;
                    job.AddTransferred(bytes);
                }

                try
                {
                    if (job.Direction == TransferDirection.ToDevice)
                    {
                        await job.Session.UploadFileAsync(item.Source, item.Destination, OnProgress, ct).ConfigureAwait(false);
                    }
                    else
                    {
                        await job.Session.DownloadFileAsync(item.Source, item.Destination, OnProgress, ct).ConfigureAwait(false);
                        try { File.SetLastWriteTimeUtc(item.Destination, item.ModifiedUtc); } catch { /* önemsiz */ }
                    }

                    // Planlanan boyut gerçek boyuttan farklıysa (dosya değişti vb.) toplamı düzelt.
                    if (itemTransferred != item.Size) job.AdjustTotal(itemTransferred - item.Size);
                    job.FileCompleted();
                }
                catch (Exception ex) when (!IsFatal(ex))
                {
                    job.FileFailed($"{item.DisplayName}: {ex.Message}", item.Size, itemTransferred);
                }
            }

            job.Finish(job.FailedFiles > 0 ? TransferStatus.CompletedWithErrors : TransferStatus.Completed);
        }
        catch (OperationCanceledException)
        {
            job.Finish(TransferStatus.Canceled);
        }
        catch (Exception ex)
        {
            job.Message = ex.Message;
            job.Finish(TransferStatus.Failed);
        }
    }

    private static bool IsFatal(Exception ex) =>
        ex is OperationCanceledException or ObjectDisposedException or DeviceException { IsConnectionLost: true };

    #region Planlama: PC → Telefon

    private static async Task<List<TransferItem>> PlanUploadAsync(TransferJob job, CancellationToken ct)
    {
        var session = job.Session;
        var items = new List<TransferItem>();
        var reserved = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (string source in job.Sources)
        {
            ct.ThrowIfCancellationRequested();
            if (File.Exists(source))
            {
                var file = new FileInfo(source);
                string? target = await ResolveRemoteAsync(session, RemotePath.Combine(job.DestinationDirectory, file.Name),
                    job.Policy, reserved, ct).ConfigureAwait(false);
                if (target is null) job.FileSkipped();
                else items.Add(new TransferItem(file.FullName, target, file.Length, file.LastWriteTimeUtc, file.Name));
            }
            else if (Directory.Exists(source))
            {
                var root = new DirectoryInfo(source);
                string remoteRoot = RemotePath.Combine(job.DestinationDirectory, ToRemoteName(root));
                job.CurrentFile = "Taranıyor: " + root.Name;

                // Hedef klasör yoksa çakışma olamaz; her dosya için cihaza sormaya gerek yok.
                bool checkConflicts = await session.StatAsync(remoteRoot, ct).ConfigureAwait(false) is not null;

                foreach (var file in root.EnumerateFiles("*", LocalEnumeration))
                {
                    ct.ThrowIfCancellationRequested();
                    string relative = Path.GetRelativePath(root.FullName, file.FullName).Replace('\\', '/');
                    string candidate = RemotePath.Combine(remoteRoot, relative);
                    string? target = checkConflicts
                        ? await ResolveRemoteAsync(session, candidate, job.Policy, reserved, ct).ConfigureAwait(false)
                        : candidate;
                    if (target is null) job.FileSkipped();
                    else items.Add(new TransferItem(file.FullName, target, file.Length, file.LastWriteTimeUtc, root.Name + "/" + relative));
                }

                // Boş klasörler SEND ile oluşmaz; ayrıca oluşturulur.
                var emptyDirectories = new List<string>();
                foreach (var directory in root.EnumerateDirectories("*", LocalEnumeration).Prepend(root))
                {
                    if (!IsEmptyDirectory(directory)) continue;
                    emptyDirectories.Add(directory.FullName == root.FullName
                        ? remoteRoot
                        : RemotePath.Combine(remoteRoot, Path.GetRelativePath(root.FullName, directory.FullName).Replace('\\', '/')));
                }
                if (emptyDirectories.Count > 0)
                    await session.CreateDirectoriesAsync(emptyDirectories, ct).ConfigureAwait(false);
            }
            else
            {
                job.FileFailed($"Kaynak bulunamadı: {source}", 0, 0);
            }
        }
        return items;
    }

    private static async Task<string?> ResolveRemoteAsync(IDeviceSession session, string path, ConflictPolicy policy,
        HashSet<string> reserved, CancellationToken ct)
    {
        async Task<bool> ExistsAsync(string p) =>
            reserved.Contains(p) || await session.StatAsync(p, ct).ConfigureAwait(false) is not null;

        if (policy == ConflictPolicy.Overwrite || !await ExistsAsync(path).ConfigureAwait(false))
        {
            reserved.Add(path);
            return path;
        }
        if (policy == ConflictPolicy.Skip) return null;

        string directory = RemotePath.GetParent(path);
        string name = RemotePath.GetName(path);
        string stem = Path.GetFileNameWithoutExtension(name), extension = Path.GetExtension(name);
        for (int i = 1; i < 10_000; i++)
        {
            string candidate = RemotePath.Combine(directory, $"{stem} ({i}){extension}");
            if (!await ExistsAsync(candidate).ConfigureAwait(false))
            {
                reserved.Add(candidate);
                return candidate;
            }
        }
        return null;
    }

    private static bool IsEmptyDirectory(DirectoryInfo directory)
    {
        try { return !directory.EnumerateFileSystemInfos().Any(); }
        catch { return false; }
    }

    private static string ToRemoteName(DirectoryInfo directory)
    {
        if (directory.Parent is null) // sürücü kökü, ör. "D:\"
            return "Disk " + directory.FullName.TrimEnd('\\', '/', ':');
        return directory.Name;
    }

    #endregion

    #region Planlama: Telefon → PC

    private static async Task<List<TransferItem>> PlanDownloadAsync(TransferJob job, CancellationToken ct)
    {
        var session = job.Session;
        var items = new List<TransferItem>();
        var reserved = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (string source in job.Sources)
        {
            ct.ThrowIfCancellationRequested();
            var entry = await session.StatAsync(source, ct).ConfigureAwait(false);
            if (entry is null)
            {
                job.FileFailed($"Kaynak bulunamadı: {source}", 0, 0);
                continue;
            }

            string localTarget = Path.Combine(job.DestinationDirectory, ToWindowsName(entry.Name));
            if (!entry.IsDirectory)
            {
                string? target = ResolveLocal(localTarget, job.Policy, reserved);
                if (target is null) job.FileSkipped();
                else items.Add(new TransferItem(entry.FullPath, target, entry.Size, entry.ModifiedUtc, entry.Name));
                continue;
            }

            bool checkConflicts = Directory.Exists(localTarget);
            var pending = new Stack<(string Remote, string Local, string Relative, int Depth)>();
            pending.Push((entry.FullPath, localTarget, entry.Name, 0));

            while (pending.Count > 0)
            {
                ct.ThrowIfCancellationRequested();
                var (remoteDir, localDir, relative, depth) = pending.Pop();
                job.CurrentFile = "Taranıyor: " + relative;

                var children = await session.ListAsync(remoteDir, ct).ConfigureAwait(false);
                if (children.Count == 0) Directory.CreateDirectory(localDir);

                foreach (var child in children)
                {
                    string localChild = Path.Combine(localDir, ToWindowsName(child.Name));
                    string relativeChild = relative + "/" + child.Name;
                    if (child.IsDirectory)
                    {
                        if (depth < MaxRemoteDepth) pending.Push((child.FullPath, localChild, relativeChild, depth + 1));
                        continue;
                    }

                    string? target = checkConflicts ? ResolveLocal(localChild, job.Policy, reserved) : localChild;
                    if (target is null) job.FileSkipped();
                    else items.Add(new TransferItem(child.FullPath, target, child.Size, child.ModifiedUtc, relativeChild));
                }
            }
        }
        return items;
    }

    private static string? ResolveLocal(string path, ConflictPolicy policy, HashSet<string> reserved)
    {
        bool Exists(string p) => reserved.Contains(p) || File.Exists(p) || Directory.Exists(p);

        if (policy == ConflictPolicy.Overwrite || !Exists(path))
        {
            reserved.Add(path);
            return path;
        }
        if (policy == ConflictPolicy.Skip) return null;

        string directory = Path.GetDirectoryName(path) ?? "";
        string stem = Path.GetFileNameWithoutExtension(path), extension = Path.GetExtension(path);
        for (int i = 1; i < 10_000; i++)
        {
            string candidate = Path.Combine(directory, $"{stem} ({i}){extension}");
            if (!Exists(candidate))
            {
                reserved.Add(candidate);
                return candidate;
            }
        }
        return null;
    }

    /// <summary>Android'de geçerli olup Windows'ta geçersiz olan adları düzeltir.</summary>
    internal static string ToWindowsName(string name)
    {
        var chars = name.Select(c => Array.IndexOf(InvalidWindowsChars, c) >= 0 ? '_' : c).ToArray();
        string result = new string(chars).TrimEnd(' ', '.');
        if (result.Length == 0) result = "_";
        if (ReservedWindowsNames.Contains(Path.GetFileNameWithoutExtension(result))) result = "_" + result;
        return result;
    }

    #endregion

    public void Dispose()
    {
        _channel.Writer.TryComplete();
        _shutdown.Cancel();
    }

    private sealed record TransferItem(string Source, string Destination, long Size, DateTime ModifiedUtc, string DisplayName);
}
