using System.Collections;

namespace fernien.UI;

/// <summary>Her iki dosya panelindeki satırların ortak verisi.</summary>
public sealed record EntryTag(string Name, string FullPath, bool IsDirectory, long Size, DateTime? Modified, bool IsDrive = false);

/// <summary>Klasörler önce; ardından seçilen sütuna göre doğal sıralama.</summary>
public sealed class EntryComparer : IComparer
{
    public int Column { get; set; }
    public bool Descending { get; set; }

    public void Toggle(int column)
    {
        if (Column == column) Descending = !Descending;
        else
        {
            Column = column;
            Descending = false;
        }
    }

    public int Compare(object? x, object? y)
    {
        if ((x as ListViewItem)?.Tag is not EntryTag a || (y as ListViewItem)?.Tag is not EntryTag b) return 0;
        if (a.IsDirectory != b.IsDirectory) return a.IsDirectory ? -1 : 1;

        int result = Column switch
        {
            1 => a.Size.CompareTo(b.Size),
            2 => string.Compare(Path.GetExtension(a.Name), Path.GetExtension(b.Name), StringComparison.CurrentCultureIgnoreCase),
            3 => Nullable.Compare(a.Modified, b.Modified),
            _ => 0
        };
        if (result == 0) result = ShellIcons.CompareNatural(a.Name, b.Name);
        return Descending ? -result : result;
    }
}

internal static class Fmt
{
    private static readonly string[] Units = ["B", "KB", "MB", "GB", "TB"];

    public static string Size(long bytes)
    {
        if (bytes < 1024) return $"{Math.Max(0, bytes)} B";
        double value = bytes;
        int unit = 0;
        while (value >= 1024 && unit < Units.Length - 1)
        {
            value /= 1024;
            unit++;
        }
        string format = value < 10 ? "0.##" : value < 100 ? "0.#" : "0";
        return value.ToString(format) + " " + Units[unit];
    }

    public static string Speed(double bytesPerSecond) => bytesPerSecond <= 0 ? "" : Size((long)bytesPerSecond) + "/sn";

    public static string Duration(TimeSpan time)
    {
        if (time.TotalHours >= 1) return $"{(int)time.TotalHours} sa {time.Minutes} dk";
        if (time.TotalMinutes >= 1) return $"{time.Minutes} dk {time.Seconds} sn";
        return $"{Math.Max(1, (int)Math.Ceiling(time.TotalSeconds))} sn";
    }
}
