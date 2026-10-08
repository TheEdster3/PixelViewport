# Compiled integration recipes

These examples are compiled and exercised by the headless verification project. Each recipe is extracted from [IntegrationExamples.cs](examples/IntegrationExamples.cs), so the website code and compiled code share a source.

## Shared imports

Use System.Buffers, System.Buffers.Binary, global::Avalonia, PixelViewport.Imaging, PixelViewport.Core.Geometry, and PixelViewport.OpenCvSharp. Alias VisionViewport to PixelViewport.Avalonia.ImageViewport. Each method assumes an existing Avalonia UI application; these are integration helpers, not a standalone program.

<!-- snippet:imports -->

## Managed lease

Create a full-range Gray16 image in pooled memory. Transfer the lifetime to the frame; clean it up if factory validation fails. Do not dispose the owner after submission.

<!-- snippet:managed-lease -->

## Safe camera callback copy

When the camera callback's buffer cannot be retained, copy before returning from the callback. Keep camera acquisition in your application.

<!-- snippet:safe-copy -->

## Retained OpenCvSharp Mat

Make a frame copy while retaining caller ownership of the original Mat. Supply the matching native runtime in the host application.

<!-- snippet:opencv-copy -->

## Transferred OpenCvSharp Mat

Pass a Mat the producer no longer needs. A producer-side using block around that Mat would be incorrect after transfer.

<!-- snippet:opencv-transfer -->

## Pointer inspection

Convert from viewport DIPs to image coordinates before querying the pixel. This preserves the distinction between a raw sample and its display mapping.

<!-- snippet:inspection -->

## Analysis rectangle

The application supplies the rectangle and optional metadata. Community renders an outline only; it does not calculate detection results or confidence.

<!-- snippet:overlays -->

## Shutdown

Cancel and await your producer before closing/disposal. Dispose the viewport on its UI thread. If cancellation races a final SubmitFrame, the disposed mailbox releases the rejected incoming frame and throws ObjectDisposedException. Do not catch and ignore unrelated lifetime errors.
