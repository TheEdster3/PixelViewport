namespace PixelViewport.Imaging;

/// <summary>
/// A decoded pixel sample. Eight-bit formats use values from 0 through 255;
/// sixteen-bit grayscale uses values from 0 through 65535.
/// </summary>
public readonly record struct PixelSample(
    ushort Red,
    ushort Green,
    ushort Blue,
    ushort Alpha,
    ushort Intensity,
    int BitsPerChannel)
{
    public ushort MaximumChannelValue => BitsPerChannel == 16 ? ushort.MaxValue : byte.MaxValue;

    public double NormalizedIntensity => Intensity / (double)MaximumChannelValue;
}
