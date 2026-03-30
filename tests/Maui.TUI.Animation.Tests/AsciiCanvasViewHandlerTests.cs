#nullable enable
using Maui.TUI.Animation;
using Maui.TUI.Controls;
using Maui.TUI.Handlers;
using Maui.TUI.Platform;
using Microsoft.Maui;
using Microsoft.Maui.Graphics;

namespace Maui.TUI.Animation.Tests;

/// <summary>
/// Handler-level tests using MauiMocks to provide a mock MauiContext.
/// These tests verify the full handler lifecycle: creation, connection,
/// property mapping, ticker subscription, and the OnTick animation loop.
/// </summary>
public class AsciiCanvasViewHandlerTests : IDisposable
{
	private readonly MockMauiContext _defaultContext = new();

	public AsciiCanvasViewHandlerTests()
	{
		MauiMocks.Init();
	}

	public void Dispose()
	{
		MauiMocks.Reset();
	}

	/// <summary>
	/// Creates a handler wired to the given view and context.
	/// Encapsulates the 3-step boilerplate: new handler → SetMauiContext → SetVirtualView.
	/// </summary>
	private AsciiCanvasViewHandler CreateHandler(
		AsciiCanvasView view, MockMauiContext? context = null)
	{
		var handler = new AsciiCanvasViewHandler();
		handler.SetMauiContext(context ?? _defaultContext);
		handler.SetVirtualView(view);
		return handler;
	}

	/// <summary>
	/// Creates a <see cref="MockMauiContext"/> with a <see cref="TuiTicker"/> registered.
	/// </summary>
	private static MockMauiContext CreateContextWithTicker(TuiTicker ticker) =>
		new((typeof(TuiTicker), ticker));

	// ────────────────────────────────────────────
	// Handler creation
	// ────────────────────────────────────────────

	[Fact]
	public void Handler_CanBeInstantiated()
	{
		var handler = new AsciiCanvasViewHandler();
		Assert.NotNull(handler);
	}

	[Fact]
	public void Handler_CreatesPlatformView()
	{
		var handler = CreateHandler(new AsciiCanvasView());
		Assert.NotNull(handler.PlatformView);
	}

	[Fact]
	public void Handler_PlatformView_IsXenoAtomCanvas()
	{
		var handler = CreateHandler(new AsciiCanvasView());
		Assert.IsType<XenoAtom.Terminal.UI.Controls.Canvas>(handler.PlatformView);
	}

	// ────────────────────────────────────────────
	// Buffer management
	// ────────────────────────────────────────────

	[Fact]
	public void Handler_EnsuresBuffer_OnConnection()
	{
		var handler = CreateHandler(new AsciiCanvasView { CanvasWidth = 40, CanvasHeight = 12 });

		Assert.Multiple(
			() => Assert.NotNull(handler.Buffer),
			() => Assert.Equal(40, handler.Buffer!.Width),
			() => Assert.Equal(12, handler.Buffer!.Height));
	}

	[Fact]
	public void Handler_EnsuresDrawEventArgs_OnConnection()
	{
		var handler = CreateHandler(new AsciiCanvasView());

		Assert.Multiple(
			() => Assert.NotNull(handler.DrawEventArgs),
			() => Assert.Same(handler.Buffer, handler.DrawEventArgs!.Buffer));
	}

	[Fact]
	public void Handler_DrawEventArgs_ReusedInstance()
	{
		var view = new AsciiCanvasView { CanvasWidth = 20, CanvasHeight = 5 };
		var handler = CreateHandler(view);
		var args1 = handler.DrawEventArgs;

		// Trigger a property update which calls EnsureBuffer again
		AsciiCanvasViewHandler.MapCanvasSize(handler, view);

		Assert.Same(args1, handler.DrawEventArgs);
	}

	// ────────────────────────────────────────────
	// MapCanvasSize
	// ────────────────────────────────────────────

	[Fact]
	public void MapCanvasSize_ResizesBuffer_WhenDimensionsChange()
	{
		var view = new AsciiCanvasView { CanvasWidth = 40, CanvasHeight = 12 };
		var handler = CreateHandler(view);

		Assert.Multiple(
			() => Assert.Equal(40, handler.Buffer!.Width),
			() => Assert.Equal(12, handler.Buffer!.Height));

		// Change dimensions and remap
		view.CanvasWidth = 80;
		view.CanvasHeight = 24;
		AsciiCanvasViewHandler.MapCanvasSize(handler, view);

		Assert.Multiple(
			() => Assert.Equal(80, handler.Buffer!.Width),
			() => Assert.Equal(24, handler.Buffer!.Height));
	}

