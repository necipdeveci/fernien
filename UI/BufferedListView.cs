namespace fernien.UI;

/// <summary>
/// Titremeyi önlemek için çift tamponlu ListView.
/// Not: ListView'da DoubleBuffered, yerel LVS_EX_DOUBLEBUFFER stilini açar. ControlStyles.OptimizedDoubleBuffer /
/// AllPaintingInWmPaint gibi stiller yerel ListView çizimini bozduğu için kullanılmaz.
/// </summary>
public class BufferedListView : ListView
{
    public BufferedListView()
    {
        DoubleBuffered = true;
    }
}
