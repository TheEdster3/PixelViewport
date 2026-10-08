using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using PixelViewport.Core.Geometry;
using PixelViewport.Imaging;
using VisionViewport = PixelViewport.Avalonia.ImageViewport;

namespace PixelViewport.VisionDemo;

public sealed class MainWindow : Window
{
    public event EventHandler? SmokeFramePresented;
    private readonly VisionViewport _viewport = new() { Background = Brush("#060E13"), Margin = new Thickness(12) };
    private readonly TextBlock _frameStatus = Status("Waiting for a frame…");
    private readonly TextBlock _pointerStatus = Status("Move over the image to inspect original pixels.");
    private readonly TextBlock _streamStatus = Status("Synthetic data / CPU evaluation renderer / Windows x64");
    private readonly TextBlock _errorStatus = Status("");
    private readonly CancellationTokenSource _cancellation = new();
    private DemoSettings _settings = new(ImagePixelFormat.Gray16LittleEndian, FrameSource.ManagedLease, false, 30);
    private Task? _producer;
    private long _sequence;
    private int _paused, _step;
    private bool _showOverlays = true;

    public MainWindow()
    {
        Title = "PixelViewport Vision — Community evaluation";
        Width = 1280; Height = 850; MinWidth = 820; MinHeight = 600;
        Background = Brush("#F6F8F8");
        _viewport.FramePresented += OnFramePresented;
        _viewport.ImagePointerMoved += OnImagePointerMoved;
        _viewport.ViewChanged += (_, e) => _streamStatus.Text = $"View: {e.Zoom:P0} / pan {e.PanX:F1}, {e.PanY:F1} DIPs";
        Content = BuildLayout();
        Opened += (_, _) => _producer = Task.Run(() => ProduceAsync(_cancellation.Token));
        Closed += (_, _) => {
            _cancellation.Cancel();
            _viewport.Dispose();
            if (_producer is not null) _ = _producer.ContinueWith(_ => _cancellation.Dispose(), TaskScheduler.Default);
            else _cancellation.Dispose();
        };
    }

