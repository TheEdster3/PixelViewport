# Troubleshooting an integration

## Blank viewport

Check that the control is in a visible layout with nonzero bounds and that a valid PixelFrame has been submitted. Create the control on the UI thread. If no FramePresented event occurs, verify the UI dispatcher is running and not blocked by synchronous acquisition/processing. FitToViewport is a no-op before valid image/bounds exist.

For WinUI, check the Source is an ImageSource, the default theme/template is included, and image loading completed. WinUI does not accept SubmitFrame.

## Dark or flat Gray16

Inspect raw Intensity and BitsPerChannel. The current display uses the high byte (raw >> 8); values 0–255 become almost black. This does not prove sensor data is empty. Window/level and LUT adjustment are not implemented. Scale a separate display buffer upstream if needed, and retain original values elsewhere if your mapping loses information.

## Wrong colors, slanted rows, or noisy pixels

Match RGB versus BGR literally. OpenCV CV_8UC3/CV_8UC4 is mapped to BGR/BGRA. Stride is bytes per row, not pixels. Include padding and supply enough readable bytes for the final visible row. Gray16 is little-endian and unpacked. Negative stride, Bayer mosaics, float pixels, and packed 10/12-bit values need upstream conversion.

Unexpected flicker often means the producer is overwriting/returning a buffer while still leased. Use a safe copy to distinguish lifetime bugs from conversion bugs.

## ObjectDisposedException during shutdown

A disposed viewport/mailbox rejects and releases incoming frames. Stop the producer and observe its completion; do not keep retrying forever. If this occurs during normal operation, inspect producer-side using blocks, Mat disposal, duplicate submission, and camera pool recycling.

## OpenCvSharp native load failure

The optional adapter supplies managed contracts, not the operating-system runtime. Match OpenCvSharp4.runtime.win to the supported managed package version for Windows x64; inspect process architecture and native dependency resolution. Missing OpenCvSharpExtern.dll, mismatched x86/x64, or native prerequisites can produce DllNotFoundException or TypeInitializationException. The downloadable Windows sample includes the matching native runtime for its Mat examples; it does not acquire video.

## Pan appears not to move

An image smaller than the viewport is intentionally centered. Zoom until it exceeds the viewport on an axis, then drag. Check IsPanEnabled and avoid parent controls intercepting pointer input. Bounds constraints prevent panning the entire image off-screen.

## Inspection does not match the pointer

TryGetPixel takes image coordinates. Call ViewportToImage before inspection of a viewport point. Use Math.Floor, not rounding, to select a pixel. At zoom 1, coordinates are DIPs; monitor scaling can produce more than one physical pixel per DIP. During a drag the pointer event precedes that movement's pan update; query again after ViewChanged if necessary.

## Dropped frames or growing age

Dropped counts are expected when a producer replaces a pending frame. Confirm there is no extra unbounded queue upstream. Keep processing off the UI thread and UI event handlers short. The current CPU renderer may not meet your target rate at large resolutions. Reduce the workload or evaluate a production-renderer requirement; do not label a target FPS as measured throughput.

PresentationAge can reflect clock adjustments and mismatched timestamp origins. It is not camera-to-screen latency or a monotonic duration.

## Overlay labels do not appear

Community draws outlines, not Label/Confidence text. Invalid bounds or confidence values are filtered. SetOverlays(null) clears the collection. Use image coordinates and associate analysis with the displayed sequence yourself. Editing/calibrated measurement/high-volume rendering are not Community features.

## Reporting a reproducible problem

Provide package/source tag, .NET/framework versions, Windows version, process architecture, format, dimensions, stride, lifetime choice, expected versus actual behavior, and a minimal synthetic reproduction. Include measured boundaries for performance reports. Public GitHub issues are not confidential; do not attach production images, customer information, camera credentials, or proprietary algorithms.
