# Architecture

PixelViewport separates domain contracts, interaction math, and UI adapters so camera/vendor dependencies remain at the application edge.

```text
camera / file / OpenCV / native producer
                  |
             PixelFrame lease
                  |
          LatestFrameMailbox
                  |
      UI adapter + display backend
                  |
    operator input, pixels, overlays
```

## `PixelViewport.Core`

Pure .NET geometry and viewport transforms with no UI dependency:

- fit and centering;
- cursor-anchored zoom;
- constrained pan;
- image/viewport coordinate conversion;
- framework-neutral rectangles and sizes.

## `PixelViewport.Imaging`

Framework-neutral live-image contracts:

- validated managed and unmanaged `PixelFrame` leases;
- explicit producer/consumer ownership and disposal;
- common mono/RGB pixel formats;
- raw pixel inspection;
- deterministic BGRA conversion;
- single-slot latest-frame delivery with submitted/taken/dropped metrics;
- framework-neutral rectangle overlay records.

`PixelFrame` intentionally does not depend on OpenCvSharp or a camera SDK. Integration packages or application adapters translate those lifetimes at the edge.

## `PixelViewport.OpenCvSharp`

An optional edge adapter maps common two-dimensional `Mat` types to the shared frame contract. `TakeOwnership` transfers the matrix lifetime into a zero-copy `PixelFrame`; `Copy` packs visible rows into PixelViewport-owned memory. OpenCvSharp remains outside Core and Imaging, so applications that do not use it take no dependency.

## `PixelViewport.WinUI`

The original Windows baseline uses a templated control and compositor transforms for static `ImageSource` workflows. It remains supported as a verified reference adapter; it is not yet wired to the new live-frame pipeline.

## `PixelViewport.Avalonia`

The evaluation adapter drains the newest frame on the UI thread, converts it to BGRA32, writes an Avalonia bitmap, and renders interaction/overlays using the shared math. The displayed `PixelFrame` lease remains alive for pixel inspection until a newer frame replaces it.

This is a portability and correctness proof. A production backend may move conversion/upload off the UI thread or use native/GPU resources without changing the public frame and coordinate contracts.

## Invariants

- Submission transfers frame ownership.
- Replaced pending frames are disposed.
- Queue depth never exceeds one pending frame.
- Image-space coordinates do not change with UI framework.
- Public contracts contain no application, camera-vendor, or facility concepts.
- Performance claims name the source format, target hardware, workload, and measurement boundary.
- Third-party integration packages remain optional edge dependencies.
