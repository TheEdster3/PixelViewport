# Windows evaluation application

The downloadable VisionDemo is a native .NET/Avalonia application, separate from the website's JavaScript illustration. It exercises implemented Community functionality with generated data. It is not a camera acquisition application or the planned Professional edition.

## Download and run

Download [PixelViewport-VisionDemo-win-x64.zip](https://github.com/TheEdster3/PixelViewport/releases/download/v0.2.0-alpha.2/PixelViewport-VisionDemo-win-x64.zip) and [SHA256SUMS.txt](https://github.com/TheEdster3/PixelViewport/releases/download/v0.2.0-alpha.2/SHA256SUMS.txt). Extract the entire archive to a writable directory; run PixelViewport.VisionDemo.exe. Do not run from inside the ZIP or copy only the executable. The archive is self-contained: no .NET SDK or separate .NET installation is needed. Intended target: Windows 10/11 x64; native launch is verified on Windows 11 build 22631 only. Other OS versions, architectures, and platforms are not validated.

The alpha is unsigned. Windows may show a reputation/security warning. Review the source and provenance, verify hashes, and follow your organization's software policy; do not disable security protections. This is evaluation software, not a production certification.

## Explore the controls

| Control | What it demonstrates |
| --- | --- |
| Pixel format | Gray8, Gray16LittleEndian, RGB24, BGR24, RGBA32, BGRA32 |
| Frame source | Managed lease, safe copy, native lease, OpenCvSharp copy, OpenCvSharp ownership |
| Target FPS | 5, 30, or 60 generation target; not achieved display FPS |
| Pause / Step | Hold a frame for inspection; request one new frame |
| Padded rows | Raw row padding; non-contiguous Mat ROI examples |
| Fit / 100% | Full-frame fit; centered one image pixel per DIP |
| Wheel / left drag | Pointer-anchored zoom and constrained pan |
| Crosshair / overlays | Toggle native crosshair and image-space rectangle outlines |
| Pan / wheel toggles | Enable/disable the corresponding native interactions |
| Footer | Sequence, submitted/taken/dropped counts, wall-clock age, original channels/intensity |

OpenCV selections map RGB24/RGBA32 requests to BGR24/BGRA32 because these are the supported Mat conventions. The footer calls out that mapping. Alpha examples are opaque; the API also accepts straight-alpha data. Rectangles are generated examples, not detector output.

Pause stops generation after any in-flight frame; changing a source/format while paused requests a fresh frame. Fit/100% remain usable while paused. An error stops the producer and displays the error; restart the sample after resolving deployment prerequisites.

## A five-minute evaluation

1. Start with Gray16 and managed lease. Move the pointer; observe original 16-bit intensities (channel range 0–65535), not the eight-bit display approximation.
2. Pause, zoom with the wheel, then drag. Confirm image coordinates change while raw values belong to the selected pixels.
3. Toggle overlays/crosshair. Rectangles remain in image coordinates; the crosshair follows viewport position.
4. Enable padded rows and switch through the formats and ownership sources. Expect consistent channel ordering.
5. Resume at the rate appropriate to your hardware. Inspect drop counts and responsiveness; do not infer acquisition/scan-out latency from wall-clock age.

## Scope and companion samples

VisionDemo covers the raw-frame/Avalonia path and optional Mat adapter. The distinct WinUI ImageSource control is demonstrated by samples/PixelViewport.Sample in the source repository, including transformed overlay content. Its API is documented separately; do not infer that all WinUI and Avalonia capabilities are interchangeable.

Not in this download: camera drivers, image-file decoding UI, GPU/zero-copy rendering, window/level, editable ROI, calibrated measurements, private Pro code, or Linux validation. The browser's +/-/F/arrow shortcuts are not native SDK shortcuts.

## Reproduce the build

```powershell
./scripts/verify.ps1
./scripts/publish-demo.ps1 -Version 0.2.0-alpha.2
```

The release workflow runs verification and creates the ZIP, license notices, package provenance, and SHA-256 manifest from the tagged source. Keep the whole dependency/license directory when redistributing. Read docs/dependencies.md for the license record. The sample targets evaluation correctness, not minimum allocations or sustained performance.
