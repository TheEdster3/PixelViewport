namespace PixelViewport.Imaging;

/// <summary>
/// Describes the byte layout of one uncompressed image pixel.
/// </summary>
public enum ImagePixelFormat
{
    Gray8,
    Gray16LittleEndian,
    Rgb24,
    Bgr24,
    Rgba32,
    Bgra32,
}

public static class ImagePixelFormatExtensions
{
    public static int GetBytesPerPixel(this ImagePixelFormat format) => format switch
    {
        ImagePixelFormat.Gray8 => 1,
        ImagePixelFormat.Gray16LittleEndian => 2,
        ImagePixelFormat.Rgb24 or ImagePixelFormat.Bgr24 => 3,
        ImagePixelFormat.Rgba32 or ImagePixelFormat.Bgra32 => 4,
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Unsupported pixel format."),
    };

    public static int GetMinimumStride(this ImagePixelFormat format, int width)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        return checked(width * format.GetBytesPerPixel());
    }
}
