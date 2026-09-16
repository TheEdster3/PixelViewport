using System.Buffers;
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
    private const int FrameWidth = 960;
    private const int FrameHeight = 600;
    private readonly VisionViewport _viewport;
    private readonly TextBlock _frameStatus;
    private readonly TextBlock _pointerStatus;
    private readonly TextBlock _streamStatus;
    private readonly CancellationTokenSource _streamCancellation = new();
    private long _sequence;

    public MainWindow()
    {
        Title = "PixelViewport Vision — live inspection proof";
        Width = 1280;
        Height = 820;
        MinWidth = 900;
        MinHeight = 600;
        Background = new SolidColorBrush(Color.Parse("#0B1017"));

        _viewport = new VisionViewport
        {
            Background = new SolidColorBrush(Color.Parse("#05080D")),
            Margin = new Thickness(18),
        };
        _viewport.FramePresented += OnFramePresented;
        _viewport.ImagePointerMoved += OnImagePointerMoved;

        _frameStatus = CreateStatusText("Waiting for first frame…");
        _pointerStatus = CreateStatusText("Pointer —");
        _streamStatus = CreateStatusText("Managed BGRA32 • synthetic source • 30 FPS target");

        Content = BuildLayout();
        Opened += OnOpened;
        Closed += OnClosed;
    }

    private Control BuildLayout()
    {
        var root = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*,Auto"),
        };

        var header = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            Margin = new Thickness(22, 18, 22, 8),
        };
        var title = new StackPanel { Spacing = 3 };
        title.Children.Add(new TextBlock
        {
            Text = "PIXELVIEWPORT VISION",
            FontSize = 13,
            FontWeight = FontWeight.SemiBold,
            Foreground = new SolidColorBrush(Color.Parse("#66D9EF")),
        });
        title.Children.Add(new TextBlock
        {
            Text = "Live inspection surface",
            FontSize = 27,
            FontWeight = FontWeight.SemiBold,
            Foreground = Brushes.White,
        });
        title.Children.Add(new TextBlock
        {
            Text = "Vendor-neutral frames • stable image coordinates • bounded latency",
            FontSize = 13,
            Foreground = new SolidColorBrush(Color.Parse("#91A0B5")),
        });
        header.Children.Add(title);

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            VerticalAlignment = VerticalAlignment.Center,
        };
        actions.Children.Add(CreateButton("Fit", (_, _) => _viewport.FitToViewport()));
        actions.Children.Add(CreateButton("100%", (_, _) => _viewport.ZoomToActualSize()));
        Grid.SetColumn(actions, 1);
        header.Children.Add(actions);
        Grid.SetRow(header, 0);
        root.Children.Add(header);

        var viewportBorder = new Border
        {
            Margin = new Thickness(22, 8),
            Background = new SolidColorBrush(Color.Parse("#111923")),
            BorderBrush = new SolidColorBrush(Color.Parse("#253244")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Child = _viewport,
        };
        Grid.SetRow(viewportBorder, 1);
        root.Children.Add(viewportBorder);

        var footer = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"),
            Margin = new Thickness(22, 8, 22, 18),
        };
        footer.Children.Add(_streamStatus);
        Grid.SetColumn(_frameStatus, 1);
        _frameStatus.Margin = new Thickness(24, 0, 0, 0);
        footer.Children.Add(_frameStatus);
        Grid.SetColumn(_pointerStatus, 2);
        _pointerStatus.Margin = new Thickness(24, 0, 0, 0);
        footer.Children.Add(_pointerStatus);
        Grid.SetRow(footer, 2);
        root.Children.Add(footer);

        return root;
    }

    private static Button CreateButton(string label, EventHandler<global::Avalonia.Interactivity.RoutedEventArgs> handler)
    {
        var button = new Button
        {
            Content = label,
            Padding = new Thickness(16, 8),
            HorizontalContentAlignment = HorizontalAlignment.Center,
        };
        button.Click += handler;
        return button;
    }

    private static TextBlock CreateStatusText(string text) => new()
    {
        Text = text,
        FontFamily = new FontFamily("Consolas"),
        FontSize = 12,
        Foreground = new SolidColorBrush(Color.Parse("#A9B6C8")),
        VerticalAlignment = VerticalAlignment.Center,
    };

    private void OnOpened(object? sender, EventArgs e)
    {
        _ = Task.Run(() => ProduceFramesAsync(_streamCancellation.Token));
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _streamCancellation.Cancel();
        _viewport.Dispose();
    }

    private async Task ProduceFramesAsync(CancellationToken cancellationToken)
    {
        int stride = FrameWidth * 4;
        int length = stride * FrameHeight;
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(1000d / 30d));

        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            {
                IMemoryOwner<byte> owner = MemoryPool<byte>.Shared.Rent(length);
                long sequence = Interlocked.Increment(ref _sequence);
                FillSyntheticFrame(owner.Memory.Span[..length], stride, sequence);
                PixelFrame frame = PixelFrame.Wrap(
                    owner.Memory[..length],
                    FrameWidth,
                    FrameHeight,
                    stride,
                    ImagePixelFormat.Bgra32,
                    sequence,
                    DateTimeOffset.UtcNow,
                    owner);

                try
                {
                    _viewport.SubmitFrame(frame);
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Expected during window shutdown.
        }
    }

    private static void FillSyntheticFrame(Span<byte> destination, int stride, long sequence)
    {
        int movingX = (int)((sequence * 5) % (FrameWidth - 180));
        int movingY = 160 + (int)(70 * Math.Sin(sequence * 0.08));

        for (int y = 0; y < FrameHeight; y++)
        {
            Span<byte> row = destination.Slice(y * stride, stride);
            for (int x = 0; x < FrameWidth; x++)
            {
                int offset = x * 4;
                int radial = Math.Abs(x - (FrameWidth / 2)) + Math.Abs(y - (FrameHeight / 2));
                byte texture = (byte)(24 + ((x * 7 + y * 3 + sequence) & 15));
                bool scanLine = ((x + sequence * 4) % 180) < 3;
                bool defect = x >= movingX && x < movingX + 150 && y >= movingY && y < movingY + 110;

                row[offset] = defect ? (byte)45 : texture;
                row[offset + 1] = defect ? (byte)84 : (byte)Math.Max(18, texture - (radial / 160));
                row[offset + 2] = defect ? (byte)218 : scanLine ? (byte)52 : (byte)(texture + 8);
                row[offset + 3] = byte.MaxValue;
            }
        }
    }

    private void OnFramePresented(object? sender, PixelViewport.Avalonia.FramePresentedEventArgs e)
    {
        long sequence = e.SequenceNumber;
        int movingX = (int)((sequence * 5) % (FrameWidth - 180));
        int movingY = 160 + (int)(70 * Math.Sin(sequence * 0.08));
        _viewport.SetOverlays(
        [
            new RectangleOverlay(
                new RectD(movingX, movingY, 150, 110),
                "surface anomaly",
                0.94,
                0xFFFFB020),
            new RectangleOverlay(
                new RectD(610, 310, 115, 85),
                "reference feature",
                0.87,
                0xFF36D399),
        ]);

        string age = e.PresentationAge is TimeSpan presentationAge
            ? $"age {Math.Max(0, presentationAge.TotalMilliseconds):F1} ms"
            : "age —";
        _frameStatus.Text =
            $"frame {sequence:N0}  dropped {e.Statistics.Dropped:N0}  {age}  zoom {_viewport.ZoomFactor:P0}";
    }

    private void OnImagePointerMoved(object? sender, PixelViewport.Avalonia.ImagePointerEventArgs e)
    {
        if (!e.IsInsideImage || e.Sample is null)
        {
            _pointerStatus.Text = "Pointer —";
            return;
        }

        PixelSample sample = e.Sample.Value;
        _pointerStatus.Text =
            $"x {(int)e.ImagePosition.X,4}  y {(int)e.ImagePosition.Y,4}  rgb {sample.Red,3}/{sample.Green,3}/{sample.Blue,3}";
    }
}
