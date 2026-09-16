using PixelViewport.Imaging;
using Xunit;

namespace PixelViewport.Imaging.Tests;

public sealed class PixelFrameConverterTests
{
    [Theory]
    [InlineData(ImagePixelFormat.Gray8, new byte[] { 80 }, new byte[] { 80, 80, 80, 255 })]
    [InlineData(ImagePixelFormat.Gray16LittleEndian, new byte[] { 0x34, 0x12 }, new byte[] { 0x12, 0x12, 0x12, 255 })]
    [InlineData(ImagePixelFormat.Rgb24, new byte[] { 10, 20, 30 }, new byte[] { 30, 20, 10, 255 })]
    [InlineData(ImagePixelFormat.Bgr24, new byte[] { 10, 20, 30 }, new byte[] { 10, 20, 30, 255 })]
    [InlineData(ImagePixelFormat.Rgba32, new byte[] { 10, 20, 30, 40 }, new byte[] { 30, 20, 10, 40 })]
    [InlineData(ImagePixelFormat.Bgra32, new byte[] { 10, 20, 30, 40 }, new byte[] { 10, 20, 30, 40 })]
    public void CopyToBgra32_converts_supported_formats(
        ImagePixelFormat format,
        byte[] source,
        byte[] expected)
    {
        using PixelFrame frame = PixelFrame.Wrap(source, 1, 1, source.Length, format);
        byte[] destination = new byte[4];

        PixelFrameConverter.CopyToBgra32(frame, destination, 4);

        Assert.Equal(expected, destination);
    }

    [Fact]
    public void CopyToBgra32_honors_source_and_destination_stride()
    {
        byte[] source =
        [
            1, 2, 99, 99,
            3, 4, 99, 99,
        ];
        using PixelFrame frame = PixelFrame.Wrap(source, 2, 2, 4, ImagePixelFormat.Gray8);
        byte[] destination = new byte[20];

        PixelFrameConverter.CopyToBgra32(frame, destination, 12);

        Assert.Equal(new byte[] { 1, 1, 1, 255, 2, 2, 2, 255 }, destination[..8]);
        Assert.Equal(new byte[] { 3, 3, 3, 255, 4, 4, 4, 255 }, destination[12..20]);
    }
}
