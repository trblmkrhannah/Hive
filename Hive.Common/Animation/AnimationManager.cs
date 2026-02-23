using Avalonia.Threading;

namespace Hive.Common.Animation;

/// <summary>
/// Manages and coordinates tile animations.
/// </summary>
public class AnimationManager
{
    private readonly List<TileAnimation> _activeAnimations = [];
    private DispatcherTimer? _timer;
    private DateTime _lastTick;
    
    /// <summary>
    /// Callback to request a redraw of the game canvas.
    /// </summary>
    public Action? OnRedrawRequested { get; set; }

    /// <summary>
    /// Whether any animations are currently active.
    /// </summary>
    public bool IsAnimating => _activeAnimations.Count > 0;

    /// <summary>
    /// Starts a new animation.
    /// </summary>
    public void Start(TileAnimation animation)
    {
        _activeAnimations.Add(animation);
        EnsureTimerRunning();
    }

    /// <summary>
    /// Starts multiple animations simultaneously.
    /// </summary>
    public void StartAll(IEnumerable<TileAnimation> animations)
    {
        _activeAnimations.AddRange(animations);
        EnsureTimerRunning();
    }

    /// <summary>
    /// Queues an animation to start after current animations complete.
    /// </summary>
    public void QueueAfterCurrent(TileAnimation animation)
    {
        if (!IsAnimating)
        {
            Start(animation);
        }
        else
        {
            // Find the last animation and chain this one
            var lastAnim = _activeAnimations.LastOrDefault();
            if (lastAnim != null)
            {
                var originalCallback = lastAnim.OnComplete;
                lastAnim.OnComplete = () =>
                {
                    originalCallback?.Invoke();
                    Start(animation);
                };
            }
            else
            {
                Start(animation);
            }
        }
    }

    /// <summary>
    /// Cancels all active animations.
    /// </summary>
    public void CancelAll()
    {
        foreach (var anim in _activeAnimations)
        {
            foreach (var tile in anim.Tiles)
            {
                tile.IsAnimating = false;
            }
        }
        _activeAnimations.Clear();
        StopTimer();
    }

    /// <summary>
    /// Waits for all current animations to complete.
    /// </summary>
    public async Task WaitForAnimationsAsync()
    {
        while (IsAnimating)
        {
            await Task.Delay(16);
        }
    }

    private void EnsureTimerRunning()
    {
        if (_timer == null)
        {
            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(16) // ~60 FPS
            };
            _timer.Tick += OnTick;
            _lastTick = DateTime.Now;
            _timer.Start();
        }
    }

    private void StopTimer()
    {
        _timer?.Stop();
        _timer = null;
    }

    private void OnTick(object? sender, EventArgs e)
    {
        var now = DateTime.Now;
        var deltaMs = (now - _lastTick).TotalMilliseconds;
        _lastTick = now;

        // Update all active animations
        var completedAnimations = new List<TileAnimation>();

        foreach (var anim in _activeAnimations)
        {
            anim.Update(deltaMs);
            
            if (anim.IsComplete)
            {
                completedAnimations.Add(anim);
            }
        }

        // Remove completed animations and invoke their callbacks
        foreach (var completed in completedAnimations)
        {
            _activeAnimations.Remove(completed);
            completed.OnComplete?.Invoke();
        }

        // Stop timer if no more animations
        if (_activeAnimations.Count == 0)
        {
            StopTimer();
        }

        // Request redraw
        OnRedrawRequested?.Invoke();
    }
}
