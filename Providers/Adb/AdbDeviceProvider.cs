using fernien.Core;

namespace fernien.Providers.Adb;

/// <summary>USB kablo üzerinden ADB ile aktarım yöntemi (Android).</summary>
public sealed class AdbDeviceProvider : IDeviceProvider
{
    private readonly AdbClient _client = new();
    private CancellationTokenSource? _trackCts;

    public string Name => "USB Kablo (ADB)";

    public event EventHandler? DevicesChanged;

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        await AdbServer.EnsureRunningAsync(_client, ct).ConfigureAwait(false);
        if (_trackCts is null)
        {
            _trackCts = new CancellationTokenSource();
            _ = Task.Run(() => TrackLoopAsync(_trackCts.Token));
        }
    }

    /// <summary>Takılan/çıkarılan cihazları anlık izler; ADB sunucusu kapanırsa yeniden başlatır.</summary>
    private async Task TrackLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await _client.TrackDevicesAsync(RaiseDevicesChanged, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return;
            }
            catch
            {
                // Sunucu kapandı veya bağlantı koptu: aşağıda yeniden denenecek.
            }

            RaiseDevicesChanged();
            try
            {
                await Task.Delay(2000, ct).ConfigureAwait(false);
                await AdbServer.EnsureRunningAsync(_client, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch
            {
                // Bir sonraki turda tekrar denenir.
            }
        }
    }

    private void RaiseDevicesChanged() => DevicesChanged?.Invoke(this, EventArgs.Empty);

    public async Task<IReadOnlyList<DeviceInfo>> GetDevicesAsync(CancellationToken ct = default)
    {
        var entries = await _client.GetDevicesAsync(ct).ConfigureAwait(false);
        return entries.Select(e => new DeviceInfo(
            this,
            e.Serial,
            e.Model ?? e.Product ?? e.Serial,
            e.IsUsb ? "USB" : "Ağ",
            MapState(e.State))).ToList();
    }

    private static DeviceState MapState(string state) => state switch
    {
        "device" => DeviceState.Ready,
        "unauthorized" => DeviceState.Unauthorized,
        "offline" => DeviceState.Offline,
        "authorizing" or "connecting" => DeviceState.Connecting,
        _ => DeviceState.Unknown
    };

    public IDeviceSession OpenSession(DeviceInfo device)
    {
        if (device.Provider != this) throw new ArgumentException("Cihaz bu sağlayıcıya ait değil.", nameof(device));
        return new AdbDeviceSession(_client, device);
    }

    public string GetConnectionHelp(DeviceInfo? device) => device?.State switch
    {
        DeviceState.Unauthorized =>
            "Telefon bağlı fakat bu bilgisayara henüz izin verilmedi.\n\n" +
            "Telefon ekranındaki \"USB hata ayıklamaya izin verilsin mi?\" penceresinde " +
            "\"Bu bilgisayara her zaman izin ver\" kutusunu işaretleyip \"İzin ver\"e dokunun.\n\n" +
            "Pencere görünmüyorsa kabloyu çıkarıp tekrar takın veya Geliştirici seçeneklerinden " +
            "\"USB hata ayıklama yetkilerini iptal et\" deyip yeniden deneyin.",
        DeviceState.Offline =>
            "Telefon çevrimdışı görünüyor.\n\nKabloyu çıkarıp yeniden takın veya telefonun kilidini açın.",
        DeviceState.Connecting =>
            "Telefona bağlanılıyor…",
        DeviceState.Unknown =>
            "Telefon tanınamadı. Kabloyu çıkarıp yeniden takın.",
        _ =>
            "Telefon bulunamadı.\n\n" +
            "1. Telefonu USB kablosuyla bilgisayara bağlayın.\n" +
            "2. Ayarlar › Telefon hakkında › Yapım numarası'na 7 kez dokunarak Geliştirici seçeneklerini açın.\n" +
            "3. Ayarlar › Geliştirici seçenekleri › \"USB hata ayıklama\"yı etkinleştirin.\n" +
            "4. Telefonda çıkan izin penceresini onaylayın.\n\n" +
            "Telefon takıldığında otomatik olarak algılanır.\n" +
            "Hiç görünmüyorsa telefon üreticinizin USB sürücüsünü (ör. Samsung USB Driver) kurun " +
            "ve veri aktarımı destekleyen bir kablo kullandığınızdan emin olun."
    };

    public void Dispose()
    {
        _trackCts?.Cancel();
        _trackCts?.Dispose();
        _trackCts = null;
    }
}
