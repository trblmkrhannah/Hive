using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Hive.Common.Controllers;
using Hive.Common.Services;

namespace Hive.Common.Views;

public partial class HiveGameView : UserControl
{
    private readonly GameController _controller;
    private readonly InputHandler _inputHandler;
    private bool _isInitialized;

    public HiveGameView()
    {
        InitializeComponent();

        // Full-app hex grid background (tiled brush fills entire area regardless of size)
        HexGridBackgroundLayer.Background = HexGridBackground.CreateTiledBrush();

        // Initialize game controller
        _controller = new GameController();
        
        // Set up the canvas
        GameCanvas.SetController(_controller);
        
        // Set up input handler
        _inputHandler = new InputHandler(_controller, GameCanvas);

        // Wire up events
        NewGameButton.Click += OnNewGameClick;
        RotationToggle.IsCheckedChanged += OnRotationToggleChanged;
        
        GameCanvas.PointerPressed += OnCanvasPointerPressed;
        GameCanvas.PointerMoved += OnCanvasPointerMoved;
        GameCanvas.PointerReleased += OnCanvasPointerReleased;
        GameCanvas.PointerExited += OnCanvasPointerExited;
        
        // Keyboard input for debug controls
        KeyDown += OnKeyDown;
        
        _controller.ScoreChanged += OnScoreChanged;
        _controller.MoveCompleted += OnMoveCompleted;
        _controller.GameOver += OnGameOver;

        // Wire up game over overlay button
        GameOverNewGameButton.Click += OnGameOverNewGameClick;

        // Handle layout changes
        GameCanvas.SizeChanged += OnCanvasSizeChanged;

        // Set up debug panel if enabled
        SetupDebugPanel();

        // Start with coordinate converter setup after layout
        this.Loaded += OnViewLoaded;
    }

    public void SaveGameState()
    {
        if (_isInitialized && !_controller.AnimationManager.IsAnimating && !_controller.GameState.IsGameOver)
        {
            GameStateSerializer.SaveToFile(_controller.GameState);
        }
    }
    
    private void SetupDebugPanel()
    {
        if (GameStateSerializer.IsDebugModeEnabled())
        {
            DebugPanel.IsVisible = true;
            ExportButton.Click += OnExportClick;
            ImportButton.Click += OnImportClick;
        }
    }

    private async void OnExportClick(object? sender, RoutedEventArgs e)
    {
        if (_controller.AnimationManager.IsAnimating) return;

        try
        {
            var json = GameStateSerializer.Export(_controller.GameState);
            
            // Copy to clipboard
            if (TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard)
            {
                await clipboard.SetTextAsync(json);
                
                // Visual feedback - briefly change button text
                var originalContent = ExportButton.Content;
                ExportButton.Content = "Copied!";
                await Task.Delay(1500);
                ExportButton.Content = originalContent;
            }
        }
        catch (Exception ex)
        {
            ExportButton.Content = $"Error: {ex.Message}";
            await Task.Delay(2000);
            ExportButton.Content = "Export State";
        }
    }

    private async void OnImportClick(object? sender, RoutedEventArgs e)
    {
        if (_controller.AnimationManager.IsAnimating) return;

        try
        {
            // Get from clipboard
            if (TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard)
            {
#pragma warning disable CS0618 // GetTextAsync is obsolete but TryGetTextAsync has API issues
                var json = await clipboard.GetTextAsync();
#pragma warning restore CS0618
                
                if (string.IsNullOrWhiteSpace(json))
                {
                    ImportButton.Content = "Clipboard empty";
                    await Task.Delay(1500);
                    ImportButton.Content = "Import State";
                    return;
                }

                if (GameStateSerializer.TryImport(json, _controller.GameState, out var error))
                {
                    // Update UI
                    ScoreText.Text = _controller.GameState.Score.ToString();
                    RotationToggle.IsChecked = _controller.GameState.IsClockwise;
                    
                    // Update tile positions
                    _controller.UpdateAllTilePositions();
                    GameCanvas.InvalidateVisual();
                    
                    // Visual feedback
                    ImportButton.Content = "Imported!";
                    await Task.Delay(1500);
                    ImportButton.Content = "Import State";
                }
                else
                {
                    ImportButton.Content = error ?? "Import failed";
                    await Task.Delay(2000);
                    ImportButton.Content = "Import State";
                }
            }
        }
        catch (Exception ex)
        {
            ImportButton.Content = $"Error: {ex.Message}";
            await Task.Delay(2000);
            ImportButton.Content = "Import State";
        }
    }

