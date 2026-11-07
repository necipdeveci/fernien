using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Text;
using fernien.Core;

namespace fernien.Providers.Adb;

/// <summary>
/// ADB ile bağlı bir Android cihaz üzerindeki dosya işlemleri.
/// Sync bağlantıları havuzlanır: aktarım sürerken gezinme ayrı bir bağlantı kullanır,
/// çok sayıda küçük dosyada her dosya için yeniden bağlantı kurulmaz.
/// </summary>
internal sealed class AdbDeviceSession : IDeviceSession
{
    private const int DefaultFileMode = 0x81A4; // S_IFREG | 0644
    private const string ExitCodeMarker = "__FERNIEN_RC=";

    private static readonly string[] ProtectedPaths =
        ["/", "/sdcard", "/storage", "/storage/emulated", "/storage/emulated/0", "/data", "/system"];

    private readonly AdbClient _client;
    private readonly ConcurrentBag<AdbSyncService> _pool = [];
    private readonly Lazy<Task<bool>> _useV2;
    private volatile bool _disposed;

    public AdbDeviceSession(AdbClient client, DeviceInfo device)
    {
        _client = client;
        Device = device;
        _useV2 = new Lazy<Task<bool>>(DetectV2Async);
    }

    public DeviceInfo Device { get; }

    public string HomePath => "/sdcard";

    public IReadOnlyList<PathShortcut> Shortcuts { get; } =
    [
        new("Dahili depolama", "/sdcard"),
        new("Kamera (DCIM)", "/sdcard/DCIM/Camera"),
        new("Ekran görüntüleri", "/sdcard/Pictures/Screenshots"),
        new("İndirilenler", "/sdcard/Download"),
        new("Resimler", "/sdcard/Pictures"),
        new("Belgeler", "/sdcard/Documents"),
        new("Müzik", "/sdcard/Music"),
        new("Filmler", "/sdcard/Movies"),
        new("WhatsApp medya", "/sdcard/Android/media/com.whatsapp/WhatsApp/Media"),
        new("Kök dizin (/)", "/")
    ];

    private async Task<bool> DetectV2Async()
    {
        try
        {
            var features = await _client.GetFeaturesAsync(Device.Id, CancellationToken.None).ConfigureAwait(false);
            return features.Contains("stat_v2") && features.Contains("ls_v2");
        }
        catch
        {
            return false;
        }
    }

    #region Sync bağlantı havuzu

