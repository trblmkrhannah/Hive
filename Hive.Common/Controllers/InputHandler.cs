using Avalonia;
using Avalonia.Input;
using Hive.Common.Models;
using Hive.Common.Services;
using Hive.Common.Views;

namespace Hive.Common.Controllers;

/// <summary>
/// Debug action to apply on next click (only when debug mode is enabled).
/// </summary>
public enum DebugAction
{
    None,
    MakeStar,
    MakePearl,
    MakeBomb,
    MakeBonus,
    MakeColor1,
    MakeColor2,
    MakeColor3,
    MakeColor4,
    MakeColor5,
    MakeColor6,
    MakeColor7,
    MakeColor8
}

/// <summary>
/// Handles touch and click input for the game.
/// </summary>
public class InputHandler
{
    private readonly GameController _controller;
    private readonly GameCanvas _canvas;
    
    private bool _isPointerPressed;
    private Point _lastPointerPosition;
    private HexCoordinate[]? _highlightedTriplet;
    private HexCoordinate? _highlightedStar;
    private HexCoordinate? _highlightedPearl;
    
    // Debug mode support
    private readonly bool _isDebugMode;
    private DebugAction _pendingDebugAction = DebugAction.None;

    public InputHandler(GameController controller, GameCanvas canvas)
    {
        _controller = controller;
        _canvas = canvas;
        _isDebugMode = GameStateSerializer.IsDebugModeEnabled();
    }
    
    /// <summary>
    /// Handles key press for debug controls.
    /// </summary>
    public void OnKeyDown(KeyEventArgs e)
    {
        if (!_isDebugMode) return;
        
        _pendingDebugAction = e.Key switch
        {
            Key.S => DebugAction.MakeStar,
            Key.P => DebugAction.MakePearl,
            Key.B => DebugAction.MakeBomb,
            Key.W => DebugAction.MakeBonus,
            Key.D1 or Key.NumPad1 => DebugAction.MakeColor1,
            Key.D2 or Key.NumPad2 => DebugAction.MakeColor2,
            Key.D3 or Key.NumPad3 => DebugAction.MakeColor3,
            Key.D4 or Key.NumPad4 => DebugAction.MakeColor4,
            Key.D5 or Key.NumPad5 => DebugAction.MakeColor5,
            Key.D6 or Key.NumPad6 => DebugAction.MakeColor6,
            Key.D7 or Key.NumPad7 => DebugAction.MakeColor7,
            Key.D8 or Key.NumPad8 => DebugAction.MakeColor8,
            Key.Escape => DebugAction.None,
            _ => _pendingDebugAction // Keep current action for other keys
        };
    }

    /// <summary>
    /// Handles pointer pressed event.
    /// </summary>
    public void OnPointerPressed(PointerPressedEventArgs e)
    {
        if (_controller.GameState.IsAnimating || _controller.AnimationManager.IsAnimating)
        {
            return;
        }

        _isPointerPressed = true;
        _lastPointerPosition = e.GetPosition(_canvas);

        UpdateHighlight(_lastPointerPosition);
    }

    /// <summary>
    /// Handles pointer moved event (hover).
    /// </summary>
    public void OnPointerMoved(PointerEventArgs e)
    {
        _lastPointerPosition = e.GetPosition(_canvas);
        
        if (_controller.GameState.IsAnimating || _controller.AnimationManager.IsAnimating)
        {
            ClearHighlight();
            return;
        }

        UpdateHighlight(_lastPointerPosition);
    }

    /// <summary>
    /// Handles pointer exited event (mouse leaves canvas).
    /// </summary>
    public void OnPointerExited(PointerEventArgs e)
    {
        if (!_isPointerPressed)
        {
            ClearHighlight();
        }
    }

