# Public API reference

Community alpha / .NET 8. This reference covers the application-facing APIs implemented in the public source. Framework property identifiers and generated record equality members follow their framework/C# conventions. The source and compiled examples take precedence over roadmap descriptions.

## Pixel formats

Namespace: PixelViewport.Imaging. ImagePixelFormat describes byte layout, not compression or a file extension.

| Enum value | Bytes/pixel | Visible byte order | Inspection range |
| --- | --- | --- | --- |
| Gray8 | 1 | intensity | 0–255 |
| Gray16LittleEndian | 2 | low byte, high byte | 0–65535 |
| Rgb24 | 3 | R, G, B | 0–255 per channel |
| Bgr24 | 3 | B, G, R | 0–255 per channel |
| Rgba32 | 4 | R, G, B, A | 0–255 per channel |
| Bgra32 | 4 | B, G, R, A | 0–255 per channel |

ImagePixelFormatExtensions.GetBytesPerPixel(format) returns 1/2/3/4. GetMinimumStride(format, width) returns checked width × bytes-per-pixel. Unknown formats or nonpositive width throw ArgumentOutOfRangeException; overflow throws OverflowException.

## PixelFrame

Namespace: PixelViewport.Imaging. Sealed IDisposable lease over one uncompressed frame. No public constructor; use a factory.

```csharp
PixelFrame Wrap(ReadOnlyMemory<byte> pixels, int width, int height,
    int stride, ImagePixelFormat format, long sequenceNumber = 0,
    DateTimeOffset timestamp = default, IDisposable? lifetime = null);

PixelFrame WrapUnmanaged(nint address, int bufferLength, int width,
    int height, int stride, ImagePixelFormat format,
    long sequenceNumber = 0, DateTimeOffset timestamp = default,
    IDisposable? lifetime = null);

PixelFrame CopyFrom(ReadOnlySpan<byte> pixels, int width, int height,
    int stride, ImagePixelFormat format, long sequenceNumber = 0,
    DateTimeOffset timestamp = default);
```

These are static factories. Wrap does not copy; its optional lifetime is released when the returned frame is disposed. WrapUnmanaged does not verify the native allocation's accessibility and does not free it without an appropriate lifetime. CopyFrom copies the required readable region into owned pooled memory.

| Read-only property | Meaning |
| --- | --- |
| Width, Height : int | Positive image dimensions |
| Stride : int | Positive source bytes per row, including padding |
| BufferLength : int | Declared readable byte count |
| Format : ImagePixelFormat | Input byte layout |
| SequenceNumber : long | Caller-supplied identifier; default 0, no monotonic enforcement |
| Timestamp : DateTimeOffset | Caller timestamp; default means no presentation-age diagnostic |
| HasManagedPixels, HasUnmanagedPixels : bool | Which backing representation is present |
| ManagedPixels : ReadOnlyMemory<byte> | Leased managed bytes; rejects native-backed/disposed frames |

Methods: GetPixel(int x, int y) returns PixelSample for an in-bounds integer pixel; CopyRowTo(int row, Span<byte> destination) copies only visible bytes, not padding; DangerousGetAddress() returns the leased native address; Dispose() releases the lifetime exactly once.

Validation: dimensions positive; stride ≥ minimum stride; enough bytes for (height − 1) × stride + minimum stride; nonempty managed memory/nonzero pointer. Bad dimensions/stride/length/indexes throw ArgumentOutOfRangeException; empty memory/null pointer and insufficient copy spans throw ArgumentException. Wrong backing access throws InvalidOperationException; post-disposal byte access throws ObjectDisposedException. Checked sizing can throw OverflowException. Factory rejection does not dispose a caller-supplied lifetime.

## PixelSample and converter

```csharp
readonly record struct PixelSample(ushort Red, ushort Green,
    ushort Blue, ushort Alpha, ushort Intensity, int BitsPerChannel);
```

MaximumChannelValue is 65535 for 16-bit samples, otherwise 255. NormalizedIntensity is Intensity / MaximumChannelValue. Gray16 has raw channels/intensity and alpha 65535. Eight-bit grayscale/color has eight-bit channels and alpha 255 unless supplied explicitly. The property is BitsPerChannel, not BitDepth.

PixelFrameConverter.CopyToBgra32(PixelFrame source, Span<byte> destination, int destinationStride) writes straight-alpha BGRA32. Destination stride must be ≥ source.Width × 4 and the span must contain the last visible row. Padding is left untouched. Gray16 uses its high byte; no window/level or LUT is applied. The caller owns both destination and source lifetime during conversion. The converter does not dispose the source.

## LatestFrameMailbox

Namespace: PixelViewport.Imaging. Sealed IDisposable, lock-protected, single pending slot.

