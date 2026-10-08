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
    ("documentation examples compile and present original Gray16", HeadlessCases.DocumentationExamples),
    ("demo frame sources preserve all six formats and padded rows", HeadlessCases.DemoFrameSources),
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
    public static void DocumentationExamples()
    {
        using var viewport = new ImageViewport();
        var window = new Window { Width = 320, Height = 240, Content = viewport };
        window.Show();
        PixelViewport.Documentation.IntegrationExamples.SubmitManagedLease(viewport);
        PixelViewport.Documentation.IntegrationExamples.ShowAnalysisRegion(viewport);
        Dispatcher.UIThread.RunJobs();
        Check.That(viewport.TryGetPixel(new Point(639, 0), out PixelSample sample), "Gray16 documentation frame absent");
        Check.Equal((ushort)65535, sample.Intensity, "Raw maximum intensity");
        Check.Equal(16, sample.BitsPerChannel, "BitsPerChannel");
        Point position = viewport.ImageToViewport(new Point(639, 0));
        Check.That(PixelViewport.Documentation.IntegrationExamples.InspectAtViewportPoint(viewport, position).Contains("65535"), "Inspection recipe failed");
        PixelViewport.Documentation.IntegrationExamples.SubmitCameraCallback(viewport, new byte[] {1,2,3,255}, 1,1,4);
        Dispatcher.UIThread.RunJobs();
        using var retained = new global::OpenCvSharp.Mat(1,1,global::OpenCvSharp.MatType.CV_8UC1, new global::OpenCvSharp.Scalar(42));
        PixelViewport.Documentation.IntegrationExamples.SubmitRetainedMat(viewport, retained);
        Dispatcher.UIThread.RunJobs();
        var transferred = new global::OpenCvSharp.Mat(1,1,global::OpenCvSharp.MatType.CV_8UC1, new global::OpenCvSharp.Scalar(43));
        PixelViewport.Documentation.IntegrationExamples.SubmitOwnedMat(viewport, transferred);
        Dispatcher.UIThread.RunJobs();
        Check.That(viewport.TryGetPixel(new Point(0,0), out sample) && sample.Intensity == 43, "Mat recipes failed");
        window.Close();
    }

    public static void DemoFrameSources()
    {
        foreach (ImagePixelFormat format in Enum.GetValues<ImagePixelFormat>())
        foreach (PixelViewport.VisionDemo.FrameSource source in Enum.GetValues<PixelViewport.VisionDemo.FrameSource>())
        foreach (bool padded in new[] { false, true })
        {
            var settings = new PixelViewport.VisionDemo.DemoSettings(format, source, padded, 30);
            using PixelFrame frame = PixelViewport.VisionDemo.SyntheticFrames.Create(settings, 1);
            PixelSample sample = frame.GetPixel(500,280);
            Check.Equal(format == ImagePixelFormat.Gray16LittleEndian ? 16 : 8, sample.BitsPerChannel, "Demo bit depth");
            Check.Equal(960, frame.Width, "Demo width");
            Check.That(frame.Stride >= frame.Format.GetMinimumStride(frame.Width), "Demo stride invalid");
            var output = new byte[frame.Width * frame.Height * 4];
            PixelFrameConverter.CopyToBgra32(frame, output, frame.Width * 4);
            int offset = (280 * frame.Width + 500) * 4;
            byte red = sample.BitsPerChannel == 16 ? (byte)(sample.Red >> 8) : (byte)sample.Red;
            Check.Equal(red, output[offset + 2], "Demo conversion red");
        }
    }

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
