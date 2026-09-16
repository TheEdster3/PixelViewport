using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using PixelViewport.Core.Geometry;
using Windows.Foundation;

namespace PixelViewport.WinUI.Controls;

[TemplatePart(Name = PartSurface, Type = typeof(Grid))]
[TemplatePart(Name = PartImageLayer, Type = typeof(Grid))]
[TemplatePart(Name = PartImage, Type = typeof(Image))]
[TemplatePart(Name = PartTransform, Type = typeof(CompositeTransform))]
public sealed class ImageViewport : Control
{
    private const string PartSurface = "PART_Surface";
    private const string PartImageLayer = "PART_ImageLayer";
    private const string PartImage = "PART_Image";
    private const string PartTransform = "PART_Transform";

    private Grid? _surface;
    private Grid? _imageLayer;
    private Image? _image;
    private CompositeTransform? _transform;

    private ViewportState _state = ViewportState.Identity;
    private SizeD _imageSize;
    private bool _isUpdatingZoomProperty;
    private bool _hasUserView;
    private bool _isMousePanning;
    private uint? _capturedPointerId;
    private Point _lastPointerPosition;

    public ImageViewport()
    {
        DefaultStyleKey = typeof(ImageViewport);
        IsTabStop = true;

        SizeChanged += OnSizeChanged;
        PointerWheelChanged += OnPointerWheelChanged;
        PointerPressed += OnPointerPressed;
        PointerMoved += OnPointerMoved;
        PointerReleased += OnPointerReleased;
        PointerCanceled += OnPointerCanceled;
        PointerCaptureLost += OnPointerCaptureLost;
        ManipulationDelta += OnManipulationDelta;
        ManipulationCompleted += OnManipulationCompleted;
    }

    public event EventHandler<ImagePointerEventArgs>? ImagePointerMoved;

    public event EventHandler<ViewportChangedEventArgs>? ViewChanged;

    public static readonly DependencyProperty SourceProperty = DependencyProperty.Register(
        nameof(Source),
        typeof(ImageSource),
        typeof(ImageViewport),
        new PropertyMetadata(null, OnSourceChanged));

    public static readonly DependencyProperty OverlayContentProperty = DependencyProperty.Register(
        nameof(OverlayContent),
        typeof(object),
        typeof(ImageViewport),
        new PropertyMetadata(null));

    public static readonly DependencyProperty ViewportOverlayContentProperty = DependencyProperty.Register(
        nameof(ViewportOverlayContent),
        typeof(object),
        typeof(ImageViewport),
        new PropertyMetadata(null));

    public static readonly DependencyProperty ZoomFactorProperty = DependencyProperty.Register(
        nameof(ZoomFactor),
        typeof(double),
        typeof(ImageViewport),
        new PropertyMetadata(1d, OnZoomFactorChanged));

    public static readonly DependencyProperty MinZoomFactorProperty = DependencyProperty.Register(
        nameof(MinZoomFactor),
        typeof(double),
        typeof(ImageViewport),
        new PropertyMetadata(0.05d, OnZoomLimitChanged));

    public static readonly DependencyProperty MaxZoomFactorProperty = DependencyProperty.Register(
        nameof(MaxZoomFactor),
        typeof(double),
        typeof(ImageViewport),
        new PropertyMetadata(32d, OnZoomLimitChanged));

    public static readonly DependencyProperty ZoomStepProperty = DependencyProperty.Register(
        nameof(ZoomStep),
        typeof(double),
        typeof(ImageViewport),
        new PropertyMetadata(1.2d));

    public static readonly DependencyProperty AutoFitProperty = DependencyProperty.Register(
        nameof(AutoFit),
        typeof(bool),
        typeof(ImageViewport),
        new PropertyMetadata(true));

    public static readonly DependencyProperty IsPanEnabledProperty = DependencyProperty.Register(
        nameof(IsPanEnabled),
        typeof(bool),
        typeof(ImageViewport),
        new PropertyMetadata(true));

    public static readonly DependencyProperty IsMouseWheelZoomEnabledProperty = DependencyProperty.Register(
        nameof(IsMouseWheelZoomEnabled),
        typeof(bool),
        typeof(ImageViewport),
        new PropertyMetadata(true));

    public static readonly DependencyProperty ViewportBackgroundProperty = DependencyProperty.Register(
        nameof(ViewportBackground),
        typeof(Brush),
        typeof(ImageViewport),
        new PropertyMetadata(null));

