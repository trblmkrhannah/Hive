using Hive.Common.Models;

namespace Hive.Common.Services;

/// <summary>
/// Detects matching patterns in the hex grid.
/// </summary>
public class MatchDetector
{
    /// <summary>
    /// Finds all triangular clusters (3 mutually adjacent same-color tiles) that involve any of the affected coordinates.
    /// Returns all coordinates that should be eliminated.
    /// </summary>
    public HashSet<HexCoordinate> FindMatchingClusters(HexGrid grid, HexCoordinate[] affectedCoords)
    {
        var matchedCoords = new HashSet<HexCoordinate>();

        // Check each affected coordinate and its neighbors for triangular clusters
        var coordsToCheck = new HashSet<HexCoordinate>(affectedCoords);
        foreach (var coord in affectedCoords)
        {
            foreach (var neighbor in coord.GetAllNeighbors())
            {
                if (grid.IsValidCoordinate(neighbor))
                {
                    coordsToCheck.Add(neighbor);
                }
            }
        }

        // Find all triangular clusters involving the coords to check
        var triangles = FindTriangularClusters(grid, coordsToCheck);
        
        foreach (var triangle in triangles)
        {
            matchedCoords.Add(triangle.Item1);
            matchedCoords.Add(triangle.Item2);
            matchedCoords.Add(triangle.Item3);
        }

        return matchedCoords;
    }

    /// <summary>
    /// Finds all triangular clusters (3 mutually adjacent same-color tiles) in the entire grid.
    /// Returns all coordinates that should be eliminated.
    /// </summary>
    public HashSet<HexCoordinate> FindAllClusters(HexGrid grid)
    {
        var matchedCoords = new HashSet<HexCoordinate>();
        var triangles = FindTriangularClusters(grid, grid.ValidCoordinates);
        
        foreach (var triangle in triangles)
        {
            matchedCoords.Add(triangle.Item1);
            matchedCoords.Add(triangle.Item2);
            matchedCoords.Add(triangle.Item3);
        }

        return matchedCoords;
    }

    /// <summary>
    /// Finds all triangular clusters where 3 tiles are mutually adjacent and the same color.
    /// A triangle is valid only if all 3 tiles are neighbors of each other.
    /// </summary>
    private HashSet<(HexCoordinate, HexCoordinate, HexCoordinate)> FindTriangularClusters(
        HexGrid grid, IEnumerable<HexCoordinate> coordsToCheck)
    {
        var triangles = new HashSet<(HexCoordinate, HexCoordinate, HexCoordinate)>();
        var checkedTriangles = new HashSet<string>();

        foreach (var coordA in coordsToCheck)
        {
            var tileA = grid.GetTile(coordA);
            if (tileA == null || tileA.IsStar || tileA.IsPearl) continue;

            var neighborsA = coordA.GetAllNeighbors();

            // Check all pairs of neighbors of A
            for (int i = 0; i < neighborsA.Length; i++)
            {
                var coordB = neighborsA[i];
                var tileB = grid.GetTile(coordB);
                if (tileB == null || tileB.IsStar || tileB.IsPearl || tileB.Color != tileA.Color) continue;

                for (int j = i + 1; j < neighborsA.Length; j++)
                {
                    var coordC = neighborsA[j];
                    var tileC = grid.GetTile(coordC);
                    if (tileC == null || tileC.IsStar || tileC.IsPearl || tileC.Color != tileA.Color) continue;

                    // Check if B and C are also neighbors (distance of 1)
                    if (coordB.DistanceTo(coordC) == 1)
                    {
                        // Found a valid triangle! Create a canonical key to avoid duplicates
                        var sorted = new[] { coordA, coordB, coordC }
                            .OrderBy(c => c.Q)
                            .ThenBy(c => c.R)
                            .ToArray();
                        var key = $"{sorted[0].Q},{sorted[0].R}|{sorted[1].Q},{sorted[1].R}|{sorted[2].Q},{sorted[2].R}";

                        if (checkedTriangles.Add(key))
                        {
                            triangles.Add((sorted[0], sorted[1], sorted[2]));
                        }
                    }
                }
            }
        }

        return triangles;
    }

    /// <summary>
    /// Checks if any triangular cluster exists involving the given triplet.
    /// </summary>
    public bool CheckTripletMatch(HexGrid grid, HexCoordinate[] triplet)
    {
        return FindMatchingClusters(grid, triplet).Count >= 3;
    }

