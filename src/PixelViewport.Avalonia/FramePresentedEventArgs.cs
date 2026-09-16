using PixelViewport.Imaging;

namespace PixelViewport.Avalonia;

public sealed class FramePresentedEventArgs(
    long sequenceNumber,
    DateTimeOffset timestamp,
    DateTimeOffset presentedAt,
    FrameMailboxStatistics statistics) : EventArgs
{
    public long SequenceNumber { get; } = sequenceNumber;

    public DateTimeOffset Timestamp { get; } = timestamp;

    public DateTimeOffset PresentedAt { get; } = presentedAt;

    /// <summary>
    /// Wall-clock age at presentation when the producer supplied a timestamp.
    /// This is diagnostic telemetry, not a monotonic end-to-end latency guarantee.
    /// </summary>
    public TimeSpan? PresentationAge { get; } = timestamp == default
        ? null
        : presentedAt - timestamp;

    public FrameMailboxStatistics Statistics { get; } = statistics;
}
