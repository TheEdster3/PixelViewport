using global::Avalonia;
using PixelViewport.Imaging;

namespace PixelViewport.Avalonia;

public sealed class ImagePointerEventArgs(
    Point viewportPosition,
    Point imagePosition,
    bool isInsideImage,
    PixelSample? sample) : EventArgs
{
    public Point ViewportPosition { get; } = viewportPosition;

    public Point ImagePosition { get; } = imagePosition;

    public bool IsInsideImage { get; } = isInsideImage;

    public PixelSample? Sample { get; } = sample;
}
