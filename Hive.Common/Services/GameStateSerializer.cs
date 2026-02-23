using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Hive.Common.Models;
using Hive.Common.Storage;

namespace Hive.Common.Services;

/// <summary>
/// Serializes and deserializes game state for debugging and persistence.
/// </summary>
public static class GameStateSerializer
{
    /// <summary>
    /// Browser storage implementation. Set this from browser startup.
    /// </summary>
    public static IStorageProvider? StorageProvider { get; set; }

    /// <summary>
    /// Saves the game state. Uses file system on desktop, localStorage on browser.
    /// </summary>
    public static void SaveToFile(GameState gameState)
    {
        try
        {
            var json = Export(gameState);
            StorageProvider?.Save(json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to save game state: {ex.Message}");
        }
    }

    /// <summary>
    /// Loads the game state. Uses file system on desktop, localStorage on browser.
    /// </summary>
    public static bool LoadFromFile(GameState gameState, out string? error)
    {
        error = null;

        try
        {
            var json = StorageProvider?.Load();

            if (string.IsNullOrEmpty(json))
            {
                error = "No save found";
                return false;
            }

            return TryImport(json!, gameState, out error);
        }
        catch (Exception ex)
        {
            error = $"Failed to load: {ex.Message}";
            return false;
        }
    }

    /// <summary>
    /// Checks if a save exists.
    /// </summary>
    public static bool SaveFileExists()
    {
        return StorageProvider?.HasSave() ?? false;
    }

    /// <summary>
    /// Deletes the save file.
    /// </summary>
    public static void DeleteSaveFile()
    {
        try
        {
            StorageProvider?.Delete();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to delete save file: {ex.Message}");
        }
    }

    /// <summary>
    /// Exports the current game state to a JSON string.
    /// </summary>
    public static string Export(GameState gameState)
    {
        var snapshot = new GameStateSnapshot
        {
            Version = 1,
            Score = gameState.Score,
            IsClockwise = gameState.IsClockwise,
            GridColumns = gameState.Grid.Columns,
            GridRows = gameState.Grid.Rows,
            Tiles = gameState.Grid.GetAllTiles()
                .Select(t => new TileSnapshot
                {
                    Q = t.Coordinate.Q,
                    R = t.Coordinate.R,
                    Color = t.Color,
                    IsStar = t.IsStar,
                    IsPearl = t.IsPearl,
                    IsBomb = t.IsBomb,
                    BombCounter = t.BombCounter,
                    IsBonus = t.IsBonus
                })
                .ToList()
        };

        return JsonSerializer.Serialize(snapshot, GameStateJsonContext.Default.GameStateSnapshot);
    }

    /// <summary>
    /// Imports game state from a JSON string.
    /// Returns true if successful, false otherwise.
    /// </summary>
    public static bool TryImport(string json, GameState gameState, out string? error)
    {
        error = null;

        try
        {
            var snapshot = JsonSerializer.Deserialize(json, GameStateJsonContext.Default.GameStateSnapshot);

            if (snapshot == null)
            {
                error = "Failed to parse JSON";
                return false;
            }

            if (snapshot.Version != 1)
            {
                error = $"Unsupported version: {snapshot.Version}";
                return false;
            }

            // Reset the game state
            gameState.Reset();

            // Restore score and direction
            gameState.Score = snapshot.Score;
            gameState.IsClockwise = snapshot.IsClockwise;

            // Restore tiles
            foreach (var tileSnapshot in snapshot.Tiles)
            {
                var coord = new HexCoordinate(tileSnapshot.Q, tileSnapshot.R);

                if (!gameState.Grid.IsValidCoordinate(coord))
                {
                    continue; // Skip invalid coordinates
                }

                var tile = new HexTile(coord, tileSnapshot.Color, tileSnapshot.IsStar, tileSnapshot.IsPearl,
                    tileSnapshot.IsBomb, tileSnapshot.BombCounter)
                {
                    IsBonus = tileSnapshot.IsBonus
                };
                gameState.Grid.SetTile(coord, tile);
            }

            return true;
        }
        catch (JsonException ex)
        {
            error = $"JSON parse error: {ex.Message}";
            return false;
        }
        catch (Exception ex)
        {
            error = $"Import error: {ex.Message}";
            return false;
        }
    }

    /// <summary>
    /// Checks if debug mode is enabled via environment variable.
    /// Set HIVE_DEBUG=1 to enable debug features.
    /// </summary>
    public static bool IsDebugModeEnabled()
    {
        var debugEnv = Environment.GetEnvironmentVariable("HIVE_DEBUG");
        return debugEnv == "1" || debugEnv?.ToLower() == "true";
    }
}

// Snapshot classes for serialization
public class GameStateSnapshot
{
    public int Version { get; set; }
    public int Score { get; set; }
    public bool IsClockwise { get; set; }
    public int GridColumns { get; set; }
    public int GridRows { get; set; }
    public List<TileSnapshot> Tiles { get; set; } = new();
}

public class TileSnapshot
{
    public int Q { get; set; }
    public int R { get; set; }
    public TileColor Color { get; set; }
    public bool IsStar { get; set; }
    public bool IsPearl { get; set; }
    public bool IsBomb { get; set; }
    public int BombCounter { get; set; }
    public bool IsBonus { get; set; }
}

// Source-generated JSON context for AOT compatibility
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(GameStateSnapshot))]
[JsonSerializable(typeof(TileSnapshot))]
[JsonSerializable(typeof(List<TileSnapshot>))]
[JsonSerializable(typeof(TileColor))]
internal partial class GameStateJsonContext : JsonSerializerContext
{
}