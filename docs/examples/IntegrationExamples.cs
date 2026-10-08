using System.Buffers;
using System.Buffers.Binary;
using global::Avalonia;
using PixelViewport.Core.Geometry;
using PixelViewport.Imaging;
using PixelViewport.OpenCvSharp;
using VisionViewport = PixelViewport.Avalonia.ImageViewport;

namespace PixelViewport.Documentation;

// Linked into the headless verification project. Website recipes embed these
// exact regions rather than maintaining a second, uncompiled snippet copy.
public static class IntegrationExamples
{
    // <doc:managed-lease>
    public static void SubmitManagedLease(VisionViewport viewport)
    {
        const int width = 640, height = 480, stride = width * 2;
        IMemoryOwner<byte> owner = MemoryPool<byte>.Shared.Rent(stride * height);
        PixelFrame frame;
        try
        {
            owner.Memory.Span[..(stride * height)].Clear();
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    BinaryPrimitives.WriteUInt16LittleEndian(
                        owner.Memory.Span.Slice(y * stride + x * 2, 2),
                        (ushort)(x * 65535 / (width - 1)));
            frame = PixelFrame.Wrap(owner.Memory[..(stride * height)],
                width, height, stride, ImagePixelFormat.Gray16LittleEndian,
                sequenceNumber: 1, timestamp: DateTimeOffset.UtcNow, lifetime: owner);
        }
        catch { owner.Dispose(); throw; } // Factory failed: producer still owns lease.
        viewport.SubmitFrame(frame); // Ownership transfers; no producer-side using.
    }
    // </doc:managed-lease>

    // <doc:safe-copy>
    public static void SubmitCameraCallback(VisionViewport viewport,
        ReadOnlySpan<byte> callbackBytes, int width, int height, int stride)
    {
        var frame = PixelFrame.CopyFrom(callbackBytes, width, height, stride,
            ImagePixelFormat.Bgra32, timestamp: DateTimeOffset.UtcNow);
        viewport.SubmitFrame(frame);
        // Caller may now return/reuse callbackBytes; the frame owns a copy.
    }
    // </doc:safe-copy>

    // <doc:opencv-copy>
    public static void SubmitRetainedMat(VisionViewport viewport, global::OpenCvSharp.Mat matrix)
    {
        viewport.SubmitFrame(MatFrameAdapter.Copy(matrix));
        // Caller retains matrix and disposes it independently.
    }
    // </doc:opencv-copy>

    // <doc:opencv-transfer>
    public static void SubmitOwnedMat(VisionViewport viewport, global::OpenCvSharp.Mat matrix)
    {
        PixelFrame frame = MatFrameAdapter.TakeOwnership(matrix);
        viewport.SubmitFrame(frame);
        // Do not dispose, mutate, or reuse matrix after successful transfer.
    }
    // </doc:opencv-transfer>

    // <doc:inspection>
    public static string InspectAtViewportPoint(VisionViewport viewport, Point viewportPoint)
    {
        Point imagePoint = viewport.ViewportToImage(viewportPoint);
        return viewport.TryGetPixel(imagePoint, out PixelSample sample)
            ? $"Intensity {sample.Intensity}; channels {sample.BitsPerChannel}-bit"
            : "Outside image";
    }
    // </doc:inspection>

    // <doc:overlays>
    public static void ShowAnalysisRegion(VisionViewport viewport)
    {
        viewport.SetOverlays([new RectangleOverlay(
            new RectD(100, 80, 160, 120), "Example result", 0.9, 0xFFA3F4BF)]);
        // Only outlines are rendered in Community; Label/Confidence are metadata.
    }
    // </doc:overlays>
}
