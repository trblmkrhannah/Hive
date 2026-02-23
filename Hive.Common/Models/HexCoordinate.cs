namespace Hive.Common.Models;

/// <summary>
/// Represents a position in the hexagonal grid using axial coordinates (q, r).
/// Axial coordinates simplify neighbor calculations and are widely used for hex grids.
/// </summary>
public readonly record struct HexCoordinate(int Q, int R)
{
    /// <summary>
    /// The 6 neighbor directions in axial coordinates.
    /// Index 0 is East, then proceeds counter-clockwise.
    /// </summary>
    public static readonly HexCoordinate[] Directions =
    [
        new(1, 0),   // East
        new(1, -1),  // Northeast
        new(0, -1),  // Northwest
        new(-1, 0),  // West
        new(-1, 1),  // Southwest
        new(0, 1)    // Southeast
    ];

    /// <summary>
    /// Gets the neighbor coordinate in the specified direction (0-5).
    /// </summary>
    public HexCoordinate GetNeighbor(int direction)
    {
        var dir = Directions[((direction % 6) + 6) % 6];
        return new HexCoordinate(Q + dir.Q, R + dir.R);
    }

    /// <summary>
    /// Gets all 6 neighboring coordinates.
    /// </summary>
    public HexCoordinate[] GetAllNeighbors()
    {
        var neighbors = new HexCoordinate[6];
        for (var i = 0; i < 6; i++)
        {
            neighbors[i] = GetNeighbor(i);
        }
        return neighbors;
    }

    /// <summary>
    /// Calculates the distance between two hex coordinates.
    /// </summary>
    public int DistanceTo(HexCoordinate other)
    {
        return (Math.Abs(Q - other.Q) 
              + Math.Abs(Q + R - other.Q - other.R) 
              + Math.Abs(R - other.R)) / 2;
    }

    public override string ToString() => $"({Q}, {R})";

    public static HexCoordinate operator +(HexCoordinate a, HexCoordinate b) => new(a.Q + b.Q, a.R + b.R);
    public static HexCoordinate operator -(HexCoordinate a, HexCoordinate b) => new(a.Q - b.Q, a.R - b.R);
}
