using System.Diagnostics;
using System.Net.Sockets;
using fernien.Core;

namespace fernien.Providers.Adb;

/// <summary>adb.exe'yi bulur ve ADB sunucusunun çalıştığından emin olur.</summary>
internal static class AdbServer
{
    public const string NotFoundMessage =
        "adb.exe bulunamadı.\n\n" +
        "Google'ın \"SDK Platform-Tools for Windows\" paketini indirip içindeki \"platform-tools\" klasörünü " +
        "uygulamanın yanına kopyalayın ya da klasörü PATH ortam değişkenine ekleyin.";

    public static string? FindAdb()
    {
        var candidates = new List<string>
        {
            Path.Combine(AppContext.BaseDirectory, "platform-tools", "adb.exe"),
            Path.Combine(AppContext.BaseDirectory, "adb.exe")
        };

        foreach (string variable in new[] { "ANDROID_HOME", "ANDROID_SDK_ROOT" })
        {
            string? root = Environment.GetEnvironmentVariable(variable);
            if (!string.IsNullOrWhiteSpace(root)) candidates.Add(Path.Combine(root, "platform-tools", "adb.exe"));
        }

        candidates.Add(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Android", "Sdk", "platform-tools", "adb.exe"));

        foreach (string dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try { candidates.Add(Path.Combine(dir.Trim().Trim('"'), "adb.exe")); }
            catch (ArgumentException) { }
        }

        return candidates.FirstOrDefault(File.Exists);
    }

    public static async Task EnsureRunningAsync(AdbClient client, CancellationToken ct)
    {
        if (await IsRunningAsync(client, ct).ConfigureAwait(false)) return;

        // Önce sistemdeki adb (Android Studio vb. ile aynı sürüm kullanılsın), yoksa exe içine gömülü kopya.
        string adb = FindAdb() ?? EmbeddedAdb.Extract() ?? throw new DeviceException(NotFoundMessage);
        var startInfo = new ProcessStartInfo(adb, "start-server")
        {
            CreateNoWindow = true,
            UseShellExecute = false,
            WindowStyle = ProcessWindowStyle.Hidden
        };

        // Not: Çıktı yönlendirilmez; arka planda kalan sunucu süreci boruları açık tutup beklemeye sebep olur.
        using (var process = Process.Start(startInfo))
        {
            if (process is not null)
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeout.CancelAfter(TimeSpan.FromSeconds(20));
                try { await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false); }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested) { }
            }
        }

        for (int attempt = 0; attempt < 20; attempt++)
        {
            if (await IsRunningAsync(client, ct).ConfigureAwait(false)) return;
            await Task.Delay(250, ct).ConfigureAwait(false);
        }
        throw new DeviceException("ADB sunucusu başlatılamadı.");
    }

    private static async Task<bool> IsRunningAsync(AdbClient client, CancellationToken ct)
    {
        try
        {
            await client.GetServerVersionAsync(ct).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex) when (ex is SocketException or IOException or AdbException)
        {
            return false;
        }
    }
}
