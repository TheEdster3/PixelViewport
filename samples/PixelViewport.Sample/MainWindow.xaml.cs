using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;
using PixelViewport.WinUI.Controls;

namespace PixelViewport.Sample;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Viewer.Source = new BitmapImage(new Uri("ms-appx:///Assets/SampleImage.png"));
    }

    private void Fit_Click(object sender, RoutedEventArgs e) => Viewer.FitToViewport();

    private void ActualSize_Click(object sender, RoutedEventArgs e) => Viewer.ZoomToActualSize();

    private void ZoomOut_Click(object sender, RoutedEventArgs e) => Viewer.ZoomOut();

    private void ZoomIn_Click(object sender, RoutedEventArgs e) => Viewer.ZoomIn();

    private void Viewer_ViewChanged(object? sender, ViewportChangedEventArgs e)
    {
        ZoomText.Text = $"{e.ZoomFactor:P0}";
    }

    private void Viewer_ImagePointerMoved(object? sender, ImagePointerEventArgs e)
    {
        PositionText.Text = e.IsInsideImage
            ? $"x: {e.ImagePosition.X:0.0}   y: {e.ImagePosition.Y:0.0}"
            : "x: —   y: —";
    }
}
