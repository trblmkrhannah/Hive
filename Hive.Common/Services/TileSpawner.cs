using Hive.Common.Models;

namespace Hive.Common.Services;

/// <summary>
/// Generates new tiles for the game.
/// </summary>
public class TileSpawner
{
    private readonly Random _random = new();
    private readonly TileColor[] _availableColors;

    /// <summary>
    /// All supported colors in order (up to 8).
    /// </summary>
    private static readonly TileColor[] AllColors =
    [
        TileColor.Red,
        TileColor.Blue,
        TileColor.Green,
        TileColor.Yellow,
        TileColor.Purple,
        TileColor.Orange,
        TileColor.Cyan,
        TileColor.Pink
    ];

    /// <summary>
    /// Number of colors to use (5 = easier, 8 = harder).
    /// </summary>
    private const int ColorCount = 5;

    /// <summary>
    /// Probability of spawning a bomb tile (1% = 0.01).
    /// </summary>
    private const double BombSpawnProbability = 0.002;

    /// <summary>
    /// Probability of spawning a bonus tile (5% = 0.05).
    /// Only applies to regular tiles (not bombs, stars, or pearls).
    /// </summary>
    private const double BonusSpawnProbability = 0.05;

    /// <summary>
    /// Initial countdown value for bomb tiles.
    /// </summary>
    public const int InitialBombCounter = 9;

    public TileSpawner()
    {
        // Use configured number of colors
        _availableColors = AllColors.Take(ColorCount).ToArray();
    }

    /// <summary>
    /// Creates a new tile with a random color at the specified coordinate.
    /// Has a chance of spawning a bomb or bonus tile (if allowed).
    /// </summary>
    /// <param name="coord">The coordinate to spawn the tile at.</param>
    /// <param name="allowBombs">Whether bombs can spawn. Default is true.</param>
    /// <param name="allowBonus">Whether bonus tiles can spawn. Default is true.</param>
    public HexTile SpawnTile(HexCoordinate coord, bool allowBombs = true, bool allowBonus = true)
    {
        var color = _availableColors[_random.Next(_availableColors.Length)];
        
        // Chance to spawn a bomb (only if allowed)
        if (allowBombs && _random.NextDouble() < BombSpawnProbability)
        {
            return new HexTile(coord, color, isBomb: true, bombCounter: InitialBombCounter)
            {
                BombJustSpawned = true // Don't decrement on the turn it spawns
            };
        }
        
        // Chance to spawn a bonus tile (only for regular tiles, if allowed)
        if (allowBonus && _random.NextDouble() < BonusSpawnProbability)
        {
            return new HexTile(coord, color) { IsBonus = true };
        }
        
        return new HexTile(coord, color);
    }

    /// <summary>
    /// Creates a star tile at the specified coordinate.
    /// </summary>
    public HexTile SpawnStarTile(HexCoordinate coord)
    {
        // Star tiles use a special appearance but still have a color for the base
        var color = TileColor.Yellow; // Default color for star background
        return new HexTile(coord, color, isStar: true);
    }

    /// <summary>
    /// Creates a pearl tile at the specified coordinate.
    /// Pearl tiles are created when a hexagon of stars is formed.
    /// </summary>
    public HexTile SpawnPearlTile(HexCoordinate coord)
    {
        // Pearl tiles use a light blue/white color for the base
        var color = TileColor.Blue; // Default color for pearl background
        return new HexTile(coord, color, isPearl: true);
    }

    /// <summary>
    /// Fills all empty valid coordinates in the grid with new tiles.
    /// Does not spawn bombs (bombs only appear after gameplay begins).
    /// </summary>
    public List<HexTile> FillGrid(HexGrid grid)
    {
        var newTiles = new List<HexTile>();
        
        foreach (var coord in grid.ValidCoordinates)
        {
            if (grid.GetTile(coord) == null)
            {
                // Don't allow bombs during initial grid fill
                var tile = SpawnTile(coord, allowBombs: false, allowBonus: false);
                grid.SetTile(coord, tile);
                newTiles.Add(tile);
            }
        }
        
        return newTiles;
    }

    /// <summary>
    /// Spawns a new tile at the top of the specified column.
    /// </summary>
    public HexTile SpawnAtTop(HexGrid grid, int col)
    {
        var coord = grid.GetTopCoordinate(col);
        var tile = SpawnTile(coord);
        grid.SetTile(coord, tile);
        return tile;
    }
}
