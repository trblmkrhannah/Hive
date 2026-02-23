using System;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Hive.Common.Models;
using Hive.Common.Services;

namespace Hive.Common.Views;

public static class HexGridBackground
{
    private const double HexSize = 15;
    private static readonly Point Origin = new(0, 0);

    public static IBrush CreateTiledBrush()
    {
        double tileW = 3 * HexSize;
        double tileH = 2 * HexMath.GetVerticalSpacing(HexSize);
        const int scale = 2;
        // Margin = HexSize so strokes near tile edges (hex at 0,0 has edge at -HexSize) are inside bitmap
        double margin = HexSize;
        int pixelW = (int)Math.Ceiling((tileW + 2 * margin) * scale);
        int pixelH = (int)Math.Ceiling((tileH + 2 * margin) * scale);
        int tilePixelW = (int)Math.Ceiling(tileW * scale);
        int tilePixelH = (int)Math.Ceiling(tileH * scale);
        int marginPx = (int)Math.Ceiling(margin * scale);

        var bitmap = new RenderTargetBitmap(new PixelSize(pixelW, pixelH));
        using (var dc = bitmap.CreateDrawingContext())
        {
            using (dc.PushTransform(Matrix.CreateScale(scale, scale)))
            using (dc.PushTransform(Matrix.CreateTranslation(margin, margin)))
            {
                var pen = new Pen(
                    new SolidColorBrush(Color.FromArgb(255, 255, 255, 255)), 5);

                // Hex range: all hexes whose centers or strokes could overlap [0, tileW] x [0, tileH]
                var topLeft = new Point(-HexSize, -HexSize);
                var bottomRight = new Point(tileW + HexSize, tileH + HexSize);
                var qMin = HexMath.PixelToHex(topLeft, HexSize, Origin).Q;
                var rMin = HexMath.PixelToHex(topLeft, HexSize, Origin).R;
                var qMax = HexMath.PixelToHex(bottomRight, HexSize, Origin).Q;
                var rMax = HexMath.PixelToHex(bottomRight, HexSize, Origin).R;

                for (int q = qMin; q <= qMax; q++)
                {
                    for (int r = rMin; r <= rMax; r++)
                    {
                        var center = HexMath.HexToPixel(new HexCoordinate(q, r), HexSize, Origin);
                        var geometry = CreateHexagonGeometry(center, HexSize);
                        dc.DrawGeometry(null, pen, geometry);
                    }
                }
            }
        }

        // Sample exactly one period; (0,0) and (tileW, tileH) are hex centers so no stroke on boundary
        var brush = new ImageBrush(bitmap)
        {
            TileMode = TileMode.Tile,
            SourceRect = new RelativeRect(marginPx, marginPx, tilePixelW, tilePixelH, RelativeUnit.Absolute),
            DestinationRect = new RelativeRect(0, 0, tileW, tileH, RelativeUnit.Absolute)
        };
        return brush;
    }

    private static StreamGeometry CreateHexagonGeometry(Point center, double size)
    {
        var geometry = new StreamGeometry();
        using var ctx = geometry.Open();
        var corners = HexMath.GetHexCorners(center, size);
        ctx.BeginFigure(corners[0], true);
        for (int i = 1; i < 6; i++)
            ctx.LineTo(corners[i]);
        ctx.EndFigure(true);
        return geometry;
    }
}
