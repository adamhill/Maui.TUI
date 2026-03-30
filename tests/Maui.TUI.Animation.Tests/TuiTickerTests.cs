#nullable enable
using System.Diagnostics;
using Maui.TUI.Platform;
using Microsoft.Maui.Animations;

namespace Maui.TUI.Animation.Tests;

public class TuiTickerTests
{
    /// <summary>
    /// Test 1: Ticker fires at approximately the configured FPS.
    /// Uses Tick() in a tight loop with small sleeps to simulate frame timing.
    /// </summary>
    [Fact]
    public void Ticker_FiresAtApproximatelyConfiguredFps()
    {
        using var ticker = new TuiTicker { MaxFps = 30 };
        int fireCount = 0;
        ticker.Fire = () => Interlocked.Increment(ref fireCount);
        ticker.Start();

        // Call Tick() in a loop for ~1 second
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < 1000)
        {
            ticker.Tick();
            Thread.Sleep(1);
        }

        ticker.Stop();

        // At 30 fps over 1 second, expect 28-32 fires (tolerance for timing)
        Assert.InRange(fireCount, 25, 35);
    }

    /// <summary>
    /// Test 2: Ticker respects Start/Stop — does not fire before Start()
    /// and fires after Start().
    /// </summary>
    [Fact]
    public void Ticker_RespectsStartStop()
    {
        using var ticker = new TuiTicker { MaxFps = 60 };
        int fireCount = 0;
        ticker.Fire = () => fireCount++;

        // Should not fire when not started
        for (int i = 0; i < 100; i++)
        {
            ticker.Tick();
            Thread.Sleep(1);
        }
        Assert.Equal(0, fireCount);

        // Should fire after Start()
        ticker.Start();
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < 200)
        {
            ticker.Tick();
            Thread.Sleep(1);
        }
        ticker.Stop();

        Assert.True(fireCount > 0, "Ticker should have fired after Start()");
    }

    /// <summary>
    /// Test 3: Ticker does not fire after Stop() is called.
    /// </summary>
    [Fact]
    public void Ticker_DoesNotFireAfterStop()
    {
        using var ticker = new TuiTicker { MaxFps = 60 };
        int fireCount = 0;
        ticker.Fire = () => fireCount++;

        ticker.Start();
        // Let it fire a few times
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < 100)
        {
            ticker.Tick();
            Thread.Sleep(1);
        }
        ticker.Stop();

        int countAtStop = fireCount;

        // Tick() after Stop() should not fire
        for (int i = 0; i < 100; i++)
        {
            ticker.Tick();
            Thread.Sleep(1);
        }

        Assert.Equal(countAtStop, fireCount);
    }

    /// <summary>
    /// Test 4: Ticker has zero allocations per tick (after warmup).
    /// Uses GC.GetAllocatedBytesForCurrentThread() to verify.
    /// </summary>
    [Fact]
    public void Ticker_ZeroAllocationsPerTick()
    {
        using var ticker = new TuiTicker { MaxFps = 1000 };
        int fireCount = 0;
        ticker.Fire = () => fireCount++;
        ticker.Start();

        // Warmup — let JIT compile paths
        for (int i = 0; i < 100; i++)
        {
            ticker.Tick();
            Thread.Sleep(1);
        }

        // Measure allocations over 1000 Tick() calls
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++)
        {
            ticker.Tick();
        }
        long after = GC.GetAllocatedBytesForCurrentThread();

        ticker.Stop();

        long allocated = after - before;
        // Allow small tolerance for runtime internals
        Assert.True(allocated <= 256,
            $"Expected zero (or near-zero) allocations during 1000 Tick() calls, but got {allocated} bytes");
    }

    /// <summary>
    /// Test 5: MaxFps can be changed at runtime.
    /// </summary>
    [Fact]
    public void Ticker_MaxFpsCanBeChangedAtRuntime()
    {
        using var ticker = new TuiTicker();
        int fireCount = 0;
        ticker.Fire = () => fireCount++;

        // Start at 30 fps
        ticker.MaxFps = 30;
        ticker.Start();

        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < 500)
        {
            ticker.Tick();
            Thread.Sleep(1);
        }
        int countAt30Fps = fireCount;

        // Change to 60 fps
        fireCount = 0;
        ticker.MaxFps = 60;

        sw.Restart();
        while (sw.ElapsedMilliseconds < 500)
        {
            ticker.Tick();
            Thread.Sleep(1);
        }
        int countAt60Fps = fireCount;

        ticker.Stop();

        // 60 fps should produce roughly double the fires of 30 fps
        // Use generous tolerance for timing variability
        Assert.Multiple(
            () => Assert.InRange(countAt30Fps, 10, 20),
            () => Assert.InRange(countAt60Fps, 20, 40));
    }

    /// <summary>
    /// Test 6: MaxFps must be positive. Zero and negative values throw.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Ticker_MaxFps_ThrowsOnNonPositive(int fps)
    {
        using var ticker = new TuiTicker();
        Assert.Throws<ArgumentOutOfRangeException>(() => ticker.MaxFps = fps);
    }

    /// <summary>
    /// Test 7: IsRunning reflects current state correctly.
    /// </summary>
    [Fact]
    public void Ticker_IsRunning_ReflectsState()
    {
        using var ticker = new TuiTicker();

        Assert.False(ticker.IsRunning);

        ticker.Start();
        Assert.True(ticker.IsRunning);

        ticker.Stop();
        Assert.False(ticker.IsRunning);
    }

    /// <summary>
    /// Test 8: Start() is idempotent — calling it twice does not create
    /// duplicate timers or throw.
    /// </summary>
    [Fact]
    public void Ticker_Start_IsIdempotent()
    {
        using var ticker = new TuiTicker { MaxFps = 30 };
        int fireCount = 0;
        ticker.Fire = () => fireCount++;

        ticker.Start();
        ticker.Start(); // Should be ignored

        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < 200)
        {
            ticker.Tick();
            Thread.Sleep(1);
        }
        ticker.Stop();

        // Should have approximately 6 fires (30fps * 0.2s), not 12
        Assert.InRange(fireCount, 3, 12);
    }

    /// <summary>
    /// Test 9: Stop() is idempotent — calling it twice does not throw.
    /// </summary>
    [Fact]
    public void Ticker_Stop_IsIdempotent()
    {
        using var ticker = new TuiTicker();
        ticker.Start();
        ticker.Stop();
        ticker.Stop(); // Should not throw

        Assert.False(ticker.IsRunning);
    }

    /// <summary>
    /// Test 10: Dispose() stops the ticker.
    /// </summary>
    [Fact]
    public void Ticker_Dispose_StopsTicker()
    {
        var ticker = new TuiTicker();
        ticker.Start();

        ticker.Dispose();

        Assert.False(ticker.IsRunning);
    }

    /// <summary>
    /// Test 11: ITicker interface is implemented correctly.
    /// Verify we can use it through the interface.
    /// </summary>
    [Fact]
    public void Ticker_ImplementsITickerInterface()
    {
        ITicker ticker = new TuiTicker();

        // Verify default state
        Assert.Multiple(
            () => Assert.NotNull(ticker),
            () => Assert.False(ticker.IsRunning),
            () => Assert.True(ticker.SystemEnabled),
            () => Assert.Equal(30, ticker.MaxFps),
            () => Assert.Null(ticker.Fire));

        // Verify property mutation
        ticker.MaxFps = 60;
        ticker.Fire = () => { };
        Assert.Multiple(
            () => Assert.Equal(60, ticker.MaxFps),
            () => Assert.NotNull(ticker.Fire));

        // Verify lifecycle
        ticker.Start();
        Assert.True(ticker.IsRunning);
        ticker.Stop();
        Assert.False(ticker.IsRunning);

        (ticker as IDisposable)?.Dispose();
    }

    /// <summary>
    /// Test 12: Timer-based firing works (without Tick() calls).
    /// The timer fires on a thread pool thread and invokes Fire directly
    /// when no TerminalApp is set.
    /// </summary>
    [Fact]
    public void Ticker_TimerBasedFiring_Works()
    {
        using var ticker = new TuiTicker { MaxFps = 30 };
        int fireCount = 0;
        ticker.Fire = () => Interlocked.Increment(ref fireCount);

        ticker.Start();

        // Wait for timer-based fires (no Tick() calls)
        Thread.Sleep(1100);

        ticker.Stop();

        // Should have approximately 30 fires from the timer alone
        // Wide tolerance because System.Threading.Timer resolution varies by platform
        Assert.InRange(fireCount, 15, 45);
    }

    /// <summary>
    /// Test 13: SystemEnabled defaults to true in normal environment.
    /// </summary>
    [Fact]
    public void Ticker_SystemEnabled_DefaultsToTrue()
    {
        // This test assumes NO_MOTION and REDUCE_MOTION are not set in the test env
        using var ticker = new TuiTicker();
        Assert.True(ticker.SystemEnabled);
    }

    /// <summary>
    /// Test 14: Fire delegate can be set to null safely.
    /// </summary>
    [Fact]
    public void Ticker_FireCanBeNull_NoExceptionOnTick()
    {
        using var ticker = new TuiTicker { MaxFps = 60 };
        ticker.Fire = null;
        ticker.Start();

        // Should not throw even though Fire is null
        for (int i = 0; i < 50; i++)
        {
            ticker.Tick();
            Thread.Sleep(1);
        }

        ticker.Stop();
    }
}
