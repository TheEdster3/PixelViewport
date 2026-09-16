using System.Runtime.InteropServices;
using PixelViewport.Imaging;
using Xunit;

namespace PixelViewport.Imaging.Tests;

public sealed class PixelFrameTests
{
    [Theory]
    [InlineData(ImagePixelFormat.Gray8, 1)]
    [InlineData(ImagePixelFormat.Gray16LittleEndian, 2)]
    [InlineData(ImagePixelFormat.Rgb24, 3)]
    [InlineData(ImagePixelFormat.Bgr24, 3)]
    [InlineData(ImagePixelFormat.Rgba32, 4)]
    [InlineData(ImagePixelFormat.Bgra32, 4)]
    public void BytesPerPixel_matches_format(ImagePixelFormat format, int expected)
    {
        Assert.Equal(expected, format.GetBytesPerPixel());
    }

    [Fact]
    public void Wrap_rejects_short_buffer()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PixelFrame.Wrap(new byte[11], 4, 1, 12, ImagePixelFormat.Rgb24));
    }

    [Fact]
    public void Wrap_accepts_padded_stride()
    {
        using PixelFrame frame = PixelFrame.Wrap(new byte[16], 3, 2, 8, ImagePixelFormat.Gray8);

        Assert.Equal(8, frame.Stride);
        Assert.Equal(16, frame.BufferLength);
    }

    [Fact]
    public void CopyFrom_owns_an_independent_copy()
    {
        byte[] source = [10, 20, 30, 255];
        using PixelFrame frame = PixelFrame.CopyFrom(source, 1, 1, 4, ImagePixelFormat.Rgba32);

        source[0] = 99;

        Assert.Equal((ushort)10, frame.GetPixel(0, 0).Red);
    }

    [Fact]
    public void GetPixel_decodes_gray16_without_losing_raw_value()
    {
        using PixelFrame frame = PixelFrame.Wrap(
            new byte[] { 0x34, 0x12 },
            1,
            1,
            2,
            ImagePixelFormat.Gray16LittleEndian);

        PixelSample sample = frame.GetPixel(0, 0);

        Assert.Equal((ushort)0x1234, sample.Intensity);
        Assert.Equal(16, sample.BitsPerChannel);
    }

    [Theory]
    [InlineData(ImagePixelFormat.Rgb24, 10, 20, 30, 10, 20, 30)]
    [InlineData(ImagePixelFormat.Bgr24, 10, 20, 30, 30, 20, 10)]
    [InlineData(ImagePixelFormat.Rgba32, 10, 20, 30, 10, 20, 30)]
    [InlineData(ImagePixelFormat.Bgra32, 10, 20, 30, 30, 20, 10)]
    public void GetPixel_decodes_color_channel_order(
        ImagePixelFormat format,
        byte first,
        byte second,
        byte third,
        byte red,
        byte green,
        byte blue)
    {
        int bytesPerPixel = format.GetBytesPerPixel();
        byte[] pixels = bytesPerPixel == 4
            ? [first, second, third, 40]
            : [first, second, third];
        using PixelFrame frame = PixelFrame.Wrap(pixels, 1, 1, bytesPerPixel, format);

        PixelSample sample = frame.GetPixel(0, 0);

        Assert.Equal(red, sample.Red);
        Assert.Equal(green, sample.Green);
        Assert.Equal(blue, sample.Blue);
        Assert.Equal(bytesPerPixel == 4 ? (ushort)40 : (ushort)255, sample.Alpha);
    }

    [Fact]
    public void Unmanaged_frame_reads_pixels_and_releases_lifetime_once()
    {
        nint address = Marshal.AllocHGlobal(3);
        var lifetime = new CallbackDisposable(() => Marshal.FreeHGlobal(address));
        Marshal.Copy(new byte[] { 7, 8, 9 }, 0, address, 3);

        var frame = PixelFrame.WrapUnmanaged(address, 3, 1, 1, 3, ImagePixelFormat.Bgr24, lifetime: lifetime);
        PixelSample sample = frame.GetPixel(0, 0);
        frame.Dispose();
        frame.Dispose();

        Assert.Equal((ushort)9, sample.Red);
        Assert.Equal((ushort)8, sample.Green);
        Assert.Equal((ushort)7, sample.Blue);
        Assert.Equal(1, lifetime.DisposeCount);
    }

    [Fact]
    public void Disposed_frame_rejects_access()
    {
        var frame = PixelFrame.Wrap(new byte[] { 1 }, 1, 1, 1, ImagePixelFormat.Gray8);
        frame.Dispose();

        Assert.Throws<ObjectDisposedException>(() => frame.GetPixel(0, 0));
    }

    private sealed class CallbackDisposable(Action callback) : IDisposable
    {
        private int _disposeCount;

        public int DisposeCount => _disposeCount;

        public void Dispose()
        {
            if (Interlocked.Increment(ref _disposeCount) == 1)
            {
                callback();
            }
        }
    }
}
