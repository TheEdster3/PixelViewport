using System.Buffers;

namespace PixelViewport.Imaging;

public static class PixelFrameConverter
{
    /// <summary>
    /// Converts a frame to straight-alpha BGRA32. Gray16 is mapped linearly by using its high byte.
    /// </summary>
    public static void CopyToBgra32(PixelFrame source, Span<byte> destination, int destinationStride)
    {
        ArgumentNullException.ThrowIfNull(source);

        int outputRowLength = checked(source.Width * 4);
        if (destinationStride < outputRowLength)
        {
            throw new ArgumentOutOfRangeException(nameof(destinationStride));
        }

        int requiredOutputLength = checked(((source.Height - 1) * destinationStride) + outputRowLength);
        if (destination.Length < requiredOutputLength)
        {
            throw new ArgumentException(
                $"Destination contains {destination.Length} bytes but requires {requiredOutputLength}.",
                nameof(destination));
        }

        int sourceRowLength = source.Format.GetMinimumStride(source.Width);
        byte[] rented = ArrayPool<byte>.Shared.Rent(sourceRowLength);
        try
        {
            Span<byte> sourceRow = rented.AsSpan(0, sourceRowLength);
            for (int y = 0; y < source.Height; y++)
            {
                source.CopyRowTo(y, sourceRow);
                Span<byte> outputRow = destination.Slice(y * destinationStride, outputRowLength);
                ConvertRow(sourceRow, outputRow, source.Width, source.Format);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }

    private static void ConvertRow(
        ReadOnlySpan<byte> source,
        Span<byte> destination,
        int width,
        ImagePixelFormat format)
    {
        for (int x = 0; x < width; x++)
        {
            int destinationOffset = x * 4;
            switch (format)
            {
                case ImagePixelFormat.Gray8:
                    byte gray8 = source[x];
                    WriteBgra(destination, destinationOffset, gray8, gray8, gray8, byte.MaxValue);
                    break;

                case ImagePixelFormat.Gray16LittleEndian:
                    byte gray16Display = source[(x * 2) + 1];
                    WriteBgra(destination, destinationOffset, gray16Display, gray16Display, gray16Display, byte.MaxValue);
                    break;

                case ImagePixelFormat.Rgb24:
                    int rgb = x * 3;
                    WriteBgra(destination, destinationOffset, source[rgb + 2], source[rgb + 1], source[rgb], byte.MaxValue);
                    break;

                case ImagePixelFormat.Bgr24:
                    int bgr = x * 3;
                    WriteBgra(destination, destinationOffset, source[bgr], source[bgr + 1], source[bgr + 2], byte.MaxValue);
                    break;

                case ImagePixelFormat.Rgba32:
                    int rgba = x * 4;
                    WriteBgra(destination, destinationOffset, source[rgba + 2], source[rgba + 1], source[rgba], source[rgba + 3]);
                    break;

                case ImagePixelFormat.Bgra32:
                    source.Slice(x * 4, 4).CopyTo(destination.Slice(destinationOffset, 4));
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(format), format, "Unsupported pixel format.");
            }
        }
    }

    private static void WriteBgra(
        Span<byte> destination,
        int offset,
        byte blue,
        byte green,
        byte red,
        byte alpha)
    {
        destination[offset] = blue;
        destination[offset + 1] = green;
        destination[offset + 2] = red;
        destination[offset + 3] = alpha;
    }
}