    public ImageSource? Source
    {
        get => (ImageSource?)GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    public object? OverlayContent
    {
        get => GetValue(OverlayContentProperty);
        set => SetValue(OverlayContentProperty, value);
    }

    public object? ViewportOverlayContent
    {
        get => GetValue(ViewportOverlayContentProperty);
        set => SetValue(ViewportOverlayContentProperty, value);
    }

    public double ZoomFactor
    {
        get => (double)GetValue(ZoomFactorProperty);
        set => SetValue(ZoomFactorProperty, value);
    }

    public double MinZoomFactor
    {
        get => (double)GetValue(MinZoomFactorProperty);
        set => SetValue(MinZoomFactorProperty, value);
    }

    public double MaxZoomFactor
    {
        get => (double)GetValue(MaxZoomFactorProperty);
        set => SetValue(MaxZoomFactorProperty, value);
    }

    public double ZoomStep
    {
        get => (double)GetValue(ZoomStepProperty);
        set => SetValue(ZoomStepProperty, value);
    }

    public bool AutoFit
    {
        get => (bool)GetValue(AutoFitProperty);
        set => SetValue(AutoFitProperty, value);
    }

    public bool IsPanEnabled
    {
        get => (bool)GetValue(IsPanEnabledProperty);
        set => SetValue(IsPanEnabledProperty, value);
    }

    public bool IsMouseWheelZoomEnabled
    {
        get => (bool)GetValue(IsMouseWheelZoomEnabledProperty);
        set => SetValue(IsMouseWheelZoomEnabledProperty, value);
    }

    public Brush? ViewportBackground
    {
        get => (Brush?)GetValue(ViewportBackgroundProperty);
        set => SetValue(ViewportBackgroundProperty, value);
    }

    protected override void OnApplyTemplate()
    {
        DetachTemplatePartHandlers();
        base.OnApplyTemplate();

        _surface = GetTemplateChild(PartSurface) as Grid;
        _imageLayer = GetTemplateChild(PartImageLayer) as Grid;
        _image = GetTemplateChild(PartImage) as Image;
        _transform = GetTemplateChild(PartTransform) as CompositeTransform;

        if (_surface is not null)
        {
            _surface.ManipulationMode = ManipulationModes.TranslateX
                | ManipulationModes.TranslateY
                | ManipulationModes.Scale;
        }

        if (_image is not null)
        {
            _image.ImageOpened += OnImageOpened;
            _image.ImageFailed += OnImageFailed;
            _image.Source = Source;
        }

        UpdateImageSize();
        UpdateClip();

        if (AutoFit)
        {
            QueueFit();
        }
        else
        {
            QueueRequestedZoom();
        }
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

    public void ZoomIn()
    {
        ZoomAt(ZoomFactor * GetValidZoomStep(), GetViewportCenter());
    }

    public void ZoomOut()
    {
        ZoomAt(ZoomFactor / GetValidZoomStep(), GetViewportCenter());
    }

    public void ZoomAt(double zoomFactor, Point viewportAnchor)
    {
        if (!CanTransform())
        {
            return;
        }

        _state = ViewportMath.ZoomAt(
            _state,
            zoomFactor,
            viewportAnchor.ToPointD(),
            _imageSize,
            GetViewportSize(),
            GetValidMinZoom(),
            GetValidMaxZoom());

        _hasUserView = true;
        ApplyState();
    }

    public Point ViewportToImage(Point viewportPoint)
    {
        PointD point = ViewportMath.ViewportToImage(_state, viewportPoint.ToPointD());
        return new Point(point.X, point.Y);
    }

    public Point ImageToViewport(Point imagePoint)
    {
        PointD point = ViewportMath.ImageToViewport(_state, imagePoint.ToPointD());
        return new Point(point.X, point.Y);
    }

    public bool IsInsideImage(Point imagePoint)
    {
        return ViewportMath.IsInsideImage(imagePoint.ToPointD(), _imageSize);
    }

    private static void OnSourceChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is not ImageViewport viewport)
        {
            return;
        }

        viewport._imageSize = default;
        viewport._hasUserView = false;

        if (viewport._image is not null)
        {
            viewport._image.Source = args.NewValue as ImageSource;
        }

        viewport.UpdateImageSize();

        if (viewport.AutoFit)
        {
            viewport.QueueFit();
        }
        else
        {
            viewport.QueueRequestedZoom();
        }
    }

