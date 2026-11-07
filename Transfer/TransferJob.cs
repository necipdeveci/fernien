using System.Diagnostics;
using fernien.Core;

namespace fernien.Transfer;

public enum TransferDirection
{
    ToDevice,
    ToComputer
}

/// <summary>Sıralama önemlidir: Completed ve sonrası "bitmiş" sayılır.</summary>
public enum TransferStatus
{
    Queued,
    Preparing,
    Running,
    Completed,
    CompletedWithErrors,
    Failed,
    Canceled
}

/// <summary>Hedefte aynı adda öğe varsa ne yapılacağı.</summary>
public enum ConflictPolicy
{
    Rename,
    Overwrite,
    Skip
}

/// <summary>Bir kullanıcı eylemine karşılık gelen aktarım (bir veya birden çok dosya/klasör).</summary>
public sealed class TransferJob
{
    private readonly CancellationTokenSource _cts = new();
    private int _status;
    private long _totalBytes;
    private long _transferredBytes;
    private int _totalFiles;
    private int _completedFiles;
    private int _failedFiles;
    private int _skippedFiles;

    private long _sampleBytes;
    private long _sampleTimestamp;

    internal TransferJob(IDeviceSession session, TransferDirection direction, IReadOnlyList<string> sources,
        string destinationDirectory, ConflictPolicy policy)
    {
        if (sources.Count == 0) throw new ArgumentException("En az bir kaynak gerekli.", nameof(sources));
        Session = session;
        Direction = direction;
        Sources = sources;
        DestinationDirectory = destinationDirectory;
        Policy = policy;

        string first = direction == TransferDirection.ToDevice
            ? Path.GetFileName(sources[0].TrimEnd('\\', '/'))
            : RemotePath.GetName(sources[0]);
        if (string.IsNullOrEmpty(first)) first = sources[0];
        DisplayName = sources.Count == 1 ? first : $"{first} ve {sources.Count - 1} öğe daha";
    }

    public IDeviceSession Session { get; }
    public TransferDirection Direction { get; }
    public IReadOnlyList<string> Sources { get; }
    public string DestinationDirectory { get; }
    public ConflictPolicy Policy { get; }
    public string DisplayName { get; }

    /// <summary>Tek dosya indirildiğinde iş bitince dosya açılsın mı?</summary>
    public bool OpenWhenDone { get; init; }

    public TransferStatus Status => (TransferStatus)Volatile.Read(ref _status);
    public bool IsFinished => Status >= TransferStatus.Completed;
    public bool IsActive => Status is TransferStatus.Preparing or TransferStatus.Running;

    public string? Message { get; internal set; }
    public string? CurrentFile { get; internal set; }
    public string? FirstOutputPath { get; internal set; }
    public DateTime? StartedAt { get; internal set; }
    public DateTime? FinishedAt { get; internal set; }

    public long TotalBytes => Interlocked.Read(ref _totalBytes);
    public long TransferredBytes => Interlocked.Read(ref _transferredBytes);
    public int TotalFiles => Volatile.Read(ref _totalFiles);
    public int CompletedFiles => Volatile.Read(ref _completedFiles);
    public int FailedFiles => Volatile.Read(ref _failedFiles);
    public int SkippedFiles => Volatile.Read(ref _skippedFiles);

    internal CancellationToken Token => _cts.Token;

    internal bool TryTransition(TransferStatus from, TransferStatus to) =>
        Interlocked.CompareExchange(ref _status, (int)to, (int)from) == (int)from;

    internal void SetStatus(TransferStatus status) => Volatile.Write(ref _status, (int)status);

    internal void Finish(TransferStatus status)
    {
        FinishedAt = DateTime.Now;
        CurrentFile = null;
        SetStatus(status);
    }

    /// <summary>İptal ister; iş henüz başlamadıysa hemen "İptal edildi" olur.</summary>
    internal bool RequestCancel()
    {
        if (IsFinished) return false;
        _cts.Cancel();
        if (TryTransition(TransferStatus.Queued, TransferStatus.Canceled)) FinishedAt = DateTime.Now;
        return true;
    }

    internal void SetPlan(int files, long bytes)
    {
        Volatile.Write(ref _totalFiles, files);
        Interlocked.Exchange(ref _totalBytes, bytes);
    }

    internal void AddTransferred(long bytes) => Interlocked.Add(ref _transferredBytes, bytes);
    internal void AdjustTotal(long delta) => Interlocked.Add(ref _totalBytes, delta);
    internal void FileCompleted() => Interlocked.Increment(ref _completedFiles);
    internal void FileSkipped() => Interlocked.Increment(ref _skippedFiles);

    internal void FileFailed(string message, long plannedSize, long transferred)
    {
        Interlocked.Increment(ref _failedFiles);
        Message = message;
        AdjustTotal(-plannedSize);
        AddTransferred(-transferred);
    }

    #region Hız ölçümü (yalnızca UI iş parçacığından çağrılır)

    public double BytesPerSecond { get; private set; }

    public void SampleSpeed()
    {
        long now = Stopwatch.GetTimestamp();
        long bytes = TransferredBytes;
        if (_sampleTimestamp == 0)
        {
            _sampleTimestamp = now;
            _sampleBytes = bytes;
            return;
        }

        double seconds = (now - _sampleTimestamp) / (double)Stopwatch.Frequency;
        if (seconds < 0.2) return;

        double instant = Math.Max(0, (bytes - _sampleBytes) / seconds);
        BytesPerSecond = BytesPerSecond <= 0 ? instant : BytesPerSecond * 0.7 + instant * 0.3;
        _sampleBytes = bytes;
        _sampleTimestamp = now;
    }

    public double AverageBytesPerSecond
    {
        get
        {
            if (StartedAt is not { } start) return 0;
            double seconds = ((FinishedAt ?? DateTime.Now) - start).TotalSeconds;
            return seconds > 0.05 ? TransferredBytes / seconds : 0;
        }
    }

    public TimeSpan? EstimatedRemaining
    {
        get
        {
            if (Status != TransferStatus.Running || BytesPerSecond < 1) return null;
            long remaining = Math.Max(0, TotalBytes - TransferredBytes);
            return TimeSpan.FromSeconds(Math.Min(remaining / BytesPerSecond, TimeSpan.FromDays(30).TotalSeconds));
        }
    }

    #endregion
}
