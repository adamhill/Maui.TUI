#nullable enable
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.Maui.Animations;
using Serilog;
using XenoAtom.Terminal.UI;

namespace Maui.TUI.Platform;

/// <summary>
/// Animation tick source for the TUI backend. Implements <see cref="ITicker"/>
/// to drive MAUI's animation system (Animation.Commit, FadeTo, etc.)
/// from a timer that posts to the XenoAtom terminal UI thread.
/// </summary>
/// <remarks>
/// <para>
/// Frame pacing uses <see cref="Stopwatch"/> (monotonic clock) to ensure
/// consistent timing regardless of system clock adjustments.
/// </para>
/// <para>
/// The ticker fires at approximately <see cref="MaxFps"/> by tracking elapsed
/// time since the last fire and skipping ticks when the interval has not elapsed.
/// This avoids coupling to any specific timer resolution.
/// </para>
/// <para>
/// Timer callbacks are marshaled to the XenoAtom UI thread via
/// <see cref="TerminalApp.Post(Action)"/> to ensure MAUI property changes
/// and XenoAtom visual updates happen on the correct thread.
/// </para>
/// </remarks>
public sealed class TuiTicker : ITicker, IDisposable
{
    private static readonly ILogger Logger = Log.ForContext<TuiTicker>();

    private readonly Stopwatch _stopwatch = new();
    private long _lastFireTimestampTicks;
    private Timer? _timer;
    private bool _isRunning;
    private int _maxFps = 30;
    private long _minIntervalTicks;

    // Cached delegates to avoid allocation on each Start() and timer callback
    private readonly TimerCallback _timerCallback;
    private readonly Action _postCallback;

    /// <summary>
    /// Initializes a new instance of <see cref="TuiTicker"/> with default settings.
    /// MaxFps defaults to 30, which is appropriate for most terminal emulators.
    /// </summary>
    public TuiTicker()
    {
        _timerCallback = OnTimerElapsed;
        _postCallback = OnPostedToUIThread;
        _minIntervalTicks = CalculateMinIntervalTicks(_maxFps);

        Logger.Debug("TuiTicker created with MaxFps={MaxFps}, SystemEnabled={SystemEnabled}",
            _maxFps, SystemEnabled);
    }

    /// <summary>
    /// Gets or sets the <see cref="TerminalApp"/> used to marshal tick callbacks
    /// to the UI thread. Set this before calling <see cref="Start"/>.
    /// When <see langword="null"/>, <see cref="Fire"/> is invoked directly
    /// on the timer thread (useful for testing).
    /// </summary>
    internal TerminalApp? TerminalApp { get; set; }

    /// <inheritdoc/>
    public bool IsRunning => _isRunning;

    /// <summary>
    /// Gets a value indicating whether animations are enabled at the system level.
    /// Returns <see langword="false"/> when the <c>NO_MOTION</c> or
    /// <c>REDUCE_MOTION</c> environment variables are set, indicating the user
    /// prefers reduced motion for accessibility.
    /// </summary>
    public bool SystemEnabled { get; private set; } = CheckSystemEnabled();

    /// <summary>
    /// Gets or sets the maximum frames per second. Defaults to 30.
    /// Terminal emulators are typically the rendering bottleneck, so 30 fps
    /// provides smooth animation without overwhelming the terminal's ANSI parser.
    /// </summary>
    /// <remarks>
    /// Changing this while the ticker is running takes effect on the next tick
    /// cycle without restarting the timer.
    /// </remarks>
    public int MaxFps
    {
        get => _maxFps;
        set
        {
            if (value <= 0)
                throw new ArgumentOutOfRangeException(nameof(value), value, "MaxFps must be positive.");

            _maxFps = value;
            _minIntervalTicks = CalculateMinIntervalTicks(value);

            // If running, update the timer interval
            if (_isRunning && _timer is not null)
            {
                var interval = TimeSpan.FromMilliseconds(1000.0 / value);
                _timer.Change(interval, interval);
            }
        }
    }

