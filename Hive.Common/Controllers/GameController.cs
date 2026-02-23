using System.Linq;
using Avalonia;
using Hive.Common.Animation;
using Hive.Common.Models;
using Hive.Common.Services;

namespace Hive.Common.Controllers;

/// <summary>
/// Main game controller that orchestrates game logic.
/// </summary>
public class GameController
{
    public GameState GameState { get; }
    public AnimationManager AnimationManager { get; }
    
    private readonly TileSpawner _spawner;
    private readonly MatchDetector _matchDetector;
    private readonly GravitySystem _gravitySystem;
    
    private Func<HexCoordinate, Point>? _coordToScreen;
    private Func<double>? _getHexSize;
    private Func<Point>? _getOrigin;

    /// <summary>
    /// Event raised when the score changes.
    /// </summary>
    public event Action<int>? ScoreChanged;

    /// <summary>
    /// Event raised when a move is completed.
    /// </summary>
    public event Action? MoveCompleted;

    /// <summary>
    /// Event raised when the game is over (bomb exploded).
    /// </summary>
    public event Action? GameOver;

    public GameController()
    {
        GameState = new GameState();
        AnimationManager = new AnimationManager();
        _spawner = new TileSpawner();
        _matchDetector = new MatchDetector();
        _gravitySystem = new GravitySystem();
    }

    /// <summary>
    /// Sets the coordinate conversion functions from the canvas.
    /// </summary>
    public void SetCoordinateConverter(
        Func<HexCoordinate, Point> coordToScreen,
        Func<double> getHexSize,
        Func<Point> getOrigin)
    {
        _coordToScreen = coordToScreen;
        _getHexSize = getHexSize;
        _getOrigin = getOrigin;
    }

    /// <summary>
    /// Starts a new game.
    /// </summary>
    public void NewGame()
    {
        AnimationManager.CancelAll();
        GameState.Reset();
        
        // Fill the grid with initial tiles
        _spawner.FillGrid(GameState.Grid);
        
        // Keep replacing tiles until no initial matches
        EliminateInitialMatches();
        
        // Position tiles
        UpdateAllTilePositions();
    }

    /// <summary>
    /// Removes any initial matching clusters by replacing tiles until no matches exist.
    /// Checks for both hexagon patterns (6 same-color ring) and triangular clusters.
    /// </summary>
    private void EliminateInitialMatches()
    {
        const int maxIterations = 100; // Safety limit to prevent infinite loops
        
        for (int i = 0; i < maxIterations; i++)
        {
            bool foundMatch = false;

            // First check for hexagon patterns (6 same-color tiles around a center)
            foreach (var coord in GameState.Grid.ValidCoordinates)
            {
                if (_matchDetector.CheckHexagonAtCenter(GameState.Grid, coord))
                {
                    foundMatch = true;
                    // Replace one of the ring tiles to break the pattern
                    var neighbors = coord.GetAllNeighbors();
                    foreach (var neighbor in neighbors)
                    {
                        if (GameState.Grid.IsValidCoordinate(neighbor))
                        {
                            // Don't allow bombs during initial setup
                            var newTile = _spawner.SpawnTile(neighbor, allowBombs: false);
                            GameState.Grid.SetTile(neighbor, newTile);
                            break; // Only need to replace one tile to break the pattern
                        }
                    }
                }
            }

            // Then check for triangular clusters
            var clusters = _matchDetector.FindAllClusters(GameState.Grid);
            if (clusters.Count > 0)
            {
                foundMatch = true;
                // Replace matched tiles with new random colors
                foreach (var coord in clusters)
                {
                    // Don't allow bombs during initial setup
                    var newTile = _spawner.SpawnTile(coord, allowBombs: false);
                    GameState.Grid.SetTile(coord, newTile);
                }
            }

            if (!foundMatch) break;
        }
    }

    /// <summary>
    /// Updates all tile screen positions based on their grid coordinates.
    /// </summary>
    public void UpdateAllTilePositions()
    {
        if (_coordToScreen == null) return;

        foreach (var tile in GameState.Grid.GetAllTiles())
        {
            var screenPos = _coordToScreen(tile.Coordinate);
            tile.ScreenPosition = screenPos;
            tile.TargetPosition = screenPos;
        }
    }

