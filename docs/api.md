# Public API overview

## Imaging contracts

`PixelFrame` describes one uncompressed frame:

- dimensions, stride, and `ImagePixelFormat`;
- optional sequence number and acquisition timestamp;
- either managed `ReadOnlyMemory<byte>` or an unmanaged address;
- an optional lifetime object disposed with the frame.

Factories:

- `Wrap(...)` — managed memory, no copy;
- `WrapUnmanaged(...)` — native memory, no copy;
- `CopyFrom(...)` — safe copy into pooled memory.

Pixel formats: `Gray8`, `Gray16LittleEndian`, `Rgb24`, `Bgr24`, `Rgba32`, and `Bgra32`.

`GetPixel(x, y)` returns display RGBA plus raw intensity/bit depth. `PixelFrameConverter.CopyToBgra32` supplies the current deterministic display conversion.

`LatestFrameMailbox` owns submitted frames. When a newer frame replaces a pending frame, the stale one is disposed and the drop counter increases. `TryTake` transfers the newest pending frame to the consumer.

## OpenCvSharp adapter

`MatFrameAdapter.TakeOwnership(Mat, ...)` wraps supported matrix memory without copying and transfers matrix disposal to the returned frame. `MatFrameAdapter.Copy(Mat, ...)` copies visible rows and leaves matrix ownership with the caller.

Supported matrix types:

| OpenCV type | PixelViewport format |
| --- | --- |
| `CV_8UC1` | `Gray8` |
| `CV_16UC1` | `Gray16LittleEndian` |
| `CV_8UC3` | `Bgr24` |
| `CV_8UC4` | `Bgra32` |

Regions of interest with padded/non-contiguous row steps are supported.

## Avalonia `ImageViewport`

Key properties:

| Property | Default | Purpose |
| --- | ---: | --- |
| `AutoFit` | `true` | Fit until the user changes the view |
| `MinZoomFactor` | `0.01` | Lower zoom bound |
| `MaxZoomFactor` | `64` | Upper zoom bound |
| `ZoomStep` | `1.2` | Wheel multiplier |
| `IsPanEnabled` | `true` | Enable left-drag pan |
| `IsMouseWheelZoomEnabled` | `true` | Enable pointer-anchored wheel zoom |
| `ShowCrosshair` | `true` | Draw a viewport-space pointer crosshair |

Methods:

- `SubmitFrame(PixelFrame)` — transfers ownership;
- `SetOverlays(IEnumerable<RectangleOverlay>?)`;
- `FitToViewport()`;
- `ZoomToActualSize()`;
- `ViewportToImage(Point)` / `ImageToViewport(Point)`;
- `TryGetPixel(Point, out PixelSample)`.

Events:

- `FramePresented` — sequence/acquisition timestamp, presentation age, and mailbox metrics;
- `ImagePointerMoved` — viewport/image position and optional pixel sample;
- `ViewChanged` — zoom and pan.

## WinUI `ImageViewport`

The WinUI baseline retains its `ImageSource`-based API documented in [`quickstart.md`](quickstart.md), including fit/actual size, zoom/pan, transformed overlay content, and pointer/view events.

## Coordinate convention

Image coordinate `(0, 0)` is the top-left pixel-space origin. At zoom `1.0`, one image pixel maps to one device-independent pixel before platform scaling.
