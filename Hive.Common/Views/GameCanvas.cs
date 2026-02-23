using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Hive.Common.Controllers;
using Hive.Common.Models;
using Hive.Common.Services;

namespace Hive.Common.Views;

/// <summary>
/// Custom control for rendering the hexagonal game grid.
/// </summary>
public class GameCanvas : Control
{
    private GameController? _controller;
    private double _hexSize = 40;
    private Point _origin;
    
    // Cached brushes for tile colors (supports up to 8 colors)
    private static readonly Dictionary<TileColor, IBrush> TileBrushes = new()
    {
        { TileColor.Red, new SolidColorBrush(Color.Parse("#E74C3C")) },
        { TileColor.Blue, new SolidColorBrush(Color.Parse("#3498DB")) },
        { TileColor.Green, new SolidColorBrush(Color.Parse("#2ECC71")) },
        { TileColor.Yellow, new SolidColorBrush(Color.Parse("#F1C40F")) },
        { TileColor.Purple, new SolidColorBrush(Color.Parse("#9B59B6")) },
        { TileColor.Orange, new SolidColorBrush(Color.Parse("#E67E22")) },
        { TileColor.Cyan, new SolidColorBrush(Color.Parse("#00BCD4")) },
        { TileColor.Pink, new SolidColorBrush(Color.Parse("#E91E63")) }
    };

    private static readonly IPen TilePen = new Pen(new SolidColorBrush(Color.Parse("#2C3E50")), 2);
    
    // Bomb rendering
    private static readonly IBrush BombIconBrush = new SolidColorBrush(Color.Parse("#1a1a2e")); // Dark icon
    private static readonly IBrush BombCounterBrush = new SolidColorBrush(Color.Parse("#FFFFFF")); // White text
    private static readonly IBrush BombCounterBackgroundBrush = new SolidColorBrush(Color.Parse("#CC000000")); // Semi-transparent black
    