    /// <summary>
    /// Attempts to rotate a triplet of tiles.
    /// Will try up to 3 rotations (full 360 degrees) before giving up.
    /// </summary>
    public async Task TryRotateTriplet(HexCoordinate[] triplet)
    {
        if (GameState.IsAnimating || AnimationManager.IsAnimating) return;
        if (triplet.Length != 3) return;

        // Get the tiles
        var tiles = triplet
            .Select(c => GameState.Grid.GetTile(c))
            .Where(t => t != null)
            .ToList()!;

        if (tiles.Count != 3) return;

        GameState.IsAnimating = true;

        // Calculate rotation center
        var center = HexMath.GetTripletCenter(triplet, _getHexSize?.Invoke() ?? 40, _getOrigin?.Invoke() ?? new Point());

        // Try up to 3 rotations (full circle back to original position)
        for (int rotation = 0; rotation < 3; rotation++)
        {
            // Get current tiles at these positions (they may have moved from previous rotations)
            tiles = triplet
                .Select(c => GameState.Grid.GetTile(c))
                .Where(t => t != null)
                .ToList()!;

            if (tiles.Count != 3) break;

            // Create and play rotation animation
            var rotationAnim = new RotationAnimation(tiles!, center, GameState.IsClockwise);
            
            var animationComplete = new TaskCompletionSource<bool>();
            rotationAnim.OnComplete = () => animationComplete.SetResult(true);
            
            AnimationManager.Start(rotationAnim);
            await animationComplete.Task;

            // Update grid positions after rotation
            ApplyTripletRotation(triplet, GameState.IsClockwise);

            // Check for matches in priority order:
            // Priority 1: Star hexagon (6 stars around center) - produces pearl
            var starHexagonCenter = _matchDetector.CheckForStarHexagonPattern(GameState.Grid, triplet);
            if (starHexagonCenter.HasValue)
            {
                await ProcessStarHexagonMatch(starHexagonCenter.Value);
                GameState.IsAnimating = false;
                OnTurnComplete();
                return;
            }

            // Priority 2: Pearl hexagon (6 pearls around center) - produces star
            var pearlHexagonCenter = _matchDetector.CheckForPearlHexagonPattern(GameState.Grid, triplet);
            if (pearlHexagonCenter.HasValue)
            {
                await ProcessPearlHexagonMatch(pearlHexagonCenter.Value);
                GameState.IsAnimating = false;
                OnTurnComplete();
                return;
            }

            // Priority 3: Regular hexagon (6 same-color around center) - produces star
            var hexagonCenter = _matchDetector.CheckForHexagonPattern(GameState.Grid, triplet);
            if (hexagonCenter.HasValue)
            {
                await ProcessHexagonMatch(hexagonCenter.Value);
                GameState.IsAnimating = false;
                OnTurnComplete();
                return;
            }

            // Priority 4: Star cluster (3 adjacent stars)
            var starCluster = _matchDetector.FindStarTriangularClusters(GameState.Grid, triplet);
            if (starCluster.Count >= 3)
            {
                await ProcessStarClusterMatch(starCluster.ToArray());
                GameState.IsAnimating = false;
                OnTurnComplete();
                return;
            }

            // Priority 5: Pearl cluster (3 adjacent pearls)
            var pearlCluster = _matchDetector.FindPearlTriangularClusters(GameState.Grid, triplet);
            if (pearlCluster.Count >= 3)
            {
                await ProcessPearlClusterMatch(pearlCluster.ToArray());
                GameState.IsAnimating = false;
                OnTurnComplete();
                return;
            }

            // Priority 6: Regular cluster (3 same-color)
            var matchedCoords = _matchDetector.FindMatchingClusters(GameState.Grid, triplet);
            if (matchedCoords.Count >= 3)
            {
                await ProcessRegularMatch(matchedCoords.ToArray());
                GameState.IsAnimating = false;
                OnTurnComplete();
                return;
            }
        }

        // After 3 rotations, we're back to original position - no bounce needed
        // (tiles have visually returned to their starting positions)
        GameState.IsAnimating = false;
        OnTurnComplete();
    }

    /// <summary>
    /// Processes matched tile clusters.
    /// </summary>
    private async Task ProcessMatchedClusters(HashSet<HexCoordinate> matchedCoords)
    {
        // Check for hexagon pattern first (takes priority)
        HexCoordinate? hexagonCenter = null;
        foreach (var coord in matchedCoords)
        {
            hexagonCenter = _matchDetector.CheckForHexagonPattern(GameState.Grid, matchedCoords.ToArray());
            if (hexagonCenter.HasValue) break;
        }

        if (hexagonCenter.HasValue)
        {
            await ProcessHexagonMatch(hexagonCenter.Value);
        }
        else
        {
            await ProcessRegularMatch(matchedCoords.ToArray());
        }
    }

    /// <summary>
    /// Attempts to rotate tiles around a star.
    /// </summary>
    public async Task TryRotateAroundStar(HexCoordinate starCoord)
    {
        if (GameState.IsAnimating || AnimationManager.IsAnimating) return;

        var starTile = GameState.Grid.GetTile(starCoord);
        if (starTile == null || !starTile.IsStar) return;

        // Check if star has all 6 neighbors
        if (!GameState.Grid.HasAllNeighbors(starCoord)) return;

        var neighborCoords = starCoord.GetAllNeighbors();
        var neighborTiles = neighborCoords
            .Select(c => GameState.Grid.GetTile(c))
            .Where(t => t != null)
            .ToList()!;

        if (neighborTiles.Count != 6) return;

        GameState.IsAnimating = true;

        // Rotate the 6 neighbors around the star
        var center = starTile.ScreenPosition;
        var rotationAnim = new RotationAnimation(neighborTiles!, center, GameState.IsClockwise);

        var animationComplete = new TaskCompletionSource<bool>();
        rotationAnim.OnComplete = () => animationComplete.SetResult(true);

        AnimationManager.Start(rotationAnim);
        await animationComplete.Task;

        // Update grid positions
        ApplySixTileRotation(starCoord, GameState.IsClockwise);

        // Update tile screen positions
        foreach (var tile in neighborTiles)
        {
            if (tile != null && _coordToScreen != null)
            {
                tile!.ScreenPosition = _coordToScreen(tile.Coordinate);
                tile.TargetPosition = tile.ScreenPosition;
            }
        }

        // Check for matches in priority order - include the star coordinate as an affected coord
        var affectedCoords = new List<HexCoordinate>(neighborCoords) { starCoord };
        
        // Priority 1: Star hexagon (this could happen if rotating stars around a star creates a ring of stars around another tile)
        var starHexagonCenter = _matchDetector.CheckForStarHexagonPattern(GameState.Grid, affectedCoords.ToArray());
        if (starHexagonCenter.HasValue)
        {
            await ProcessStarHexagonMatch(starHexagonCenter.Value);
            GameState.IsAnimating = false;
            OnTurnComplete();
            return;
        }

        // Priority 2: Pearl hexagon
        var pearlHexagonCenter = _matchDetector.CheckForPearlHexagonPattern(GameState.Grid, affectedCoords.ToArray());
        if (pearlHexagonCenter.HasValue)
        {
            await ProcessPearlHexagonMatch(pearlHexagonCenter.Value);
            GameState.IsAnimating = false;
            OnTurnComplete();
            return;
        }

        // Priority 3: Regular hexagon
        var hexagonCenter = _matchDetector.CheckForHexagonPattern(GameState.Grid, affectedCoords.ToArray());
        if (hexagonCenter.HasValue)
        {
            await ProcessHexagonMatch(hexagonCenter.Value);
            GameState.IsAnimating = false;
            OnTurnComplete();
            return;
        }

        // Priority 4: Star cluster
        var starCluster = _matchDetector.FindStarTriangularClusters(GameState.Grid, affectedCoords.ToArray());
        if (starCluster.Count >= 3)
        {
            await ProcessStarClusterMatch(starCluster.ToArray());
            GameState.IsAnimating = false;
            OnTurnComplete();
            return;
        }

        // Priority 5: Pearl cluster
        var pearlCluster = _matchDetector.FindPearlTriangularClusters(GameState.Grid, affectedCoords.ToArray());
        if (pearlCluster.Count >= 3)
        {
            await ProcessPearlClusterMatch(pearlCluster.ToArray());
            GameState.IsAnimating = false;
            OnTurnComplete();
            return;
        }

        // Priority 6: Regular cluster
        var matchedCoords = _matchDetector.FindMatchingClusters(GameState.Grid, affectedCoords.ToArray());
        if (matchedCoords.Count >= 3)
        {
            await ProcessRegularMatch(matchedCoords.ToArray());
            GameState.IsAnimating = false;
            OnTurnComplete();
            return;
        }

        // No matches found - rotation still happens but nothing is eliminated
        GameState.IsAnimating = false;
        OnTurnComplete();
    }

