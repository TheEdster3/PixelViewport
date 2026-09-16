using PixelViewport.Imaging;
using Xunit;

namespace PixelViewport.Imaging.Tests;

public sealed class LatestFrameMailboxTests
{
    [Fact]
    public void Submit_replaces_and_disposes_stale_frame()
    {
        var firstLifetime = new CountingDisposable();
        using var mailbox = new LatestFrameMailbox();
        mailbox.Submit(CreateFrame(1, firstLifetime));
        mailbox.Submit(CreateFrame(2));

        Assert.Equal(1, firstLifetime.DisposeCount);
        Assert.Equal(new FrameMailboxStatistics(2, 0, 1, true), mailbox.Statistics);
    }

    [Fact]
    public void TryTake_transfers_latest_frame_ownership()
    {
        using var mailbox = new LatestFrameMailbox();
        mailbox.Submit(CreateFrame(42));

        Assert.True(mailbox.TryTake(out PixelFrame? frame));
        using (frame)
        {
            Assert.NotNull(frame);
            Assert.Equal(42, frame.SequenceNumber);
        }

        Assert.False(mailbox.TryTake(out _));
        Assert.Equal(new FrameMailboxStatistics(1, 1, 0, false), mailbox.Statistics);
    }

    [Fact]
    public void Dispose_releases_pending_frame()
    {
        var lifetime = new CountingDisposable();
        var mailbox = new LatestFrameMailbox();
        mailbox.Submit(CreateFrame(1, lifetime));

        mailbox.Dispose();

        Assert.Equal(1, lifetime.DisposeCount);
    }

    [Fact]
    public void Submit_after_dispose_releases_rejected_frame()
    {
        var lifetime = new CountingDisposable();
        var mailbox = new LatestFrameMailbox();
        mailbox.Dispose();

        Assert.Throws<ObjectDisposedException>(() => mailbox.Submit(CreateFrame(1, lifetime)));
        Assert.Equal(1, lifetime.DisposeCount);
    }

    private static PixelFrame CreateFrame(long sequence, IDisposable? lifetime = null) =>
        PixelFrame.Wrap(
            new byte[] { 1 },
            1,
            1,
            1,
            ImagePixelFormat.Gray8,
            sequence,
            lifetime: lifetime);

    private sealed class CountingDisposable : IDisposable
    {
        public int DisposeCount { get; private set; }

        public void Dispose() => DisposeCount++;
    }
}
