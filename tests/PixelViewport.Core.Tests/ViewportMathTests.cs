using PixelViewport.Core.Geometry;
using Xunit;

namespace PixelViewport.Core.Tests;

public sealed class ViewportMathTests
{
    [Theory]
    [InlineData(1600, 1000, 800, 800, 0.5, 0, 150)]
    [InlineData(1000, 1600, 800, 800, 0.5, 150, 0)]
    [InlineData(400, 200, 1000, 700, 2.5, 0, 100)]
    public void Fit_CentersImageAndUsesLargestContainedZoom(
        double imageWidth,
        double imageHeight,
        double viewportWidth,
        double viewportHeight,
        double expectedZoom,
        double expectedPanX,
        double expectedPanY)
    {
        var state = ViewportMath.Fit(
            new SizeD(imageWidth, imageHeight),
            new SizeD(viewportWidth, viewportHeight),
            0.05,
            32);

        Assert.Equal(expectedZoom, state.Zoom, 8);
        Assert.Equal(expectedPanX, state.PanX, 8);
        Assert.Equal(expectedPanY, state.PanY, 8);
    }

    [Theory]
    [InlineData(100, 100, 1000, 1000, 0.05, 4, 4, 300, 300)]
    [InlineData(10000, 10000, 100, 100, 0.05, 32, 0.05, -200, -200)]
    public void Fit_RespectsZoomLimits(
        double imageWidth,
        double imageHeight,
        double viewportWidth,
        double viewportHeight,
        double minZoom,
        double maxZoom,
        double expectedZoom,
        double expectedPanX,
        double expectedPanY)
    {
        ViewportState state = ViewportMath.Fit(
            new SizeD(imageWidth, imageHeight),
            new SizeD(viewportWidth, viewportHeight),
            minZoom,
            maxZoom);

        Assert.Equal(expectedZoom, state.Zoom, 8);
        Assert.Equal(expectedPanX, state.PanX, 8);
        Assert.Equal(expectedPanY, state.PanY, 8);
    }

    [Theory]
    [InlineData(0.25, 125, 175)]
    [InlineData(1.0, 400, 300)]
    [InlineData(16.0, 725, 525)]
    public void ZoomAt_KeepsImagePointUnderCursorWhenNotConstrained(
        double requestedZoom,
        double anchorX,
        double anchorY)
    {
        var image = new SizeD(10000, 8000);
        var viewport = new SizeD(800, 600);
        var initial = new ViewportState(0.5, -500, -400);
        var anchor = new PointD(anchorX, anchorY);
        PointD before = ViewportMath.ViewportToImage(initial, anchor);

        ViewportState zoomed = ViewportMath.ZoomAt(
            initial,
            requestedZoom,
            anchor,
            image,
            viewport,
            0.05,
            32);

        PointD after = ViewportMath.ViewportToImage(zoomed, anchor);

        Assert.Equal(before.X, after.X, 8);
        Assert.Equal(before.Y, after.Y, 8);
    }

    [Fact]
    public void ZoomAt_ClampsRequestedZoomAndPreservesAnchorWhenPossible()
    {
        var image = new SizeD(2000, 1200);
        var viewport = new SizeD(800, 600);
        var initial = new ViewportState(1, -400, -300);
        var anchor = new PointD(350, 250);
        PointD before = ViewportMath.ViewportToImage(initial, anchor);

        ViewportState zoomed = ViewportMath.ZoomAt(
            initial,
            100,
            anchor,
            image,
            viewport,
            0.05,
            32);

        PointD after = ViewportMath.ViewportToImage(zoomed, anchor);
        Assert.Equal(32, zoomed.Zoom, 8);
        Assert.Equal(before.X, after.X, 8);
        Assert.Equal(before.Y, after.Y, 8);
    }

    [Fact]
    public void ZoomAt_CentersAnAxisWhenTheScaledImageIsSmallerThanTheViewport()
    {
        var image = new SizeD(2000, 200);
        var viewport = new SizeD(800, 600);
        var initial = new ViewportState(0.5, -100, 250);
        var anchor = new PointD(400, 275);
        double imageXBefore = ViewportMath.ViewportToImage(initial, anchor).X;

        ViewportState zoomed = ViewportMath.ZoomAt(
            initial,
            1,
            anchor,
            image,
            viewport,
            0.05,
            32);

        Assert.Equal(imageXBefore, ViewportMath.ViewportToImage(zoomed, anchor).X, 8);
        Assert.Equal(200, zoomed.PanY, 8);
    }

    [Fact]
    public void Constrain_CentersImageWhenItIsSmallerThanViewport()
    {
        var state = ViewportMath.Constrain(
            new ViewportState(0.5, 999, -999),
            new SizeD(400, 200),
            new SizeD(1000, 700));

        Assert.Equal(400, state.PanX, 8);
        Assert.Equal(300, state.PanY, 8);
    }

