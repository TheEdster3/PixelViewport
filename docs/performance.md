# Performance contract

Performance claims are valid only when the workload and measurement method are named.

## Current implementation

The public Avalonia adapter uses a correctness-first path:

1. a producer submits a `PixelFrame` from any thread;
2. a one-slot mailbox replaces and disposes stale frames;
3. the UI thread converts the newest frame to straight-alpha BGRA32;
4. an Avalonia `WriteableBitmap` presents the result;
5. the displayed frame remains leased for pixel inspection until replacement.

This bounds queue growth and keeps ownership deterministic. It does not eliminate conversion or upload cost.

## Reproduce the CPU probe

```powershell
dotnet run --project tools/PixelViewport.Benchmarks/PixelViewport.Benchmarks.csproj -c Release
```

The probe reports median and p95 conversion time for 1920×1080 BGRA32 and little-endian Gray16 frames, plus a mailbox burst. It intentionally excludes camera acquisition, UI scheduling, bitmap upload, composition, monitor scan-out, and overlays. Results describe only that machine and runtime.

## Production acceptance template

Agree on all of these before optimizing or making a claim:

- operating system, CPU, GPU, memory, and runtime;
- image width/height, pixel format, stride, and acquisition memory type;
- target FPS and burst behavior;
- overlay count and interaction workload;
- definition of presentation age and where timestamps are captured;
- p50, p95, and p99 thresholds;
- allowed drop policy and maximum memory growth;
- run duration and thermal state.

The first production target should come from a paid integration rather than an invented universal benchmark.