    private void ApplyTripletRotation(HexCoordinate[] triplet, bool clockwise)
    {
        // Get current tiles
        var tiles = triplet.Select(c => GameState.Grid.GetTile(c)).ToArray();

        // Remove from current positions
        foreach (var coord in triplet)
        {
            GameState.Grid.RemoveTile(coord);
        }

        // Place at rotated positions
        if (clockwise)
        {
            // Clockwise: 0->1, 1->2, 2->0
            GameState.Grid.SetTile(triplet[1], tiles[0]);
            GameState.Grid.SetTile(triplet[2], tiles[1]);
            GameState.Grid.SetTile(triplet[0], tiles[2]);
        }
        else
        {
            // Counter-clockwise: 0->2, 1->0, 2->1
            GameState.Grid.SetTile(triplet[2], tiles[0]);
            GameState.Grid.SetTile(triplet[0], tiles[1]);
            GameState.Grid.SetTile(triplet[1], tiles[2]);
        }

        // Update screen positions
        foreach (var coord in triplet)
        {
            var tile = GameState.Grid.GetTile(coord);
            if (tile != null && _coordToScreen != null)
            {
                tile.ScreenPosition = _coordToScreen(coord);
                tile.TargetPosition = tile.ScreenPosition;
            }
        }
    }

    private void ApplySixTileRotation(HexCoordinate center, bool clockwise)
    {
        var neighbors = center.GetAllNeighbors();
        var tiles = neighbors.Select(c => GameState.Grid.GetTile(c)).ToArray();

        // Remove from current positions
        foreach (var coord in neighbors)
        {
            GameState.Grid.RemoveTile(coord);
        }

        // Place at rotated positions (shift by 1 in direction)
        for (int i = 0; i < 6; i++)
        {
            int newIndex = clockwise ? (i + 1) % 6 : (i + 5) % 6;
            GameState.Grid.SetTile(neighbors[newIndex], tiles[i]);
        }
    }

    private async Task RevertRotation(List<HexTile> tiles, Point center, Point[] originalPositions, HexCoordinate[] originalCoords)
    {
        // Revert grid positions
        foreach (var tile in tiles)
        {
            GameState.Grid.RemoveTile(tile.Coordinate);
        }

        for (int i = 0; i < tiles.Count; i++)
        {
            GameState.Grid.SetTile(originalCoords[i], tiles[i]);
        }

        // Animate bounce back
        var bounceAnim = new BounceAnimation(tiles, center, originalPositions, GameState.IsClockwise);
        
        var animationComplete = new TaskCompletionSource<bool>();
        bounceAnim.OnComplete = () => animationComplete.SetResult(true);
        
        AnimationManager.Start(bounceAnim);
        await animationComplete.Task;

        // Ensure positions are correct
        for (int i = 0; i < tiles.Count; i++)
        {
            tiles[i].ScreenPosition = originalPositions[i];
            tiles[i].TargetPosition = originalPositions[i];
        }
    }

    private async Task ProcessMatch(HexCoordinate[] matchedCoords)
    {
        // Check for hexagon pattern first
        var hexagonCenter = _matchDetector.CheckForHexagonPattern(GameState.Grid, matchedCoords);

        if (hexagonCenter.HasValue)
        {
            // Process hexagon pattern (creates star)
            await ProcessHexagonMatch(hexagonCenter.Value);
        }
        else
        {
            // Process regular 3-tile match
            await ProcessRegularMatch(matchedCoords);
        }
    }

