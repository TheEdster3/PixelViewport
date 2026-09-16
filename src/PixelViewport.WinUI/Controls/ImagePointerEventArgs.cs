using Windows.Foundation;

namespace PixelViewport.WinUI.Controls;

public sealed class ImagePointerEventArgs : EventArgs
{
    public ImagePointerEventArgs(Point viewportPosition, Point imagePosition, bool isInsideImage)
    {
        ViewportPosition = viewportPosition;
        ImagePosition = imagePosition;
        IsInsideImage = isInsideImage;
    }

    public Point ViewportPosition { get; }

    public Point ImagePosition { get; }

    public bool IsInsideImage { get; }
}
