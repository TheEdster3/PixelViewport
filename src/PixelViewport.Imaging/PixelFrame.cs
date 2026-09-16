using System.Buffers;
using System.Buffers.Binary;
using System.Runtime.InteropServices;

namespace PixelViewport.Imaging;

/// <summary>
/// A validated lease over one uncompressed image frame. Disposing the frame
/// releases any lifetime object supplied by the producer.
/// </summary>
public sealed class PixelFrame : IDisposable
{
    private readonly ReadOnlyMemory<byte> _managedPixels;
    private readonly nint _unmanagedAddress;
    private IDisposable? _lifetime;
    private int _isDisposed;

    private PixelFrame(
        ReadOnlyMemory<byte> managedPixels,
        nint unmanagedAddress,
        int bufferLength,
        int width,
        int height,
        int stride,
        ImagePixelFormat format,
        long sequenceNumber,
        DateTimeOffset timestamp,
        IDisposable? lifetime)
    {
        Validate(bufferLength, width, height, stride, format);

        _managedPixels = managedPixels;
        _unmanagedAddress = unmanagedAddress;
        _lifetime = lifetime;
        BufferLength = bufferLength;
        Width = width;
        Height = height;
        Stride = stride;
        Format = format;
        SequenceNumber = sequenceNumber;
        Timestamp = timestamp;
    }

    public int Width { get; }

    public int Height { get; }

    public int Stride { get; }

    public int BufferLength { get; }

    public ImagePixelFormat Format { get; }

    public long SequenceNumber { get; }

    public DateTimeOffset Timestamp { get; }

    public bool HasManagedPixels => !_managedPixels.IsEmpty;

    public bool HasUnmanagedPixels => _unmanagedAddress != 0;

    public ReadOnlyMemory<byte> ManagedPixels
    {
        get
        {
            ThrowIfDisposed();
            return HasManagedPixels
                ? _managedPixels
                : throw new InvalidOperationException("This frame is backed by unmanaged memory.");
        }
    }

    /// <summary>
    /// Gets an unmanaged source address. The address is valid only until this frame is disposed.
    /// </summary>
    public nint DangerousGetAddress()
    {
        ThrowIfDisposed();
        return HasUnmanagedPixels
            ? _unmanagedAddress
            : throw new InvalidOperationException("This frame is backed by managed memory.");
    }

    /// <summary>
    /// Wraps managed memory without copying it. The producer must not mutate or release
    /// the memory until this frame is disposed.
    /// </summary>
    public static PixelFrame Wrap(
        ReadOnlyMemory<byte> pixels,
        int width,
        int height,
        int stride,
        ImagePixelFormat format,
        long sequenceNumber = 0,
        DateTimeOffset timestamp = default,
        IDisposable? lifetime = null)
    {
        if (pixels.IsEmpty)
        {
            throw new ArgumentException("Pixel memory cannot be empty.", nameof(pixels));
        }

        return new PixelFrame(
            pixels,
            0,
            pixels.Length,
            width,
            height,
            stride,
            format,
            sequenceNumber,
            timestamp,
            lifetime);
    }