    private async Task ProcessRegularMatch(HexCoordinate[] matchedCoords)
    {
        var tiles = matchedCoords
            .Select(c => GameState.Grid.GetTile(c))
            .Where(t => t != null)
            .ToList()!;

        var bonusTilesInMatch = tiles.Where(t => t!.IsBonus).ToList();
        var bombTilesInMatch = tiles.Where(t => t!.IsBomb).ToList();
        var additionalTilesToClear = new List<HexTile>();
        
        // Get the color of the matched tiles (they should all be the same color for a regular match)
        var matchColor = tiles[0]!.Color;
        
        // Check for bomb + bonus of same color: clears all tiles of that color from the board
        var hasBombBonusCombo = bombTilesInMatch.Any() && 
                                bonusTilesInMatch.Any(b => b!.Color == matchColor);
        
        if (hasBombBonusCombo)
        {
            // Find all tiles of the same color on the entire board
            var allSameColorTiles = GameState.Grid.GetAllTiles()
                .Where(t => t.Color == matchColor && !t.IsStar && !t.IsPearl)
                .ToList();
            
            // Add tiles that aren't already in the match
            foreach (var tile in allSameColorTiles)
            {
                if (!matchedCoords.Contains(tile.Coordinate))
                {
                    additionalTilesToClear.Add(tile);
                }
            }
        }
        // Check for 2+ bonus tiles: clears all tiles within proximity 1 of the BONUS tiles (except stars/pearls)
        else if (bonusTilesInMatch.Count >= 2)
        {
            // Get all neighbors of just the BONUS tiles (not the entire cluster)
            var neighborCoords = new HashSet<HexCoordinate>();
            foreach (var bonusTile in bonusTilesInMatch)
            {
                foreach (var neighbor in bonusTile!.Coordinate.GetAllNeighbors())
                {
                    // Don't include tiles that are already in the match
                    if (!matchedCoords.Contains(neighbor))
                    {
                        neighborCoords.Add(neighbor);
                    }
                }
            }
            
            // Get tiles at those coordinates (excluding stars and pearls)
            foreach (var coord in neighborCoords)
            {
                var tile = GameState.Grid.GetTile(coord);
                if (tile != null && !tile.IsStar && !tile.IsPearl)
                {
                    additionalTilesToClear.Add(tile);
                }
            }
        }

        // Combine original tiles with additional tiles to clear
        var allTilesToEliminate = tiles.Concat(additionalTilesToClear).ToList();

        // Animate elimination
        var eliminationAnim = new EliminationAnimation(allTilesToEliminate!);
        
        var animationComplete = new TaskCompletionSource<bool>();
        eliminationAnim.OnComplete = () => animationComplete.SetResult(true);
        
        AnimationManager.Start(eliminationAnim);
        await animationComplete.Task;

        // Remove tiles from grid
        foreach (var coord in matchedCoords)
        {
            GameState.Grid.RemoveTile(coord);
        }
        foreach (var tile in additionalTilesToClear)
        {
            GameState.Grid.RemoveTile(tile.Coordinate);
        }

        // Update score - count bonus tiles and regular tiles separately
        var totalBonusTiles = allTilesToEliminate.Count(t => t!.IsBonus);
        var regularTileCount = allTilesToEliminate.Count - totalBonusTiles;
        
        GameState.AddMatchScore(regularTileCount);
        if (totalBonusTiles > 0)
        {
            GameState.AddBonusEliminationScore(totalBonusTiles);
        }
        ScoreChanged?.Invoke(GameState.Score);

        // Apply gravity and spawn new tiles
        await ApplyGravityAndSpawn();

        // Check for chain reactions - include additional cleared coords
        var allClearedCoords = matchedCoords.Concat(additionalTilesToClear.Select(t => t.Coordinate)).ToArray();
        await CheckAndProcessMatches(allClearedCoords);
        
        UpdateAllTilePositions();
    }

    private async Task ProcessHexagonMatch(HexCoordinate center)
    {
        var centerTile = GameState.Grid.GetTile(center);
        bool centerWasStar = centerTile?.IsStar ?? false;
        bool centerWasPearl = centerTile?.IsPearl ?? false;
        bool centerIsSpecial = centerWasStar || centerWasPearl;

        // Get the 6 neighbor tiles (and center if not a star/pearl)
        var neighborCoords = center.GetAllNeighbors();
        var tilesToEliminate = new List<HexTile>();
        
        foreach (var coord in neighborCoords)
        {
            var tile = GameState.Grid.GetTile(coord);
            if (tile != null) tilesToEliminate.Add(tile);
        }

        // Only eliminate center if it's not already a star or pearl
        if (!centerIsSpecial && centerTile != null)
        {
            tilesToEliminate.Add(centerTile);
        }

        // Animate elimination
        var eliminationAnim = new EliminationAnimation(tilesToEliminate);
        
        var animationComplete = new TaskCompletionSource<bool>();
        eliminationAnim.OnComplete = () => animationComplete.SetResult(true);
        
        AnimationManager.Start(eliminationAnim);
        await animationComplete.Task;

        // Remove eliminated tiles from grid
        foreach (var coord in neighborCoords)
        {
            GameState.Grid.RemoveTile(coord);
        }
        
        if (!centerIsSpecial)
        {
            GameState.Grid.RemoveTile(center);
        }

        // Create star tile at center (or keep existing star/pearl)
        if (!centerIsSpecial)
        {
            var starTile = _spawner.SpawnStarTile(center);
            GameState.Grid.SetTile(center, starTile);
            
            if (_coordToScreen != null)
            {
                starTile.ScreenPosition = _coordToScreen(center);
                starTile.TargetPosition = starTile.ScreenPosition;
            }
        }

        // Update score
        int eliminatedCount = centerIsSpecial ? 6 : 7;
        GameState.AddMatchScore(eliminatedCount, createdStar: true);
        ScoreChanged?.Invoke(GameState.Score);

        if (centerIsSpecial)
        {
            // When center was a star or pearl:
            // 1. Apply gravity first (tiles fall down, creating empty space at top)
            await ApplyGravityOnly();
            // 2. Spawn the additional star at the lowest vacant position in the column (so it "falls" into place)
            await SpawnAdditionalStarAtTop(center);
            // 3. Spawn regular tiles for remaining empty positions
            await SpawnNewTiles();
        }
        else
        {
            // Normal case: apply gravity and spawn new tiles together
            await ApplyGravityAndSpawn();
        }

        // Check for chain reactions
        var affectedCoords = new List<HexCoordinate>(neighborCoords);
        if (!centerIsSpecial) affectedCoords.Add(center);
        await CheckAndProcessMatches(affectedCoords.ToArray());
    }

