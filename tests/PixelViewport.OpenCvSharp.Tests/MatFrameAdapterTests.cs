using OpenCvSharp;
using PixelViewport.Imaging;
using Xunit;

namespace PixelViewport.OpenCvSharp.Tests;

public sealed class MatFrameAdapterTests
{
    [Fact]
    public void TakeOwnership_wraps_bgr_matrix_without_copying()
    {
        var matrix = new Mat(1, 2, MatType.CV_8UC3);
        matrix.Set(0, 0, new Vec3b(10, 20, 30));
        matrix.Set(0, 1, new Vec3b(40, 50, 60));

        using PixelFrame frame = MatFrameAdapter.TakeOwnership(matrix, 42);

        Assert.True(frame.HasUnmanagedPixels);
        Assert.Equal(ImagePixelFormat.Bgr24, frame.Format);
        Assert.Equal(42, frame.SequenceNumber);
        Assert.Equal((ushort)30, frame.GetPixel(0, 0).Red);
        Assert.Equal((ushort)50, frame.GetPixel(1, 0).Green);
        Assert.Equal((ushort)40, frame.GetPixel(1, 0).Blue);
    }

    [Fact]
    public void Copy_keeps_pixels_alive_after_source_is_disposed()
    {
        var matrix = new Mat(1, 1, MatType.CV_16UC1);
        matrix.Set(0, 0, (ushort)0x4321);

        using PixelFrame frame = MatFrameAdapter.Copy(matrix);
        matrix.Dispose();

        Assert.True(frame.HasManagedPixels);
        Assert.Equal(ImagePixelFormat.Gray16LittleEndian, frame.Format);
        Assert.Equal((ushort)0x4321, frame.GetPixel(0, 0).Intensity);
    }

    [Fact]
    public void Copy_handles_non_contiguous_region_of_interest()
    {
        using var parent = new Mat(3, 4, MatType.CV_8UC1, Scalar.All(0));
        parent.Set(1, 1, (byte)7);
        parent.Set(1, 2, (byte)8);
        using var region = new Mat(parent, new Rect(1, 1, 2, 1));

        using PixelFrame frame = MatFrameAdapter.Copy(region);

        Assert.Equal((ushort)7, frame.GetPixel(0, 0).Intensity);
        Assert.Equal((ushort)8, frame.GetPixel(1, 0).Intensity);
        Assert.Equal(2, frame.Stride);
    }

    [Fact]
    public void Unsupported_matrix_type_is_rejected()
    {
        using var matrix = new Mat(1, 1, MatType.CV_32FC1);

        NotSupportedException exception =
            Assert.Throws<NotSupportedException>(() => MatFrameAdapter.Copy(matrix));

        Assert.Contains("CV_8UC1", exception.Message);
    }

    [Fact]
    public void Empty_matrix_is_rejected_without_taking_ownership()
    {
        using var matrix = new Mat();

        Assert.Throws<ArgumentException>(() => MatFrameAdapter.TakeOwnership(matrix));
    }
}
