using PixelViewport.Core.Geometry;

namespace PixelViewport.Imaging;

/// <summary>
/// Framework-neutral metadata for a rectangle drawn over an image.
/// </summary>
public readonly record struct RectangleOverlay(
    RectD Bounds,
    string? Label = null,
    double? Confidence = null,
    uint ColorArgb = 0xFFFFB020)
{
    public bool IsValid =>
        Bounds.IsValid
        && Bounds.Width > 0
        && Bounds.Height > 0
        && (Confidence is null || (double.IsFinite(Confidence.Value) && Confidence is >= 0 and <= 1));
}