    /// <summary>
    /// Spawns an additional star tile from the top; it is placed at the lowest vacant position
    /// in the center column after gravity has been applied, so it visually "falls" into place.
    /// Called after ApplyGravityOnly when a hexagon is formed around an existing star/pearl.
    /// </summary>
    private async Task SpawnAdditionalStarAtTop(HexCoordinate center)
    {
        // Get the column of the center piece
        var (centerCol, _) = HexGrid.AxialToOffset(center);
        
        // Find the lowest empty coordinate in that column (scan from bottom to top)
        // so the new star occupies the vacant spot just above existing tiles.
        HexCoordinate? spawnCoord = null;
        
        for (int row = GameState.Grid.Rows - 1; row >= 0; row--)
        {
            var coord = HexGrid.OffsetToAxial(centerCol, row);
            if (GameState.Grid.IsValidCoordinate(coord) && GameState.Grid.GetTile(coord) == null)
            {
                spawnCoord = coord;
                break;
            }
        }
        
        // If no empty spot in that column, use the lowest empty spot from any column
        if (!spawnCoord.HasValue)
        {
            var emptyCoords = _gravitySystem.GetEmptyTopCoordinates(GameState.Grid);
            if (emptyCoords.Count > 0)
            {
                // Prefer same column; otherwise take the last (lowest in column order) empty
                var inColumn = emptyCoords.Where(e => e.Column == centerCol).ToList();
                spawnCoord = inColumn.Count > 0
                    ? inColumn.Last().Coordinate
                    : emptyCoords.Last().Coordinate;
            }
        }
        
        if (spawnCoord.HasValue)
        {
            var starTile = _spawner.SpawnStarTile(spawnCoord.Value);
            GameState.Grid.SetTile(spawnCoord.Value, starTile);

            var endPos = _coordToScreen?.Invoke(spawnCoord.Value) ?? new Point();
            
            // Start position should be at the TOP of the column (above the grid)
            // Get the position of row 0 in this column and start above that
            var topOfColumnCoord = HexGrid.OffsetToAxial(centerCol, 0);
            var topOfColumnPos = _coordToScreen?.Invoke(topOfColumnCoord) ?? new Point();
            var startPos = new Point(topOfColumnPos.X, topOfColumnPos.Y - (_getHexSize?.Invoke() ?? 40) * 2);
            
            starTile.ScreenPosition = startPos;
            
            var spawnAnim = new SpawnAnimation(
                new List<HexTile> { starTile }, 
                new[] { startPos }, 
                new[] { endPos });
            
            var animationComplete = new TaskCompletionSource<bool>();
            spawnAnim.OnComplete = () => animationComplete.SetResult(true);
            
            AnimationManager.Start(spawnAnim);
            await animationComplete.Task;
        }
    }

    /// <summary>
    /// Processes a star hexagon match (6 stars surrounding a center) - creates a pearl.
    /// If the center is already a pearl, the existing pearl is kept and an additional pearl falls from the top.
    /// </summary>
    private async Task ProcessStarHexagonMatch(HexCoordinate center)
    {
        var centerTile = GameState.Grid.GetTile(center);
        bool centerWasPearl = centerTile?.IsPearl ?? false;

        // Get the 6 star tiles around the center
        var neighborCoords = center.GetAllNeighbors();
        var tilesToEliminate = new List<HexTile>();
        
        foreach (var coord in neighborCoords)
        {
            var tile = GameState.Grid.GetTile(coord);
            if (tile != null) tilesToEliminate.Add(tile);
        }

        // Animate elimination
        var eliminationAnim = new EliminationAnimation(tilesToEliminate);
        
        var animationComplete = new TaskCompletionSource<bool>();
        eliminationAnim.OnComplete = () => animationComplete.SetResult(true);
        
        AnimationManager.Start(eliminationAnim);
        await animationComplete.Task;

        // Remove the 6 star tiles from grid
        foreach (var coord in neighborCoords)
        {
            GameState.Grid.RemoveTile(coord);
        }

        if (centerWasPearl)
        {
            // Center is already a pearl: keep it, apply gravity, spawn additional pearl falling from top
            GameState.AddStarEliminationScore(6);
            GameState.AddMatchScore(0, createdPearl: true);
            ScoreChanged?.Invoke(GameState.Score);

            await ApplyGravityOnly();
            await SpawnAdditionalPearlAtTop(center);
            await SpawnNewTiles();
        }
        else
        {
            // Remove center tile and create pearl at center
            GameState.Grid.RemoveTile(center);
            var pearlTile = _spawner.SpawnPearlTile(center);
            GameState.Grid.SetTile(center, pearlTile);
            
            if (_coordToScreen != null)
            {
                pearlTile.ScreenPosition = _coordToScreen(center);
                pearlTile.TargetPosition = pearlTile.ScreenPosition;
            }

            // Update score (6 star tiles eliminated + pearl bonus)
            GameState.AddStarEliminationScore(6);
            GameState.AddMatchScore(0, createdPearl: true); // Just add pearl bonus
            ScoreChanged?.Invoke(GameState.Score);

            // Apply gravity and spawn new tiles
            await ApplyGravityAndSpawn();
        }

        // Check for chain reactions
        await CheckAndProcessMatches(neighborCoords);
    }

