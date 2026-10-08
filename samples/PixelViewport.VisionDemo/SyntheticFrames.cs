using System.Buffers;
using System.Buffers.Binary;
using System.Runtime.InteropServices;
using OpenCvSharp;
using PixelViewport.Imaging;
using PixelViewport.OpenCvSharp;

namespace PixelViewport.VisionDemo;

public enum FrameSource { ManagedLease, SafeCopy, NativeLease, OpenCvCopy, OpenCvOwnership }

// Immutable settings cross the producer/UI boundary.
public sealed record DemoSettings(ImagePixelFormat Format, FrameSource Source, bool PaddedRows, int TargetFps);

public static class SyntheticFrames
{
    public const int Width = 960, Height = 600;

    public static PixelFrame Create(DemoSettings settings, long sequence)
    {
        ImagePixelFormat format = settings.Format;
        if (settings.Source is FrameSource.OpenCvCopy or FrameSource.OpenCvOwnership)
            format = format switch { ImagePixelFormat.Rgb24 => ImagePixelFormat.Bgr24, ImagePixelFormat.Rgba32 => ImagePixelFormat.Bgra32, _ => format };
        int stride = format.GetMinimumStride(Width) + (settings.PaddedRows ? 32 : 0);
        int length = checked(stride * Height);
        IMemoryOwner<byte>? owner = MemoryPool<byte>.Shared.Rent(length);
        try
        {
            Span<byte> data = owner.Memory.Span[..length];
            data.Clear();
            Fill(data, stride, format, sequence);
            var timestamp = DateTimeOffset.UtcNow;
            if (settings.Source == FrameSource.ManagedLease)
            {
                var frame = PixelFrame.Wrap(owner.Memory[..length], Width, Height, stride, format, sequence, timestamp, owner);
                owner = null;
                return frame;
            }
            if (settings.Source == FrameSource.SafeCopy)
                return PixelFrame.CopyFrom(data, Width, Height, stride, format, sequence, timestamp);
            if (settings.Source == FrameSource.NativeLease)
            {
                var lease = new NativeLease(length);
                try
                {
                    Marshal.Copy(data.ToArray(), 0, lease.Address, length);
                    return PixelFrame.WrapUnmanaged(lease.Address, length, Width, Height, stride, format, sequence, timestamp, lease);
                }
                catch { lease.Dispose(); throw; }
            }
            MatType type = format switch
            {
                ImagePixelFormat.Gray8 => MatType.CV_8UC1,
                ImagePixelFormat.Gray16LittleEndian => MatType.CV_16UC1,
                ImagePixelFormat.Bgr24 => MatType.CV_8UC3,
                _ => MatType.CV_8UC4,
            };
            // A parent Mat with extra columns makes the ROI non-contiguous.
            using var parent = new Mat(Height, Width + (settings.PaddedRows ? 16 : 0), type);
            Mat? roi = new Mat(parent, new Rect(0, 0, Width, Height));
            try
            {
                byte[] packed = data.ToArray();
                for (int y = 0; y < Height; y++) Marshal.Copy(packed, y * stride, roi.Ptr(y), format.GetMinimumStride(Width));
                if (settings.Source == FrameSource.OpenCvCopy) return MatFrameAdapter.Copy(roi, sequence, timestamp);
                var frame = MatFrameAdapter.TakeOwnership(roi, sequence, timestamp);
                roi = null; // Mat's reference-counted storage survives parent disposal.
                return frame;
            }
            finally { roi?.Dispose(); }
        }
        finally { owner?.Dispose(); }
    }

    private static void Fill(Span<byte> data, int stride, ImagePixelFormat format, long sequence)
    {
        int bpp = format.GetBytesPerPixel();
        int movingX = (int)(sequence * 5 % (Width - 180));
        int movingY = 160 + (int)(70 * Math.Sin(sequence * .08));
        for (int y = 0; y < Height; y++)
        for (int x = 0; x < Width; x++)
        {
            int offset = y * stride + x * bpp;
            if (format is ImagePixelFormat.Gray8 or ImagePixelFormat.Gray16LittleEndian)
            {
                double distance = ((x - 500d) * (x - 500d) + (y - 280d) * (y - 280d)) / 110000;
                ushort intensity = (ushort)Math.Clamp(1500 + 53000 * Math.Exp(-distance) + 4800 * Math.Sin(x / 48d + sequence / 15d) + 2200 * Math.Cos(y / 31d), 0, 65535);
                if (format == ImagePixelFormat.Gray8) data[offset] = (byte)(intensity >> 8);
                else BinaryPrimitives.WriteUInt16LittleEndian(data.Slice(offset, 2), intensity);
                continue;
            }
            byte texture = (byte)(24 + ((x * 7 + y * 3 + sequence) & 15));
            bool region = x >= movingX && x < movingX + 150 && y >= movingY && y < movingY + 110;
            byte r = region ? (byte)218 : (byte)(texture + 8), g = region ? (byte)84 : texture, b = region ? (byte)45 : texture;
            bool rgb = format is ImagePixelFormat.Rgb24 or ImagePixelFormat.Rgba32;
            data[offset] = rgb ? r : b; data[offset + 1] = g; data[offset + 2] = rgb ? b : r;
            if (bpp == 4) data[offset + 3] = 255;
        }
    }

    private sealed class NativeLease(int length) : IDisposable
    {
        private nint _address = Marshal.AllocHGlobal(length);
        public nint Address => _address;
        public void Dispose() { nint address = Interlocked.Exchange(ref _address, 0); if (address != 0) Marshal.FreeHGlobal(address); }
    }
}
