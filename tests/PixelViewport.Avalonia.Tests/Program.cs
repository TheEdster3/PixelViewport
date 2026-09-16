using global::Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using PixelViewport.Avalonia;
using PixelViewport.Imaging;

using HeadlessUnitTestSession session = HeadlessUnitTestSession.StartNew(typeof(TestAppBuilder));
var cases = new (string Name, Action Test)[]
{
    ("submitted frame is presented and inspectable", HeadlessCases.SubmittedFrameIsPresentedAndInspectable),
    ("wheel zoom preserves its image-space anchor", HeadlessCases.WheelZoomPreservesAnchor),
    ("left drag changes pan after zoom", HeadlessCases.LeftDragChangesPan),
    ("disposed viewport releases rejected frame", HeadlessCases.DisposedViewportReleasesRejectedFrame),
};

foreach ((string name, Action test) in cases)
{
    await session.Dispatch(test, CancellationToken.None);
    Console.WriteLine($"PASS: {name}");
}

Console.WriteLine($"Avalonia headless verification passed: {cases.Length}/{cases.Length}");

public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<TestApplication>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions());
}

public sealed class TestApplication : Application
{
    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
    }
}

internal static class HeadlessCases
{
    public static void SubmittedFrameIsPresentedAndInspectable()
    {
        using var viewport = new ImageViewport();
        var window = new Window { Width = 320, Height = 240, Content = viewport };
        FramePresentedEventArgs? presented = null;
        viewport.FramePresented += (_, args) => presented = args;
        window.Show();

        viewport.SubmitFrame(PixelFrame.Wrap(
            new byte[]
            {
                1, 2, 3, 255, 4, 5, 6, 255,
                7, 8, 9, 255, 10, 11, 12, 255,
            },
            2,
            2,
            8,
            ImagePixelFormat.Bgra32,
            17,
            DateTimeOffset.UtcNow));
        Dispatcher.UIThread.RunJobs();

        Check.That(presented is not null, "FramePresented was not raised.");
        Check.Equal(17L, presented!.SequenceNumber, "Presented sequence");
        Check.Equal(1L, presented.Statistics.Submitted, "Submitted count");
        Check.Equal(1L, presented.Statistics.Taken, "Taken count");
        Check.That(viewport.TryGetPixel(new Point(1, 1), out PixelSample sample), "Pixel was not inspectable.");
        Check.Equal((ushort)12, sample.Red, "Red channel");
        Check.Equal((ushort)11, sample.Green, "Green channel");
        Check.Equal((ushort)10, sample.Blue, "Blue channel");

        window.Close();
    }

    public static void WheelZoomPreservesAnchor()
    {
        using var viewport = new ImageViewport();
        var window = new Window { Width = 400, Height = 300, Content = viewport };
        window.Show();
        viewport.SubmitFrame(CreateBgraFrame(400, 300));
        Dispatcher.UIThread.RunJobs();

        var anchor = new Point(135, 92);
        Point before = viewport.ViewportToImage(anchor);
        double zoomBefore = viewport.ZoomFactor;

        window.MouseWheel(anchor, new Vector(0, 1), RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();

        Point after = viewport.ViewportToImage(anchor);
        Check.That(viewport.ZoomFactor > zoomBefore, "Wheel input did not increase zoom.");
        Check.Near(before.X, after.X, 1e-8, "Zoom anchor X");
        Check.Near(before.Y, after.Y, 1e-8, "Zoom anchor Y");

        window.Close();
    }

    public static void LeftDragChangesPan()
    {
        using var viewport = new ImageViewport();
        var window = new Window { Width = 320, Height = 240, Content = viewport };
        window.Show();
        viewport.SubmitFrame(CreateBgraFrame(640, 480));
        Dispatcher.UIThread.RunJobs();

        var anchor = new Point(160, 120);
        window.MouseWheel(anchor, new Vector(0, 2), RawInputModifiers.None);
        Point before = viewport.ViewportToImage(anchor);

        window.MouseDown(anchor, MouseButton.Left, RawInputModifiers.None);
        window.MouseMove(new Point(185, 140), RawInputModifiers.LeftMouseButton);
        window.MouseUp(new Point(185, 140), MouseButton.Left, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();

        Point after = viewport.ViewportToImage(anchor);
        Check.That(before != after, "Drag input did not change the view.");

        window.Close();
    }

    public static void DisposedViewportReleasesRejectedFrame()
    {
        var lifetime = new CountingDisposable();
        var viewport = new ImageViewport();
        viewport.Dispose();
        PixelFrame frame = PixelFrame.Wrap(
            new byte[] { 1 },
            1,
            1,
            1,
            ImagePixelFormat.Gray8,
            lifetime: lifetime);

        Check.Throws<ObjectDisposedException>(() => viewport.SubmitFrame(frame));
        Check.Equal(1, lifetime.DisposeCount, "Rejected-frame dispose count");
    }

    private static PixelFrame CreateBgraFrame(int width, int height) =>
        PixelFrame.Wrap(
            new byte[checked(width * height * 4)],
            width,
            height,
            width * 4,
            ImagePixelFormat.Bgra32,
            timestamp: DateTimeOffset.UtcNow);

    private sealed class CountingDisposable : IDisposable
    {
        public int DisposeCount { get; private set; }

        public void Dispose() => DisposeCount++;
    }
}

internal static class Check
{
    public static void That(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    public static void Equal<T>(T expected, T actual, string label)
        where T : IEquatable<T>
    {
        if (!expected.Equals(actual))
        {
            throw new InvalidOperationException($"{label}: expected {expected}, actual {actual}.");
        }
    }

    public static void Near(double expected, double actual, double tolerance, string label)
    {
        if (Math.Abs(expected - actual) > tolerance)
        {
            throw new InvalidOperationException($"{label}: expected {expected}, actual {actual}.");
        }
    }

    public static void Throws<TException>(Action action)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }

        throw new InvalidOperationException($"Expected {typeof(TException).Name}.");
    }
}