    /// <inheritdoc/>
    public Action? Fire { get; set; }

    /// <inheritdoc/>
    public void Start()
    {
        if (_isRunning)
            return;

        _isRunning = true;
        _stopwatch.Start();
        _lastFireTimestampTicks = _stopwatch.ElapsedTicks;

        var interval = TimeSpan.FromMilliseconds(1000.0 / _maxFps);
        _timer = new Timer(_timerCallback, null, interval, interval);

        Logger.Information("TuiTicker started at {MaxFps} fps (interval={IntervalMs:F1}ms)",
            _maxFps, interval.TotalMilliseconds);
    }

    /// <inheritdoc/>
    public void Stop()
    {
        if (!_isRunning)
            return;

        _isRunning = false;
        _stopwatch.Stop();

        _timer?.Dispose();
        _timer = null;

        Logger.Information("TuiTicker stopped");
    }

    /// <summary>
    /// Advances the ticker by one step. Call this from a frame loop callback
    /// (e.g. XenoAtom's onUpdate) as an alternative to the timer-based approach.
    /// Only fires the <see cref="Fire"/> delegate if enough time has elapsed
    /// since the last fire to maintain the <see cref="MaxFps"/> rate.
    /// </summary>
    /// <remarks>
    /// This method must be called on the UI thread. It invokes <see cref="Fire"/>
    /// synchronously without posting. Zero allocations per call.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Tick()
    {
        if (!_isRunning)
            return;

        var now = _stopwatch.ElapsedTicks;
        var elapsed = now - _lastFireTimestampTicks;

        if (elapsed >= _minIntervalTicks)
        {
            _lastFireTimestampTicks = now;
            Fire?.Invoke();
        }
    }

    /// <summary>
    /// Releases the timer resources.
    /// </summary>
    public void Dispose()
    {
        Stop();
    }

    /// <summary>
    /// Timer callback — fires on a thread pool thread.
    /// Performs rate-limiting check, then posts the Fire invocation
    /// to the UI thread via TerminalApp.Post().
    /// </summary>
    private void OnTimerElapsed(object? state)
    {
        if (!_isRunning)
            return;

        var now = _stopwatch.ElapsedTicks;
        var elapsed = now - _lastFireTimestampTicks;

        if (elapsed >= _minIntervalTicks)
        {
            _lastFireTimestampTicks = now;

            if (TerminalApp is { } app)
            {
                // Marshal to the UI thread using the cached delegate
                app.Post(_postCallback);
            }
            else
            {
                // No TerminalApp yet (testing or pre-Run phase) — fire directly
                Fire?.Invoke();
            }
        }
    }

    /// <summary>
    /// Executes on the UI thread after being posted by <see cref="OnTimerElapsed"/>.
    /// This is a cached delegate instance — no allocation per post.
    /// </summary>
    private void OnPostedToUIThread()
    {
        if (_isRunning)
            Fire?.Invoke();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static long CalculateMinIntervalTicks(int fps)
    {
        // Stopwatch.Frequency gives ticks per second (high-resolution)
        return Stopwatch.Frequency / fps;
    }

    /// <summary>
    /// Checks environment variables for accessibility hints that
    /// indicate the user prefers reduced or no motion.
    /// </summary>
    private static bool CheckSystemEnabled()
    {
        // NO_MOTION is a de-facto standard for disabling animations
        if (Environment.GetEnvironmentVariable("NO_MOTION") is not null)
        {
            Logger.Warning("Animations disabled: NO_MOTION environment variable is set");
            return false;
        }

        // REDUCE_MOTION is another common hint
        if (Environment.GetEnvironmentVariable("REDUCE_MOTION") is not null)
        {
            Logger.Warning("Animations disabled: REDUCE_MOTION environment variable is set");
            return false;
        }

        return true;
    }
}