    private static void OnZoomFactorChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is not ImageViewport viewport || viewport._isUpdatingZoomProperty)
        {
            return;
        }

        double requestedZoom = args.NewValue is double zoom ? zoom : 1d;
        if (!double.IsFinite(requestedZoom))
        {
            viewport.SyncZoomProperty();
            return;
        }

        viewport.ZoomAt(requestedZoom, viewport.GetViewportCenter());
    }

    private static void OnZoomLimitChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is not ImageViewport viewport || !viewport.CanTransform())
        {
            return;
        }

        double minZoom = viewport.GetValidMinZoom();
        double maxZoom = viewport.GetValidMaxZoom();
        double clampedZoom = Math.Clamp(viewport._state.Zoom, minZoom, maxZoom);

        viewport._state = ViewportMath.ZoomAt(
            viewport._state,
            clampedZoom,
            viewport.GetViewportCenter().ToPointD(),
            viewport._imageSize,
            viewport.GetViewportSize(),
            minZoom,
            maxZoom);

        viewport.ApplyState();
    }

    private void OnImageOpened(object sender, RoutedEventArgs e)
    {
        UpdateImageSize();

        if (AutoFit)
        {
            QueueFit();
        }
        else
        {
            QueueRequestedZoom();
        }
    }

    private void OnImageFailed(object sender, ExceptionRoutedEventArgs e)
    {
        _imageSize = default;
        _state = ViewportState.Identity;
        ApplyState();
    }

    private void UpdateImageSize()
    {
        if (_image is null || _imageLayer is null || Source is null)
        {
            return;
        }

        double width = 0;
        double height = 0;

        if (Source is BitmapSource bitmapSource
            && bitmapSource.PixelWidth > 0
            && bitmapSource.PixelHeight > 0)
        {
            width = bitmapSource.PixelWidth;
            height = bitmapSource.PixelHeight;
        }
        else if (_image.ActualWidth > 0 && _image.ActualHeight > 0)
        {
            width = _image.ActualWidth;
            height = _image.ActualHeight;
        }

        if (width <= 0 || height <= 0)
        {
            return;
        }

        _imageSize = new SizeD(width, height);
        _image.Width = width;
        _image.Height = height;
        _imageLayer.Width = width;
        _imageLayer.Height = height;
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateClip();

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

    private void UpdateClip()
    {
        if (_surface is null || ActualWidth <= 0 || ActualHeight <= 0)
        {
            return;
        }

        _surface.Clip = new RectangleGeometry
        {
            Rect = new Rect(0, 0, ActualWidth, ActualHeight),
        };
    }

    private void OnPointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        if (!IsMouseWheelZoomEnabled || !CanTransform())
        {
            return;
        }

        int wheelDelta = e.GetCurrentPoint(this).Properties.MouseWheelDelta;
        if (wheelDelta == 0)
        {
            return;
        }

        double notches = wheelDelta / 120d;
        double factor = Math.Pow(GetValidZoomStep(), notches);
        Point anchor = e.GetCurrentPoint(this).Position;

        ZoomAt(_state.Zoom * factor, anchor);
        e.Handled = true;
    }

    private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (!IsPanEnabled || !CanTransform())
        {
            return;
        }

        var point = e.GetCurrentPoint(this);
        if (e.Pointer.PointerDeviceType != PointerDeviceType.Mouse || !point.Properties.IsLeftButtonPressed)
        {
            return;
        }

        _isMousePanning = true;
        _capturedPointerId = e.Pointer.PointerId;
        _lastPointerPosition = point.Position;
        CapturePointer(e.Pointer);
        e.Handled = true;
    }

    private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        Point position = e.GetCurrentPoint(this).Position;
        RaiseImagePointerMoved(position);

        if (!_isMousePanning
            || !IsPanEnabled
            || _capturedPointerId != e.Pointer.PointerId
            || !CanTransform())
        {
            return;
        }

        Point delta = new(
            position.X - _lastPointerPosition.X,
            position.Y - _lastPointerPosition.Y);

        _lastPointerPosition = position;
        _state = ViewportMath.PanBy(
            _state,
            delta.ToPointD(),
            _imageSize,
            GetViewportSize());

        _hasUserView = true;
        ApplyState();
        e.Handled = true;
    }

    private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        EndMousePan(e.Pointer);
    }

    private void OnPointerCanceled(object sender, PointerRoutedEventArgs e)
    {
        EndMousePan(e.Pointer);
    }

    private void OnPointerCaptureLost(object sender, PointerRoutedEventArgs e)
    {
        _isMousePanning = false;
        _capturedPointerId = null;
    }

    private void EndMousePan(Microsoft.UI.Xaml.Input.Pointer pointer)
    {
        if (_capturedPointerId != pointer.PointerId)
        {
            return;
        }

        _isMousePanning = false;
        _capturedPointerId = null;
        ReleasePointerCapture(pointer);
    }

    private void OnManipulationDelta(object sender, ManipulationDeltaRoutedEventArgs e)
    {
        if (!CanTransform())
        {
            return;
        }

        ViewportState next = _state;

        if (IsPanEnabled)
        {
            next = ViewportMath.PanBy(
                next,
                new PointD(e.Delta.Translation.X, e.Delta.Translation.Y),
                _imageSize,
                GetViewportSize());
        }

        if (e.Delta.Scale != 1f)
        {
            next = ViewportMath.ZoomAt(
                next,
                next.Zoom * e.Delta.Scale,
                e.Position.ToPointD(),
                _imageSize,
                GetViewportSize(),
                GetValidMinZoom(),
                GetValidMaxZoom());
        }

        _state = next;
        _hasUserView = true;
        ApplyState();
        e.Handled = true;
    }

    private void OnManipulationCompleted(object sender, ManipulationCompletedRoutedEventArgs e)
    {
        if (CanTransform())
        {
            _state = ViewportMath.Constrain(_state, _imageSize, GetViewportSize());
            ApplyState();
        }
    }

    private void RaiseImagePointerMoved(Point viewportPosition)
    {
        Point imagePosition = ViewportToImage(viewportPosition);
        ImagePointerMoved?.Invoke(
            this,
            new ImagePointerEventArgs(
                viewportPosition,
                imagePosition,
                IsInsideImage(imagePosition)));
    }

    private void ApplyState()
    {
        if (_transform is null)
        {
            return;
        }

        _transform.ScaleX = _state.Zoom;
        _transform.ScaleY = _state.Zoom;
        _transform.TranslateX = _state.PanX;
        _transform.TranslateY = _state.PanY;

        SyncZoomProperty();
        ViewChanged?.Invoke(this, new ViewportChangedEventArgs(_state.Zoom, _state.PanX, _state.PanY));
    }

    private void SyncZoomProperty()
    {
        if (Math.Abs(ZoomFactor - _state.Zoom) < 0.000001)
        {
            return;
        }

        _isUpdatingZoomProperty = true;
        try
        {
            SetValue(ZoomFactorProperty, _state.Zoom);
        }
        finally
        {
            _isUpdatingZoomProperty = false;
        }
    }

    private void QueueFit()
    {
        if (!AutoFit)
        {
            return;
        }

        DispatcherQueue.TryEnqueue(() =>
        {
            UpdateImageSize();
            FitToViewport();
        });
    }

    private void QueueRequestedZoom()
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            UpdateImageSize();

            if (!CanTransform())
            {
                return;
            }

            _state = ViewportMath.CenterAtZoom(
                _imageSize,
                GetViewportSize(),
                ZoomFactor,
                GetValidMinZoom(),
                GetValidMaxZoom());

            ApplyState();
        });
    }

    private void DetachTemplatePartHandlers()
    {
        if (_image is not null)
        {
            _image.ImageOpened -= OnImageOpened;
            _image.ImageFailed -= OnImageFailed;
        }
    }

    private bool CanTransform()
    {
        return _imageSize.IsValid && ActualWidth > 0 && ActualHeight > 0;
    }

    private SizeD GetViewportSize() => new(ActualWidth, ActualHeight);

    private Point GetViewportCenter() => new(ActualWidth / 2d, ActualHeight / 2d);

    private double GetValidMinZoom()
    {
        return double.IsFinite(MinZoomFactor) && MinZoomFactor > 0 ? MinZoomFactor : 0.01d;
    }

    private double GetValidMaxZoom()
    {
        double minZoom = GetValidMinZoom();
        double maxZoom = double.IsFinite(MaxZoomFactor) ? MaxZoomFactor : 32d;
        return Math.Max(minZoom, maxZoom);
    }

    private double GetValidZoomStep()
    {
        return double.IsFinite(ZoomStep) && ZoomStep > 1d ? ZoomStep : 1.2d;
    }
}

internal static class PointExtensions
{
    public static PointD ToPointD(this Point point) => new(point.X, point.Y);
}
