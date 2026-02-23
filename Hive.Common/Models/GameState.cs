using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Hive.Common.Models;

/// <summary>
/// Represents the current state of the game.
/// </summary>
public class GameState : INotifyPropertyChanged
{
    private int _score;
    private bool _isClockwise = true;
    private bool _isAnimating;
    private bool _isGameOver;

    /// <summary>
    /// The current player score.
    /// </summary>
    public int Score
    {
        get => _score;
        set => SetField(ref _score, value);
    }

    /// <summary>
    /// Whether rotation is clockwise (true) or counter-clockwise (false).
    /// </summary>
    public bool IsClockwise
    {
        get => _isClockwise;
        set => SetField(ref _isClockwise, value);
    }

    /// <summary>
    /// Whether an animation is currently in progress.
    /// </summary>
    public bool IsAnimating
    {
        get => _isAnimating;
        set => SetField(ref _isAnimating, value);
    }

    /// <summary>
    /// Whether the game is over (no valid moves).
    /// </summary>
    public bool IsGameOver
    {
        get => _isGameOver;
        set => SetField(ref _isGameOver, value);
    }

    /// <summary>
    /// The game grid containing all tiles.
    /// </summary>
    public HexGrid Grid { get; private set; }

    /// <summary>
    /// Points awarded per matched tile.
    /// </summary>
    public const int PointsPerTile = 10;

    /// <summary>
    /// Points awarded per eliminated star tile (5x regular).
    /// </summary>
    public const int StarTilePoints = 150;

    /// <summary>
    /// Points awarded per eliminated pearl tile (10x regular).
    /// </summary>
    public const int PearlTilePoints = 250;

    /// <summary>
    /// Bonus points for creating a star tile.
    /// </summary>
    public const int StarBonus = 500;

    /// <summary>
    /// Bonus points for creating a pearl tile.
    /// </summary>
    public const int PearlBonus = 1000;

    /// <summary>
    /// Points awarded per eliminated bonus tile (3x regular).
    /// </summary>
    public const int BonusTilePoints = 30;

    public GameState(int columns = 9, int rows = 9)
    {
        Grid = new HexGrid(columns, rows);
    }

    /// <summary>
    /// Resets the game to initial state.
    /// </summary>
    public void Reset()
    {
        Score = 0;
        IsGameOver = false;
        IsAnimating = false;
        Grid.Clear();
    }

    /// <summary>
    /// Adds points for matched tiles.
    /// </summary>
    public void AddMatchScore(int tileCount, bool createdStar = false, bool createdPearl = false)
    {
        Score += tileCount * PointsPerTile;
        if (createdStar)
        {
            Score += StarBonus;
        }
        if (createdPearl)
        {
            Score += PearlBonus;
        }
    }

    /// <summary>
    /// Adds points for eliminated star tiles.
    /// </summary>
    public void AddStarEliminationScore(int starCount)
    {
        Score += starCount * StarTilePoints;
    }

    /// <summary>
    /// Adds points for eliminated pearl tiles.
    /// </summary>
    public void AddPearlEliminationScore(int pearlCount)
    {
        Score += pearlCount * PearlTilePoints;
    }

    /// <summary>
    /// Adds points for eliminated bonus tiles.
    /// </summary>
    public void AddBonusEliminationScore(int bonusCount)
    {
        Score += bonusCount * BonusTilePoints;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}
