namespace PixelViewport.Avalonia;

public sealed class ViewportChangedEventArgs(double zoom, double panX, double panY) : EventArgs
{
    public double Zoom { get; } = zoom;

    public double PanX { get; } = panX;

    public double PanY { get; } = panY;
}