	[Fact]
	public void MapCanvasSize_SetsPlatformViewSizeConstraints()
	{
		var view = new AsciiCanvasView { CanvasWidth = 50, CanvasHeight = 15 };
		var handler = CreateHandler(view);

		AsciiCanvasViewHandler.MapCanvasSize(handler, view);

		Assert.Multiple(
			() => Assert.Equal(50, handler.PlatformView.MinWidth),
			() => Assert.Equal(15, handler.PlatformView.MinHeight),
			() => Assert.Equal(50, handler.PlatformView.MaxWidth),
			() => Assert.Equal(15, handler.PlatformView.MaxHeight));
	}

	// ────────────────────────────────────────────
	// OnTick — the animation loop
	// ────────────────────────────────────────────

	[Fact]
	public void OnTick_WhenAnimating_RaisesDrawFrame()
	{
		var view = new AsciiCanvasView { CanvasWidth = 10, CanvasHeight = 5, IsAnimating = true };
		var handler = CreateHandler(view);

		int drawCount = 0;
		view.DrawFrame += (s, e) => drawCount++;

		handler.OnTick();

		Assert.Equal(1, drawCount);
	}

	[Fact]
	public void OnTick_WhenNotAnimating_DoesNotRaiseDrawFrame()
	{
		var view = new AsciiCanvasView { CanvasWidth = 10, CanvasHeight = 5, IsAnimating = false };
		var handler = CreateHandler(view);

		int drawCount = 0;
		view.DrawFrame += (s, e) => drawCount++;

		handler.OnTick();

		Assert.Equal(0, drawCount);
	}

	[Fact]
	public void OnTick_IncrementsFrameNumber()
	{
		var view = new AsciiCanvasView { CanvasWidth = 10, CanvasHeight = 5, IsAnimating = true };
		var handler = CreateHandler(view);

		var frameNumbers = new List<int>();
		view.DrawFrame += (s, e) => frameNumbers.Add(e.FrameNumber);

		handler.OnTick();
		handler.OnTick();
		handler.OnTick();

		Assert.Equal(new[] { 0, 1, 2 }, frameNumbers);
	}

	[Fact]
	public void OnTick_ElapsedTimeIncreases()
	{
		var view = new AsciiCanvasView { CanvasWidth = 10, CanvasHeight = 5, IsAnimating = true };
		var handler = CreateHandler(view);

		var elapsedTimes = new List<TimeSpan>();
		view.DrawFrame += (s, e) => elapsedTimes.Add(e.ElapsedTime);

		handler.OnTick();
		Thread.Sleep(10); // Small delay to ensure nonzero elapsed
		handler.OnTick();

		Assert.Multiple(
			() => Assert.Equal(2, elapsedTimes.Count),
			() => Assert.True(elapsedTimes[1] > elapsedTimes[0],
				$"Second elapsed ({elapsedTimes[1]}) should be greater than first ({elapsedTimes[0]})"));
	}

	[Fact]
	public void OnTick_DeltaTimeIsPositive()
	{
		var view = new AsciiCanvasView { CanvasWidth = 10, CanvasHeight = 5, IsAnimating = true };
		var handler = CreateHandler(view);

		var deltas = new List<TimeSpan>();
		view.DrawFrame += (s, e) => deltas.Add(e.DeltaTime);

		handler.OnTick();
		Thread.Sleep(5);
		handler.OnTick();

		// First delta may be zero (no prior frame), second should be positive
		Assert.Multiple(
			() => Assert.Equal(2, deltas.Count),
			() => Assert.True(deltas[1] > TimeSpan.Zero,
				$"Delta time should be positive, was {deltas[1]}"));
	}

	[Fact]
	public void OnTick_ClearBeforeDraw_True_ClearsBuffer()
	{
		var view = new AsciiCanvasView
		{
			CanvasWidth = 10,
			CanvasHeight = 5,
			IsAnimating = true,
			ClearBeforeDraw = true,
		};
		var handler = CreateHandler(view);

		bool wasClearedBeforeDraw = false;
		view.DrawFrame += (s, e) =>
		{
			// On the second tick, check if the cell written in the first tick was cleared
			if (e.FrameNumber == 1)
				wasClearedBeforeDraw = e.Buffer[0, 0].IsEmpty;
			else
				e.Buffer.SetCell(0, 0, new System.Text.Rune('X'), Colors.Red, Colors.Black);
		};

		handler.OnTick(); // Frame 0: writes 'X'
		handler.OnTick(); // Frame 1: checks if cleared

		Assert.True(wasClearedBeforeDraw, "Buffer should be cleared before DrawFrame when ClearBeforeDraw is true");
	}