    // Glow effect pens for move preview (drawn from outer to inner for glow effect)
    private static readonly IPen GlowOuterPen = new Pen(new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)), 8);
    private static readonly IPen GlowMiddlePen = new Pen(new SolidColorBrush(Color.FromArgb(120, 255, 255, 255)), 5);
    private static readonly IPen GlowInnerPen = new Pen(new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)), 3);

    // Bomb jiggle: timer drives redraws while bombs are on the board
    private DispatcherTimer? _bombJiggleTimer;

    /// <summary>
    /// The currently highlighted triplet (for visual feedback).
    /// </summary>
    public HexCoordinate[]? HighlightedTriplet { get; set; }

    /// <summary>
    /// The currently highlighted star (for 6-tile rotation).
    /// </summary>
    public HexCoordinate? HighlightedStar { get; set; }

    /// <summary>
    /// The currently highlighted pearl (for 3-tile rotation).
    /// </summary>
    public HexCoordinate? HighlightedPearl { get; set; }

    public void SetController(GameController controller)
    {
        _controller = controller;
        _controller.AnimationManager.OnRedrawRequested = InvalidateVisual;
        StartBombJiggleTimer();
    }

    private void StartBombJiggleTimer()
    {
        if (_bombJiggleTimer != null) return;
        _bombJiggleTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(50) // ~20 FPS for jiggle
        };
        _bombJiggleTimer.Tick += (_, _) =>
        {
            if (_controller == null || _controller.GameState.IsGameOver) return;
            var hasBombs = false;
            foreach (var tile in _controller.GameState.Grid.GetAllTiles())
            {
                if (tile.IsBomb) { hasBombs = true; break; }
            }
            if (hasBombs) InvalidateVisual();
        };
        _bombJiggleTimer.Start();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        if (_controller == null) return;

        // Calculate origin to center the grid
        CalculateLayout();

        // Draw all tiles
        foreach (var tile in _controller.GameState.Grid.GetAllTiles())
        {
            DrawTile(context, tile);
        }

        // Draw tile glow effects AFTER tiles so they appear on top
        if (HighlightedTriplet != null)
        {
            DrawTripletTileGlow(context, HighlightedTriplet);
        }

        if (HighlightedStar.HasValue)
        {
            DrawStarTileGlow(context, HighlightedStar.Value);
        }

        if (HighlightedPearl.HasValue)
        {
            DrawPearlTileGlow(context, HighlightedPearl.Value);
        }
    }

    /// <summary>
    /// Returns an erratic pixel offset for bomb jiggle, based on time and tile id so each bomb moves differently.
    /// Speed and amplitude both increase as the counter approaches explosion (more urgent and visibly agitated).
    /// </summary>
    private static Point GetBombJiggleOffset(HexTile tile)
    {
        var counter = Math.Max(1, tile.BombCounter);
        // Amplitude: subtle when counter is high, grows as it approaches explosion (0.35 px → 2.0 px)
        var progressToExplosion = (TileSpawner.InitialBombCounter - counter) / (double)(TileSpawner.InitialBombCounter - 1);
        var amplitude = 0.35 + progressToExplosion * (2.0 - 0.35);
        const double twoPi = 2.0 * Math.PI;
        var h = tile.Id.GetHashCode();
        var phase1 = (h % 1000) / 1000.0 * twoPi;
        var phase2 = ((h >> 7) % 1000) / 1000.0 * twoPi;
        var t = Environment.TickCount64 / 1000.0;
        // Speed up jiggle as counter drops: 1x at initial, ~2.8x when counter is 1
        var speedFactor = 1.0 + (TileSpawner.InitialBombCounter - counter) * 0.22;
        t *= speedFactor;
        var dx = amplitude * (
            Math.Sin(t * 4.2 + phase1) +
            0.6 * Math.Sin(t * 7.1 + phase2) +
            0.35 * Math.Sin(t * 11.3 + phase1 * 2));
        var dy = amplitude * (
            Math.Sin(t * 3.7 + phase2) +
            0.6 * Math.Sin(t * 6.8 + phase1) +
            0.35 * Math.Sin(t * 9.2 + phase2 * 2));
        return new Point(dx, dy);
    }

    private void CalculateLayout()
    {
        if (_controller == null) return;
        
        // Ensure we have valid bounds
        if (Bounds.Width <= 0 || Bounds.Height <= 0) return;

        var grid = _controller.GameState.Grid;
        
        // Calculate hex size to fit the grid in the available space
        double availableWidth = Math.Max(100, Bounds.Width - 40); // Padding
        double availableHeight = Math.Max(100, Bounds.Height - 40);

        // Start with a base hex size and calculate grid dimensions
        // For flat-top: width = 1.5 * size per column, height = sqrt(3) * size per row
        double baseHexSize = 40;
        double gridWidth = HexMath.GetHorizontalSpacing(baseHexSize) * (grid.Columns - 1) + HexMath.GetHexWidth(baseHexSize);
        double gridHeight = HexMath.GetVerticalSpacing(baseHexSize) * grid.Rows + HexMath.GetHexHeight(baseHexSize) * 0.5;

        // Scale to fit
        double scaleX = availableWidth / gridWidth;
        double scaleY = availableHeight / gridHeight;
        double scale = Math.Min(scaleX, scaleY);
        
        // Apply minimum size for touch friendliness (minimum 30, maximum 55)
        _hexSize = Math.Max(30, Math.Min(55, baseHexSize * scale));

        // Recalculate grid size with new hex size
        gridWidth = HexMath.GetHorizontalSpacing(_hexSize) * (grid.Columns - 1) + HexMath.GetHexWidth(_hexSize);
        gridHeight = HexMath.GetVerticalSpacing(_hexSize) * grid.Rows + HexMath.GetHexHeight(_hexSize) * 0.5;

        // Center the grid - origin is top-left hex center
        _origin = new Point(
            (Bounds.Width - gridWidth) / 2 + _hexSize,
            (Bounds.Height - gridHeight) / 2 + HexMath.GetHexHeight(_hexSize) / 2);
    }

    private void DrawTile(DrawingContext context, HexTile tile)
    {
        if (tile.Opacity <= 0) return;

        var center = tile.ScreenPosition;
        var size = _hexSize * tile.Scale;

        // Get the hexagon path
        var geometry = CreateHexagonGeometry(center, size);

        // Star tiles use the bee stripe pattern as fill, so we handle them separately
        if (tile.IsStar)
        {
            // Draw the bee stripe pattern (provides its own fill)
            DrawStarShape(context, center, size, tile.Opacity);
            // Draw the hexagon outline on top
            context.DrawGeometry(null, TilePen, geometry);
            return;
        }
        
        if (tile.IsPearl)
        {
            // Draw the bee stripe pattern (provides its own fill)
            DrawPearlShape(context, center, size, tile.Opacity);
            // Draw the hexagon outline on top
            context.DrawGeometry(null, TilePen, geometry);
            return;
        }
        
        // Draw tile fill - determine brush based on tile type
        IBrush brush;
        
        {
            // Regular tiles and bombs use their color
            brush = TileBrushes[tile.Color];
        }
        
        if (tile.Opacity < 1)
        {
            // Apply opacity for animations
            if (brush is SolidColorBrush solidBrush)
            {
                var color = solidBrush.Color;
                brush = new SolidColorBrush(Color.FromArgb((byte)(color.A * tile.Opacity), color.R, color.G, color.B));
            }
        }

        context.DrawGeometry(brush, TilePen, geometry);

        // Draw overlay for special tiles (bomb icon + number jiggle within the tile)
        if (tile.IsBomb)
        {
            var jiggle = GetBombJiggleOffset(tile);
            var bombCenter = new Point(center.X + jiggle.X, center.Y + jiggle.Y);
            DrawBombOverlay(context, bombCenter, size, tile.BombCounter, tile.Opacity);
        }
        else if (tile.IsBonus)
        {
            DrawBonusShape(context, center, size * 0.6, tile.Opacity);
        }
        else
        {
            context.DrawGeometry(null, TilePen, geometry);
        }
        
        DrawOverlay5(context, center, size, tile.Opacity);
    }

    // Cached bee stripe drawing for star tiles
    private static DrawingImage? _beeStripeDrawing;
    private static DrawingImage? _honeycombDrawing;
    private static DrawingImage? _overlay3;
    private static DrawingImage? _overlay4;
    private static DrawingImage? _overlay5;
    
    private void DrawOverlay3(DrawingContext context, Point center, double size, double opacity)
    {
        _overlay3 ??= Application.Current?.FindResource("Overlay3") as DrawingImage;
        
        if (_overlay3?.Drawing == null) 
            return;
        
        DrawWithDrawing(_overlay3.Drawing,  context, center, size, opacity);
    }
    
    private void DrawOverlay4(DrawingContext context, Point center, double size, double opacity)
    {
        _overlay4 ??= Application.Current?.FindResource("Overlay4") as DrawingImage;
        
        if (_overlay4?.Drawing == null) 
            return;
        
        DrawWithDrawing(_overlay4.Drawing,  context, center, size, opacity);
    }
    
    
    private void DrawOverlay5(DrawingContext context, Point center, double size, double opacity)
    {
        _overlay5 ??= Application.Current?.FindResource("Overlay5") as DrawingImage;
        
        if (_overlay5?.Drawing == null) 
            return;
        
        DrawWithDrawing(_overlay5.Drawing,  context, center, size, opacity);
    }
    
    private void DrawStarShape(DrawingContext context, Point center, double size, double opacity)
    {
        _beeStripeDrawing ??= Application.Current?.FindResource("BeeStripeIcon") as DrawingImage;
        
        if (_beeStripeDrawing?.Drawing == null) 
            return;
        
        DrawWithDrawing(_beeStripeDrawing.Drawing,  context, center, size, opacity);
    }
    
    private void DrawPearlShape(DrawingContext context, Point center, double size, double opacity)
    {
        _honeycombDrawing ??= Application.Current?.FindResource("Honeycomb") as DrawingImage;
        
        if (_honeycombDrawing?.Drawing == null) 
            return;
        
        DrawWithDrawing(_honeycombDrawing.Drawing,  context, center, size, opacity);
    }

    private void DrawWithDrawing(Drawing drawing, DrawingContext context, Point center, double size, double opacity)
    {
        // Create a hexagon geometry for clipping (full size to fill the tile)
        var clipGeometry = CreateHexagonGeometry(center, size);
        
        // Calculate the bounds for the drawing - sized to show the full pattern
        var drawingSize = size * 1.95;
        var rect = new Rect(
            center.X - drawingSize / 2,
            center.Y - drawingSize / 2,
            drawingSize,
            drawingSize);
        
        // Apply clipping to hexagon shape and draw the bee stripe pattern
        using (context.PushGeometryClip(clipGeometry))
        using (context.PushOpacity(opacity))
        {
            // Scale and position the drawing to fill the hexagon
            var scaleX = rect.Width / 1024;
            var scaleY = rect.Height / 1024;
            
            using (context.PushTransform(Matrix.CreateTranslation(rect.X, rect.Y)))
            using (context.PushTransform(Matrix.CreateScale(scaleX, scaleY)))
            {
                drawing.Draw(context);
            }
        }
    }
    
    private void DrawBonusShape(DrawingContext context, Point center, double size, double opacity)
    {
        // Draw a "!" in the center of the bonus tile
        var textBrush = new SolidColorBrush(Color.Parse("#FFFFFF")); // White for contrast
        
        if (opacity < 1)
        {
            var color = textBrush.Color;
            textBrush = new SolidColorBrush(Color.FromArgb((byte)(color.A * opacity), color.R, color.G, color.B));
        }

        // Create formatted text for "!"
        var formattedText = new FormattedText(
            "+",
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface("Arial", FontStyle.Normal, FontWeight.Bold),
            size * 1.4,
            textBrush);

        // Center the text
        var textOrigin = new Point(
            center.X - formattedText.Width / 2,
            center.Y - formattedText.Height / 2);

        context.DrawText(formattedText, textOrigin);
    }

    private void DrawBombOverlay(DrawingContext context, Point center, double size, int counter, double opacity)
    {
        // Draw bomb icon (small circle at top like a bomb fuse point)
        var fuseRadius = size * 0.2;
        var fuseCenter = new Point(center.X, center.Y - size * 0.5);
        
        IBrush fuseBrush = BombIconBrush;
        if (opacity < 1)
        {
            var color = ((SolidColorBrush)fuseBrush).Color;
            fuseBrush = new SolidColorBrush(Color.FromArgb((byte)(color.A * opacity), color.R, color.G, color.B));
        }
        context.DrawEllipse(fuseBrush, null, fuseCenter, fuseRadius, fuseRadius);

        // Draw circular background for counter
        var counterRadius = size * 0.5;
        IBrush counterBgBrush = BombCounterBackgroundBrush;
        if (opacity < 1)
        {
            var color = ((SolidColorBrush)counterBgBrush).Color;
            counterBgBrush = new SolidColorBrush(Color.FromArgb((byte)(color.A * opacity), color.R, color.G, color.B));
        }
        context.DrawEllipse(counterBgBrush, null, center, counterRadius, counterRadius);

        // Draw counter number
        IBrush textBrush = BombCounterBrush;
        if (opacity < 1)
        {
            var color = ((SolidColorBrush)textBrush).Color;
            textBrush = new SolidColorBrush(Color.FromArgb((byte)(color.A * opacity), color.R, color.G, color.B));
        }

        // Use red for low counter (3 or less)
        if (counter <= 3)
        {
            var urgentColor = Color.Parse("#FF4444");
            if (opacity < 1)
            {
                textBrush = new SolidColorBrush(Color.FromArgb((byte)(urgentColor.A * opacity), urgentColor.R, urgentColor.G, urgentColor.B));
            }
            else
            {
                textBrush = new SolidColorBrush(urgentColor);
            }
        }

        var counterText = new FormattedText(
            counter.ToString(),
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface("Arial", FontStyle.Normal, FontWeight.Bold),
            size * 0.5,
            textBrush);

        var textOrigin = new Point(
            center.X - counterText.Width / 2,
            center.Y - counterText.Height / 2);

        context.DrawText(counterText, textOrigin);
    }

    private void DrawTripletTileGlow(DrawingContext context, HexCoordinate[] triplet)
    {
        // Draw glow around each tile that will be moved
        foreach (var coord in triplet)
        {
            var tile = _controller?.GameState.Grid.GetTile(coord);
            if (tile != null)
            {
                DrawTileGlow(context, tile.ScreenPosition, _hexSize);
            }
        }
    }

    private void DrawStarTileGlow(DrawingContext context, HexCoordinate starCoord)
    {
        if (_controller == null) return;

        // Draw glow around each of the 6 neighbor tiles that will be moved
        var neighborCoords = starCoord.GetAllNeighbors();
        foreach (var neighborCoord in neighborCoords)
        {
            var neighborTile = _controller.GameState.Grid.GetTile(neighborCoord);
            if (neighborTile != null)
            {
                DrawTileGlow(context, neighborTile.ScreenPosition, _hexSize);
            }
        }
    }

    private void DrawPearlTileGlow(DrawingContext context, HexCoordinate pearlCoord)
    {
        if (_controller == null) return;

        // Draw glow around the 3 neighbor tiles that will be moved
        // Pearl rotates: Northwest (2) = above, Southwest (4) = bottom-left, East (0) = bottom-right
        var allNeighbors = pearlCoord.GetAllNeighbors();
        var rotatingIndices = new[] { 2, 4, 0 };
        
        foreach (var index in rotatingIndices)
        {
            var neighborTile = _controller.GameState.Grid.GetTile(allNeighbors[index]);
            if (neighborTile != null)
            {
                DrawTileGlow(context, neighborTile.ScreenPosition, _hexSize);
            }
        }
    }

    /// <summary>
    /// Draws a white glow effect around a hexagon tile.
    /// </summary>
    private void DrawTileGlow(DrawingContext context, Point center, double size)
    {
        // Create hexagon geometry for the glow
        var geometry = CreateHexagonGeometry(center, size);
        
        // Draw multiple layers from outer to inner for a soft glow effect
        context.DrawGeometry(null, GlowOuterPen, geometry);
        context.DrawGeometry(null, GlowMiddlePen, geometry);
        context.DrawGeometry(null, GlowInnerPen, geometry);
    }

    private static StreamGeometry CreateHexagonGeometry(Point center, double size)
    {
        var geometry = new StreamGeometry();
        using var ctx = geometry.Open();

        var corners = HexMath.GetHexCorners(center, size);
        ctx.BeginFigure(corners[0], true);
        for (int i = 1; i < 6; i++)
        {
            ctx.LineTo(corners[i]);
        }
        ctx.EndFigure(true);

        return geometry;
    }

    /// <summary>
    /// Gets the hex size for coordinate calculations.
    /// </summary>
    public double HexSize => _hexSize;

    /// <summary>
    /// Gets the origin point for coordinate calculations.
    /// </summary>
    public Point Origin => _origin;

    /// <summary>
    /// Converts a screen point to a hex coordinate.
    /// </summary>
    public HexCoordinate ScreenToHex(Point screenPoint)
    {
        return HexMath.PixelToHex(screenPoint, _hexSize, _origin);
    }

    /// <summary>
    /// Converts a hex coordinate to a screen point.
    /// </summary>
    public Point HexToScreen(HexCoordinate coord)
    {
        return HexMath.HexToPixel(coord, _hexSize, _origin);
    }

    /// <summary>
    /// Gets the triplet at a screen point, if any.
    /// </summary>
    public HexCoordinate[]? GetTripletAtScreen(Point screenPoint)
    {
        if (_controller == null) return null;

        return HexMath.GetTripletAtPoint(
            screenPoint, 
            _hexSize, 
            _origin,
            _controller.GameState.Grid.IsValidCoordinate);
    }
}