    /// <summary>
    /// Spawns an additional pearl tile from the top; it is placed at the lowest vacant position
    /// in the center column after gravity has been applied, so it visually "falls" into place.
    /// Called when a star hexagon is formed around an existing pearl.
    /// </summary>
    private async Task SpawnAdditionalPearlAtTop(HexCoordinate center)
    {
        var (centerCol, _) = HexGrid.AxialToOffset(center);
        
        HexCoordinate? spawnCoord = null;
        
        for (int row = GameState.Grid.Rows - 1; row >= 0; row--)
        {
            var coord = HexGrid.OffsetToAxial(centerCol, row);
            if (GameState.Grid.IsValidCoordinate(coord) && GameState.Grid.GetTile(coord) == null)
            {
                spawnCoord = coord;
                break;
            }
        }
        
        if (!spawnCoord.HasValue)
        {
            var emptyCoords = _gravitySystem.GetEmptyTopCoordinates(GameState.Grid);
            if (emptyCoords.Count > 0)
            {
                var inColumn = emptyCoords.Where(e => e.Column == centerCol).ToList();
                spawnCoord = inColumn.Count > 0
                    ? inColumn.Last().Coordinate
                    : emptyCoords.Last().Coordinate;
            }
        }
        
        if (spawnCoord.HasValue)
        {
            var pearlTile = _spawner.SpawnPearlTile(spawnCoord.Value);
            GameState.Grid.SetTile(spawnCoord.Value, pearlTile);

            var endPos = _coordToScreen?.Invoke(spawnCoord.Value) ?? new Point();
            var topOfColumnCoord = HexGrid.OffsetToAxial(centerCol, 0);
            var topOfColumnPos = _coordToScreen?.Invoke(topOfColumnCoord) ?? new Point();
            var startPos = new Point(topOfColumnPos.X, topOfColumnPos.Y - (_getHexSize?.Invoke() ?? 40) * 2);
            
            pearlTile.ScreenPosition = startPos;
            
            var spawnAnim = new SpawnAnimation(
                new List<HexTile> { pearlTile }, 
                new[] { startPos }, 
                new[] { endPos });
            
            var animationComplete = new TaskCompletionSource<bool>();
            spawnAnim.OnComplete = () => animationComplete.SetResult(true);
            
            AnimationManager.Start(spawnAnim);
            await animationComplete.Task;
        }
    }

    /// <summary>
    /// Processes a star cluster match (3 adjacent stars) - eliminates with bonus points.
    /// </summary>
    private async Task ProcessStarClusterMatch(HexCoordinate[] matchedCoords)
    {
        var tiles = matchedCoords
            .Select(c => GameState.Grid.GetTile(c))
            .Where(t => t != null)
            .ToList()!;

        // Animate elimination
        var eliminationAnim = new EliminationAnimation(tiles!);
        
        var animationComplete = new TaskCompletionSource<bool>();
        eliminationAnim.OnComplete = () => animationComplete.SetResult(true);
        
        AnimationManager.Start(eliminationAnim);
        await animationComplete.Task;

        // Remove tiles from grid
        foreach (var coord in matchedCoords)
        {
            GameState.Grid.RemoveTile(coord);
        }

        // Update score (star tiles are worth more)
        GameState.AddStarEliminationScore(tiles.Count);
        ScoreChanged?.Invoke(GameState.Score);

        // Apply gravity and spawn new tiles
        await ApplyGravityAndSpawn();

        // Check for chain reactions
        await CheckAndProcessMatches(matchedCoords);
    }

    /// <summary>
    /// Processes a pearl cluster match (3 adjacent pearls) - eliminates with bonus points.
    /// </summary>
    private async Task ProcessPearlClusterMatch(HexCoordinate[] matchedCoords)
    {
        var tiles = matchedCoords
            .Select(c => GameState.Grid.GetTile(c))
            .Where(t => t != null)
            .ToList()!;

        // Animate elimination
        var eliminationAnim = new EliminationAnimation(tiles!);
        
        var animationComplete = new TaskCompletionSource<bool>();
        eliminationAnim.OnComplete = () => animationComplete.SetResult(true);
        
        AnimationManager.Start(eliminationAnim);
        await animationComplete.Task;

        // Remove tiles from grid
        foreach (var coord in matchedCoords)
        {
            GameState.Grid.RemoveTile(coord);
        }

        // Update score (pearl tiles are worth even more than stars)
        GameState.AddPearlEliminationScore(tiles.Count);
        ScoreChanged?.Invoke(GameState.Score);

        // Apply gravity and spawn new tiles
        await ApplyGravityAndSpawn();

        // Check for chain reactions
        await CheckAndProcessMatches(matchedCoords);
    }

    /// <summary>
    /// Processes a pearl hexagon match (6 pearls surrounding a center) - creates a star.
    /// If the center is already a star, the existing star is kept and an additional star falls from the top.
    /// </summary>
    private async Task ProcessPearlHexagonMatch(HexCoordinate center)
    {
        var centerTile = GameState.Grid.GetTile(center);
        bool centerWasStar = centerTile?.IsStar ?? false;

        // Get the 6 pearl tiles around the center
        var neighborCoords = center.GetAllNeighbors();
        var tilesToEliminate = new List<HexTile>();
        
        foreach (var coord in neighborCoords)
        {
            var tile = GameState.Grid.GetTile(coord);
            if (tile != null) tilesToEliminate.Add(tile);
        }

        // Animate elimination
        var eliminationAnim = new EliminationAnimation(tilesToEliminate);
        
        var animationComplete = new TaskCompletionSource<bool>();
        eliminationAnim.OnComplete = () => animationComplete.SetResult(true);
        
        AnimationManager.Start(eliminationAnim);
        await animationComplete.Task;

        // Remove the 6 pearl tiles from grid
        foreach (var coord in neighborCoords)
        {
            GameState.Grid.RemoveTile(coord);
        }

        if (centerWasStar)
        {
            // Center is already a star: keep it, apply gravity, spawn additional star falling from top
            GameState.AddPearlEliminationScore(6);
            GameState.AddMatchScore(0, createdStar: true);
            ScoreChanged?.Invoke(GameState.Score);

            await ApplyGravityOnly();
            await SpawnAdditionalStarAtTop(center);
            await SpawnNewTiles();
        }
        else
        {
            // Remove center tile and create star at center
            GameState.Grid.RemoveTile(center);
            var starTile = _spawner.SpawnStarTile(center);
            GameState.Grid.SetTile(center, starTile);
            
            if (_coordToScreen != null)
            {
                starTile.ScreenPosition = _coordToScreen(center);
                starTile.TargetPosition = starTile.ScreenPosition;
            }

            // Update score (6 pearl tiles eliminated + star bonus)
            GameState.AddPearlEliminationScore(6);
            GameState.AddMatchScore(0, createdStar: true); // Just add star bonus
            ScoreChanged?.Invoke(GameState.Score);

            // Apply gravity and spawn new tiles
            await ApplyGravityAndSpawn();
        }

        // Check for chain reactions
        await CheckAndProcessMatches(neighborCoords);
    }

