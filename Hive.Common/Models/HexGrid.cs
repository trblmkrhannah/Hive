namespace Hive.Common.Models;

/// <summary>
/// Represents the hexagonal game grid containing all tiles.
/// </summary>
public class HexGrid
{
    private readonly Dictionary<HexCoordinate, HexTile> _tiles = new();
    
    /// <summary>
    /// Number of columns in the grid.
    /// </summary>
    public int Columns { get; }
    
    /// <summary>
    /// Number of rows in the grid.
    /// </summary>
    public int Rows { get; }

    /// <summary>
    /// All valid coordinates in this grid.
    /// </summary>
    public IReadOnlySet<HexCoordinate> ValidCoordinates { get; }

    public HexGrid(int columns = 9, int rows = 10)
    {
        Columns = columns;
        Rows = rows;
        ValidCoordinates = GenerateValidCoordinates();
    }

    private HashSet<HexCoordinate> GenerateValidCoordinates()
    {
        var coords = new HashSet<HexCoordinate>();
        
        // Generate offset coordinates and convert to axial
        // Using odd-q offset layout (odd columns are shifted down) for flat-top hexagons
        for (int row = 0; row < Rows; row++)
        {
            for (int col = 0; col < Columns; col++)
            {
                var axial = OffsetToAxial(col, row);
                coords.Add(axial);
            }
        }
        
        return coords;
    }

    /// <summary>
    /// Converts offset coordinates (col, row) to axial coordinates.
    /// Uses odd-q offset layout for flat-top hexagons.
    /// </summary>
    public static HexCoordinate OffsetToAxial(int col, int row)
    {
        int q = col;
        int r = row - (col - (col & 1)) / 2;
        return new HexCoordinate(q, r);
    }

    /// <summary>
    /// Converts axial coordinates to offset coordinates (col, row).
    /// Uses odd-q offset layout for flat-top hexagons.
    /// </summary>
    public static (int Col, int Row) AxialToOffset(HexCoordinate axial)
    {
        int col = axial.Q;
        int row = axial.R + (axial.Q - (axial.Q & 1)) / 2;
        return (col, row);
    }

    /// <summary>
    /// Checks if a coordinate is valid within this grid.
    /// </summary>
    public bool IsValidCoordinate(HexCoordinate coord) => ValidCoordinates.Contains(coord);

    /// <summary>
    /// Gets the tile at the specified coordinate, or null if empty.
    /// </summary>
    public HexTile? GetTile(HexCoordinate coord)
    {
        return _tiles.TryGetValue(coord, out var tile) ? tile : null;
    }

    /// <summary>
    /// Sets the tile at the specified coordinate.
    /// </summary>
    public void SetTile(HexCoordinate coord, HexTile? tile)
    {
        if (tile == null)
        {
            _tiles.Remove(coord);
        }
        else
        {
            tile.Coordinate = coord;
            _tiles[coord] = tile;
        }
    }

    /// <summary>
    /// Removes the tile at the specified coordinate.
    /// </summary>
    public void RemoveTile(HexCoordinate coord)
    {
        _tiles.Remove(coord);
    }

    /// <summary>
    /// Gets all tiles currently in the grid.
    /// </summary>
    public IEnumerable<HexTile> GetAllTiles() => _tiles.Values;

    /// <summary>
    /// Gets all neighboring tiles of the specified coordinate.
    /// </summary>
    public List<HexTile> GetNeighborTiles(HexCoordinate coord)
    {
        var neighbors = new List<HexTile>();
        foreach (var neighborCoord in coord.GetAllNeighbors())
        {
            var tile = GetTile(neighborCoord);
            if (tile != null)
            {
                neighbors.Add(tile);
            }
        }
        return neighbors;
    }

    /// <summary>
    /// Checks if a coordinate has all 6 neighbors (not on edge).
    /// </summary>
    public bool HasAllNeighbors(HexCoordinate coord)
    {
        return coord.GetAllNeighbors().All(IsValidCoordinate);
    }

    /// <summary>
    /// Gets all coordinates in a column for gravity calculations.
    /// Returns coordinates from bottom to top.
    /// </summary>
    public List<HexCoordinate> GetColumnCoordinates(int col)
    {
        var coords = new List<HexCoordinate>();
        for (int row = Rows - 1; row >= 0; row--)
        {
            var axial = OffsetToAxial(col, row);
            if (ValidCoordinates.Contains(axial))
            {
                coords.Add(axial);
            }
        }
        return coords;
    }

    /// <summary>
    /// Clears all tiles from the grid.
    /// </summary>
    public void Clear()
    {
        _tiles.Clear();
    }

    /// <summary>
    /// Gets coordinates that are above the given coordinate (for gravity).
    /// </summary>
    public List<HexCoordinate> GetCoordinatesAbove(HexCoordinate coord)
    {
        var (col, row) = AxialToOffset(coord);
        var above = new List<HexCoordinate>();
        
        for (int r = row - 1; r >= 0; r--)
        {
            var axial = OffsetToAxial(col, r);
            if (ValidCoordinates.Contains(axial))
            {
                above.Add(axial);
            }
        }
        
        return above;
    }

    /// <summary>
    /// Gets the coordinate directly below the given coordinate.
    /// Returns null if there is no valid coordinate below.
    /// </summary>
    public HexCoordinate? GetCoordinateBelow(HexCoordinate coord)
    {
        var (col, row) = AxialToOffset(coord);
        if (row + 1 >= Rows) return null;
        
        var below = OffsetToAxial(col, row + 1);
        return ValidCoordinates.Contains(below) ? below : null;
    }

    /// <summary>
    /// Gets the topmost coordinate in a column.
    /// </summary>
    public HexCoordinate GetTopCoordinate(int col)
    {
        return OffsetToAxial(col, 0);
    }
}