    [Fact]
    public void PanBy_DoesNotAllowBlankSpaceAroundLargeImage()
    {
        var state = ViewportMath.PanBy(
            new ViewportState(1, 0, 0),
            new PointD(500, 500),
            new SizeD(1600, 1000),
            new SizeD(800, 600));

        Assert.Equal(0, state.PanX, 8);
        Assert.Equal(0, state.PanY, 8);
    }

    [Fact]
    public void PanBy_MovesDirectlyWhenNoConstraintIsReached()
    {
        var initial = new ViewportState(1, -400, -200);
        var imagePoint = new PointD(900, 500);
        PointD before = ViewportMath.ImageToViewport(initial, imagePoint);

        ViewportState panned = ViewportMath.PanBy(
            initial,
            new PointD(75, -40),
            new SizeD(1600, 1000),
            new SizeD(800, 600));

        PointD after = ViewportMath.ImageToViewport(panned, imagePoint);
        Assert.Equal(before.X + 75, after.X, 8);
        Assert.Equal(before.Y - 40, after.Y, 8);
    }

    [Fact]
    public void Constrain_ClampsPanAfterViewportResize()
    {
        ViewportState resized = ViewportMath.Constrain(
            new ViewportState(1, -600, -300),
            new SizeD(1600, 1000),
            new SizeD(1200, 800));

        Assert.Equal(-400, resized.PanX, 8);
        Assert.Equal(-200, resized.PanY, 8);
    }

    [Theory]
    [InlineData(1, 0, 0, 123.4, 567.8)]
    [InlineData(0.05, 320, 240, 4000, 2500)]
    [InlineData(2.5, -100, 50, 123.4, 567.8)]
    [InlineData(32, -31000, -15000, 999.99, 500.25)]
    public void CoordinateConversions_ImageToViewportToImage_RoundTrip(
        double zoom,
        double panX,
        double panY,
        double pointX,
        double pointY)
    {
        var state = new ViewportState(zoom, panX, panY);
        var source = new PointD(pointX, pointY);

        PointD viewport = ViewportMath.ImageToViewport(state, source);
        PointD roundTrip = ViewportMath.ViewportToImage(state, viewport);

        Assert.Equal(source.X, roundTrip.X, 8);
        Assert.Equal(source.Y, roundTrip.Y, 8);
    }

    [Theory]
    [InlineData(1, 0, 0, 123.4, 567.8)]
    [InlineData(0.05, 320, 240, 400, 300)]
    [InlineData(2.5, -100, 50, 700, 25)]
    [InlineData(32, -31000, -15000, 320, 240)]
    public void CoordinateConversions_ViewportToImageToViewport_RoundTrip(
        double zoom,
        double panX,
        double panY,
        double pointX,
        double pointY)
    {
        var state = new ViewportState(zoom, panX, panY);
        var source = new PointD(pointX, pointY);

        PointD image = ViewportMath.ViewportToImage(state, source);
        PointD roundTrip = ViewportMath.ImageToViewport(state, image);

        Assert.Equal(source.X, roundTrip.X, 8);
        Assert.Equal(source.Y, roundTrip.Y, 8);
    }

    [Theory]
    [InlineData(0, 0, true)]
    [InlineData(1599.999, 999.999, true)]
    [InlineData(-0.001, 0, false)]
    [InlineData(1600, 500, false)]
    [InlineData(800, 1000, false)]
    public void IsInsideImage_UsesHalfOpenPixelBounds(double x, double y, bool expected)
    {
        Assert.Equal(expected, ViewportMath.IsInsideImage(new PointD(x, y), new SizeD(1600, 1000)));
    }

    [Theory]
    [InlineData(0, 100)]
    [InlineData(-1, 100)]
    [InlineData(100, 0)]
    [InlineData(100, double.NaN)]
    [InlineData(double.PositiveInfinity, 100)]
    public void InvalidImageDimensions_DoNotProduceTransforms(double width, double height)
    {
        var image = new SizeD(width, height);

        Assert.False(image.IsValid);
        Assert.Equal(
            ViewportState.Identity,
            ViewportMath.Fit(image, new SizeD(800, 600), 0.05, 32));
    }

    [Theory]
    [InlineData(0, 32)]
    [InlineData(double.NaN, 32)]
    [InlineData(1, 0.5)]
    [InlineData(1, double.PositiveInfinity)]
    public void Fit_RejectsInvalidZoomRanges(double minZoom, double maxZoom)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ViewportMath.Fit(new SizeD(1600, 1000), new SizeD(800, 600), minZoom, maxZoom));
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void ZoomAt_RejectsNonFiniteRequestedZoom(double requestedZoom)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ViewportMath.ZoomAt(
                ViewportState.Identity,
                requestedZoom,
                new PointD(400, 300),
                new SizeD(1600, 1000),
                new SizeD(800, 600),
                0.05,
                32));
    }
}
