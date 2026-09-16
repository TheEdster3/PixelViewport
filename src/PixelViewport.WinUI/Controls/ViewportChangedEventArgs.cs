namespace PixelViewport.WinUI.Controls;

public sealed class ViewportChangedEventArgs : EventArgs
{
    public ViewportChangedEventArgs(double zoomFactor, double panX, double panY)
    {
        ZoomFactor = zoomFactor;
        PanX = panX;
        PanY = panY;
    }

    public double ZoomFactor { get; }

    public double PanX { get; }

    public double PanY { get; }
}
