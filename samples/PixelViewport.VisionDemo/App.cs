using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;

namespace PixelViewport.VisionDemo;

public sealed class App : Application
{
    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var window = new MainWindow();
            desktop.MainWindow = window;
            if (desktop.Args?.Contains("--smoke-test") == true)
            {
                // Exercise deployed native dependencies, not just managed default input.
                try
                {
                    foreach (var format in Enum.GetValues<PixelViewport.Imaging.ImagePixelFormat>())
                    foreach (var source in Enum.GetValues<FrameSource>())
                    foreach (bool padding in new[] { false, true })
                    {
                        using var frame = SyntheticFrames.Create(new DemoSettings(format, source, padding, 30), 1);
                        _ = frame.GetPixel(100, 100);
                    }
                }
                catch (Exception error)
                {
                    Console.Error.WriteLine(error);
                    Environment.Exit(3);
                }
                string? reportPath = desktop.Args.FirstOrDefault(a => a.StartsWith("--smoke-report="))?["--smoke-report=".Length..];
                bool completed = false;
                var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
                timer.Tick += (_, _) => { if (!completed) { completed = true; timer.Stop(); desktop.Shutdown(2); } };
                window.SmokeFramePresented += (_, _) => {
                    if (completed) return;
                    completed = true;
                    timer.Stop();
                    if (reportPath is not null)
                        System.IO.File.WriteAllText(reportPath, System.Text.Json.JsonSerializer.Serialize(new {
                            nativePresentation = true, frameSourceCombinations = 60,
                            runtimeVersion = Environment.Version.ToString(),
                            openCvBuild = global::OpenCvSharp.Cv2.GetBuildInformation()
                        }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
                    Console.WriteLine("Native Windows frame presentation succeeded.");
                    Dispatcher.UIThread.Post(() => desktop.Shutdown(0));
                };
                timer.Start();
            }
        }

        base.OnFrameworkInitializationCompleted();
    }
}