    private async Task CheckAndProcessMatches(HexCoordinate[] affectedCoords)
    {
        // Check for any new matches formed by gravity
        var (toRemove, center, matchType) = _matchDetector.ScanForMatches(GameState.Grid);

        if (toRemove.Count > 0)
        {
            switch (matchType)
            {
                case MatchDetector.MatchType.StarHexagon:
                    await ProcessStarHexagonMatch(center!.Value);
                    break;
                case MatchDetector.MatchType.PearlHexagon:
                    await ProcessPearlHexagonMatch(center!.Value);
                    break;
                case MatchDetector.MatchType.RegularHexagon:
                    await ProcessHexagonMatch(center!.Value);
                    break;
                case MatchDetector.MatchType.StarCluster:
                    await ProcessStarClusterMatch(toRemove.ToArray());
                    break;
                case MatchDetector.MatchType.PearlCluster:
                    await ProcessPearlClusterMatch(toRemove.ToArray());
                    break;
                case MatchDetector.MatchType.RegularCluster:
                    await ProcessRegularMatch(toRemove.ToArray());
                    break;
            }
        }
    }

    private async Task ApplyGravityAndSpawn()
    {
        await ApplyGravityOnly();
        await SpawnNewTiles();
    }

    /// <summary>
    /// Applies gravity only, making tiles fall down to fill empty spaces.
    /// Does not spawn new tiles.
    /// </summary>
    private async Task ApplyGravityOnly()
    {
        // Calculate fall movements
        var movements = _gravitySystem.CalculateFalls(GameState.Grid);

        if (movements.Count > 0)
        {
            // Prepare fall animation
            var fallingTiles = movements.Select(m => m.Tile).ToList();
            var endPositions = movements.Select(m => _coordToScreen?.Invoke(m.To) ?? new Point()).ToArray();

            var fallAnim = new FallAnimation(fallingTiles, endPositions);
            
            var animationComplete = new TaskCompletionSource<bool>();
            fallAnim.OnComplete = () => animationComplete.SetResult(true);
            
            AnimationManager.Start(fallAnim);
            await animationComplete.Task;

            // Apply movements to grid
            _gravitySystem.ApplyMovements(GameState.Grid, movements);
        }
    }

    /// <summary>
    /// Spawns new tiles at all empty top positions.
    /// </summary>
    private async Task SpawnNewTiles()
    {
        // Spawn new tiles at empty positions
        var emptyCoords = _gravitySystem.GetEmptyTopCoordinates(GameState.Grid);
        
        if (emptyCoords.Count > 0)
        {
            var newTiles = new List<HexTile>();
            var startPositions = new List<Point>();
            var endPositions = new List<Point>();

            foreach (var (col, coord) in emptyCoords)
            {
                var tile = _spawner.SpawnTile(coord);
                GameState.Grid.SetTile(coord, tile);
                newTiles.Add(tile);

                var endPos = _coordToScreen?.Invoke(coord) ?? new Point();
                var startPos = new Point(endPos.X, endPos.Y - (_getHexSize?.Invoke() ?? 40) * 2);
                
                startPositions.Add(startPos);
                endPositions.Add(endPos);
            }

            var spawnAnim = new SpawnAnimation(newTiles, startPositions.ToArray(), endPositions.ToArray());
            
            var animationComplete = new TaskCompletionSource<bool>();
            spawnAnim.OnComplete = () => animationComplete.SetResult(true);
            
            AnimationManager.Start(spawnAnim);
            await animationComplete.Task;
        }
    }

    /// <summary>
    /// Checks if a coordinate contains a star that can be rotated.
    /// </summary>
    public bool CanRotateStar(HexCoordinate coord)
    {
        var tile = GameState.Grid.GetTile(coord);
        if (tile == null || !tile.IsStar) return false;
        return GameState.Grid.HasAllNeighbors(coord);
    }

    /// <summary>
    /// Checks if a coordinate contains a pearl that can be rotated.
    /// </summary>
    public bool CanRotatePearl(HexCoordinate coord)
    {
        var tile = GameState.Grid.GetTile(coord);
        if (tile == null || !tile.IsPearl) return false;
        
        // Pearl rotates 3 neighbors: Northwest (2) = above, Southwest (4) = bottom-left, East (0) = bottom-right
        var neighbors = coord.GetAllNeighbors();
        return GameState.Grid.IsValidCoordinate(neighbors[2]) &&
               GameState.Grid.IsValidCoordinate(neighbors[4]) &&
               GameState.Grid.IsValidCoordinate(neighbors[0]) &&
               GameState.Grid.GetTile(neighbors[2]) != null &&
               GameState.Grid.GetTile(neighbors[4]) != null &&
               GameState.Grid.GetTile(neighbors[0]) != null;
    }

