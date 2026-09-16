using global::Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using PixelViewport.Core.Geometry;
using PixelViewport.Imaging;
using AvaloniaPoint = Avalonia.Point;
using AvaloniaRect = Avalonia.Rect;

namespace PixelViewport.Avalonia;

/// <summary>
/// Cross-platform evaluation viewport for managed and unmanaged PixelViewport frames.
/// The current community renderer converts frames to BGRA32 on the UI thread; it is
/// intentionally a correctness path, not a zero-copy performance claim.
/// </summary>
public sealed class ImageViewport : Control, IDisposable
{
    private readonly LatestFrameMailbox _mailbox = new();
    private WriteableBitmap? _bitmap;
    private PixelFrame? _currentFrame;
    private ViewportState _state = ViewportState.Identity;
    private SizeD _imageSize;
    private IReadOnlyList<RectangleOverlay> _overlays = [];
    private AvaloniaPoint _lastPointerPosition;
    private bool _isPanning;
    private bool _isPointerInside;
    private bool _hasUserView;
    private bool _disposed;
    private int _drainScheduled;

    public static readonly StyledProperty<IBrush?> BackgroundProperty =
        AvaloniaProperty.Register<ImageViewport, IBrush?>(nameof(Background), Brushes.Black);

    public static readonly StyledProperty<bool> AutoFitProperty =
        AvaloniaProperty.Register<ImageViewport, bool>(nameof(AutoFit), true);

    public static readonly StyledProperty<double> MinZoomFactorProperty =
        AvaloniaProperty.Register<ImageViewport, double>(nameof(MinZoomFactor), 0.01d);

    public static readonly StyledProperty<double> MaxZoomFactorProperty =
        AvaloniaProperty.Register<ImageViewport, double>(nameof(MaxZoomFactor), 64d);

    public static readonly StyledProperty<double> ZoomStepProperty =
        AvaloniaProperty.Register<ImageViewport, double>(nameof(ZoomStep), 1.2d);

    public static readonly StyledProperty<bool> IsPanEnabledProperty =
        AvaloniaProperty.Register<ImageViewport, bool>(nameof(IsPanEnabled), true);

    public static readonly StyledProperty<bool> IsMouseWheelZoomEnabledProperty =
        AvaloniaProperty.Register<ImageViewport, bool>(nameof(IsMouseWheelZoomEnabled), true);

    public static readonly StyledProperty<bool> ShowCrosshairProperty =
        AvaloniaProperty.Register<ImageViewport, bool>(nameof(ShowCrosshair), true);

    static ImageViewport()
    {
        AffectsRender<ImageViewport>(BackgroundProperty, ShowCrosshairProperty);
    }

    public ImageViewport()
    {
        Focusable = true;
        ClipToBounds = true;
        RenderOptions.SetBitmapInterpolationMode(this, BitmapInterpolationMode.None);
    }

    public event EventHandler<FramePresentedEventArgs>? FramePresented;

    public event EventHandler<ImagePointerEventArgs>? ImagePointerMoved;

    public event EventHandler<ViewportChangedEventArgs>? ViewChanged;

    public IBrush? Background
    {
        get => GetValue(BackgroundProperty);
        set => SetValue(BackgroundProperty, value);
    }

    public bool AutoFit
    {
        get => GetValue(AutoFitProperty);
        set => SetValue(AutoFitProperty, value);
    }

    public double MinZoomFactor
    {
        get => GetValue(MinZoomFactorProperty);
        set => SetValue(MinZoomFactorProperty, value);
    }

    public double MaxZoomFactor
    {
        get => GetValue(MaxZoomFactorProperty);
        set => SetValue(MaxZoomFactorProperty, value);
    }

    public double ZoomStep
    {
        get => GetValue(ZoomStepProperty);
        set => SetValue(ZoomStepProperty, value);
    }

    public bool IsPanEnabled
    {
        get => GetValue(IsPanEnabledProperty);
        set => SetValue(IsPanEnabledProperty, value);
    }

    public bool IsMouseWheelZoomEnabled
    {
        get => GetValue(IsMouseWheelZoomEnabledProperty);
        set => SetValue(IsMouseWheelZoomEnabledProperty, value);
    }

    public bool ShowCrosshair
    {
        get => GetValue(ShowCrosshairProperty);
        set => SetValue(ShowCrosshairProperty, value);
    }

    public double ZoomFactor => _state.Zoom;

    public FrameMailboxStatistics FrameStatistics => _mailbox.Statistics;

    /// <summary>
    /// Submits a frame and transfers ownership to the viewport. If rendering falls
    /// behind, an unrendered frame is disposed when a newer one replaces it.
    /// </summary>
    public void SubmitFrame(PixelFrame frame)
    {
        // LatestFrameMailbox owns the rejection path too: if disposal races a
        // producer, Submit disposes the frame before throwing.
        _mailbox.Submit(frame);
        ScheduleDrain();
    }

