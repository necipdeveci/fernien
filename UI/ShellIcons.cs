using System.Runtime.InteropServices;

namespace fernien.UI;

/// <summary>Windows Gezgini'nin dosya/klasör/sürücü simgelerini ve tür adlarını sağlar (uzantı bazında önbellekli).</summary>
internal static class ShellIcons
{
    private const uint SHGFI_ICON = 0x100;
    private const uint SHGFI_SMALLICON = 0x1;
    private const uint SHGFI_TYPENAME = 0x400;
    private const uint SHGFI_USEFILEATTRIBUTES = 0x10;
    private const uint FILE_ATTRIBUTE_DIRECTORY = 0x10;
    private const uint FILE_ATTRIBUTE_NORMAL = 0x80;

    private static readonly Dictionary<string, string> TypeNames = new(StringComparer.OrdinalIgnoreCase);

    public static ImageList SmallImages { get; } = new()
    {
        ColorDepth = ColorDepth.Depth32Bit,
        ImageSize = SystemInformation.SmallIconSize
    };

    public static string FolderKey => Ensure("folder", "folder", FILE_ATTRIBUTE_DIRECTORY, useAttributes: true);

    public static string FolderTypeName => TypeNames.GetValueOrDefault(FolderKey) is { Length: > 0 } name ? name : "Klasör";

    public static string GetFileIconKey(string fileName)
    {
        string extension = Path.GetExtension(fileName).ToLowerInvariant();
        return Ensure("ext:" + (extension.Length == 0 ? "-" : extension), "file" + extension, FILE_ATTRIBUTE_NORMAL, useAttributes: true);
    }

    public static string GetFileTypeName(string fileName) =>
        TypeNames.GetValueOrDefault(GetFileIconKey(fileName)) is { Length: > 0 } name ? name : "Dosya";

    public static string GetDriveIconKey(string root) => Ensure("drive:" + root, root, 0, useAttributes: false);

    public static string GetDriveTypeName(string root) => TypeNames.GetValueOrDefault(GetDriveIconKey(root)) ?? "";

    private static string Ensure(string key, string path, uint attributes, bool useAttributes)
    {
        if (SmallImages.Images.ContainsKey(key)) return key;

        var info = new SHFILEINFO();
        uint flags = SHGFI_ICON | SHGFI_SMALLICON | SHGFI_TYPENAME | (useAttributes ? SHGFI_USEFILEATTRIBUTES : 0);
        IntPtr result = SHGetFileInfo(path, attributes, ref info, (uint)Marshal.SizeOf<SHFILEINFO>(), flags);
        TypeNames[key] = info.szTypeName ?? "";

        if (result != IntPtr.Zero && info.hIcon != IntPtr.Zero)
        {
            try
            {
                using var icon = Icon.FromHandle(info.hIcon);
                SmallImages.Images.Add(key, icon.ToBitmap());
            }
            finally
            {
                DestroyIcon(info.hIcon);
            }
        }
        else
        {
            SmallImages.Images.Add(key, new Bitmap(SmallImages.ImageSize.Width, SmallImages.ImageSize.Height));
        }
        return key;
    }

    /// <summary>Gezgin gibi "doğal" sıralama (dosya2 &lt; dosya10).</summary>
    public static int CompareNatural(string a, string b) => StrCmpLogicalW(a, b);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEINFO
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)] public string szTypeName;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
    private static extern int StrCmpLogicalW(string psz1, string psz2);
}
