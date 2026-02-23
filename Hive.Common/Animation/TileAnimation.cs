using Avalonia;
using Hive.Common.Models;

namespace Hive.Common.Animation;

/// <summary>
/// Base class for tile animations.
/// </summary>
public abstract class TileAnimation
{
    /// <summary>
    /// The tiles involved in this animation.
    /// </summary>
    public List<HexTile> Tiles { get; protected set; } = [];

    /// <summary>
    /// Duration of the animation in milliseconds.
    /// </summary>
    public double DurationMs { get; protected set; }

    /// <summary>
    /// Current elapsed time in milliseconds.
    /// </summary>
    public double ElapsedMs { get; protected set; }

    /// <summary>
    /// Whether the animation has completed.
    /// </summary>
    public bool IsComplete => ElapsedMs >= DurationMs;

    /// <summary>
    /// Progress of the animation from 0 to 1.
    /// </summary>
    public double Progress => Math.Min(1.0, ElapsedMs / DurationMs);

    /// <summary>
    /// Callback when animation completes.
    /// </summary>
    public Action? OnComplete { get; set; }

    /// <summary>
    /// Updates the animation state.
    /// </summary>
    public virtual void Update(double deltaMs)
    {
        ElapsedMs += deltaMs;
        ApplyAnimation(EaseInOutQuad(Progress));
    }

    /// <summary>
    /// Apply the animation effect to tiles.
    /// </summary>
    protected abstract void ApplyAnimation(double easedProgress);

    /// <summary>
    /// Smooth easing function for natural motion.
    /// </summary>
    protected static double EaseInOutQuad(double t)
    {
        return t < 0.5 ? 2 * t * t : 1 - Math.Pow(-2 * t + 2, 2) / 2;
    }

    /// <summary>
    /// Bounce easing for rejected moves.
    /// </summary>
    protected static double EaseOutBounce(double t)
    {
        const double n1 = 7.5625;
        const double d1 = 2.75;

        if (t < 1 / d1)
        {
            return n1 * t * t;
        }
        else if (t < 2 / d1)
        {
            return n1 * (t -= 1.5 / d1) * t + 0.75;
        }
        else if (t < 2.5 / d1)
        {
            return n1 * (t -= 2.25 / d1) * t + 0.9375;
        }
        else
        {
            return n1 * (t -= 2.625 / d1) * t + 0.984375;
        }
    }
}

/// <summary>
/// Animation for rotating tiles around a center point.
/// </summary>
public class RotationAnimation : TileAnimation
{
    private readonly Point _center;
    private readonly Point[] _startPositions;
    private readonly Point[] _endPositions;
    private readonly double _angleRadians;

    public RotationAnimation(List<HexTile> tiles, Point center, bool clockwise, double durationMs = 200)
    {
        Tiles = tiles;
        _center = center;
        DurationMs = durationMs;
        
        // For 3 tiles: rotate 120 degrees (2π/3)
        // For 6 tiles: rotate 60 degrees (π/3)
        double baseAngle = tiles.Count == 3 ? (2 * Math.PI / 3) : (Math.PI / 3);
        _angleRadians = clockwise ? -baseAngle : baseAngle;

        _startPositions = tiles.Select(t => t.ScreenPosition).ToArray();
        _endPositions = new Point[tiles.Count];

        // Calculate end positions
        for (int i = 0; i < tiles.Count; i++)
        {
            _endPositions[i] = RotatePoint(_startPositions[i], _center, _angleRadians);
        }

        // Mark tiles as animating
        foreach (var tile in tiles)
        {
            tile.IsAnimating = true;
        }
    }

    protected override void ApplyAnimation(double easedProgress)
    {
        double currentAngle = _angleRadians * easedProgress;
        
        for (int i = 0; i < Tiles.Count; i++)
        {
            Tiles[i].ScreenPosition = RotatePoint(_startPositions[i], _center, currentAngle);
        }

        if (IsComplete)
        {
            // Snap to final positions
            for (int i = 0; i < Tiles.Count; i++)
            {
                Tiles[i].ScreenPosition = _endPositions[i];
                Tiles[i].IsAnimating = false;
            }
        }
    }

    private static Point RotatePoint(Point point, Point center, double angle)
    {
        double cos = Math.Cos(angle);
        double sin = Math.Sin(angle);
        double dx = point.X - center.X;
        double dy = point.Y - center.Y;
        
        return new Point(
            center.X + dx * cos - dy * sin,
            center.Y + dx * sin + dy * cos);
    }
}

/// <summary>
/// Animation for reverting a failed rotation (bounce back).
/// </summary>
public class BounceAnimation : TileAnimation
{
    private readonly Point _center;
    private readonly Point[] _currentPositions;
    private readonly Point[] _originalPositions;
    private readonly double _overshootAngle;
    private readonly bool _clockwise;