| Member | Contract |
| --- | --- |
| Submit(PixelFrame frame) | Transfers ownership; replaces and disposes previous pending frame |
| TryTake(out PixelFrame? frame) : bool | True transfers newest pending frame to caller; false returns null |
| Statistics : FrameMailboxStatistics | Lock-consistent snapshot |
| Dispose() | Releases pending frame; idempotent |

FrameMailboxStatistics is a readonly record struct with long Submitted, Taken, Dropped and bool HasPendingFrame. Counters are cumulative; taking a frame is not proof of successful rendering. Statistics remains readable after disposal. Submit after disposal releases the incoming frame then throws ObjectDisposedException; TryTake after disposal throws. Dispose callbacks run outside the mailbox lock and should not throw.

## RectangleOverlay

```csharp
readonly record struct RectangleOverlay(RectD Bounds,
    string? Label = null, double? Confidence = null,
    uint ColorArgb = 0xFFFFB020);
```

IsValid requires finite, positive-width/height bounds and either null confidence or finite confidence in [0,1]. Color is 0xAARRGGBB. Avalonia filters invalid entries and draws fixed-width rectangle outlines. Label and Confidence are retained metadata, not rendered text or interactive ROI tools.

## OpenCvSharp adapter

Namespace: PixelViewport.OpenCvSharp. Static MatFrameAdapter. Both methods take Mat matrix, optional long sequenceNumber = 0, and DateTimeOffset timestamp = default, and return PixelFrame.

| Method | Copying and ownership |
| --- | --- |
| TakeOwnership | Wraps original Mat memory/step without input copying; frame later disposes Mat |
| Copy | Packs visible rows, then copies to pooled frame memory; caller keeps Mat |

CV_8UC1 → Gray8; CV_16UC1 → Gray16LittleEndian; CV_8UC3 → Bgr24; CV_8UC4 → Bgra32. Only nonempty two-dimensional Mats are accepted. Row step may be padded/non-contiguous. Null throws ArgumentNullException; empty/non-2D/unreadable/invalid-step input throws ArgumentException; unsupported MatType throws NotSupportedException; checked large sizing may overflow. Rejected input remains caller-owned.

Native OpenCV is an application deployment choice, not bundled in this adapter package. Match the pinned OpenCvSharp version and selected OS runtime. Ownership transfer is not zero-copy rendering.

## Avalonia adapter

Namespace: PixelViewport.Avalonia. ImageViewport : Avalonia.Controls.Control, IDisposable. Use global::Avalonia aliases if your namespace contains Avalonia. Create/add/configure/dispose the control on the UI thread.

| Property | Type | Default | Behavior |
| --- | --- | --- | --- |
| Background | IBrush? | Brushes.Black | Viewport background |
| AutoFit | bool | true | Fit on size changes until user view is established |
| MinZoomFactor | double | 0.01 | Minimum zoom; invalid values fall back to 0.01 |
| MaxZoomFactor | double | 64 | Maximum; at least minimum, nonfinite falls back to 64 |
| ZoomStep | double | 1.2 | Wheel multiplier; invalid/≤1 falls back to 1.2 |
| IsPanEnabled | bool | true | Left-pointer drag |
| IsMouseWheelZoomEnabled | bool | true | Cursor-anchored wheel zoom |
| ShowCrosshair | bool | true | Pointer crosshair inside viewport |
| ZoomFactor | double, read-only | 1 initially | Current effective zoom |
| FrameStatistics | FrameMailboxStatistics, read-only | Zero counts | Submission mailbox snapshot |

Styled properties have corresponding public static property identifiers (BackgroundProperty, AutoFitProperty, etc.). Defaults are evaluation behavior, not a throughput guarantee.

| Method | Contract |
| --- | --- |
| SubmitFrame(PixelFrame frame) | Producer-thread safe ownership transfer; schedules UI drain |
| SetOverlays(IEnumerable<RectangleOverlay>? overlays) | Null clears; snapshots valid entries; marshals application to UI |
| FitToViewport() | Fit within zoom bounds; clears user-view flag |
| ZoomToActualSize() | Center at zoom 1 within bounds; establishes user view |
| ViewportToImage(Avalonia.Point) : Point | Viewport DIPs to continuous image coordinates |
| ImageToViewport(Avalonia.Point) : Point | Inverse transform |
| TryGetPixel(Avalonia.Point imagePoint, out PixelSample) : bool | IMAGE coordinate input; floors x/y; false outside/no frame |
| Dispose() | UI-thread teardown of mailbox, bitmap, and current frame |

Fit/actual-size do nothing until a frame and valid viewport bounds exist. Smaller images remain centered; larger ones cannot be panned completely off-screen. Native Avalonia control has wheel/drag interaction, not built-in +/-/arrow/F keyboard commands or a public ZoomIn/ZoomAt method in this alpha.