    private Control BuildLayout()
    {
        var root = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,*,Auto"), Margin = new Thickness(22) };
        var heading = new StackPanel { Spacing = 4, Margin = new Thickness(0,0,0,18) };
        heading.Children.Add(new TextBlock { Text = "PIXELVIEWPORT / VISION", Foreground = Brush("#006C57"), FontSize = 14 });
        heading.Children.Add(new TextBlock { Text = "Community evaluation surface", FontSize = 28, Foreground = Brush("#132025") });
        heading.Children.Add(new TextBlock { Text = "Six formats. Explicit ownership. Raw inspection. No GPU, acquisition, or production-rate claim.", TextWrapping = TextWrapping.Wrap, Foreground = Brush("#536169") });
        root.Children.Add(heading);

        var controls = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0,0,0,12) };
        var formats = new ComboBox { ItemsSource = Enum.GetValues<ImagePixelFormat>(), SelectedItem = ImagePixelFormat.Gray16LittleEndian, Width = 190, Margin = new Thickness(0,4,10,4) };
        var sources = new ComboBox { ItemsSource = Enum.GetValues<FrameSource>(), SelectedItem = FrameSource.ManagedLease, Width = 190, Margin = new Thickness(0,4,10,4) };
        var rates = new ComboBox { ItemsSource = new[] { 5, 30, 60 }, SelectedItem = 30, Width = 70, Margin = new Thickness(0,4,10,4) };
        global::Avalonia.Automation.AutomationProperties.SetName(formats, "Pixel format");
        global::Avalonia.Automation.AutomationProperties.SetName(sources, "Frame ownership source");
        global::Avalonia.Automation.AutomationProperties.SetName(rates, "Target generation frames per second");
        ToolTip.SetTip(formats, "Input pixel format");
        ToolTip.SetTip(sources, "Buffer ownership/integration example");
        ToolTip.SetTip(rates, "Generation target FPS, not guaranteed display FPS");
        formats.SelectionChanged += (_, _) => { if (formats.SelectedItem is ImagePixelFormat f) UpdateSettings(s => s with { Format = f }); };
        sources.SelectionChanged += (_, _) => { if (sources.SelectedItem is FrameSource source) UpdateSettings(s => s with { Source = source }); };
        rates.SelectionChanged += (_, _) => { if (rates.SelectedItem is int fps) UpdateSettings(s => s with { TargetFps = fps }); };
        controls.Children.Add(formats); controls.Children.Add(sources); controls.Children.Add(rates);
        var pause = Button("Pause", null);
        pause.Click += (_, _) => { int paused = Volatile.Read(ref _paused) == 0 ? 1 : 0; Volatile.Write(ref _paused, paused); pause.Content = paused == 1 ? "Resume" : "Pause"; };
        controls.Children.Add(pause);
        controls.Children.Add(Button("Step", (_, _) => { Volatile.Write(ref _paused, 1); pause.Content = "Resume"; Interlocked.Exchange(ref _step, 1); }));
        controls.Children.Add(Button("Fit", (_, _) => _viewport.FitToViewport()));
        controls.Children.Add(Button("100%", (_, _) => _viewport.ZoomToActualSize()));
        controls.Children.Add(Toggle("Overlays", true, value => { _showOverlays = value; if (!value) _viewport.SetOverlays(null); else SetOverlay(_sequence); }));
        controls.Children.Add(Toggle("Crosshair", true, value => _viewport.ShowCrosshair = value));
        controls.Children.Add(Toggle("Padded rows", false, value => UpdateSettings(s => s with { PaddedRows = value })));
        controls.Children.Add(Toggle("Pan", true, value => _viewport.IsPanEnabled = value));
        controls.Children.Add(Toggle("Wheel zoom", true, value => _viewport.IsMouseWheelZoomEnabled = value));
        Grid.SetRow(controls, 1); root.Children.Add(controls);

        var surface = new Border { Background = Brush("#111F25"), BorderBrush = Brush("#344957"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(3), Child = _viewport };
        Grid.SetRow(surface, 2); root.Children.Add(surface);
        var footer = new StackPanel { Spacing = 5, Margin = new Thickness(0,14,0,0) };
        footer.Children.Add(_frameStatus); footer.Children.Add(_pointerStatus); footer.Children.Add(_streamStatus);
        footer.Children.Add(Status("Wheel: cursor zoom / Left drag: pan / 100%: one pixel per DIP / target FPS is not guaranteed"));
        _errorStatus.Foreground = Brush("#A12923"); footer.Children.Add(_errorStatus);
        Grid.SetRow(footer, 3); root.Children.Add(footer);
        return root;
    }

    private void UpdateSettings(Func<DemoSettings, DemoSettings> update)
    {
        Volatile.Write(ref _settings, update(Volatile.Read(ref _settings)));
        // Changing the source while paused requests one fresh frame.
        Interlocked.Exchange(ref _step, 1);
    }

    private static Button Button(string label, EventHandler<global::Avalonia.Interactivity.RoutedEventArgs>? click)
    {
        var button = new Button { Content = label, Margin = new Thickness(0,4,8,4), Padding = new Thickness(13,7) };
        if (click is not null) button.Click += click;
        return button;
    }

    private static CheckBox Toggle(string label, bool initial, Action<bool> change)
    {
        var box = new CheckBox { Content = label, IsChecked = initial, Margin = new Thickness(4,4,14,4) };
        box.IsCheckedChanged += (_, _) => change(box.IsChecked == true);
        return box;
    }

    private static SolidColorBrush Brush(string color) => new(Color.Parse(color));
    private static TextBlock Status(string text) => new() { Text = text, FontFamily = new FontFamily("Consolas"), FontSize = 13, Foreground = Brush("#536169"), TextWrapping = TextWrapping.Wrap };

    private async Task ProduceAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                int targetFps = Volatile.Read(ref _settings).TargetFps;
                await Task.Delay(TimeSpan.FromSeconds(1d / targetFps), cancellationToken).ConfigureAwait(false);
                int stepRequested = Interlocked.Exchange(ref _step, 0);
                if (Volatile.Read(ref _paused) == 1 && stepRequested == 0) continue;
                DemoSettings settings = Volatile.Read(ref _settings);
                long sequence = Interlocked.Increment(ref _sequence);
                PixelFrame frame = SyntheticFrames.Create(settings, sequence);
                _viewport.SubmitFrame(frame); // Rejection during shutdown releases the frame.
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception error)
        {
            Volatile.Write(ref _paused, 1);
            Dispatcher.UIThread.Post(() => _errorStatus.Text = $"Producer stopped: {error.GetType().Name}: {error.Message}");
        }
    }

    private void SetOverlay(long sequence)
    {
        if (!_showOverlays) return;
        _viewport.SetOverlays([
            new RectangleOverlay(new RectD((sequence * 5) % (SyntheticFrames.Width - 180), 160 + (int)(70 * Math.Sin(sequence * .08)), 150, 110), "Example region", ColorArgb: 0xFFFFB020),
            new RectangleOverlay(new RectD(610,310,115,85), "Reference", ColorArgb: 0xFFA3F4BF)
        ]);
    }

    private void OnFramePresented(object? sender, PixelViewport.Avalonia.FramePresentedEventArgs e)
    {
        SmokeFramePresented?.Invoke(this, EventArgs.Empty);
        SetOverlay(e.SequenceNumber);
        DemoSettings settings = Volatile.Read(ref _settings);
        string age = e.PresentationAge is { } a ? $"{a.TotalMilliseconds:F1} ms" : "unknown";
        _frameStatus.Text = $"Frame {e.SequenceNumber:N0} / submitted {e.Statistics.Submitted:N0} / taken {e.Statistics.Taken:N0} / dropped {e.Statistics.Dropped:N0} / wall-clock age {age}";
        _streamStatus.Text = $"{settings.Source} / requested {settings.Format} / target {settings.TargetFps} FPS / {(settings.PaddedRows ? "padded rows" : "packed rows")}. OpenCV RGB/RGBA selections map to BGR/BGRA.";
    }

    private void OnImagePointerMoved(object? sender, PixelViewport.Avalonia.ImagePointerEventArgs e)
    {
        if (!e.IsInsideImage || e.Sample is not { } sample) { _pointerStatus.Text = "Outside image"; return; }
        _pointerStatus.Text = $"X {(int)e.ImagePosition.X} / Y {(int)e.ImagePosition.Y} / RGBA {sample.Red},{sample.Green},{sample.Blue},{sample.Alpha} / intensity {sample.Intensity} / {sample.BitsPerChannel}-bit";
    }
}
