using System.Reflection;
using System.Security.Cryptography;

namespace fernien.Providers.Adb;

/// <summary>
/// Exe içine gömülü adb dosyalarını kullanıcının yerel uygulama klasörüne çıkarır.
/// Klasör adı gömülü adb.exe'nin özetinden türetilir; böylece sürüm değişince yenisi ayrı klasöre çıkar
/// ve çalışmakta olan (dosyaları kilitli) eski bir ADB sunucusuyla çakışmaz.
/// </summary>
internal static class EmbeddedAdb
{
    private const string Prefix = "adb/";
    private static readonly object Gate = new();

    public static bool IsAvailable => Assembly.GetManifestResourceInfo(Prefix + "adb.exe") is not null;

    private static Assembly Assembly => typeof(EmbeddedAdb).Assembly;

    /// <summary>adb.exe'nin çıkarıldığı yolu döndürür; gömülü kopya yoksa veya çıkarılamazsa null.</summary>
    public static string? Extract()
    {
        if (!IsAvailable) return null;
        lock (Gate)
        {
            try
            {
                string version;
                using (var adb = Assembly.GetManifestResourceStream(Prefix + "adb.exe")!)
                    version = Convert.ToHexString(SHA256.HashData(adb))[..12].ToLowerInvariant();

                string directory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Fernien", "platform-tools", version);
                Directory.CreateDirectory(directory);

                foreach (string resource in Assembly.GetManifestResourceNames().Where(n => n.StartsWith(Prefix, StringComparison.Ordinal)))
                {
                    string target = Path.Combine(directory, resource[Prefix.Length..]);
                    using var source = Assembly.GetManifestResourceStream(resource)!;
                    if (File.Exists(target) && new FileInfo(target).Length == source.Length) continue;

                    // Önce geçici dosyaya yaz, sonra taşı: yarım kalmış bir adb.exe oluşmasın.
                    string temp = target + "." + Environment.ProcessId + ".tmp";
                    using (var output = File.Create(temp)) source.CopyTo(output);
                    File.Move(temp, target, overwrite: true);
                }

                string adbPath = Path.Combine(directory, "adb.exe");
                return File.Exists(adbPath) ? adbPath : null;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return null;
            }
        }
    }
}
