# Windows quickstart

## Run the Avalonia vision sample

Prerequisites: Windows and .NET 8 SDK. The solution also contains a WinUI sample using Windows App SDK.

```powershell
git clone https://github.com/TheEdster3/PixelViewport.git
cd PixelViewport
dotnet run --project samples/PixelViewport.VisionDemo -c Release
```

The sample generates 960x600 BGRA32 frames at a **30 FPS target**, not a throughput guarantee. Wheel zoom follows the cursor; left drag pans after zoom; Fit and 100% reset the view. The footer shows image coordinates, RGB values, sequence, replaced-frame count, and application presentation age.

```powershell
./scripts/verify.ps1
./scripts/package.ps1
```

Verification covers the Windows solution, 71 unit/native tests, four headless Avalonia checks, and a conversion probe. Packages go to `artifacts/packages`.

## Install alpha packages from the release

Download all `.nupkg` files from the [alpha release](https://github.com/TheEdster3/PixelViewport/releases), put them in a local folder, and add it as a feed. Do not assume NuGet.org listings exist until publication is confirmed.

```powershell
dotnet nuget add source C:/path/to/pixelviewport-packages --name PixelViewportLocal
dotnet add package PixelViewport.Avalonia --version 0.2.0-alpha.1
dotnet add package PixelViewport.OpenCvSharp --version 0.2.0-alpha.1
```

NuGet.org supplies adapter dependencies. Add a matching native OpenCV runtime separately when using OpenCvSharp. Review [dependency licenses](dependencies.md).

## Add an Avalonia viewport

```csharp
var viewport = new PixelViewport.Avalonia.ImageViewport();
// Add viewport to your window/layout on the UI thread.
viewport.SubmitFrame(PixelViewport.Imaging.PixelFrame.CopyFrom(
    pixels, width, height, stride,
    PixelViewport.Imaging.ImagePixelFormat.Bgra32));
```

Submission transfers ownership. Use `Wrap` with a lifetime lease for no-copy input; do not reuse that memory while leased. No-copy input is not a zero-copy renderer promise. Dispose the viewport when its host closes.

- [OpenCvSharp integration](https://pixelviewport-vision.glassyloach1.chatgpt.site/opencv.html)
- [16-bit display and inspection](https://pixelviewport-vision.glassyloach1.chatgpt.site/gray16.html)
- [Performance contract](performance.md)

## Existing WinUI baseline

```powershell
dotnet run --project samples/PixelViewport.Sample -c Release
```

In XAML, use `xmlns:pv="using:PixelViewport.WinUI.Controls"` and `<pv:ImageViewport Source="{x:Bind ViewModel.Image}" AutoFit="True" />`. The source is a WinUI image source; Avalonia raw-frame APIs are not interchangeable with WinUI APIs.

Interactive ROI editing is not in this Community alpha. Linux and embedded targets have not been validated.