    private async Task<T> UseSyncAsync<T>(Func<AdbSyncService, Task<T>> action, CancellationToken ct)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        AdbSyncService? sync = null;
        try
        {
            if (!_pool.TryTake(out sync))
                sync = await AdbSyncService.OpenAsync(_client, Device.Id, await _useV2.Value.ConfigureAwait(false), ct).ConfigureAwait(false);

            T result = await action(sync).ConfigureAwait(false);

            if (_disposed) sync.Dispose();
            else _pool.Add(sync);
            return result;
        }
        catch (Exception ex)
        {
            sync?.Dispose(); // Hata sonrası protokol durumu belirsiz; bağlantı atılır.
            if (ex is OperationCanceledException) throw;
            if (ct.IsCancellationRequested) throw new OperationCanceledException(ct);
            throw Translate(ex);
        }
    }

    private Task UseSyncAsync(Func<AdbSyncService, Task> action, CancellationToken ct) =>
        UseSyncAsync(async sync =>
        {
            await action(sync).ConfigureAwait(false);
            return true;
        }, ct);

    private static Exception Translate(Exception ex) => ex switch
    {
        DeviceException => ex,
        AdbException adb when IsDeviceGone(adb.Message) =>
            new DeviceException("Cihaz bağlantısı kesildi. Kabloyu ve telefondaki izinleri kontrol edin.", adb, connectionLost: true),
        AdbException adb => new DeviceException(adb.Message, adb),
        SocketException or EndOfStreamException or ObjectDisposedException or IOException { InnerException: SocketException } =>
            new DeviceException("Cihaz bağlantısı kesildi. Kabloyu kontrol edin.", ex, connectionLost: true),
        _ => ex
    };

    private static bool IsDeviceGone(string message) =>
        message.StartsWith("device", StringComparison.OrdinalIgnoreCase) &&
        (message.Contains("not found") || message.Contains("offline") || message.Contains("unauthorized"));

    #endregion

    private static async Task<SyncStat?> ResolveStatAsync(AdbSyncService sync, string path, CancellationToken ct)
    {
        var stat = await sync.StatAsync(path, ct).ConfigureAwait(false);
        // Eski protokolde STAT sembolik bağı takip etmez; sondaki "/" ile hedef klasör çözülür.
        if (stat is { IsSymlink: true })
            stat = await sync.StatAsync(path.TrimEnd('/') + "/", ct).ConfigureAwait(false) ?? stat;
        return stat;
    }

    public Task<IReadOnlyList<RemoteEntry>> ListAsync(string path, CancellationToken ct = default) =>
        UseSyncAsync<IReadOnlyList<RemoteEntry>>(async sync =>
        {
            var directory = await ResolveStatAsync(sync, path, ct).ConfigureAwait(false);
            if (directory is null) throw new DeviceException($"Klasör bulunamadı: {path}");
            if (!directory.IsDirectory && !directory.IsSymlink) throw new DeviceException($"Bu bir klasör değil: {path}");

            var entries = await sync.ListAsync(path, ct).ConfigureAwait(false);
            var result = new List<RemoteEntry>(entries.Count);
            foreach (var entry in entries)
            {
                string fullPath = RemotePath.Combine(path, entry.Name);
                var stat = entry.IsSymlink ? await ResolveStatAsync(sync, fullPath, ct).ConfigureAwait(false) ?? entry : entry;
                result.Add(new RemoteEntry(entry.Name, fullPath, stat.IsDirectory, stat.IsDirectory ? 0 : stat.Size, stat.ModifiedUtc));
            }
            return result;
        }, ct);

    public Task<RemoteEntry?> StatAsync(string path, CancellationToken ct = default) =>
        UseSyncAsync(async sync =>
        {
            var stat = await ResolveStatAsync(sync, path, ct).ConfigureAwait(false);
            return stat is null
                ? null
                : new RemoteEntry(RemotePath.GetName(path), path, stat.IsDirectory, stat.IsDirectory ? 0 : stat.Size, stat.ModifiedUtc);
        }, ct);

    public async Task UploadFileAsync(string localPath, string remotePath, Action<long> progress, CancellationToken ct = default)
    {
        var info = new FileInfo(localPath);
        await using var stream = new FileStream(localPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete,
            bufferSize: 0, FileOptions.SequentialScan | FileOptions.Asynchronous);

        bool started = false;
        try
        {
            await UseSyncAsync(sync =>
            {
                started = true;
                return sync.PushAsync(stream, remotePath, DefaultFileMode, info.LastWriteTimeUtc, progress, ct);
            }, ct).ConfigureAwait(false);
        }
        catch when (started)
        {
            await TryRemovePartialAsync(remotePath).ConfigureAwait(false);
            throw;
        }
    }

    public async Task DownloadFileAsync(string remotePath, string localPath, Action<long> progress, CancellationToken ct = default)
    {
        string? directory = Path.GetDirectoryName(localPath);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

        try
        {
            await using var stream = new FileStream(localPath, FileMode.Create, FileAccess.Write, FileShare.None,
                bufferSize: 256 * 1024, FileOptions.SequentialScan | FileOptions.Asynchronous);
            await UseSyncAsync(sync => sync.PullAsync(remotePath, stream, progress, ct), ct).ConfigureAwait(false);
        }
        catch
        {
            try { File.Delete(localPath); } catch { /* yarım dosya silinemedi */ }
            throw;
        }
    }

    private async Task TryRemovePartialAsync(string remotePath)
    {
        if (_disposed) return;
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await _client.ShellAsync(Device.Id, "rm -f " + RemotePath.ShellQuote(remotePath), timeout.Token).ConfigureAwait(false);
        }
        catch
        {
            // Bağlantı yoksa temizlik yapılamaz.
        }
    }

    public async Task CreateDirectoriesAsync(IReadOnlyCollection<string> paths, CancellationToken ct = default)
    {
        var command = new StringBuilder();
        foreach (string path in paths)
        {
            string argument = " " + RemotePath.ShellQuote(path);
            if (command.Length > 0 && command.Length + argument.Length > 3000)
            {
                await ShellCheckedAsync("mkdir -p" + command, ct).ConfigureAwait(false);
                command.Clear();
            }
            command.Append(argument);
        }
        if (command.Length > 0) await ShellCheckedAsync("mkdir -p" + command, ct).ConfigureAwait(false);
    }

    public async Task DeleteAsync(string path, CancellationToken ct = default)
    {
        if (ProtectedPaths.Contains(RemotePath.Normalize(path), StringComparer.Ordinal))
            throw new DeviceException("Bu konum güvenlik nedeniyle silinemez.");
        await ShellCheckedAsync("rm -rf " + RemotePath.ShellQuote(path), ct).ConfigureAwait(false);
    }

    public async Task RenameAsync(string path, string newPath, CancellationToken ct = default)
    {
        if (await StatAsync(newPath, ct).ConfigureAwait(false) is not null)
            throw new DeviceException("Bu adda bir öğe zaten var.");
        await ShellCheckedAsync($"mv {RemotePath.ShellQuote(path)} {RemotePath.ShellQuote(newPath)}", ct).ConfigureAwait(false);
    }

    public async Task<StorageInfo?> GetStorageInfoAsync(CancellationToken ct = default)
    {
        string output;
        try
        {
            output = await _client.ShellAsync(Device.Id, "df -k /sdcard/ 2>/dev/null", ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return null;
        }

        string[] lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (lines.Length < 2) return null;
        string[] parts = lines[^1].Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 4 && long.TryParse(parts[1], out long total) && long.TryParse(parts[3], out long available))
            return new StorageInfo(total * 1024, available * 1024);
        return null;
    }

    /// <summary>Kabuk komutunu çalıştırır; çıkış kodu sıfır değilse çıktıyı hata olarak fırlatır.</summary>
    private async Task ShellCheckedAsync(string command, CancellationToken ct)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        string output;
        try
        {
            output = await _client.ShellAsync(Device.Id, $"{command} 2>&1; echo \"{ExitCodeMarker}$?\"", ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw Translate(ex);
        }

        int index = output.LastIndexOf(ExitCodeMarker, StringComparison.Ordinal);
        if (index < 0) throw new DeviceException("Cihaz komutunun sonucu okunamadı.");
        string exitCode = output[(index + ExitCodeMarker.Length)..].Trim();
        if (exitCode == "0") return;

        string message = output[..index].Trim();
        throw new DeviceException(string.IsNullOrEmpty(message) ? $"Cihaz komutu başarısız oldu (kod {exitCode})." : message);
    }

    public void Dispose()
    {
        _disposed = true;
        while (_pool.TryTake(out var sync)) sync.Dispose();
    }
}
