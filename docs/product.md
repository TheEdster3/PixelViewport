# Product direction

## Positioning

**PixelViewport Vision is the vendor-neutral display and interaction SDK between a .NET vision pipeline and its operator.**

The first customer is a technical lead or senior .NET developer at a machine builder, system integrator, instrument maker, or vision OEM. Their application displays live or high-bit-depth imagery, may support several camera/vision vendors, must remain maintainable for years, and commonly runs on Windows today with an Avalonia or embedded-Linux path ahead.

The product is not another camera SDK, computer-vision algorithm library, photo editor, or generic image control.

## Wedge

The initial promise is deliberately narrow:

> Put managed or native frames into one stable API; get bounded-latency presentation, reliable zoom/pan coordinates, pixel inspection, and image-space overlays without coupling the UI to a camera vendor.

## Editions

### Community — free / MIT

- framework-neutral geometry and frame contracts;
- explicit buffer ownership and disposal;
- six common uncompressed pixel formats;
- deterministic pixel inspection and CPU conversion;
- latest-frame-wins mailbox;
- WinUI baseline and Avalonia evaluation adapters.

### Professional — target $999 per developer per year

- measured production live renderer;
- configurable 12/16-bit window and level transforms;
- interactive rectangle, ellipse, polygon, and line ROI tools;
- selection, resize, keyboard editing, undo/redo, and serialization;
- production diagnostics, source access, updates, and email support.

### Team — target $3,999 per year for five developers

- Professional features;
- onboarding and integration session;
- shared support entitlement;
- defined maintenance response targets.

### Enterprise — from $10,000 per year

- redistribution and offline entitlement terms;
- prioritized fixes and roadmap review;
- architecture/integration support;
- negotiated support and procurement terms.

Prices are hypotheses until buyer conversations and paid integrations validate them. Do not activate checkout based on this document alone.

## Validation gates

Investment increases only when evidence increases:

1. Five problem interviews with qualified teams.
2. Three paid design partners totaling at least $7,500.
3. Two integrations using real camera or scientific-image data.
4. Published, reproducible results against agreed workloads.
5. At least one renewal or referenceable deployment before broad feature expansion.

Downloads, stars, and compliments are useful discovery signals; they are not proof of willingness to pay.

## Near-term priorities

1. Prove correctness and frame-lifetime safety across managed and native acquisition buffers.
2. Define a reproducible performance contract: resolution, format, FPS, percentile latency, dropped-frame policy, hardware, and overlay load.
3. Implement production rendering only against those target workloads.
4. Validate the completed optional OpenCvSharp edge adapter with real application matrices; add one vendor SDK adapter only after buyer hardware identifies the right vendor.
5. Build the smallest complete ROI workflow that a paid partner needs.

## Explicitly deferred

- broad annotation-suite features without customer evidence;
- cloud services, collaboration, and AI inference;
- runtime licensing infrastructure before misuse is real;
- macOS/mobile support;
- claims of zero-copy, 4K60, GPU acceleration, or Linux production readiness before measurement.
