# PixelViewport Vision

The vendor-neutral image display and interaction layer for .NET machine-vision, scientific-imaging, and inspection applications.

[![Windows CI](https://github.com/TheEdster3/PixelViewport/actions/workflows/ci.yml/badge.svg)](https://github.com/TheEdster3/PixelViewport/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/license-MIT-5ae8bb)](LICENSE)

[Website and interactive illustration](https://pixelviewport-vision.glassyloach1.chatgpt.site/) · [Alpha downloads](https://github.com/TheEdster3/PixelViewport/releases) · [Documentation](https://pixelviewport-vision.glassyloach1.chatgpt.site/docs.html) · [Windows demo](docs/demo.md) · [Quickstart](docs/quickstart.md) · [Request an evaluation](https://github.com/TheEdster3/PixelViewport/issues/new?template=evaluation.yml)

> **Status:** `0.2.0-alpha.2` foundation. WinUI is the verified Windows baseline. The new framework-neutral frame pipeline and Avalonia adapter are an evaluation-quality correctness path while production rendering is measured with real workloads.

## Why it exists

Camera SDKs and vision libraries acquire or process pixels; application teams still have to build the operator-facing viewport. PixelViewport supplies a stable boundary between those systems and the UI: explicit frame ownership, predictable live-stream backpressure, pixel inspection, overlays, and invariant image coordinates across zoom and pan.

## What is included

| Package | Purpose | Status |
| --- | --- | --- |
| `PixelViewport.Core` | Framework-independent viewport geometry and transforms | Stable MVP |
| `PixelViewport.Imaging` | Managed/unmanaged frame leases, six pixel formats, inspection, conversion, latest-frame delivery | Alpha |
| `PixelViewport.OpenCvSharp` | Explicit zero-copy ownership and safe-copy adapters for OpenCvSharp `Mat` | Alpha |
| `PixelViewport.WinUI` | Windows App SDK image viewport | Verified baseline |
| `PixelViewport.Avalonia` | Cross-platform live-frame evaluation adapter | Alpha |

The Avalonia adapter accepts `Gray8`, little-endian `Gray16`, `RGB24`, `BGR24`, `RGBA32`, and `BGRA32`. It provides cursor-centered wheel zoom, drag pan, fit/actual-size commands, pixel inspection, rectangle overlays, and a single-slot mailbox that drops stale frames instead of accumulating latency.

## Try the live proof on Windows

```powershell
dotnet run --project samples/PixelViewport.VisionDemo/PixelViewport.VisionDemo.csproj -c Release
```

Download the self-contained Windows application from the release, or build from source. The demo generates a 960×600 stream at a 30 FPS target on a background thread, with six selectable formats and five ownership sources. Drag to pan, use the mouse wheel to zoom around the cursor, and inspect RGB coordinates in the footer.

## Submit a frame

```csharp
using PixelViewport.Imaging;

PixelFrame frame = PixelFrame.Wrap(
    cameraBuffer,
    width,
    height,
    stride,
    ImagePixelFormat.Gray16LittleEndian,
    sequenceNumber,
    DateTimeOffset.UtcNow,
    cameraBufferLease);

viewport.SubmitFrame(frame); // ownership transfers to the viewport
```

`PixelFrame.Wrap` does not copy. The producer must keep the memory valid until the frame is disposed; pass a lease as the final argument when the camera SDK requires one. `PixelFrame.CopyFrom` is available when ownership cannot be shared safely.

## OpenCvSharp integration

```csharp
using OpenCvSharp;
using PixelViewport.OpenCvSharp;

Mat result = AcquireOrProcessFrame();
viewport.SubmitFrame(MatFrameAdapter.TakeOwnership(result));
```

`TakeOwnership` wraps `CV_8UC1`, `CV_16UC1`, `CV_8UC3`, or `CV_8UC4` without copying and disposes the matrix when the viewport releases the frame. Use `MatFrameAdapter.Copy` when the caller must retain ownership. The adapter package intentionally omits native OpenCV binaries; the host application selects the correct runtime package.

## Verify the repository

On Windows with the .NET 8 SDK:

```powershell
./scripts/verify.ps1
./scripts/package.ps1
```

Verification restores and builds every library, sample, and tool; runs geometry, imaging, native OpenCvSharp, and headless Avalonia checks; then runs a deterministic CPU conversion probe. Packages are written to `artifacts/packages`.

## Performance honesty

The current community Avalonia renderer converts to BGRA32 on the UI thread. That proves format, ownership, interaction, and bounded-queue semantics; it is not a zero-copy, GPU, or end-to-end latency claim. Run `tools/PixelViewport.Benchmarks` on target hardware and read [`docs/performance.md`](docs/performance.md) before evaluating production suitability.

## Product boundary

The public MIT packages provide the adoption path and durable contracts. The commercial product is planned around the expensive production problems: a measured low-latency renderer, richer 12/16-bit display transforms, interactive ROI tooling, high-volume overlays, source access, integration help, and support. See [`docs/product.md`](docs/product.md) and [`docs/design-partners.md`](docs/design-partners.md).

## Repository layout

```text
src/PixelViewport.Core             Viewport geometry/math
src/PixelViewport.Imaging          Frame and pixel contracts
src/PixelViewport.OpenCvSharp      Optional OpenCvSharp edge adapter
src/PixelViewport.WinUI            Windows adapter
src/PixelViewport.Avalonia         Cross-platform evaluation adapter
samples/PixelViewport.Sample       WinUI baseline demo
samples/PixelViewport.VisionDemo   Live Avalonia inspection proof
tests/                             Unit, native integration, and headless UI checks
tools/PixelViewport.Benchmarks     Reproducible CPU conversion probe
site/                              Static product site
```

## License

The packages in this repository are MIT licensed. PixelViewport Pro/private source remains separately licensed.

Direct dependency versions and redistribution notes are recorded in [`docs/dependencies.md`](docs/dependencies.md).

## Integration guides

- [OpenCvSharp to Avalonia](https://pixelviewport-vision.glassyloach1.chatgpt.site/opencv.html): ownership transfer, safe copy, native runtime selection, and supported Mat types.
- [Display and inspect 16-bit data](https://pixelviewport-vision.glassyloach1.chatgpt.site/gray16.html): little-endian buffers, stride, raw intensity, and today's high-byte display mapping.

The website demo is a JavaScript illustration, not a .NET renderer benchmark. Public issues are not a private support channel; never upload confidential frames or credentials.
