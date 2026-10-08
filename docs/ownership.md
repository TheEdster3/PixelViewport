# Ownership, threading, and image correctness

## The ownership rule

A frame is a lease, not an immutable copy of its bytes. ReadOnlyMemory prevents mutation through that view; it does not prevent another owner from changing the backing array. Never overwrite or return leased pixels to a pool until the frame is released.

| Operation | Who owns the bytes afterward? | Who disposes? |
| --- | --- | --- |
| PixelFrame.Wrap | Producer supplies the backing memory and optional lease | Caller owns frame until submission; frame disposes lease |
| PixelFrame.WrapUnmanaged | Producer supplies readable native memory and optional lease | Same as Wrap; no lease means no automatic native free |
| PixelFrame.CopyFrom | New pooled copy owned by returned frame | Frame disposes its pool owner |
| MatFrameAdapter.TakeOwnership | Returned frame retains the original Mat | Frame disposes Mat; producer must stop using it |
| MatFrameAdapter.Copy | Caller retains Mat; frame owns a pooled copy | Caller disposes Mat, consumer disposes frame |
| LatestFrameMailbox.Submit | Mailbox owns submitted frame | Mailbox releases replaced/pending frames |
| LatestFrameMailbox.TryTake returning true | Consumer owns taken frame | Consumer disposes when finished |
| ImageViewport.SubmitFrame | Viewport owns frame | Replaces/disposes current and pending frames |

Do not put a submitted frame in a producer-side using block. Do not reuse or resubmit the same PixelFrame instance. Each submission needs its own independently owned frame lease.

## Failure and shutdown

Wrap and WrapUnmanaged validate before construction. If a factory rejects its arguments, the caller still owns the supplied lifetime object. Release it in the producer's failure path. CopyFrom cleans up its own allocation if construction fails. TakeOwnership transfers Mat ownership only after successful return; a rejected unsupported Mat remains the caller's responsibility.

Submit to a disposed mailbox/viewport disposes the incoming frame and then throws ObjectDisposedException. Treat that as normal shutdown only when the producer is stopping. Other exceptions are not a universal consume-on-failure contract: do not use a blanket catch that guesses ownership.

Stop/cancel the producer, prevent further submissions, and dispose the Avalonia viewport on its UI thread. Join/await the producer so failures are observed. A viewport keeps the displayed frame leased for raw inspection until a replacement or disposal; submission completion is not permission to reclaim the memory.

## Thread-affinity map

| Operation | Thread policy |
| --- | --- |
| Construct/validate PixelFrame or process source data | Producer thread; backing data must stay stable while consumed |
| LatestFrameMailbox.Submit/TryTake/Statistics | Lock-protected; different producers/consumer may call concurrently |
| Avalonia SubmitFrame | May be called from producer thread; posts a bounded UI drain |
| Avalonia SetOverlays | Copies valid metadata immediately, then applies on UI thread if necessary |
| Construct/add viewport, set styled properties, transforms, TryGetPixel, Dispose | UI thread |
| FramePresented, ViewChanged, ImagePointerMoved | UI thread; keep handlers short and non-throwing |
| Direct GetPixel/CopyRowTo/converter reads | Caller must ensure frame is not disposed or mutated concurrently |

PixelFrame.Dispose is idempotent, but read-versus-dispose is not a synchronized snapshot. Native pointer reads cannot validate that an address is mapped or that its declared buffer length is truthful. A lease must hold the real native allocation or camera SDK buffer lock.

## Bounded display is not lossless capture

The mailbox holds one pending frame. A new submission replaces the previous pending frame, releases its lease, and increments Dropped. TryTake increments Taken. The currently displayed frame is separate; it can stay leased while a newer frame is pending.

Store every acquisition frame elsewhere if every frame must be preserved. Display dropping is a freshness policy, not a recorder. Do not wrap a mailbox in an unbounded ConcurrentQueue or post one UI callback per frame.

## Describe the memory, not the file

PixelFrame accepts unpacked, uncompressed rows. It does not decode PNG/JPEG/TIFF or camera bit packing. Width and height must be positive; stride is positive bytes per row and at least width × bytes-per-pixel. The minimum readable length is (height − 1) × stride + visible row bytes. Padding after the last visible row is not required.

Negative stride/bottom-up input must be reoriented by the producer. Gray16LittleEndian is unsigned 16-bit data, low byte first. RGB/BGR ordering is literal. Alpha input and converted output use straight alpha, not premultiplied alpha.

Gray16 conversion uses raw >> 8. A narrow signal range may look dark even when raw values are valid. PixelSample preserves original 16-bit channels/intensity; it is not display RGBA. For color, Intensity is a weighted diagnostic luminance (rounded 0.2126R + 0.7152G + 0.0722B), not a calibrated measurement.

## Coordinates and metadata

The origin is the top-left image edge. Image positions are continuous pixel-space coordinates; floor to select a pixel. Bounds are half-open: 0 ≤ x < width and 0 ≤ y < height. Zoom 1 means one pixel per device-independent unit, not necessarily one physical monitor pixel under DPI scaling.

Overlays use image coordinates. Crosshair is viewport-space. Confidence is metadata between 0 and 1; PixelViewport does not calculate confidence or detect defects. Keep analysis results matched to their frame sequence; SetOverlays is not an atomic frame-plus-analysis transaction.

## Measurement boundary

FramePresented means CPU conversion and bitmap assignment completed. It does not mean monitor scan-out occurred. PresentationAge uses supplied timestamp and wall clock; it may be negative when clocks differ. Use monotonic application measurements for latency decisions. No-copy wrapping only removes an input copy; the current renderer still converts/uploads pixels.