    /// <summary>
    /// Attempts to rotate tiles around a pearl.
    /// Rotates 3 tiles: above (NW), southwest, southeast.
    /// </summary>
    public async Task TryRotateAroundPearl(HexCoordinate pearlCoord)
    {
        if (GameState.IsAnimating || AnimationManager.IsAnimating) return;

        var pearlTile = GameState.Grid.GetTile(pearlCoord);
        if (pearlTile == null || !pearlTile.IsPearl) return;

        // Get the 3 neighbors to rotate: Northwest (2) = above, Southwest (4) = bottom-left, East (0) = bottom-right
        var allNeighbors = pearlCoord.GetAllNeighbors();
        var rotatingCoords = new[] { allNeighbors[2], allNeighbors[4], allNeighbors[0] };
        
        var rotatingTiles = rotatingCoords
            .Select(c => GameState.Grid.GetTile(c))
            .Where(t => t != null)
            .ToList()!;

        if (rotatingTiles.Count != 3) return;

        GameState.IsAnimating = true;

        // Rotate the 3 neighbors around the pearl
        var center = pearlTile.ScreenPosition;
        var rotationAnim = new RotationAnimation(rotatingTiles!, center, GameState.IsClockwise);

        var animationComplete = new TaskCompletionSource<bool>();
        rotationAnim.OnComplete = () => animationComplete.SetResult(true);

        AnimationManager.Start(rotationAnim);
        await animationComplete.Task;

        // Update grid positions
        ApplyThreeTileRotation(rotatingCoords, GameState.IsClockwise);

        // Update tile screen positions
        foreach (var tile in rotatingTiles)
        {
            if (tile != null && _coordToScreen != null)
            {
                tile!.ScreenPosition = _coordToScreen(tile.Coordinate);
                tile.TargetPosition = tile.ScreenPosition;
            }
        }

        // Check for matches - include pearl and rotating coords as affected
        var affectedCoords = new List<HexCoordinate>(rotatingCoords) { pearlCoord };
        
        // Priority 1: Star hexagon
        var starHexagonCenter = _matchDetector.CheckForStarHexagonPattern(GameState.Grid, affectedCoords.ToArray());
        if (starHexagonCenter.HasValue)
        {
            await ProcessStarHexagonMatch(starHexagonCenter.Value);
            GameState.IsAnimating = false;
            OnTurnComplete();
            return;
        }

        // Priority 2: Pearl hexagon
        var pearlHexagonCenter = _matchDetector.CheckForPearlHexagonPattern(GameState.Grid, affectedCoords.ToArray());
        if (pearlHexagonCenter.HasValue)
        {
            await ProcessPearlHexagonMatch(pearlHexagonCenter.Value);
            GameState.IsAnimating = false;
            OnTurnComplete();
            return;
        }

        // Priority 3: Regular hexagon
        var hexagonCenter = _matchDetector.CheckForHexagonPattern(GameState.Grid, affectedCoords.ToArray());
        if (hexagonCenter.HasValue)
        {
            await ProcessHexagonMatch(hexagonCenter.Value);
            GameState.IsAnimating = false;
            OnTurnComplete();
            return;
        }

        // Priority 4: Star cluster
        var starCluster = _matchDetector.FindStarTriangularClusters(GameState.Grid, affectedCoords.ToArray());
        if (starCluster.Count >= 3)
        {
            await ProcessStarClusterMatch(starCluster.ToArray());
            GameState.IsAnimating = false;
            OnTurnComplete();
            return;
        }

        // Priority 5: Pearl cluster
        var pearlCluster = _matchDetector.FindPearlTriangularClusters(GameState.Grid, affectedCoords.ToArray());
        if (pearlCluster.Count >= 3)
        {
            await ProcessPearlClusterMatch(pearlCluster.ToArray());
            GameState.IsAnimating = false;
            OnTurnComplete();
            return;
        }

        // Priority 6: Regular cluster
        var matchedCoords = _matchDetector.FindMatchingClusters(GameState.Grid, affectedCoords.ToArray());
        if (matchedCoords.Count >= 3)
        {
            await ProcessRegularMatch(matchedCoords.ToArray());
            GameState.IsAnimating = false;
            OnTurnComplete();
            return;
        }

        // No matches found - rotation still happens (like star behavior)
        GameState.IsAnimating = false;
        OnTurnComplete();
    }

    private void ApplyThreeTileRotation(HexCoordinate[] coords, bool clockwise)
    {
        if (coords.Length != 3) return;

        var tiles = coords.Select(c => GameState.Grid.GetTile(c)).ToArray();

        // Remove from current positions
        foreach (var coord in coords)
        {
            GameState.Grid.RemoveTile(coord);
        }

        // Place at rotated positions (shift by 1)
        if (clockwise)
        {
            // Clockwise: 0->1, 1->2, 2->0
            GameState.Grid.SetTile(coords[1], tiles[0]);
            GameState.Grid.SetTile(coords[2], tiles[1]);
            GameState.Grid.SetTile(coords[0], tiles[2]);
        }
        else
        {
            // Counter-clockwise: 0->2, 1->0, 2->1
            GameState.Grid.SetTile(coords[2], tiles[0]);
            GameState.Grid.SetTile(coords[0], tiles[1]);
            GameState.Grid.SetTile(coords[1], tiles[2]);
        }
    }

    /// <summary>
    /// Called at the end of each turn to decrement bomb counters and check for game over.
    /// </summary>
    private void OnTurnComplete()
    {
        // Decrement all bomb counters
        DecrementBombCounters();

        // Invoke move completed event
        MoveCompleted?.Invoke();
    }

    /// <summary>
    /// Decrements the counter on all bomb tiles.
    /// Skips bombs that just spawned this turn (they get a full countdown).
    /// If any bomb reaches 0, triggers game over.
    /// </summary>
    private void DecrementBombCounters()
    {
        var allTiles = GameState.Grid.GetAllTiles();
        
        foreach (var tile in allTiles)
        {
            if (tile.IsBomb)
            {
                // Skip bombs that just spawned this turn
                if (tile.BombJustSpawned)
                {
                    tile.BombJustSpawned = false; // Clear flag for next turn
                    continue;
                }
                
                tile.BombCounter--;
                
                if (tile.BombCounter <= 0)
                {
                    // Bomb exploded - game over!
                    GameState.IsGameOver = true;
                    GameOver?.Invoke();
                    return; // Stop processing once game is over
                }
            }
        }
    }
}
