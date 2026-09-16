# Alpha launch copy

## One-line pitch

The vendor-neutral .NET display and interaction layer between a vision pipeline and its operator.

## Short description

PixelViewport Vision helps .NET machine-vision and scientific-imaging teams accept managed or native frames, keep live display work bounded, inspect high-bit-depth pixels, and maintain stable image coordinates across zoom, pan, and overlays. The first public alpha includes framework-neutral contracts, the verified WinUI baseline, an Avalonia evaluation adapter, tests, and a reproducible CPU conversion probe.

## Technical alpha post

PixelViewport Vision 0.2.0-alpha.1 is available for technical evaluation. This release establishes explicit frame-lifetime semantics, six uncompressed pixel formats, raw 16-bit inspection, latest-frame-wins delivery, and a live Avalonia demo while preserving the WinUI viewport baseline. The current Avalonia renderer is a correctness-first CPU path; we are seeking representative industrial workloads before publishing production latency or GPU claims.

We are looking to speak with .NET machine builders, vision integrators, and instrument teams that maintain their own live-image viewport. The goal is to learn the actual formats, frame rates, hardware, overlay loads, and deployment constraints—not to collect generic feature requests.