| Event | Event args / read-only properties |
| --- | --- |
| FramePresented | SequenceNumber : long; Timestamp, PresentedAt : DateTimeOffset; PresentationAge : TimeSpan?; Statistics : FrameMailboxStatistics |
| ImagePointerMoved | ViewportPosition, ImagePosition : Avalonia.Point; IsInsideImage : bool; Sample : PixelSample? |
| ViewChanged | Zoom, PanX, PanY : double |

FramePresented follows conversion/assignment, not physical display. Default timestamp gives null age. Age is wall-clock diagnostic only. Events run on UI thread. ImagePointerMoved may report an outside point with null sample. During a drag, pointer inspection is raised before that movement's pan update; applications needing post-pan values should query using the updated transform. ViewChanged is raised when applying transform state, not on every frame.

## WinUI adapter

Namespace: PixelViewport.WinUI.Controls. ImageViewport : Microsoft.UI.Xaml.Controls.Control. This is the separate ImageSource-based baseline, not the raw-frame Avalonia renderer. No SubmitFrame, FrameStatistics, PixelSample, or IDisposable API is offered by this control.

| Property | Default / role |
| --- | --- |
| Source : ImageSource? | null; BitmapSource pixel size is used when available |
| OverlayContent : object? | null; image-space transformed content |
| ViewportOverlayContent : object? | null; viewport-space content |
| ViewportBackground : Brush? | null; background template binding |
| ZoomFactor : double | 1; read/write, centered zoom when changed externally |
| MinZoomFactor / MaxZoomFactor : double | 0.05 / 32 |
| ZoomStep : double | 1.2 |
| AutoFit / IsPanEnabled / IsMouseWheelZoomEnabled : bool | true / true / true |

Each exposes a corresponding static DependencyProperty identifier. Methods: FitToViewport(), ZoomToActualSize(), ZoomIn(), ZoomOut(), ZoomAt(double zoomFactor, Windows.Foundation.Point viewportAnchor), ViewportToImage(Point), ImageToViewport(Point), IsInsideImage(Point). Zoom anchor is a viewport DIP coordinate. Commands no-op before usable image/bounds/template parts exist.

ImagePointerMoved args: ViewportPosition, ImagePosition, IsInsideImage; no pixel sample. ViewChanged args: ZoomFactor, PanX, PanY. Note WinUI args use ZoomFactor while Avalonia args use Zoom. WinUI supports mouse-wheel/left-mouse pan and manipulation translation/scale; do not infer tested touch-device coverage from event support.

## Framework-neutral geometry

Namespace: PixelViewport.Core.Geometry.

| Value type | Members |
| --- | --- |
| PointD(double X, double Y) | Continuous coordinates |
| SizeD(double Width, double Height) | IsValid: finite and strictly positive |
| RectD(double X, double Y, double Width, double Height) | Left, Top, Right, Bottom; IsValid: finite, nonnegative dimensions |
| ViewportState(double Zoom, double PanX, double PanY) | Identity = (1,0,0) |

ViewportMath is static. These signatures use SizeD image/viewport and PointD anchors/deltas:

```csharp
ViewportState Fit(SizeD image, SizeD viewport, double minZoom, double maxZoom);
ViewportState CenterAtZoom(SizeD image, SizeD viewport, double zoom,
    double minZoom, double maxZoom);
ViewportState ZoomAt(ViewportState current, double requestedZoom,
    PointD viewportAnchor, SizeD image, SizeD viewport,
    double minZoom, double maxZoom);
ViewportState PanBy(ViewportState current, PointD delta,
    SizeD image, SizeD viewport);
ViewportState Constrain(ViewportState state, SizeD image, SizeD viewport);
PointD ViewportToImage(ViewportState state, PointD viewportPoint);
PointD ImageToViewport(ViewportState state, PointD imagePoint);
bool IsInsideImage(PointD imagePoint, SizeD image);
```

Zoom-range operations validate finite minimum >0 and maximum ≥ minimum. Requested nonfinite zoom throws; finite zoom clamps. Invalid sizes cause safe identity/center fallbacks rather than a rendered image. Coordinate conversion substitutes zoom 1 for invalid state zoom. Constrain centers an image smaller than the viewport and clamps a larger image against its edges. Boundary constraints may override exact cursor anchoring when image edges would leave empty space.

## Compatibility policy

Alpha contracts are not frozen. Pin package versions, record target framework/runtime and native dependencies, compile your adapter against that tag, and run the examples/tests before upgrading. Browser demo APIs, private Pro source, and roadmap names are not supported public SDK contracts.
