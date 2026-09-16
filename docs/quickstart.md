# Quick start

## 1. Install/reference

During development, reference `PixelViewport.WinUI.csproj`. After a NuGet release, install `PixelViewport.WinUI` from NuGet.

## 2. Add the namespace

```xml
xmlns:pv="using:PixelViewport.WinUI.Controls"
```

## 3. Add a viewport

```xml
<pv:ImageViewport
    x:Name="Viewer"
    Source="{x:Bind ViewModel.Image, Mode=OneWay}"
    AutoFit="True"
    MinZoomFactor="0.05"
    MaxZoomFactor="32"
    ZoomStep="1.2"
    ImagePointerMoved="Viewer_ImagePointerMoved" />
```

## 4. Hook pointer coordinates

```csharp
private void Viewer_ImagePointerMoved(object? sender, ImagePointerEventArgs e)
{
    if (!e.IsInsideImage)
    {
        return;
    }

    Debug.WriteLine($"Image coordinate: {e.ImagePosition.X}, {e.ImagePosition.Y}");
}
```

## Commands

```csharp
Viewer.FitToViewport();
Viewer.ZoomToActualSize();
Viewer.ZoomIn();
Viewer.ZoomOut();
Viewer.ZoomAt(2.0, new Point(400, 300));
```

## Overlay content

Overlay content is placed in the same coordinate system as the image and receives the same transform.

```xml
<pv:ImageViewport ...>
  <pv:ImageViewport.OverlayContent>
    <Canvas Width="1600" Height="1000">
      <Border Canvas.Left="100" Canvas.Top="100" Width="200" Height="150" />
    </Canvas>
  </pv:ImageViewport.OverlayContent>
</pv:ImageViewport>
```

The core overlay is deliberately hit-test-disabled in v0.1. Interactive handles, ROI editing, and screen-space adorners are intended for PixelViewport Pro.
