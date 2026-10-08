# Integrating with an AI coding assistant

More context helps only when it is accurate, relevant, and versioned. Start with the package/API reference, the ownership guide, and the compiled recipes. Do not send a coding assistant a roadmap as if it described available methods.

## Context files

The website exposes [llms.txt](https://pixelviewport-vision.glassyloach1.chatgpt.site/llms.txt) as a documentation index and [llms-full.txt](https://pixelviewport-vision.glassyloach1.chatgpt.site/llms-full.txt) as a plain-text bundle of the maintained guides and compiled examples, plus a [structured SDK manifest](https://pixelviewport-vision.glassyloach1.chatgpt.site/sdk-manifest.json). These are convenience files, not an authentication protocol, executable agent instructions, or a guarantee that every assistant will discover them automatically. Attach/link the relevant documents yourself.

Prefer scoped context: API + ownership + one integration recipe. Use the full bundle when the assistant must design an integration across packages. Keep the release tag, SDK, UI framework, source format, stride, and lifetime model with the context.

## A ready-to-adapt integration brief

```text
Integrate PixelViewport Community into my existing .NET 8 Avalonia application.
Read the API reference, ownership/threading guide, and compiled examples first.
Use only APIs implemented in the selected public release/source tag.

Framework: Avalonia [version]. OS: Windows [version].
Release/source tag: [exact version; current download is v0.2.0-alpha.2].
Input: [managed camera callback / native buffer / OpenCvSharp Mat].
Format: [exact ImagePixelFormat or MatType].
Dimensions: [width, height]. Stride: [bytes per row, including padding].
Lifetime: [callback-only / retained camera lease / pool owner / transferred Mat].
Target rate: [FPS target, not an existing performance guarantee].
Overlay requirements: [basic rectangles; describe any unmet needs separately].

Keep acquisition and processing outside the viewport.
Use CopyFrom/MatFrameAdapter.Copy when source lifetime cannot be retained.
Create/configure/dispose UI controls on the UI thread. SubmitFrame may run
on the producer thread. Do not queue every frame on the UI dispatcher.
Do not dispose/reuse a submitted frame or its bytes from producer code.
Use BitsPerChannel, not BitDepth. TryGetPixel takes IMAGE coordinates.
Do not invent Avalonia ZoomIn, ZoomAt, LUT, ROI-edit, GPU, or WPF APIs.
Keep WinUI and Avalonia namespaces/contracts distinct.

Deliver a compiling integration, ownership/shutdown explanation, and tests
for format/stride, raw inspection, drop behavior, and disposal on rejection.
Report unsupported requirements and unverified platforms explicitly.
Do not upload proprietary frames, camera secrets, or customer data.
```

Replace bracketed fields with actual facts. This template is a request for your chosen assistant; it does not authorize it to access credentials, publish your repository, or contact others.

## Common hallucinations to catch

| Incorrect assumption | Actual contract |
| --- | --- |
| sample.BitDepth | sample.BitsPerChannel |
| Avalonia viewport.ZoomIn()/ZoomAt() | Not public Avalonia methods in this alpha; WinUI has its own commands |
| TryGetPixel takes a screen point | It takes image coordinates; convert first |
| Wrap makes bytes immutable | It leases the same backing bytes |
| TakeOwnership means zero-copy display | It avoids an input copy; renderer still converts/presents a bitmap |
| Label/Confidence are drawn annotations | Community renders rectangle outlines only |
| Browser F/arrows/+ shortcuts are native SDK shortcuts | Browser behavior is separate |
| Acquisition timestamp is monotonic latency | PresentationAge is wall-clock diagnostic |
| Cross-platform framework means Linux verified | Windows only has been validated |

## Acceptance checklist

- Pin package versions and verify package source; do not assume NuGet.org publication.
- Compile against the actual source/tag, not copied pseudo-APIs.
- Test a known pixel pattern, padded rows, and each intended MatType.
- Confirm no camera buffer is returned or reused while still leased.
- Confirm shutdown cancels/joins the producer and disposes the viewport on UI thread.
- Exercise a producer faster than presentation; confirm bounded pending memory and counters.
- Check pointer/image conversions after zoom, pan, resize, and DPI changes.
- Measure the complete application separately from the CPU conversion probe.
- Record unsupported formats/features and target-platform risks before production use.

## Review generated code

The compiler catches nonexistent names, but not lifetime errors. A producer-side using around a transferred Mat can compile and still corrupt the display. Review every ownership boundary, exception path, dispatcher call, and queue. AI-generated output needs the same tests and review as handwritten integration code.
