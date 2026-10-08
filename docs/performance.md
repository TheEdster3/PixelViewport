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

## Windows launch evidence — October 8, 2026

This is a single local run, not an SLA or a comparison with another renderer.

- CPU: AMD Ryzen 7 5800X3D 8-Core Processor.
- OS: Windows NT 10.0.22631.0; runtime: .NET 8.0.31; SDK: 8.0.425.
- Release configuration; 1920x1080; random seed 1729; preallocated destination.
- Three warmups, 30 samples; median is sorted sample 16; p95 is sorted sample 29.
- Concurrent processes were not controlled. Wall-clock samples include scheduling interruptions.

| Format | Median conversion | p95 conversion | Median throughput |
| --- | --- | --- | --- |
| BGRA32 | 1.60 ms | 17.16 ms | 1297.2 MP/s |
| Gray16LittleEndian | 9.77 ms | 11.89 ms | 212.2 MP/s |

Mailbox-only burst: 10,000 submissions in 3.18 ms, 9,999 pending frames replaced, latest sequence 9,999 delivered. This is not a live-rendering frame rate.

The accompanying Windows verification passed 39 geometry tests, 27 imaging tests, 5 OpenCvSharp tests, and 4 Avalonia headless checks. A browser JavaScript illustration on the website is separate from these .NET measurements.

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