    /// <summary>
    /// Gets all tiles in matching triangular clusters that include the triplet.
    /// Returns empty if no matches found.
    /// </summary>
    public List<HexTile> GetMatchingTripletTiles(HexGrid grid, HexCoordinate[] triplet)
    {
        var matchedCoords = FindMatchingClusters(grid, triplet);
        
        return matchedCoords
            .Select(grid.GetTile)
            .Where(t => t != null)
            .ToList()!;
    }

    /// <summary>
    /// Checks if a hexagon pattern exists (6 tiles of the same color surrounding a center).
    /// Returns the center coordinate if found, null otherwise.
    /// </summary>
    public HexCoordinate? CheckForHexagonPattern(HexGrid grid, HexCoordinate[] affectedCoords)
    {
        // Check each affected coordinate and its neighbors as potential centers
        var checkedCenters = new HashSet<HexCoordinate>();
        
        foreach (var coord in affectedCoords)
        {
            // Check this coord as a potential center
            if (checkedCenters.Add(coord) && CheckHexagonAtCenter(grid, coord))
            {
                return coord;
            }

            // Check all neighbors as potential centers
            foreach (var neighbor in coord.GetAllNeighbors())
            {
                if (grid.IsValidCoordinate(neighbor) && checkedCenters.Add(neighbor))
                {
                    if (CheckHexagonAtCenter(grid, neighbor))
                    {
                        return neighbor;
                    }
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Checks if a hexagon pattern exists at the specified center (6 same-color non-star non-pearl tiles).
    /// The center can be a star (preserved) or pearl (preserved) or regular tile (replaced with star).
    /// </summary>
    public bool CheckHexagonAtCenter(HexGrid grid, HexCoordinate center)
    {
        // Must have all 6 neighbors
        if (!grid.HasAllNeighbors(center)) return false;

        var neighbors = grid.GetNeighborTiles(center);
        if (neighbors.Count != 6) return false;

        // All neighbors must be non-star, non-pearl tiles of the same color
        var regularNeighbors = neighbors.Where(t => !t.IsStar && !t.IsPearl).ToList();
        if (regularNeighbors.Count != 6) return false;

        var firstColor = regularNeighbors[0].Color;
        return regularNeighbors.All(t => t.Color == firstColor);
    }

    /// <summary>
    /// Checks if a star hexagon pattern exists at the specified center (6 star tiles surrounding any center).
    /// </summary>
    public bool CheckStarHexagonAtCenter(HexGrid grid, HexCoordinate center)
    {
        // Must have all 6 neighbors
        if (!grid.HasAllNeighbors(center)) return false;

        var neighbors = grid.GetNeighborTiles(center);
        if (neighbors.Count != 6) return false;

        // All 6 neighbors must be star tiles
        return neighbors.All(t => t.IsStar);
    }

    /// <summary>
    /// Checks if a star hexagon pattern exists (6 star tiles surrounding a center).
    /// Returns the center coordinate if found, null otherwise.
    /// </summary>
    public HexCoordinate? CheckForStarHexagonPattern(HexGrid grid, HexCoordinate[] affectedCoords)
    {
        var checkedCenters = new HashSet<HexCoordinate>();
        
        foreach (var coord in affectedCoords)
        {
            // Check this coord as a potential center
            if (checkedCenters.Add(coord) && CheckStarHexagonAtCenter(grid, coord))
            {
                return coord;
            }

            // Check all neighbors as potential centers
            foreach (var neighbor in coord.GetAllNeighbors())
            {
                if (grid.IsValidCoordinate(neighbor) && checkedCenters.Add(neighbor))
                {
                    if (CheckStarHexagonAtCenter(grid, neighbor))
                    {
                        return neighbor;
                    }
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Checks if a pearl hexagon pattern exists at the specified center (6 pearl tiles surrounding any center).
    /// </summary>
    public bool CheckPearlHexagonAtCenter(HexGrid grid, HexCoordinate center)
    {
        // Must have all 6 neighbors
        if (!grid.HasAllNeighbors(center)) return false;

        var neighbors = grid.GetNeighborTiles(center);
        if (neighbors.Count != 6) return false;

        // All 6 neighbors must be pearl tiles
        return neighbors.All(t => t.IsPearl);
    }

    /// <summary>
    /// Checks if a pearl hexagon pattern exists (6 pearl tiles surrounding a center).
    /// Returns the center coordinate if found, null otherwise.
    /// </summary>
    public HexCoordinate? CheckForPearlHexagonPattern(HexGrid grid, HexCoordinate[] affectedCoords)
    {
        var checkedCenters = new HashSet<HexCoordinate>();
        
        foreach (var coord in affectedCoords)
        {
            // Check this coord as a potential center
            if (checkedCenters.Add(coord) && CheckPearlHexagonAtCenter(grid, coord))
            {
                return coord;
            }

            // Check all neighbors as potential centers
            foreach (var neighbor in coord.GetAllNeighbors())
            {
                if (grid.IsValidCoordinate(neighbor) && checkedCenters.Add(neighbor))
                {
                    if (CheckPearlHexagonAtCenter(grid, neighbor))
                    {
                        return neighbor;
                    }
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Finds all triangular clusters of star tiles (3 mutually adjacent stars).
    /// </summary>
    public HashSet<HexCoordinate> FindStarTriangularClusters(HexGrid grid, HexCoordinate[] affectedCoords)
    {
        var matchedCoords = new HashSet<HexCoordinate>();

        // Check each affected coordinate and its neighbors
        var coordsToCheck = new HashSet<HexCoordinate>(affectedCoords);
        foreach (var coord in affectedCoords)
        {
            foreach (var neighbor in coord.GetAllNeighbors())
            {
                if (grid.IsValidCoordinate(neighbor))
                {
                    coordsToCheck.Add(neighbor);
                }
            }
        }

        var triangles = FindStarTriangles(grid, coordsToCheck);
        
        foreach (var triangle in triangles)
        {
            matchedCoords.Add(triangle.Item1);
            matchedCoords.Add(triangle.Item2);
            matchedCoords.Add(triangle.Item3);
        }

        return matchedCoords;
    }

    /// <summary>
    /// Finds all triangular clusters where 3 star tiles are mutually adjacent.
    /// </summary>
    private HashSet<(HexCoordinate, HexCoordinate, HexCoordinate)> FindStarTriangles(
        HexGrid grid, IEnumerable<HexCoordinate> coordsToCheck)
    {
        var triangles = new HashSet<(HexCoordinate, HexCoordinate, HexCoordinate)>();
        var checkedTriangles = new HashSet<string>();

        foreach (var coordA in coordsToCheck)
        {
            var tileA = grid.GetTile(coordA);
            if (tileA == null || !tileA.IsStar) continue;

            var neighborsA = coordA.GetAllNeighbors();

            // Check all pairs of neighbors of A
            for (int i = 0; i < neighborsA.Length; i++)
            {
                var coordB = neighborsA[i];
                var tileB = grid.GetTile(coordB);
                if (tileB == null || !tileB.IsStar) continue;

                for (int j = i + 1; j < neighborsA.Length; j++)
                {
                    var coordC = neighborsA[j];
                    var tileC = grid.GetTile(coordC);
                    if (tileC == null || !tileC.IsStar) continue;

                    // Check if B and C are also neighbors (distance of 1)
                    if (coordB.DistanceTo(coordC) == 1)
                    {
                        // Found a valid star triangle! Create a canonical key to avoid duplicates
                        var sorted = new[] { coordA, coordB, coordC }
                            .OrderBy(c => c.Q)
                            .ThenBy(c => c.R)
                            .ToArray();
                        var key = $"{sorted[0].Q},{sorted[0].R}|{sorted[1].Q},{sorted[1].R}|{sorted[2].Q},{sorted[2].R}";

                        if (checkedTriangles.Add(key))
                        {
                            triangles.Add((sorted[0], sorted[1], sorted[2]));
                        }
                    }
                }
            }
        }

        return triangles;
    }

    /// <summary>
    /// Finds all star triangular clusters in the entire grid.
    /// </summary>
    public HashSet<HexCoordinate> FindAllStarClusters(HexGrid grid)
    {
        var matchedCoords = new HashSet<HexCoordinate>();
        var triangles = FindStarTriangles(grid, grid.ValidCoordinates);
        
        foreach (var triangle in triangles)
        {
            matchedCoords.Add(triangle.Item1);
            matchedCoords.Add(triangle.Item2);
            matchedCoords.Add(triangle.Item3);
        }

        return matchedCoords;
    }

    /// <summary>
    /// Finds all triangular clusters of pearl tiles (3 mutually adjacent pearls).
    /// </summary>
    public HashSet<HexCoordinate> FindPearlTriangularClusters(HexGrid grid, HexCoordinate[] affectedCoords)
    {
        var matchedCoords = new HashSet<HexCoordinate>();

        // Check each affected coordinate and its neighbors
        var coordsToCheck = new HashSet<HexCoordinate>(affectedCoords);
        foreach (var coord in affectedCoords)
        {
            foreach (var neighbor in coord.GetAllNeighbors())
            {
                if (grid.IsValidCoordinate(neighbor))
                {
                    coordsToCheck.Add(neighbor);
                }
            }
        }

        var triangles = FindPearlTriangles(grid, coordsToCheck);
        
        foreach (var triangle in triangles)
        {
            matchedCoords.Add(triangle.Item1);
            matchedCoords.Add(triangle.Item2);
            matchedCoords.Add(triangle.Item3);
        }

        return matchedCoords;
    }

    /// <summary>
    /// Finds all triangular clusters where 3 pearl tiles are mutually adjacent.
    /// </summary>
    private HashSet<(HexCoordinate, HexCoordinate, HexCoordinate)> FindPearlTriangles(
        HexGrid grid, IEnumerable<HexCoordinate> coordsToCheck)
    {
        var triangles = new HashSet<(HexCoordinate, HexCoordinate, HexCoordinate)>();
        var checkedTriangles = new HashSet<string>();

        foreach (var coordA in coordsToCheck)
        {
            var tileA = grid.GetTile(coordA);
            if (tileA == null || !tileA.IsPearl) continue;

            var neighborsA = coordA.GetAllNeighbors();

            // Check all pairs of neighbors of A
            for (int i = 0; i < neighborsA.Length; i++)
            {
                var coordB = neighborsA[i];
                var tileB = grid.GetTile(coordB);
                if (tileB == null || !tileB.IsPearl) continue;

                for (int j = i + 1; j < neighborsA.Length; j++)
                {
                    var coordC = neighborsA[j];
                    var tileC = grid.GetTile(coordC);
                    if (tileC == null || !tileC.IsPearl) continue;

                    // Check if B and C are also neighbors (distance of 1)
                    if (coordB.DistanceTo(coordC) == 1)
                    {
                        // Found a valid pearl triangle! Create a canonical key to avoid duplicates
                        var sorted = new[] { coordA, coordB, coordC }
                            .OrderBy(c => c.Q)
                            .ThenBy(c => c.R)
                            .ToArray();
                        var key = $"{sorted[0].Q},{sorted[0].R}|{sorted[1].Q},{sorted[1].R}|{sorted[2].Q},{sorted[2].R}";

                        if (checkedTriangles.Add(key))
                        {
                            triangles.Add((sorted[0], sorted[1], sorted[2]));
                        }
                    }
                }
            }
        }

        return triangles;
    }

    /// <summary>
    /// Finds all pearl triangular clusters in the entire grid.
    /// </summary>
    public HashSet<HexCoordinate> FindAllPearlClusters(HexGrid grid)
    {
        var matchedCoords = new HashSet<HexCoordinate>();
        var triangles = FindPearlTriangles(grid, grid.ValidCoordinates);
        
        foreach (var triangle in triangles)
        {
            matchedCoords.Add(triangle.Item1);
            matchedCoords.Add(triangle.Item2);
            matchedCoords.Add(triangle.Item3);
        }

        return matchedCoords;
    }

    /// <summary>
    /// Gets all tiles involved in a hexagon pattern (center + 6 neighbors).
    /// </summary>
    public List<HexTile> GetHexagonPatternTiles(HexGrid grid, HexCoordinate center)
    {
        var tiles = new List<HexTile>();
        
        var centerTile = grid.GetTile(center);
        if (centerTile != null)
        {
            tiles.Add(centerTile);
        }

        tiles.AddRange(grid.GetNeighborTiles(center));
        
        return tiles;
    }

    /// <summary>
    /// Finds all connected tiles of the same color starting from a given tile.
    /// Used for potential future chain reaction detection.
    /// </summary>
    public HashSet<HexCoordinate> FindConnectedSameColor(HexGrid grid, HexCoordinate start)
    {
        var visited = new HashSet<HexCoordinate>();
        var startTile = grid.GetTile(start);
        
        if (startTile == null || startTile.IsStar || startTile.IsPearl) return visited;

        var targetColor = startTile.Color;
        var queue = new Queue<HexCoordinate>();
        queue.Enqueue(start);
        visited.Add(start);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            
            foreach (var neighbor in current.GetAllNeighbors())
            {
                if (visited.Contains(neighbor)) continue;
                
                var tile = grid.GetTile(neighbor);
                if (tile != null && !tile.IsStar && !tile.IsPearl && tile.Color == targetColor)
                {
                    visited.Add(neighbor);
                    queue.Enqueue(neighbor);
                }
            }
        }

        return visited;
    }

    /// <summary>
    /// Result of scanning for matches, with priority information.
    /// </summary>
    public enum MatchType
    {
        None,
        RegularCluster,      // 3 same-color tiles
        StarCluster,         // 3 star tiles
        PearlCluster,        // 3 pearl tiles
        RegularHexagon,      // 6 same-color around center - creates star
        StarHexagon,         // 6 stars around center - creates pearl
        PearlHexagon         // 6 pearls around center - creates star
    }

    /// <summary>
    /// Scans the entire grid for any matches after tiles have settled.
    /// Returns all coordinates that should be removed and the match type.
    /// Priority: StarHexagon > PearlHexagon > RegularHexagon > StarCluster > PearlCluster > RegularCluster
    /// </summary>
    public (List<HexCoordinate> ToRemove, HexCoordinate? Center, MatchType Type) ScanForMatches(HexGrid grid)
    {
        var toRemove = new HashSet<HexCoordinate>();
        HexCoordinate? center = null;
        var matchType = MatchType.None;

        // Priority 1: Check for star hexagon patterns (6 stars around a center) - produces pearl
        foreach (var coord in grid.ValidCoordinates)
        {
            if (CheckStarHexagonAtCenter(grid, coord))
            {
                center = coord;
                matchType = MatchType.StarHexagon;
                // Only remove the 6 surrounding stars, not the center
                foreach (var neighbor in coord.GetAllNeighbors())
                {
                    toRemove.Add(neighbor);
                }
                return (toRemove.ToList(), center, matchType);
            }
        }

        // Priority 2: Check for pearl hexagon patterns (6 pearls around a center) - produces star
        foreach (var coord in grid.ValidCoordinates)
        {
            if (CheckPearlHexagonAtCenter(grid, coord))
            {
                center = coord;
                matchType = MatchType.PearlHexagon;
                // Only remove the 6 surrounding pearls, not the center
                foreach (var neighbor in coord.GetAllNeighbors())
                {
                    toRemove.Add(neighbor);
                }
                return (toRemove.ToList(), center, matchType);
            }
        }

        // Priority 3: Check for regular hexagon patterns (6 same-color around center) - produces star
        foreach (var coord in grid.ValidCoordinates)
        {
            if (CheckHexagonAtCenter(grid, coord))
            {
                center = coord;
                matchType = MatchType.RegularHexagon;
                var centerTile = grid.GetTile(coord);
                // Don't remove center if it's a star or pearl (they get preserved)
                if (centerTile != null && !centerTile.IsStar && !centerTile.IsPearl)
                {
                    toRemove.Add(coord);
                }
                foreach (var neighbor in coord.GetAllNeighbors())
                {
                    toRemove.Add(neighbor);
                }
                return (toRemove.ToList(), center, matchType);
            }
        }

        // Priority 4: Check for star clusters (3 adjacent stars)
        var starClusters = FindAllStarClusters(grid);
        if (starClusters.Count > 0)
        {
            matchType = MatchType.StarCluster;
            foreach (var coord in starClusters)
            {
                toRemove.Add(coord);
            }
            return (toRemove.ToList(), null, matchType);
        }

        // Priority 5: Check for pearl clusters (3 adjacent pearls)
        var pearlClusters = FindAllPearlClusters(grid);
        if (pearlClusters.Count > 0)
        {
            matchType = MatchType.PearlCluster;
            foreach (var coord in pearlClusters)
            {
                toRemove.Add(coord);
            }
            return (toRemove.ToList(), null, matchType);
        }

        // Priority 6: Check for regular clusters (3 same-color)
        var clusters = FindAllClusters(grid);
        if (clusters.Count > 0)
        {
            matchType = MatchType.RegularCluster;
            foreach (var coord in clusters)
            {
                toRemove.Add(coord);
            }
        }

        return (toRemove.ToList(), center, matchType);
    }
}
