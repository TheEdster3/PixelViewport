namespace PixelViewport.Imaging;

/// <summary>
/// A thread-safe, single-slot frame queue. Submission transfers ownership to the
/// mailbox; a stale frame is disposed when a newer frame replaces it.
/// </summary>
public sealed class LatestFrameMailbox : IDisposable
{
    private readonly object _sync = new();
    private PixelFrame? _latest;
    private bool _isDisposed;
    private long _submitted;
    private long _taken;
    private long _dropped;

    public FrameMailboxStatistics Statistics
    {
        get
        {
            lock (_sync)
            {
                return new FrameMailboxStatistics(_submitted, _taken, _dropped, _latest is not null);
            }
        }
    }

    public void Submit(PixelFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);

        PixelFrame? replaced;
        lock (_sync)
        {
            if (_isDisposed)
            {
                frame.Dispose();
                throw new ObjectDisposedException(nameof(LatestFrameMailbox));
            }

            replaced = _latest;
            _latest = frame;
            _submitted++;
            if (replaced is not null)
            {
                _dropped++;
            }
        }

        replaced?.Dispose();
    }

    /// <summary>
    /// Transfers ownership of the latest pending frame to the caller.
    /// </summary>
    public bool TryTake(out PixelFrame? frame)
    {
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this);

            frame = _latest;
            _latest = null;
            if (frame is null)
            {
                return false;
            }

            _taken++;
            return true;
        }
    }

    public void Dispose()
    {
        PixelFrame? pending;
        lock (_sync)
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            pending = _latest;
            _latest = null;
        }

        pending?.Dispose();
    }
}

public readonly record struct FrameMailboxStatistics(
    long Submitted,
    long Taken,
    long Dropped,
    bool HasPendingFrame);