    public void SetOverlays(IEnumerable<RectangleOverlay>? overlays)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        RectangleOverlay[] snapshot = overlays?.Where(static overlay => overlay.IsValid).ToArray() ?? [];

        if (Dispatcher.UIThread.CheckAccess())
        {
            _overlays = snapshot;
            InvalidateVisual();
            return;
        }

        Dispatcher.UIThread.Post(() =>
        {
            if (_disposed)
            {
                return;
            }

            _overlays = snapshot;
            InvalidateVisual();
        }, DispatcherPriority.Render);
    }

    public void FitToViewport()
    {
        if (!CanTransform())
        {
            return;
        }

        _state = ViewportMath.Fit(
            _imageSize,
            GetViewportSize(),
            GetValidMinZoom(),
            GetValidMaxZoom());
        _hasUserView = false;
        ApplyState();
    }

    public void ZoomToActualSize()
    {
        if (!CanTransform())
        {
            return;
        }

        _state = ViewportMath.CenterAtZoom(
            _imageSize,
            GetViewportSize(),
            1d,
            GetValidMinZoom(),
            GetValidMaxZoom());
        _hasUserView = true;
        ApplyState();
    }

    public AvaloniaPoint ViewportToImage(AvaloniaPoint viewportPoint)
    {
        PointD result = ViewportMath.ViewportToImage(
            _state,
            new PointD(viewportPoint.X, viewportPoint.Y));
        return new AvaloniaPoint(result.X, result.Y);
    }

    public AvaloniaPoint ImageToViewport(AvaloniaPoint imagePoint)
    {
        PointD result = ViewportMath.ImageToViewport(
            _state,
            new PointD(imagePoint.X, imagePoint.Y));
        return new AvaloniaPoint(result.X, result.Y);
    }

    public bool TryGetPixel(AvaloniaPoint imagePoint, out PixelSample sample)
    {
        int x = (int)Math.Floor(imagePoint.X);
        int y = (int)Math.Floor(imagePoint.Y);
        if (_currentFrame is null || x < 0 || y < 0 || x >= _currentFrame.Width || y >= _currentFrame.Height)
        {
            sample = default;
            return false;
        }

        sample = _currentFrame.GetPixel(x, y);
        return true;
    }

    public override void Render(DrawingContext context)
    {
        AvaloniaRect viewportBounds = new(Bounds.Size);
        if (Background is not null)
        {
            context.FillRectangle(Background, viewportBounds);
        }

        if (_bitmap is null || !CanTransform())
        {
            return;
        }

        using (context.PushClip(viewportBounds))
        {
            var source = new AvaloniaRect(0, 0, _imageSize.Width, _imageSize.Height);
            var destination = new AvaloniaRect(
                _state.PanX,
                _state.PanY,
                _imageSize.Width * _state.Zoom,
                _imageSize.Height * _state.Zoom);
            context.DrawImage(_bitmap, source, destination);

            foreach (RectangleOverlay overlay in _overlays)
            {
                AvaloniaPoint topLeft = ImageToViewport(new AvaloniaPoint(overlay.Bounds.Left, overlay.Bounds.Top));
                AvaloniaPoint bottomRight = ImageToViewport(new AvaloniaPoint(overlay.Bounds.Right, overlay.Bounds.Bottom));
                var rectangle = new AvaloniaRect(topLeft, bottomRight);
                var color = Color.FromUInt32(overlay.ColorArgb);
                context.DrawRectangle(null, new Pen(new SolidColorBrush(color), 2), rectangle);
            }

            if (ShowCrosshair && _isPointerInside)
            {
                var crosshairPen = new Pen(new SolidColorBrush(Color.FromArgb(190, 102, 217, 239)), 1);
                context.DrawLine(
                    crosshairPen,
                    new AvaloniaPoint(_lastPointerPosition.X, 0),
                    new AvaloniaPoint(_lastPointerPosition.X, Bounds.Height));
                context.DrawLine(
                    crosshairPen,
                    new AvaloniaPoint(0, _lastPointerPosition.Y),
                    new AvaloniaPoint(Bounds.Width, _lastPointerPosition.Y));
            }
        }
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        if (!CanTransform())
        {
            return;
        }

        if (AutoFit && !_hasUserView)
        {
            FitToViewport();
            return;
        }

        _state = ViewportMath.Constrain(_state, _imageSize, GetViewportSize());
        ApplyState();
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        if (!IsMouseWheelZoomEnabled || !CanTransform() || e.Delta.Y == 0)
        {
            return;
        }

        AvaloniaPoint anchor = e.GetPosition(this);
        double requestedZoom = _state.Zoom * Math.Pow(GetValidZoomStep(), e.Delta.Y);
        _state = ViewportMath.ZoomAt(
            _state,
            requestedZoom,
            new PointD(anchor.X, anchor.Y),
            _imageSize,
            GetViewportSize(),
            GetValidMinZoom(),
            GetValidMaxZoom());
        _hasUserView = true;
        ApplyState();
        e.Handled = true;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Focus();
        if (!IsPanEnabled || !CanTransform() || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        _isPanning = true;
        _lastPointerPosition = e.GetPosition(this);
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        AvaloniaPoint position = e.GetPosition(this);
        AvaloniaPoint previousPosition = _lastPointerPosition;
        _lastPointerPosition = position;
        RaiseImagePointerMoved(position);

        if (!_isPanning || !IsPanEnabled || !CanTransform())
        {
            return;
        }

        _state = ViewportMath.PanBy(
            _state,
            new PointD(position.X - previousPosition.X, position.Y - previousPosition.Y),
            _imageSize,
            GetViewportSize());
        _hasUserView = true;
        ApplyState();
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        EndPan(e);
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        _isPointerInside = true;
        _lastPointerPosition = e.GetPosition(this);
        InvalidateVisual();
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _isPointerInside = false;
        InvalidateVisual();
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _isPanning = false;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _mailbox.Dispose();
        _currentFrame?.Dispose();
        _currentFrame = null;
        _bitmap?.Dispose();
        _bitmap = null;
    }

    private void EndPan(PointerReleasedEventArgs e)
    {
        if (!_isPanning)
        {
            return;
        }

        _isPanning = false;
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    private void ScheduleDrain()
    {
        if (Interlocked.Exchange(ref _drainScheduled, 1) != 0)
        {
            return;
        }

        Dispatcher.UIThread.Post(DrainLatestFrame, DispatcherPriority.Render);
    }

    private void DrainLatestFrame()
    {
        try
        {
            if (_disposed || !_mailbox.TryTake(out PixelFrame? frame) || frame is null)
            {
                return;
            }

            Present(frame);
        }
        finally
        {
            Interlocked.Exchange(ref _drainScheduled, 0);
            if (!_disposed && _mailbox.Statistics.HasPendingFrame)
            {
                ScheduleDrain();
            }
        }
    }

    private unsafe void Present(PixelFrame frame)
    {
        try
        {
            bool imageSizeChanged = frame.Width != _imageSize.Width || frame.Height != _imageSize.Height;
            if (_bitmap is null || imageSizeChanged)
            {
                _bitmap?.Dispose();
                _bitmap = new WriteableBitmap(
                    new PixelSize(frame.Width, frame.Height),
                    new Vector(96, 96),
                    global::Avalonia.Platform.PixelFormat.Bgra8888,
                    AlphaFormat.Unpremul);
                _imageSize = new SizeD(frame.Width, frame.Height);
            }

            using (ILockedFramebuffer framebuffer = _bitmap.Lock())
            {
                int length = checked(framebuffer.RowBytes * framebuffer.Size.Height);
                var destination = new Span<byte>((void*)framebuffer.Address, length);
                PixelFrameConverter.CopyToBgra32(frame, destination, framebuffer.RowBytes);
            }

            PixelFrame? previous = _currentFrame;
            _currentFrame = frame;
            frame = null!;
            previous?.Dispose();

            if (imageSizeChanged && AutoFit && !_hasUserView)
            {
                FitToViewport();
            }
            else
            {
                InvalidateVisual();
            }

            PixelFrame current = _currentFrame;
            FramePresented?.Invoke(
                this,
                new FramePresentedEventArgs(
                    current.SequenceNumber,
                    current.Timestamp,
                    DateTimeOffset.UtcNow,
                    _mailbox.Statistics));
        }
        finally
        {
            frame?.Dispose();
        }
    }

    private void RaiseImagePointerMoved(AvaloniaPoint viewportPosition)
    {
        AvaloniaPoint imagePosition = ViewportToImage(viewportPosition);
        bool inside = TryGetPixel(imagePosition, out PixelSample sample);
        ImagePointerMoved?.Invoke(
            this,
            new ImagePointerEventArgs(viewportPosition, imagePosition, inside, inside ? sample : null));
    }

    private void ApplyState()
    {
        InvalidateVisual();
        ViewChanged?.Invoke(this, new ViewportChangedEventArgs(_state.Zoom, _state.PanX, _state.PanY));
    }

    private bool CanTransform() => _imageSize.IsValid && Bounds.Width > 0 && Bounds.Height > 0;

    private SizeD GetViewportSize() => new(Bounds.Width, Bounds.Height);

    private double GetValidMinZoom() =>
        double.IsFinite(MinZoomFactor) && MinZoomFactor > 0 ? MinZoomFactor : 0.01d;

    private double GetValidMaxZoom()
    {
        double minimum = GetValidMinZoom();
        double maximum = double.IsFinite(MaxZoomFactor) ? MaxZoomFactor : 64d;
        return Math.Max(minimum, maximum);
    }

    private double GetValidZoomStep() =>
        double.IsFinite(ZoomStep) && ZoomStep > 1d ? ZoomStep : 1.2d;
}
