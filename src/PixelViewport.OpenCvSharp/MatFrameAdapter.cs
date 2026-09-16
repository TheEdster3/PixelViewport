using System.Runtime.InteropServices;
using OpenCvSharp;
using PixelViewport.Imaging;

namespace PixelViewport.OpenCvSharp;

/// <summary>
/// Converts supported two-dimensional OpenCvSharp matrices into PixelViewport
/// frames while keeping ownership decisions explicit at the API boundary.
/// </summary>
public static class MatFrameAdapter
{
    /// <summary>
    /// Wraps a matrix without copying and transfers ownership of the matrix to
    /// the returned frame. Disposing the frame disposes the matrix.
    /// </summary>
    public static PixelFrame TakeOwnership(
        Mat matrix,
        long sequenceNumber = 0,
        DateTimeOffset timestamp = default)
    {
        MatDescriptor descriptor = Describe(matrix);
        return PixelFrame.WrapUnmanaged(
            descriptor.Address,
            descriptor.BufferLength,
            descriptor.Width,
            descriptor.Height,
            descriptor.Stride,
            descriptor.Format,
            sequenceNumber,
            timestamp,
            matrix);
    }

    /// <summary>
    /// Copies the visible matrix rows into PixelViewport-owned pooled memory.
    /// The caller retains ownership of the matrix.
    /// </summary>
    public static PixelFrame Copy(
        Mat matrix,
        long sequenceNumber = 0,
        DateTimeOffset timestamp = default)
    {
        MatDescriptor descriptor = Describe(matrix);
        int packedStride = descriptor.Format.GetMinimumStride(descriptor.Width);
        byte[] packedPixels = GC.AllocateUninitializedArray<byte>(
            checked(packedStride * descriptor.Height));

        for (int row = 0; row < descriptor.Height; row++)
        {
            Marshal.Copy(
                matrix.Ptr(row),
                packedPixels,
                row * packedStride,
                packedStride);
        }

        return PixelFrame.CopyFrom(
            packedPixels,
            descriptor.Width,
            descriptor.Height,
            packedStride,
            descriptor.Format,
            sequenceNumber,
            timestamp);
    }

    private static MatDescriptor Describe(Mat matrix)
    {
        ArgumentNullException.ThrowIfNull(matrix);
        if (matrix.Empty())
        {
            throw new ArgumentException("The matrix must contain image pixels.", nameof(matrix));
        }

        if (matrix.Dims != 2)
        {
            throw new ArgumentException("Only two-dimensional image matrices are supported.", nameof(matrix));
        }

        ImagePixelFormat format = GetPixelFormat(matrix.Type());
        int width = matrix.Cols;
        int height = matrix.Rows;
        int stride = checked((int)matrix.Step());
        int minimumStride = format.GetMinimumStride(width);
        if (stride < minimumStride)
        {
            throw new ArgumentException(
                $"The matrix row step {stride} is smaller than the required {minimumStride} bytes.",
                nameof(matrix));
        }

        nint address = matrix.Data;
        if (address == 0)
        {
            throw new ArgumentException("The matrix has no readable data address.", nameof(matrix));
        }

        int bufferLength = checked(((height - 1) * stride) + minimumStride);
        return new MatDescriptor(address, bufferLength, width, height, stride, format);
    }

    private static ImagePixelFormat GetPixelFormat(MatType type)
    {
        if (type == MatType.CV_8UC1)
        {
            return ImagePixelFormat.Gray8;
        }

        if (type == MatType.CV_16UC1)
        {
            return ImagePixelFormat.Gray16LittleEndian;
        }

        if (type == MatType.CV_8UC3)
        {
            return ImagePixelFormat.Bgr24;
        }

        if (type == MatType.CV_8UC4)
        {
            return ImagePixelFormat.Bgra32;
        }

        throw new NotSupportedException(
            $"OpenCV matrix type {type} is not supported. Use CV_8UC1, CV_16UC1, CV_8UC3, or CV_8UC4.");
    }

    private readonly record struct MatDescriptor(
        nint Address,
        int BufferLength,
        int Width,
        int Height,
        int Stride,
        ImagePixelFormat Format);
}
