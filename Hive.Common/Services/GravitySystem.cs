using Hive.Common.Models;

namespace Hive.Common.Services;

/// <summary>
/// Handles tile gravity - making tiles fall to fill empty spaces.
/// </summary>
public class GravitySystem
{
    /// <summary>
    /// Represents a tile movement from one position to another.
    /// </summary>
    public record TileMovement(HexTile Tile, HexCoordinate From, HexCoordinate To);

    /// <summary>
    /// Calculates all tile movements needed to fill empty spaces.
    /// Tiles fall straight down in their columns.
    /// </summary>
    public List<TileMovement> CalculateFalls(HexGrid grid)
    {
        var movements = new List<TileMovement>();

        // Process each column
        for (int col = 0; col < grid.Columns; col++)
        {
            var columnMovements = ProcessColumn(grid, col);
            movements.AddRange(columnMovements);
        }

        return movements;
    }

    private List<TileMovement> ProcessColumn(HexGrid grid, int col)
    {
        var movements = new List<TileMovement>();
        
        // Get all coordinates in this column, from bottom to top
        var columnCoords = new List<HexCoordinate>();
        for (int row = grid.Rows - 1; row >= 0; row--)
        {
            var coord = HexGrid.OffsetToAxial(col, row);
            if (grid.ValidCoordinates.Contains(coord))
            {
                columnCoords.Add(coord);
            }
        }

        // Find empty slots and fill them from tiles above
        var emptySlots = new Queue<HexCoordinate>();

        foreach (var coord in columnCoords) // Bottom to top
        {
            var tile = grid.GetTile(coord);
            
            if (tile == null)
            {
                // This position is empty, add to empty slots
                emptySlots.Enqueue(coord);
            }
            else if (emptySlots.Count > 0)
            {
                // This tile needs to fall to the lowest empty slot
                var targetCoord = emptySlots.Dequeue();
                movements.Add(new TileMovement(tile, coord, targetCoord));
                
                // The current position becomes empty
                emptySlots.Enqueue(coord);
            }
        }

        return movements;
    }

    /// <summary>
    /// Applies the calculated movements to the grid.
    /// This should be called after fall animations complete.
    /// </summary>
    public void ApplyMovements(HexGrid grid, List<TileMovement> movements)
    {
        // First, remove all tiles from their original positions
        foreach (var movement in movements)
        {
            grid.RemoveTile(movement.From);
        }

        // Then, place them at their new positions
        foreach (var movement in movements)
        {
            grid.SetTile(movement.To, movement.Tile);
        }
    }

    /// <summary>
    /// Gets the coordinates that need new tiles after gravity is applied.
    /// These are the empty spaces at the top of each column.
    /// </summary>
    public List<(int Column, HexCoordinate Coordinate)> GetEmptyTopCoordinates(HexGrid grid)
    {
        var emptyTops = new List<(int, HexCoordinate)>();

        for (int col = 0; col < grid.Columns; col++)
        {
            // Check from top to bottom for empty spaces
            for (int row = 0; row < grid.Rows; row++)
            {
                var coord = HexGrid.OffsetToAxial(col, row);
                if (grid.ValidCoordinates.Contains(coord) && grid.GetTile(coord) == null)
                {
                    emptyTops.Add((col, coord));
                }
            }
        }

        return emptyTops;
    }

    /// <summary>
    /// Counts empty spaces in each column for spawn positioning.
    /// </summary>
    public Dictionary<int, int> CountEmptySpacesPerColumn(HexGrid grid)
    {
        var counts = new Dictionary<int, int>();

        for (int col = 0; col < grid.Columns; col++)
        {
            int count = 0;
            for (int row = 0; row < grid.Rows; row++)
            {
                var coord = HexGrid.OffsetToAxial(col, row);
                if (grid.ValidCoordinates.Contains(coord) && grid.GetTile(coord) == null)
                {
                    count++;
                }
            }
            counts[col] = count;
        }

        return counts;
    }
}