    private void OnViewLoaded(object? sender, RoutedEventArgs e)
    {
        // Set up coordinate conversion after layout is complete
        _controller.SetCoordinateConverter(
            coord => GameCanvas.HexToScreen(coord),
            () => GameCanvas.HexSize,
            () => GameCanvas.Origin);

        // Use dispatcher to ensure layout is complete
        Dispatcher.UIThread.Post(() =>
        {
            // Try to load saved game on startup
            StartNewGame(tryLoadSave: true);
            _isInitialized = true;
            // Take focus so KeyDown (debug bindings) works without clicking the canvas first
            this.Focus();
        }, DispatcherPriority.Loaded);
    }

    private void OnCanvasSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (_isInitialized && !_controller.AnimationManager.IsAnimating)
        {
            // Invalidate first to trigger layout recalculation
            GameCanvas.InvalidateVisual();
            
            // Defer tile position update to after the render cycle completes
            Dispatcher.UIThread.Post(() =>
            {
                if (!_controller.AnimationManager.IsAnimating)
                {
                    _controller.UpdateAllTilePositions();
                    GameCanvas.InvalidateVisual();
                }
            }, DispatcherPriority.Render);
        }
    }

    private void StartNewGame(bool tryLoadSave = false)
    {
        if (tryLoadSave && GameStateSerializer.SaveFileExists())
        {
            if (GameStateSerializer.LoadFromFile(_controller.GameState, out _))
            {
                // Successfully loaded save - update UI
                ScoreText.Text = _controller.GameState.Score.ToString();
                RotationToggle.IsChecked = _controller.GameState.IsClockwise;
                _controller.UpdateAllTilePositions();
                GameCanvas.InvalidateVisual();
                return;
            }
        }
        
        // No save or load failed - start fresh
        _controller.NewGame();
        ScoreText.Text = "0";
        GameCanvas.InvalidateVisual();
    }

    private void OnNewGameClick(object? sender, RoutedEventArgs e)
    {
        if (!_controller.AnimationManager.IsAnimating)
        {
            // Hide game over overlay if visible
            GameOverOverlay.IsVisible = false;
            GameCanvas.IsHitTestVisible = true;
            StartNewGame();
        }
    }

    private void OnRotationToggleChanged(object? sender, RoutedEventArgs e)
    {
        bool isClockwise = RotationToggle.IsChecked ?? true;
        _controller.GameState.IsClockwise = isClockwise;
        
        // Update icons
        ClockwiseIcon.IsVisible = isClockwise;
        CounterClockwiseIcon.IsVisible = !isClockwise;
    }

    private void OnScoreChanged(int newScore)
    {
        Dispatcher.UIThread.Post(() =>
        {
            ScoreText.Text = newScore.ToString();
        });
    }

    private void OnMoveCompleted()
    {
        // Auto-save after move completes (board is stable with all gaps filled)
        Dispatcher.UIThread.Post(() =>
        {
            if (_isInitialized && !_controller.GameState.IsGameOver)
            {
                GameStateSerializer.SaveToFile(_controller.GameState);
            }
        });
    }

    private void OnGameOver()
    {
        Dispatcher.UIThread.Post(() =>
        {
            // Update final score
            FinalScoreText.Text = _controller.GameState.Score.ToString();
            
            // Show game over overlay
            GameOverOverlay.IsVisible = true;
            
            // Disable canvas interaction
            GameCanvas.IsHitTestVisible = false;
            
            // Delete the save file since game is over
            GameStateSerializer.DeleteSaveFile();
        });
    }

    private void OnGameOverNewGameClick(object? sender, RoutedEventArgs e)
    {
        // Hide overlay and start new game
        GameOverOverlay.IsVisible = false;
        GameCanvas.IsHitTestVisible = true;
        StartNewGame();
    }

    private void OnCanvasPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // Take focus so KeyDown (debug bindings) is received
        this.Focus();
        _inputHandler.OnPointerPressed(e);
    }

    private void OnCanvasPointerMoved(object? sender, PointerEventArgs e)
    {
        _inputHandler.OnPointerMoved(e);
    }

    private void OnCanvasPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _inputHandler.OnPointerReleased(e);
    }

    private void OnCanvasPointerExited(object? sender, PointerEventArgs e)
    {
        _inputHandler.OnPointerExited(e);
    }
    
    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        _inputHandler.OnKeyDown(e);
    }
}