	[Fact]
	public void OnTick_ClearBeforeDraw_False_RetainsContent()
	{
		var view = new AsciiCanvasView
		{
			CanvasWidth = 10,
			CanvasHeight = 5,
			IsAnimating = true,
			ClearBeforeDraw = false,
		};
		var handler = CreateHandler(view);

		bool contentRetained = false;
		view.DrawFrame += (s, e) =>
		{
			if (e.FrameNumber == 1)
				contentRetained = e.Buffer[0, 0].Character == new System.Text.Rune('X');
			else
				e.Buffer.SetCell(0, 0, new System.Text.Rune('X'), Colors.Red, Colors.Black);
		};

		handler.OnTick();
		handler.OnTick();

		Assert.True(contentRetained, "Buffer should retain content when ClearBeforeDraw is false");
	}

	// ────────────────────────────────────────────
	// OnTick — event args are the same instance
	// ────────────────────────────────────────────

	[Fact]
	public void OnTick_EventArgs_SameInstanceAcrossFrames()
	{
		var view = new AsciiCanvasView { CanvasWidth = 10, CanvasHeight = 5, IsAnimating = true };
		var handler = CreateHandler(view);

		var argsInstances = new List<AsciiCanvasDrawEventArgs>();
		view.DrawFrame += (s, e) => argsInstances.Add(e);

		handler.OnTick();
		handler.OnTick();
		handler.OnTick();

		Assert.Multiple(
			() => Assert.Equal(3, argsInstances.Count),
			() => Assert.Same(argsInstances[0], argsInstances[1]),
			() => Assert.Same(argsInstances[1], argsInstances[2]));
	}

	[Fact]
	public void OnTick_Buffer_SameInstanceAcrossFrames()
	{
		var view = new AsciiCanvasView { CanvasWidth = 10, CanvasHeight = 5, IsAnimating = true };
		var handler = CreateHandler(view);

		var bufferInstances = new List<CellBuffer>();
		view.DrawFrame += (s, e) => bufferInstances.Add(e.Buffer);

		handler.OnTick();
		handler.OnTick();

		Assert.Same(bufferInstances[0], bufferInstances[1]);
	}

	// ────────────────────────────────────────────
	// OnTick — buffer has correct dimensions
	// ────────────────────────────────────────────

	[Theory]
	[InlineData(10, 5)]
	[InlineData(80, 24)]
	[InlineData(40, 12)]
	public void OnTick_BufferDimensions_MatchCanvasSize(int width, int height)
	{
		var view = new AsciiCanvasView
		{
			CanvasWidth = width,
			CanvasHeight = height,
			IsAnimating = true,
		};
		var handler = CreateHandler(view);

		CellBuffer? receivedBuffer = null;
		view.DrawFrame += (s, e) => receivedBuffer = e.Buffer;

		handler.OnTick();

		Assert.Multiple(
			() => Assert.NotNull(receivedBuffer),
			() => Assert.Equal(width, receivedBuffer!.Width),
			() => Assert.Equal(height, receivedBuffer!.Height));
	}

	// ────────────────────────────────────────────
	// OnTick — user code can write cells
	// ────────────────────────────────────────────

	[Fact]
	public void OnTick_UserCanWriteCells_DuringDrawFrame()
	{
		var view = new AsciiCanvasView { CanvasWidth = 20, CanvasHeight = 10, IsAnimating = true };
		var handler = CreateHandler(view);

		view.DrawFrame += (s, e) =>
		{
			e.Buffer.DrawString(0, 0, "Hello", Colors.Cyan, Colors.Black);
			e.Buffer.SetCell(10, 5, new System.Text.Rune('*'), Colors.Yellow, Colors.DarkBlue);
		};

		handler.OnTick();

		Assert.Multiple(
			() => Assert.Equal(new System.Text.Rune('H'), handler.Buffer![0, 0].Character),
			() => Assert.Equal(new System.Text.Rune('*'), handler.Buffer![10, 5].Character));
	}

	// ────────────────────────────────────────────
	// OnTick — zero allocations in steady state
	// ────────────────────────────────────────────

