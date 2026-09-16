namespace PixelViewport.Core.Geometry;

public static class ViewportMath
{
    public static ViewportState Fit(
        SizeD image,
        SizeD viewport,
        double minZoom,
        double maxZoom)
    {
        ValidateZoomRange(minZoom, maxZoom);

        if (!image.IsValid || !viewport.IsValid)
        {
            return ViewportState.Identity;
        }

        double zoom = Math.Clamp(
            Math.Min(viewport.Width / image.Width, viewport.Height / image.Height),
            minZoom,
            maxZoom);

        return CenterAtZoom(image, viewport, zoom, minZoom, maxZoom);
    }

    public static ViewportState CenterAtZoom(
        SizeD image,
        SizeD viewport,
        double zoom,
        double minZoom,
        double maxZoom)
    {
        ValidateZoomRange(minZoom, maxZoom);

        zoom = ClampZoom(zoom, minZoom, maxZoom);

        if (!image.IsValid || !viewport.IsValid)
        {
            return new ViewportState(zoom, 0, 0);
        }

        double panX = (viewport.Width - (image.Width * zoom)) / 2d;
        double panY = (viewport.Height - (image.Height * zoom)) / 2d;

        return Constrain(new ViewportState(zoom, panX, panY), image, viewport);
    }

    public static ViewportState ZoomAt(
        ViewportState current,
        double requestedZoom,
        PointD viewportAnchor,
        SizeD image,
        SizeD viewport,
        double minZoom,
        double maxZoom)
    {
        ValidateZoomRange(minZoom, maxZoom);

        double currentZoom = GetUsableZoom(current.Zoom);
        double zoom = ClampZoom(requestedZoom, minZoom, maxZoom);

        if (!image.IsValid || !viewport.IsValid)
        {
            return new ViewportState(zoom, current.PanX, current.PanY);
        }

        double imageX = (viewportAnchor.X - current.PanX) / currentZoom;
        double imageY = (viewportAnchor.Y - current.PanY) / currentZoom;

        double panX = viewportAnchor.X - (imageX * zoom);
        double panY = viewportAnchor.Y - (imageY * zoom);

        return Constrain(new ViewportState(zoom, panX, panY), image, viewport);
    }

    public static ViewportState PanBy(
        ViewportState current,
        PointD delta,
        SizeD image,
        SizeD viewport)
    {
        return Constrain(
            current with
            {
                PanX = current.PanX + delta.X,
                PanY = current.PanY + delta.Y,
            },
            image,
            viewport);
    }

    public static PointD ViewportToImage(ViewportState state, PointD viewportPoint)
    {
        double zoom = GetUsableZoom(state.Zoom);
        return new PointD(
            (viewportPoint.X - state.PanX) / zoom,
            (viewportPoint.Y - state.PanY) / zoom);
    }

    public static PointD ImageToViewport(ViewportState state, PointD imagePoint)
    {
        return new PointD(
            state.PanX + (imagePoint.X * GetUsableZoom(state.Zoom)),
            state.PanY + (imagePoint.Y * GetUsableZoom(state.Zoom)));
    }

    public static bool IsInsideImage(PointD imagePoint, SizeD image)
    {
        return image.IsValid
            && imagePoint.X >= 0
            && imagePoint.Y >= 0
            && imagePoint.X < image.Width
            && imagePoint.Y < image.Height;
    }

    public static ViewportState Constrain(
        ViewportState state,
        SizeD image,
        SizeD viewport)
    {
        if (!image.IsValid
            || !viewport.IsValid
            || !double.IsFinite(state.Zoom)
            || state.Zoom <= 0)
        {
            return state;
        }

        double scaledWidth = image.Width * state.Zoom;
        double scaledHeight = image.Height * state.Zoom;

        double panX = scaledWidth <= viewport.Width
            ? (viewport.Width - scaledWidth) / 2d
            : Math.Clamp(state.PanX, viewport.Width - scaledWidth, 0d);

        double panY = scaledHeight <= viewport.Height
            ? (viewport.Height - scaledHeight) / 2d
            : Math.Clamp(state.PanY, viewport.Height - scaledHeight, 0d);

        return state with { PanX = panX, PanY = panY };
    }

    private static void ValidateZoomRange(double minZoom, double maxZoom)
    {
        if (!double.IsFinite(minZoom) || minZoom <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(minZoom), "Minimum zoom must be finite and greater than zero.");
        }

        if (!double.IsFinite(maxZoom) || maxZoom < minZoom)
        {
            throw new ArgumentOutOfRangeException(nameof(maxZoom), "Maximum zoom must be finite and greater than or equal to minimum zoom.");
        }
    }

    private static double ClampZoom(double zoom, double minZoom, double maxZoom)
    {
        if (!double.IsFinite(zoom))
        {
            throw new ArgumentOutOfRangeException(nameof(zoom), "Zoom must be finite.");
        }

        return Math.Clamp(zoom, minZoom, maxZoom);
    }

    private static double GetUsableZoom(double zoom)
    {
        return double.IsFinite(zoom) && zoom > 0 ? zoom : 1d;
    }
}