    /// <summary>
    /// Handles pointer released event.
    /// </summary>
    public async void OnPointerReleased(PointerReleasedEventArgs e)
    {
        if (!_isPointerPressed) return;
        _isPointerPressed = false;

        // Check for debug action first
        if (_isDebugMode && _pendingDebugAction != DebugAction.None)
        {
            var clickPos = e.GetPosition(_canvas);
            var hexCoord = _canvas.ScreenToHex(clickPos);
            ApplyDebugAction(hexCoord);
            ClearHighlight();
            return;
        }

        if (_controller.GameState.IsAnimating || _controller.AnimationManager.IsAnimating)
        {
            ClearHighlight();
            return;
        }

        // Use the current highlight (set during press/move) to determine action
        if (_highlightedPearl.HasValue)
        {
            var pearlCoord = _highlightedPearl.Value;
            ClearHighlight();
            await _controller.TryRotateAroundPearl(pearlCoord);
        }
        else if (_highlightedStar.HasValue)
        {
            var starCoord = _highlightedStar.Value;
            ClearHighlight();
            await _controller.TryRotateAroundStar(starCoord);
        }
        else if (_highlightedTriplet != null)
        {
            var triplet = _highlightedTriplet;
            ClearHighlight();
            await _controller.TryRotateTriplet(triplet);
        }
        else
        {
            ClearHighlight();
        }
        
        // Don't refresh highlights - let OnPointerMoved handle it
        // This ensures touch works correctly (no phantom highlights)
        // For mouse, user needs to move slightly to see highlight again
    }
    
    /// <summary>
    /// Applies the pending debug action to the tile at the given coordinate.
    /// </summary>
    private void ApplyDebugAction(HexCoordinate coord)
    {
        var existingTile = _controller.GameState.Grid.GetTile(coord);
        if (existingTile == null) return;
        
        // Color mapping for 1-8 keys
        TileColor[] colors = 
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
        
        HexTile? newTile = _pendingDebugAction switch
        {
            DebugAction.MakeStar => new HexTile(coord, existingTile.Color, isStar: true),
            DebugAction.MakePearl => new HexTile(coord, existingTile.Color, isPearl: true),
            DebugAction.MakeBomb => new HexTile(coord, existingTile.Color, isBomb: true, bombCounter: TileSpawner.InitialBombCounter),
            DebugAction.MakeBonus => new HexTile(coord, existingTile.Color) { IsBonus = true },
            DebugAction.MakeColor1 => new HexTile(coord, colors[0]),
            DebugAction.MakeColor2 => new HexTile(coord, colors[1]),
            DebugAction.MakeColor3 => new HexTile(coord, colors[2]),
            DebugAction.MakeColor4 => new HexTile(coord, colors[3]),
            DebugAction.MakeColor5 => new HexTile(coord, colors[4]),
            DebugAction.MakeColor6 => new HexTile(coord, colors[5]),
            DebugAction.MakeColor7 => new HexTile(coord, colors[6]),
            DebugAction.MakeColor8 => new HexTile(coord, colors[7]),
            _ => null
        };
        
        if (newTile != null)
        {
            // Copy screen position for smooth rendering
            newTile.ScreenPosition = existingTile.ScreenPosition;
            newTile.TargetPosition = existingTile.TargetPosition;
            
            _controller.GameState.Grid.SetTile(coord, newTile);
            _canvas.InvalidateVisual();
        }
        
        // Clear the debug action after applying
        _pendingDebugAction = DebugAction.None;
    }

    private void UpdateHighlight(Point point)
    {
        // First, check if we're clicking on a pearl or star
        var hexCoord = _canvas.ScreenToHex(point);
        
        if (_controller.CanRotatePearl(hexCoord))
        {
            _highlightedPearl = hexCoord;
            _highlightedStar = null;
            _highlightedTriplet = null;
            _canvas.HighlightedPearl = hexCoord;
            _canvas.HighlightedStar = null;
            _canvas.HighlightedTriplet = null;
        }
        else if (_controller.CanRotateStar(hexCoord))
        {
            _highlightedStar = hexCoord;
            _highlightedPearl = null;
            _highlightedTriplet = null;
            _canvas.HighlightedStar = hexCoord;
            _canvas.HighlightedPearl = null;
            _canvas.HighlightedTriplet = null;
        }
        else
        {
            // Check for triplet
            var triplet = _canvas.GetTripletAtScreen(point);
            _highlightedTriplet = triplet;
            _highlightedStar = null;
            _highlightedPearl = null;
            _canvas.HighlightedTriplet = triplet;
            _canvas.HighlightedStar = null;
            _canvas.HighlightedPearl = null;
        }

        _canvas.InvalidateVisual();
    }

    private void ClearHighlight()
    {
        _highlightedTriplet = null;
        _highlightedStar = null;
        _highlightedPearl = null;
        _canvas.HighlightedTriplet = null;
        _canvas.HighlightedStar = null;
        _canvas.HighlightedPearl = null;
        _canvas.InvalidateVisual();
    }
}