	[Fact]
	public void OnTick_ZeroAllocations_InSteadyState()
	{
		var view = new AsciiCanvasView { CanvasWidth = 40, CanvasHeight = 12, IsAnimating = true };
		var handler = CreateHandler(view);

		// Simple draw callback that writes a few cells
		view.DrawFrame += (s, e) =>
		{
			e.Buffer.SetCell(0, 0, new System.Text.Rune('A'), Colors.White, Colors.Black);
			e.Buffer.SetCell(1, 0, new System.Text.Rune('B'), Colors.White, Colors.Black);
		};

		// Warmup — let JIT compile paths, allocate internal structures
		for (int i = 0; i < 50; i++)
			handler.OnTick();

		long before = GC.GetAllocatedBytesForCurrentThread();
		for (int i = 0; i < 500; i++)
			handler.OnTick();
		long after = GC.GetAllocatedBytesForCurrentThread();

		Assert.True(after - before <= 512,
			$"Expected near-zero allocations during OnTick loop, got {after - before} bytes");
	}

	// ────────────────────────────────────────────
	// Multiple OnTick then stop
	// ────────────────────────────────────────────

	[Fact]
	public void OnTick_StopAnimation_MidStream_StopsFiring()
	{
		var view = new AsciiCanvasView { CanvasWidth = 10, CanvasHeight = 5, IsAnimating = true };
		var handler = CreateHandler(view);

		int drawCount = 0;
		view.DrawFrame += (s, e) =>
		{
			drawCount++;
			// Stop after 3 frames
			if (e.FrameNumber == 2)
				view.IsAnimating = false;
		};

		for (int i = 0; i < 10; i++)
			handler.OnTick();

		Assert.Equal(3, drawCount);
	}

	// ────────────────────────────────────────────
	// Ticker integration — MockMauiContext with TuiTicker
	// ────────────────────────────────────────────

	[Fact]
	public void ConnectHandler_ResolvesTuiTicker_FromMockContext()
	{
		using var ticker = new TuiTicker();
		var view = new AsciiCanvasView();
		var handler = CreateHandler(view, CreateContextWithTicker(ticker));

		// The handler should have resolved the ticker during ConnectHandler.
		// We can verify indirectly: MapIsAnimating(true) should start the ticker.
		view.IsAnimating = true;
		AsciiCanvasViewHandler.MapIsAnimating(handler, view);

		Assert.True(ticker.IsRunning);

		// Cleanup
		view.IsAnimating = false;
		AsciiCanvasViewHandler.MapIsAnimating(handler, view);
	}

	[Fact]
	public void MapIsAnimating_True_SubscribesToTicker()
	{
		using var ticker = new TuiTicker();
		var view = new AsciiCanvasView { IsAnimating = true };
		var handler = CreateHandler(view, CreateContextWithTicker(ticker));

		// MapIsAnimating is called during SetVirtualView due to PropertyMapper,
		// so the ticker should already be running.
		Assert.True(ticker.IsRunning);

		// Verify the handler's OnTick is wired to the ticker's Fire delegate
		Assert.NotNull(ticker.Fire);
	}

	[Fact]
	public void MapIsAnimating_False_UnsubscribesFromTicker()
	{
		using var ticker = new TuiTicker();
		var view = new AsciiCanvasView { IsAnimating = true };
		var handler = CreateHandler(view, CreateContextWithTicker(ticker));

		Assert.True(ticker.IsRunning);
		Assert.NotNull(ticker.Fire);

		// Track draws
		int drawCount = 0;
		view.DrawFrame += (s, e) => drawCount++;

		// Verify it fires before unsubscribe
		ticker.Fire?.Invoke();
		Assert.Equal(1, drawCount);

		// Stop animation — triggers UnsubscribeFromTicker
		view.IsAnimating = false;
		AsciiCanvasViewHandler.MapIsAnimating(handler, view);

		// After unsubscribe, fire should not reach our handler
		ticker.Fire?.Invoke();
		Assert.Equal(1, drawCount); // count didn't increase
	}

	[Fact]
	public void TickerFire_InvokesHandler_OnTick()
	{
		using var ticker = new TuiTicker();
		var view = new AsciiCanvasView
		{
			CanvasWidth = 10,
			CanvasHeight = 5,
			IsAnimating = true,
		};
		var handler = CreateHandler(view, CreateContextWithTicker(ticker));

		int drawCount = 0;
		view.DrawFrame += (s, e) => drawCount++;

		// Directly invoke the ticker's Fire delegate (simulates what the timer does)
		ticker.Fire?.Invoke();
		ticker.Fire?.Invoke();
		ticker.Fire?.Invoke();

		Assert.Equal(3, drawCount);
	}

