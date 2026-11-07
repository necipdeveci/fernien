namespace fernien.Core;

/// <summary>Cihaz tarafındaki POSIX ("/") yolları için yardımcılar.</summary>
public static class RemotePath
{
    public static string Combine(string directory, string name)
    {
        if (string.IsNullOrEmpty(directory)) return name;
        return directory.EndsWith('/') ? directory + name : directory + "/" + name;
    }

    public static string GetParent(string path)
    {
        path = Normalize(path);
        if (path == "/") return "/";
        int index = path.LastIndexOf('/');
        return index <= 0 ? "/" : path[..index];
    }

    public static string GetName(string path)
    {
        path = Normalize(path);
        if (path == "/") return "/";
        return path[(path.LastIndexOf('/') + 1)..];
    }

    public static string Normalize(string path)
    {
        path = (path ?? "").Trim().Replace('\\', '/');
        while (path.Contains("//")) path = path.Replace("//", "/");
        if (!path.StartsWith('/')) path = "/" + path;
        if (path.Length > 1) path = path.TrimEnd('/');
        return path.Length == 0 ? "/" : path;
    }

    public static bool AreEqual(string? a, string? b) =>
        a is not null && b is not null && string.Equals(Normalize(a), Normalize(b), StringComparison.Ordinal);

    /// <summary>Kabuk komutları için tek tırnaklı güvenli argüman.</summary>
    public static string ShellQuote(string value) => "'" + value.Replace("'", "'\\''") + "'";
}
