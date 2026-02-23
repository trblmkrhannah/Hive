using Avalonia;

namespace Hive.Common.Models;

/// <summary>
/// Represents a single tile in the hexagonal grid.
/// </summary>
public class HexTile
{
    /// <summary>
    /// Unique identifier for the tile.
    /// </summary>
    public Guid Id { get; } = Guid.NewGuid();

    /// <summary>
    /// The tile's position in the grid using axial coordinates.
    /// </summary>
    public HexCoordinate Coordinate { get; set; }

    /// <summary>
    /// The color of this tile.
    /// </summary>
    public TileColor Color { get; set; }

    /// <summary>
    /// Whether this tile is a star tile (allows free 6-tile rotation).
    /// </summary>
    public bool IsStar { get; set; }

    /// <summary>
    /// Whether this tile is a pearl tile (created from hexagon of stars).
    /// </summary>
    public bool IsPearl { get; set; }

    /// <summary>
    /// Whether this tile is a bomb tile (has a countdown timer).
    /// </summary>
    public bool IsBomb { get; set; }

    /// <summary>
    /// The countdown counter for bomb tiles. When it reaches 0, the game is over.
    /// </summary>
    public int BombCounter { get; set; }

    /// <summary>
    /// Whether this bomb just spawned this turn (should not be decremented yet).
    /// </summary>
    public bool BombJustSpawned { get; set; }

    /// <summary>
    /// Whether this tile is a bonus tile (yields extra points, special effect when 2+ cleared together).
    /// </summary>
    public bool IsBonus { get; set; }

    /// <summary>
    /// Current screen position for rendering (may differ from grid position during animations).
    /// </summary>
    public Point ScreenPosition { get; set; }

    /// <summary>
    /// Target screen position for animations.
    /// </summary>
    public Point TargetPosition { get; set; }

    /// <summary>
    /// The current scale of the tile (for elimination animation).
    /// </summary>
    public double Scale { get; set; } = 1.0;

    /// <summary>
    /// The current opacity of the tile (for elimination animation).
    /// </summary>
    public double Opacity { get; set; } = 1.0;

    /// <summary>
    /// Whether this tile is currently being animated.
    /// </summary>
    public bool IsAnimating { get; set; }

    /// <summary>
    /// Whether this tile is marked for removal.
    /// </summary>
    public bool IsMarkedForRemoval { get; set; }

    public HexTile(HexCoordinate coordinate, TileColor color, bool isStar = false, bool isPearl = false, bool isBomb = false, int bombCounter = 0)
    {
        Coordinate = coordinate;
        Color = color;
        IsStar = isStar;
        IsPearl = isPearl;
        IsBomb = isBomb;
        BombCounter = bombCounter;
    }
}