	[Fact]
	public void TickerFire_ProducesValidBufferContent()
	{
		using var ticker = new TuiTicker();
		var view = new AsciiCanvasView
		{
			CanvasWidth = 20,
			CanvasHeight = 5,
			IsAnimating = true,
		};
		var handler = CreateHandler(view, CreateContextWithTicker(ticker));

		view.DrawFrame += (s, e) =>
		{
			e.Buffer.DrawString(0, 0, "Tick!", Colors.Cyan, Colors.Black);
		};

		// Fire through the ticker (not handler.OnTick directly)
		ticker.Fire?.Invoke();

		Assert.Multiple(
			() => Assert.NotNull(handler.Buffer),
			() => Assert.Equal(new System.Text.Rune('T'), handler.Buffer![0, 0].Character),
			() => Assert.Equal(new System.Text.Rune('!'), handler.Buffer![4, 0].Character));
	}

	// ────────────────────────────────────────────
	// Unsubscribe cleanup
	// ────────────────────────────────────────────

	[Fact]
	public void Unsubscribe_RemovesCallback_FromTickerFire()
	{
		using var ticker = new TuiTicker();
		var view = new AsciiCanvasView { IsAnimating = true };
		var handler = CreateHandler(view, CreateContextWithTicker(ticker));

		Assert.NotNull(ticker.Fire);

		int drawCount = 0;
		view.DrawFrame += (s, e) => drawCount++;

		// Verify fires before unsubscribe
		ticker.Fire?.Invoke();
		Assert.Equal(1, drawCount);

		// Unsubscribe via MapIsAnimating(false)
		view.IsAnimating = false;
		AsciiCanvasViewHandler.MapIsAnimating(handler, view);

		// After unsubscribe, invoking Fire should not trigger DrawFrame
		ticker.Fire?.Invoke();
		Assert.Equal(1, drawCount); // still 1, not 2
	}

	[Fact]
	public void Resubscribe_AfterUnsubscribe_Works()
	{
		using var ticker = new TuiTicker();
		var view = new AsciiCanvasView { IsAnimating = true };
		var handler = CreateHandler(view, CreateContextWithTicker(ticker));

		// Unsubscribe
		view.IsAnimating = false;
		AsciiCanvasViewHandler.MapIsAnimating(handler, view);

		// Re-subscribe
		view.IsAnimating = true;
		AsciiCanvasViewHandler.MapIsAnimating(handler, view);

		int drawCount = 0;
		view.DrawFrame += (s, e) => drawCount++;
		ticker.Fire?.Invoke();

		Assert.Equal(1, drawCount);
	}

	// ────────────────────────────────────────────
	// Multiple handlers sharing one ticker
	// ────────────────────────────────────────────

	[Fact]
	public void MultipleHandlers_ShareTicker_BothReceiveTicks()
	{
		using var ticker = new TuiTicker();
		var context = CreateContextWithTicker(ticker);

		var view1 = new AsciiCanvasView { CanvasWidth = 10, CanvasHeight = 5, IsAnimating = true };
		var handler1 = CreateHandler(view1, context);

		var view2 = new AsciiCanvasView { CanvasWidth = 10, CanvasHeight = 5, IsAnimating = true };
		var handler2 = CreateHandler(view2, context);

		int drawCount1 = 0, drawCount2 = 0;
		view1.DrawFrame += (s, e) => drawCount1++;
		view2.DrawFrame += (s, e) => drawCount2++;

		// Both handlers chained onto the same ticker's Fire delegate
		ticker.Fire?.Invoke();
		ticker.Fire?.Invoke();

		Assert.Multiple(
			() => Assert.Equal(2, drawCount1),
			() => Assert.Equal(2, drawCount2));
	}

	[Fact]
	public void MultipleHandlers_OneUnsubscribes_OtherContinues()
	{
		using var ticker = new TuiTicker();
		var context = CreateContextWithTicker(ticker);

		var view1 = new AsciiCanvasView { CanvasWidth = 10, CanvasHeight = 5, IsAnimating = true };
		var handler1 = CreateHandler(view1, context);

		var view2 = new AsciiCanvasView { CanvasWidth = 10, CanvasHeight = 5, IsAnimating = true };
		var handler2 = CreateHandler(view2, context);

		int drawCount1 = 0, drawCount2 = 0;
		view1.DrawFrame += (s, e) => drawCount1++;
		view2.DrawFrame += (s, e) => drawCount2++;

		// Fire once — both receive
		ticker.Fire?.Invoke();
		Assert.Multiple(
			() => Assert.Equal(1, drawCount1),
			() => Assert.Equal(1, drawCount2));

		// Unsubscribe handler1
		view1.IsAnimating = false;
		AsciiCanvasViewHandler.MapIsAnimating(handler1, view1);

		// Fire again — only handler2 receives
		ticker.Fire?.Invoke();
		Assert.Multiple(
			() => Assert.Equal(1, drawCount1),
			() => Assert.Equal(2, drawCount2));
	}
}