    public BounceAnimation(List<HexTile> tiles, Point center, Point[] originalPositions, bool clockwise, double durationMs = 300)
    {
        Tiles = tiles;
        _center = center;
        _originalPositions = originalPositions;
        _clockwise = clockwise;
        DurationMs = durationMs;

        _currentPositions = tiles.Select(t => t.ScreenPosition).ToArray();
        _overshootAngle = (clockwise ? -1 : 1) * Math.PI / 12; // Small overshoot

        foreach (var tile in tiles)
        {
            tile.IsAnimating = true;
        }
    }

    protected override void ApplyAnimation(double easedProgress)
    {
        // Reverse animation with slight bounce
        double bounceProgress = EaseOutBounce(easedProgress);
        
        for (int i = 0; i < Tiles.Count; i++)
        {
            Tiles[i].ScreenPosition = Lerp(_currentPositions[i], _originalPositions[i], bounceProgress);
        }

        if (IsComplete)
        {
            for (int i = 0; i < Tiles.Count; i++)
            {
                Tiles[i].ScreenPosition = _originalPositions[i];
                Tiles[i].IsAnimating = false;
            }
        }
    }

    private static Point Lerp(Point a, Point b, double t)
    {
        return new Point(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);
    }
}

/// <summary>
/// Animation for tiles falling due to gravity.
/// </summary>
public class FallAnimation : TileAnimation
{
    private readonly Point[] _startPositions;
    private readonly Point[] _endPositions;

    public FallAnimation(List<HexTile> tiles, Point[] endPositions, double durationMs = 250)
    {
        Tiles = tiles;
        _endPositions = endPositions;
        DurationMs = durationMs;

        _startPositions = tiles.Select(t => t.ScreenPosition).ToArray();

        foreach (var tile in tiles)
        {
            tile.IsAnimating = true;
        }
    }

    protected override void ApplyAnimation(double easedProgress)
    {
        for (int i = 0; i < Tiles.Count; i++)
        {
            Tiles[i].ScreenPosition = Lerp(_startPositions[i], _endPositions[i], easedProgress);
        }

        if (IsComplete)
        {
            for (int i = 0; i < Tiles.Count; i++)
            {
                Tiles[i].ScreenPosition = _endPositions[i];
                Tiles[i].TargetPosition = _endPositions[i];
                Tiles[i].IsAnimating = false;
            }
        }
    }

    private static Point Lerp(Point a, Point b, double t)
    {
        return new Point(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);
    }
}

/// <summary>
/// Animation for eliminating matched tiles.
/// </summary>
public class EliminationAnimation : TileAnimation
{
    public EliminationAnimation(List<HexTile> tiles, double durationMs = 200)
    {
        Tiles = tiles;
        DurationMs = durationMs;

        foreach (var tile in tiles)
        {
            tile.IsAnimating = true;
            tile.IsMarkedForRemoval = true;
        }
    }

    protected override void ApplyAnimation(double easedProgress)
    {
        double scale = 1.0 - easedProgress * 0.5; // Shrink to 50%
        double opacity = 1.0 - easedProgress;

        foreach (var tile in Tiles)
        {
            tile.Scale = scale;
            tile.Opacity = opacity;
        }

        if (IsComplete)
        {
            foreach (var tile in Tiles)
            {
                tile.Scale = 0;
                tile.Opacity = 0;
                tile.IsAnimating = false;
            }
        }
    }
}

/// <summary>
/// Animation for spawning new tiles (fade in from top).
/// </summary>
public class SpawnAnimation : TileAnimation
{
    private readonly Point[] _startPositions;
    private readonly Point[] _endPositions;

    public SpawnAnimation(List<HexTile> tiles, Point[] startPositions, Point[] endPositions, double durationMs = 300)
    {
        Tiles = tiles;
        _startPositions = startPositions;
        _endPositions = endPositions;
        DurationMs = durationMs;

        for (int i = 0; i < tiles.Count; i++)
        {
            tiles[i].ScreenPosition = startPositions[i];
            tiles[i].IsAnimating = true;
            tiles[i].Opacity = 0;
        }
    }

    protected override void ApplyAnimation(double easedProgress)
    {
        for (int i = 0; i < Tiles.Count; i++)
        {
            Tiles[i].ScreenPosition = Lerp(_startPositions[i], _endPositions[i], easedProgress);
            Tiles[i].Opacity = easedProgress;
        }

        if (IsComplete)
        {
            for (int i = 0; i < Tiles.Count; i++)
            {
                Tiles[i].ScreenPosition = _endPositions[i];
                Tiles[i].TargetPosition = _endPositions[i];
                Tiles[i].Opacity = 1;
                Tiles[i].IsAnimating = false;
            }
        }
    }

    private static Point Lerp(Point a, Point b, double t)
    {
        return new Point(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);
    }
}
