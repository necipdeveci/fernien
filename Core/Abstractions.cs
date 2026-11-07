namespace fernien.Core;

/// <summary>Bir cihazın bağlantı durumu.</summary>
public enum DeviceState
{
    Ready,
    Unauthorized,
    Offline,
    Connecting,
    Unknown
}

/// <summary>Bir sağlayıcının (ör. USB/ADB) gördüğü cihaz.</summary>
public sealed record DeviceInfo(IDeviceProvider Provider, string Id, string Name, string Transport, DeviceState State)
{
    /// <summary>Sağlayıcılar arasında benzersiz anahtar.</summary>
    public string Key => $"{Provider.Name}|{Id}";
}

/// <summary>Cihaz üzerindeki bir dosya veya klasör.</summary>
public sealed record RemoteEntry(string Name, string FullPath, bool IsDirectory, long Size, DateTime ModifiedUtc);

public sealed record StorageInfo(long TotalBytes, long FreeBytes);

public sealed record PathShortcut(string Title, string Path);

/// <summary>
/// Bir aktarım yöntemi (kablo, Wi‑Fi, Bluetooth...). Yeni bir yöntem eklemek için
/// bu arayüzü uygulayıp MainForm içindeki sağlayıcı listesine eklemek yeterlidir.
/// </summary>
public interface IDeviceProvider : IDisposable
{
    /// <summary>Kullanıcıya gösterilen yöntem adı.</summary>
    string Name { get; }

    /// <summary>Cihaz takıldığında, çıkarıldığında veya durumu değiştiğinde tetiklenir (arka plan iş parçacığında).</summary>
    event EventHandler? DevicesChanged;

    Task InitializeAsync(CancellationToken ct = default);

    Task<IReadOnlyList<DeviceInfo>> GetDevicesAsync(CancellationToken ct = default);

    IDeviceSession OpenSession(DeviceInfo device);

    /// <summary>Cihaz yokken veya hazır değilken gösterilecek yardım metni.</summary>
    string GetConnectionHelp(DeviceInfo? device);
}

/// <summary>Hazır durumdaki bir cihazla yapılan dosya işlemleri. Yollar POSIX biçimindedir ("/sdcard/DCIM").</summary>
public interface IDeviceSession : IDisposable
{
    DeviceInfo Device { get; }

    string HomePath { get; }

    IReadOnlyList<PathShortcut> Shortcuts { get; }

    Task<IReadOnlyList<RemoteEntry>> ListAsync(string path, CancellationToken ct = default);

    /// <summary>Öğe yoksa null döner.</summary>
    Task<RemoteEntry?> StatAsync(string path, CancellationToken ct = default);

    /// <summary>Yerel dosyayı cihaza yazar. <paramref name="progress"/> her parçada aktarılan bayt sayısıyla çağrılır.</summary>
    Task UploadFileAsync(string localPath, string remotePath, Action<long> progress, CancellationToken ct = default);

    /// <summary>Cihazdaki dosyayı yerel diske yazar.</summary>
    Task DownloadFileAsync(string remotePath, string localPath, Action<long> progress, CancellationToken ct = default);

    Task CreateDirectoriesAsync(IReadOnlyCollection<string> paths, CancellationToken ct = default);

    Task DeleteAsync(string path, CancellationToken ct = default);

    Task RenameAsync(string path, string newPath, CancellationToken ct = default);

    Task<StorageInfo?> GetStorageInfoAsync(CancellationToken ct = default);
}

/// <summary>Kullanıcıya gösterilebilir cihaz hatası.</summary>
public class DeviceException(string message, Exception? inner = null, bool connectionLost = false)
    : Exception(message, inner)
{
    /// <summary>Cihazla bağlantı tamamen koptuysa true (aktarım devam ettirilmez).</summary>
    public bool IsConnectionLost { get; } = connectionLost;
}
