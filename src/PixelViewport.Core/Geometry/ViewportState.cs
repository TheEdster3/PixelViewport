namespace PixelViewport.Core.Geometry;

public readonly record struct ViewportState(double Zoom, double PanX, double PanY)
{
    public static ViewportState Identity => new(1, 0, 0);
}