    /// <summary>
    /// Copies source pixels into pooled memory owned by the returned frame.
    /// </summary>
    public static PixelFrame CopyFrom(
        ReadOnlySpan<byte> pixels,
        int width,
        int height,
        int stride,
        ImagePixelFormat format,
        long sequenceNumber = 0,
        DateTimeOffset timestamp = default)
    {
        int requiredLength = GetRequiredBufferLength(width, height, stride, format);
        if (pixels.Length < requiredLength)
        {
            throw new ArgumentException(
                $"The source contains {pixels.Length} bytes but the frame requires at least {requiredLength}.",
                nameof(pixels));
        }

        IMemoryOwner<byte> owner = MemoryPool<byte>.Shared.Rent(requiredLength);
        try
        {
            pixels[..requiredLength].CopyTo(owner.Memory.Span);
            return new PixelFrame(
                owner.Memory[..requiredLength],
                0,
                requiredLength,
                width,
                height,
                stride,
                format,
                sequenceNumber,
                timestamp,
                owner);
        }
        catch
        {
            owner.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Wraps unmanaged memory without copying it. The optional lifetime is disposed with the frame.
    /// </summary>
    public static PixelFrame WrapUnmanaged(
        nint address,
        int bufferLength,
        int width,
        int height,
        int stride,
        ImagePixelFormat format,
        long sequenceNumber = 0,
        DateTimeOffset timestamp = default,
        IDisposable? lifetime = null)
    {
        if (address == 0)
        {
            throw new ArgumentException("The image address cannot be null.", nameof(address));
        }

        return new PixelFrame(
            ReadOnlyMemory<byte>.Empty,
            address,
            bufferLength,
            width,
            height,
            stride,
            format,
            sequenceNumber,
            timestamp,
            lifetime);
    }

    public PixelSample GetPixel(int x, int y)
    {
        ThrowIfDisposed();
        ArgumentOutOfRangeException.ThrowIfNegative(x);
        ArgumentOutOfRangeException.ThrowIfNegative(y);

        if (x >= Width)
        {
            throw new ArgumentOutOfRangeException(nameof(x));
        }

        if (y >= Height)
        {
            throw new ArgumentOutOfRangeException(nameof(y));
        }

        int offset = checked((y * Stride) + (x * Format.GetBytesPerPixel()));
        return Format switch
        {
            ImagePixelFormat.Gray8 => FromGray8(ReadByte(offset)),
            ImagePixelFormat.Gray16LittleEndian => FromGray16(ReadUInt16LittleEndian(offset)),
            ImagePixelFormat.Rgb24 => FromRgb8(ReadByte(offset), ReadByte(offset + 1), ReadByte(offset + 2)),
            ImagePixelFormat.Bgr24 => FromRgb8(ReadByte(offset + 2), ReadByte(offset + 1), ReadByte(offset)),
            ImagePixelFormat.Rgba32 => FromRgba8(
                ReadByte(offset),
                ReadByte(offset + 1),
                ReadByte(offset + 2),
                ReadByte(offset + 3)),
            ImagePixelFormat.Bgra32 => FromRgba8(
                ReadByte(offset + 2),
                ReadByte(offset + 1),
                ReadByte(offset),
                ReadByte(offset + 3)),
            _ => throw new InvalidOperationException($"Unsupported pixel format {Format}."),
        };
    }

    public void CopyRowTo(int row, Span<byte> destination)
    {
        ThrowIfDisposed();
        ArgumentOutOfRangeException.ThrowIfNegative(row);
        if (row >= Height)
        {
            throw new ArgumentOutOfRangeException(nameof(row));
        }

        int rowLength = Format.GetMinimumStride(Width);
        if (destination.Length < rowLength)
        {
            throw new ArgumentException($"Destination must contain at least {rowLength} bytes.", nameof(destination));
        }

        int offset = checked(row * Stride);
        if (HasManagedPixels)
        {
            _managedPixels.Span.Slice(offset, rowLength).CopyTo(destination);
            return;
        }

        byte[] rented = ArrayPool<byte>.Shared.Rent(rowLength);
        try
        {
            Marshal.Copy(_unmanagedAddress + offset, rented, 0, rowLength);
            rented.AsSpan(0, rowLength).CopyTo(destination);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _isDisposed, 1) != 0)
        {
            return;
        }

        Interlocked.Exchange(ref _lifetime, null)?.Dispose();
    }

    internal static int GetRequiredBufferLength(
        int width,
        int height,
        int stride,
        ImagePixelFormat format)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        int minimumStride = format.GetMinimumStride(width);
        if (stride < minimumStride)
        {
            throw new ArgumentOutOfRangeException(
                nameof(stride),
                stride,
                $"Stride must be at least {minimumStride} bytes for this frame.");
        }

        return checked(((height - 1) * stride) + minimumStride);
    }

    private static void Validate(
        int bufferLength,
        int width,
        int height,
        int stride,
        ImagePixelFormat format)
    {
        int requiredLength = GetRequiredBufferLength(width, height, stride, format);
        if (bufferLength < requiredLength)
        {
            throw new ArgumentOutOfRangeException(
                nameof(bufferLength),
                bufferLength,
                $"The frame requires at least {requiredLength} bytes.");
        }
    }

    private byte ReadByte(int offset) => HasManagedPixels
        ? _managedPixels.Span[offset]
        : Marshal.ReadByte(_unmanagedAddress, offset);

    private ushort ReadUInt16LittleEndian(int offset)
    {
        if (HasManagedPixels)
        {
            return BinaryPrimitives.ReadUInt16LittleEndian(_managedPixels.Span.Slice(offset, 2));
        }

        int low = Marshal.ReadByte(_unmanagedAddress, offset);
        int high = Marshal.ReadByte(_unmanagedAddress, offset + 1);
        return (ushort)(low | (high << 8));
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _isDisposed) != 0, this);
    }

    private static PixelSample FromGray8(byte intensity) =>
        new(intensity, intensity, intensity, byte.MaxValue, intensity, 8);

    private static PixelSample FromGray16(ushort intensity) =>
        new(intensity, intensity, intensity, ushort.MaxValue, intensity, 16);

    private static PixelSample FromRgb8(byte red, byte green, byte blue) =>
        FromRgba8(red, green, blue, byte.MaxValue);

    private static PixelSample FromRgba8(byte red, byte green, byte blue, byte alpha)
    {
        ushort intensity = (ushort)Math.Round((0.2126 * red) + (0.7152 * green) + (0.0722 * blue));
        return new PixelSample(red, green, blue, alpha, intensity, 8);
    }
}
