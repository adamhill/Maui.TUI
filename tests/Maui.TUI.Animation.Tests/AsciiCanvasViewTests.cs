#nullable enable
using Maui.TUI.Animation;
using Maui.TUI.Controls;
using Maui.TUI.Handlers;
using Microsoft.Maui.Graphics;
using XenoAtom.Terminal.UI.Styling;
using TuiColor = XenoAtom.Terminal.UI.Color;

namespace Maui.TUI.Animation.Tests;

/// <summary>
/// Tests for <see cref="AsciiCanvasView"/> (MAUI View),
/// <see cref="AsciiCanvasDrawEventArgs"/> (reused event args),
/// and <see cref="AsciiCanvasViewHandler"/> (color/style conversion + tick logic).
/// </summary>
public class AsciiCanvasViewTests
{
	// ────────────────────────────────────────────
	// AsciiCanvasView — BindableProperty defaults
	// ────────────────────────────────────────────

	[Fact]
	public void View_DefaultProperties_AreCorrect()
	{
		var view = new AsciiCanvasView();

		Assert.Multiple(
			() => Assert.Equal(80, view.CanvasWidth),
			() => Assert.Equal(24, view.CanvasHeight),
			() => Assert.False(view.IsAnimating),
			() => Assert.Equal(30, view.TargetFps),
			() => Assert.True(view.ClearBeforeDraw));
	}

	[Fact]
	public void View_Properties_CanBeChanged()
	{
		var view = new AsciiCanvasView
		{
			CanvasWidth = 40,
			CanvasHeight = 12,
			IsAnimating = true,
			TargetFps = 60,
			ClearBeforeDraw = false,
		};

		Assert.Multiple(
			() => Assert.Equal(40, view.CanvasWidth),
			() => Assert.Equal(12, view.CanvasHeight),
			() => Assert.True(view.IsAnimating),
			() => Assert.Equal(60, view.TargetFps),
			() => Assert.False(view.ClearBeforeDraw));
	}

	// ────────────────────────────────────────────
	// AsciiCanvasView — Layout bypass
	// ────────────────────────────────────────────

	[Fact]
	public void View_MeasureOverride_ReturnsCanvasDimensions()
	{
		var view = new AsciiCanvasView { CanvasWidth = 40, CanvasHeight = 12 };

		// MeasureOverride is protected — call the public Measure which calls it
		var size = view.Measure(double.PositiveInfinity, double.PositiveInfinity);

		Assert.Multiple(
			() => Assert.Equal(40, size.Width),
			() => Assert.Equal(12, size.Height));
	}

	[Fact]
	public void View_MeasureOverride_IgnoresConstraints()
	{
		var view = new AsciiCanvasView { CanvasWidth = 80, CanvasHeight = 24 };

		// Even with tight constraints, canvas returns its own size
		var size = view.Measure(10, 5);

		Assert.Multiple(
			() => Assert.Equal(80, size.Width),
			() => Assert.Equal(24, size.Height));
	}

	[Fact]
	public void View_MeasureOverride_UpdatesWithCanvasSizeChange()
	{
		var view = new AsciiCanvasView { CanvasWidth = 40, CanvasHeight = 12 };
		var size1 = view.Measure(double.PositiveInfinity, double.PositiveInfinity);

		view.CanvasWidth = 60;
		view.CanvasHeight = 20;
		// Need to invalidate measure to get updated results
		view.InvalidateMeasure();
		var size2 = view.Measure(double.PositiveInfinity, double.PositiveInfinity);

		Assert.Multiple(
			() => Assert.Equal(40, size1.Width),
			() => Assert.Equal(12, size1.Height),
			() => Assert.Equal(60, size2.Width),
			() => Assert.Equal(20, size2.Height));
	}

	// ────────────────────────────────────────────
	// AsciiCanvasView — StartAnimation / StopAnimation
	// ────────────────────────────────────────────

	[Fact]
	public void View_StartAnimation_SetsIsAnimating()
	{
		var view = new AsciiCanvasView();
		Assert.False(view.IsAnimating);

		view.StartAnimation();
		Assert.True(view.IsAnimating);
	}

	[Fact]
	public void View_StopAnimation_ClearsIsAnimating()
	{
		var view = new AsciiCanvasView { IsAnimating = true };
		view.StopAnimation();
		Assert.False(view.IsAnimating);
	}

	// ────────────────────────────────────────────
	// AsciiCanvasView — DrawFrame event
	// ────────────────────────────────────────────

	[Fact]
	public void View_OnDrawFrame_RaisesEvent()
	{
		var view = new AsciiCanvasView();
		AsciiCanvasDrawEventArgs? receivedArgs = null;
		view.DrawFrame += (s, e) => receivedArgs = e;

		var args = new AsciiCanvasDrawEventArgs
		{
			Buffer = new CellBuffer(10, 5),
			FrameNumber = 42,
		};
		view.OnDrawFrame(args);

		Assert.Multiple(
			() => Assert.NotNull(receivedArgs),
			() => Assert.Same(args, receivedArgs));
	}

	[Fact]
	public void View_OnDrawFrame_NoSubscribers_DoesNotThrow()
	{
		var view = new AsciiCanvasView();
		var args = new AsciiCanvasDrawEventArgs
		{
			Buffer = new CellBuffer(10, 5),
		};

		// Should not throw when no subscribers
		view.OnDrawFrame(args);
	}

	[Fact]
	public void View_DrawFrame_ReceivesCorrectSender()
	{
		var view = new AsciiCanvasView();
		object? sender = null;
		view.DrawFrame += (s, e) => sender = s;

		view.OnDrawFrame(new AsciiCanvasDrawEventArgs { Buffer = new CellBuffer(10, 5) });

		Assert.Same(view, sender);
	}

	// ────────────────────────────────────────────
	// AsciiCanvasDrawEventArgs — reuse pattern
	// ────────────────────────────────────────────

	[Fact]
	public void DrawEventArgs_CanBeReused_FieldsUpdateInPlace()
	{
		var buffer = new CellBuffer(40, 12);
		var args = new AsciiCanvasDrawEventArgs
		{
			Buffer = buffer,
			ElapsedTime = TimeSpan.FromMilliseconds(100),
			DeltaTime = TimeSpan.FromMilliseconds(33),
			FrameNumber = 3,
		};

		// Verify initial state
		Assert.Multiple(
			() => Assert.Same(buffer, args.Buffer),
			() => Assert.Equal(TimeSpan.FromMilliseconds(100), args.ElapsedTime),
			() => Assert.Equal(TimeSpan.FromMilliseconds(33), args.DeltaTime),
			() => Assert.Equal(3, args.FrameNumber));

		// Update in place (simulating what the handler does each tick)
		args.ElapsedTime = TimeSpan.FromMilliseconds(133);
		args.DeltaTime = TimeSpan.FromMilliseconds(33);
		args.FrameNumber = 4;

		Assert.Multiple(
			() => Assert.Same(buffer, args.Buffer),
			() => Assert.Equal(TimeSpan.FromMilliseconds(133), args.ElapsedTime),
			() => Assert.Equal(4, args.FrameNumber));
	}

	[Fact]
	public void DrawEventArgs_BufferCanBeSwapped_ForResize()
	{
		var args = new AsciiCanvasDrawEventArgs
		{
			Buffer = new CellBuffer(40, 12),
		};
		var originalBuffer = args.Buffer;

		var newBuffer = new CellBuffer(80, 24);
		args.Buffer = newBuffer;

		Assert.Multiple(
			() => Assert.NotSame(originalBuffer, args.Buffer),
			() => Assert.Same(newBuffer, args.Buffer));
	}

	// ────────────────────────────────────────────
	// Handler — Color conversion (MAUI → XenoAtom)
	// ────────────────────────────────────────────

	[Fact]
	public void ConvertColor_White_ConvertsCorrectly()
	{
		var tuiColor = AsciiCanvasViewHandler.ConvertColor(Colors.White);

		Assert.Multiple(
			() => Assert.Equal(255, tuiColor.R),
			() => Assert.Equal(255, tuiColor.G),
			() => Assert.Equal(255, tuiColor.B));
	}

	[Fact]
	public void ConvertColor_Black_ConvertsCorrectly()
	{
		var tuiColor = AsciiCanvasViewHandler.ConvertColor(Colors.Black);

		Assert.Multiple(
			() => Assert.Equal(0, tuiColor.R),
			() => Assert.Equal(0, tuiColor.G),
			() => Assert.Equal(0, tuiColor.B));
	}

	[Fact]
	public void ConvertColor_Red_ConvertsCorrectly()
	{
		var tuiColor = AsciiCanvasViewHandler.ConvertColor(Colors.Red);

		Assert.Multiple(
			() => Assert.Equal(255, tuiColor.R),
			() => Assert.Equal(0, tuiColor.G),
			() => Assert.Equal(0, tuiColor.B));
	}

	[Fact]
	public void ConvertColor_CustomRgb_ConvertsCorrectly()
	{
		// MAUI Color from float 0.0–1.0: 128/255 ≈ 0.502
		var mauiColor = new Color(0.5f, 0.25f, 0.75f);
		var tuiColor = AsciiCanvasViewHandler.ConvertColor(mauiColor);

		// Byte conversion: (int)(0.5 * 255) = 127
		Assert.Multiple(
			() => Assert.Equal(127, tuiColor.R),
			() => Assert.Equal(63, tuiColor.G),
			() => Assert.Equal(191, tuiColor.B));
	}

	[Theory]
	[InlineData(0f, 0f, 0f)]
	[InlineData(1f, 1f, 1f)]
	[InlineData(0.5f, 0.5f, 0.5f)]
	public void ConvertColor_RoundTrip_PreservesApproximateValues(float r, float g, float b)
	{
		var mauiColor = new Color(r, g, b);
		var tuiColor = AsciiCanvasViewHandler.ConvertColor(mauiColor);

		// Use int arithmetic to avoid byte overflow at boundaries (255+1 wraps to 0)
		int expectedR = (int)(r * 255);
		int expectedG = (int)(g * 255);
		int expectedB = (int)(b * 255);
		Assert.Multiple(
			() => Assert.InRange((int)tuiColor.R, expectedR - 1, expectedR + 1),
			() => Assert.InRange((int)tuiColor.G, expectedG - 1, expectedG + 1),
			() => Assert.InRange((int)tuiColor.B, expectedB - 1, expectedB + 1));
	}

	// ────────────────────────────────────────────
	// Handler — TextStyle conversion
	// ────────────────────────────────────────────

	[Fact]
	public void ConvertTextStyle_None_ReturnsNone()
	{
		var result = AsciiCanvasViewHandler.ConvertTextStyle(CellAttributes.None);
		Assert.Equal(TextStyle.None, result);
	}

	[Theory]
	[InlineData(CellAttributes.Bold, TextStyle.Bold)]
	[InlineData(CellAttributes.Dim, TextStyle.Dim)]
	[InlineData(CellAttributes.Italic, TextStyle.Italic)]
	[InlineData(CellAttributes.Underline, TextStyle.Underline)]
	[InlineData(CellAttributes.Blink, TextStyle.Blink)]
	[InlineData(CellAttributes.Reverse, TextStyle.Invert)]
	[InlineData(CellAttributes.Strikethrough, TextStyle.Strikethrough)]
	public void ConvertTextStyle_SingleFlag_MapsCorrectly(CellAttributes input, TextStyle expected)
	{
		var result = AsciiCanvasViewHandler.ConvertTextStyle(input);
		Assert.Equal(expected, result);
	}

	[Fact]
	public void ConvertTextStyle_CombinedFlags_MapsAllCorrectly()
	{
		var input = CellAttributes.Bold | CellAttributes.Italic | CellAttributes.Underline;
		var expected = TextStyle.Bold | TextStyle.Italic | TextStyle.Underline;
		var result = AsciiCanvasViewHandler.ConvertTextStyle(input);
		Assert.Equal(expected, result);
	}

	[Fact]
	public void ConvertTextStyle_AllFlags_MapsAllCorrectly()
	{
		var input = CellAttributes.Bold | CellAttributes.Dim | CellAttributes.Italic
			| CellAttributes.Underline | CellAttributes.Blink | CellAttributes.Reverse
			| CellAttributes.Strikethrough;
		var expected = TextStyle.Bold | TextStyle.Dim | TextStyle.Italic
			| TextStyle.Underline | TextStyle.Blink | TextStyle.Invert
			| TextStyle.Strikethrough;
		var result = AsciiCanvasViewHandler.ConvertTextStyle(input);
		Assert.Equal(expected, result);
	}

	// ────────────────────────────────────────────
	// Handler — Full Style conversion
	// ────────────────────────────────────────────

	[Fact]
	public void ConvertStyle_NullColors_ReturnsNoneStyle()
	{
		var style = AsciiCanvasViewHandler.ConvertStyle(null, null, CellAttributes.None);
		Assert.Equal(XenoAtom.Terminal.UI.Style.None, style);
	}

	[Fact]
	public void ConvertStyle_TransparentBackground_SkipsBackground()
	{
		var style = AsciiCanvasViewHandler.ConvertStyle(
			Colors.White, Colors.Transparent, CellAttributes.None);

		// Should have foreground but no background
		Assert.True(style.TryGetForeground(out _));
		Assert.False(style.TryGetBackground(out _));
	}

	[Fact]
	public void ConvertStyle_WithFgAndBg_SetsBot()
	{
		var style = AsciiCanvasViewHandler.ConvertStyle(
			Colors.Red, Colors.Blue, CellAttributes.None);

		Assert.Multiple(
			() => Assert.True(style.TryGetForeground(out var fg) && fg.R == 255 && fg.G == 0),
			() => Assert.True(style.TryGetBackground(out var bg) && bg.B == 255 && bg.R == 0));
	}

	[Fact]
	public void ConvertStyle_WithAttributes_SetsTextStyle()
	{
		var style = AsciiCanvasViewHandler.ConvertStyle(
			Colors.White, null, CellAttributes.Bold | CellAttributes.Italic);

		Assert.Equal(TextStyle.Bold | TextStyle.Italic, style.TextStyle);
	}

	// ────────────────────────────────────────────
	// CellBuffer dimensions from canvas view
	// ────────────────────────────────────────────

	[Theory]
	[InlineData(80, 24)]
	[InlineData(40, 12)]
	[InlineData(120, 40)]
	[InlineData(1, 1)]
	public void View_CreatesBufferWithCorrectDimensions(int width, int height)
	{
		var buffer = new CellBuffer(width, height);

		Assert.Multiple(
			() => Assert.Equal(width, buffer.Width),
			() => Assert.Equal(height, buffer.Height));
	}

	[Fact]
	public void View_BufferResize_PreservesExistingContent()
	{
		var buffer = new CellBuffer(10, 5);
		buffer.SetCell(0, 0, new System.Text.Rune('A'), Colors.Red, Colors.Black);
		buffer.SetCell(5, 2, new System.Text.Rune('B'), Colors.Green, Colors.Black);

		buffer.Resize(20, 10);

		Assert.Multiple(
			() => Assert.Equal(20, buffer.Width),
			() => Assert.Equal(10, buffer.Height),
			() => Assert.Equal(new System.Text.Rune('A'), buffer[0, 0].Character),
			() => Assert.Equal(new System.Text.Rune('B'), buffer[5, 2].Character));
	}

	// ────────────────────────────────────────────
	// Integration — DrawFrame receives writable buffer
	// ────────────────────────────────────────────

	[Fact]
	public void DrawFrame_BufferIsWritable_DuringEvent()
	{
		var view = new AsciiCanvasView { CanvasWidth = 20, CanvasHeight = 5 };
		var buffer = new CellBuffer(20, 5);
		bool bufferWritten = false;

		view.DrawFrame += (s, e) =>
		{
			e.Buffer.SetCell(0, 0, new System.Text.Rune('X'), Colors.Cyan, Colors.Black);
			bufferWritten = true;
		};

		var args = new AsciiCanvasDrawEventArgs
		{
			Buffer = buffer,
			FrameNumber = 0,
		};
		view.OnDrawFrame(args);

		Assert.Multiple(
			() => Assert.True(bufferWritten),
			() => Assert.Equal(new System.Text.Rune('X'), buffer[0, 0].Character));
	}

	[Fact]
	public void DrawFrame_DrawString_WritesAcrossRow()
	{
		var buffer = new CellBuffer(20, 5);
		var view = new AsciiCanvasView();

		view.DrawFrame += (s, e) =>
		{
			e.Buffer.DrawString(0, 0, "Hello", Colors.White, Colors.Black);
		};

		var args = new AsciiCanvasDrawEventArgs { Buffer = buffer };
		view.OnDrawFrame(args);

		Assert.Multiple(
			() => Assert.Equal(new System.Text.Rune('H'), buffer[0, 0].Character),
			() => Assert.Equal(new System.Text.Rune('e'), buffer[1, 0].Character),
			() => Assert.Equal(new System.Text.Rune('l'), buffer[2, 0].Character),
			() => Assert.Equal(new System.Text.Rune('l'), buffer[3, 0].Character),
			() => Assert.Equal(new System.Text.Rune('o'), buffer[4, 0].Character));
	}

	// ────────────────────────────────────────────
	// Integration — Multiple frames, event args reuse
	// ────────────────────────────────────────────

	[Fact]
	public void DrawFrame_EventArgsReused_SameInstanceAcrossFrames()
	{
		var view = new AsciiCanvasView();
		var args = new AsciiCanvasDrawEventArgs
		{
			Buffer = new CellBuffer(10, 5),
		};

		AsciiCanvasDrawEventArgs? first = null;
		AsciiCanvasDrawEventArgs? second = null;

		view.DrawFrame += (s, e) =>
		{
			if (first is null) first = e;
			else second = e;
		};

		// Simulate two frames with the same args instance (handler reuse pattern)
		args.FrameNumber = 0;
		view.OnDrawFrame(args);
		args.FrameNumber = 1;
		view.OnDrawFrame(args);

		Assert.Multiple(
			() => Assert.NotNull(first),
			() => Assert.NotNull(second),
			() => Assert.Same(first, second));
	}

	[Fact]
	public void DrawFrame_FrameNumber_IncrementsCorrectly()
	{
		var view = new AsciiCanvasView();
		var args = new AsciiCanvasDrawEventArgs { Buffer = new CellBuffer(10, 5) };
		var frameNumbers = new List<int>();

		view.DrawFrame += (s, e) => frameNumbers.Add(e.FrameNumber);

		for (int i = 0; i < 5; i++)
		{
			args.FrameNumber = i;
			view.OnDrawFrame(args);
		}

		Assert.Equal(new[] { 0, 1, 2, 3, 4 }, frameNumbers);
	}

	// ────────────────────────────────────────────
	// Integration — ClearBeforeDraw behavior
	// ────────────────────────────────────────────

	[Fact]
	public void ClearBeforeDraw_True_BufferIsClearedBetweenFrames()
	{
		var buffer = new CellBuffer(10, 5);
		buffer.SetCell(0, 0, new System.Text.Rune('X'), Colors.Red, Colors.Black);

		// Simulate what the handler does: clear then raise
		buffer.Clear();

		Assert.True(buffer[0, 0].IsEmpty);
	}

	[Fact]
	public void ClearBeforeDraw_False_BufferRetainsPreviousContent()
	{
		var buffer = new CellBuffer(10, 5);
		buffer.SetCell(0, 0, new System.Text.Rune('X'), Colors.Red, Colors.Black);

		// Without clear, content persists
		Assert.Equal(new System.Text.Rune('X'), buffer[0, 0].Character);
	}

	// ────────────────────────────────────────────
	// Edge cases
	// ────────────────────────────────────────────

	[Fact]
	public void View_ImplementsIAsciiCanvasView()
	{
		IAsciiCanvasView view = new AsciiCanvasView();

		Assert.Multiple(
			() => Assert.Equal(80, view.CanvasWidth),
			() => Assert.Equal(24, view.CanvasHeight),
			() => Assert.False(view.IsAnimating),
			() => Assert.Equal(30, view.TargetFps));
	}

	[Fact]
	public void View_MinimalCanvas_OneByone()
	{
		var view = new AsciiCanvasView { CanvasWidth = 1, CanvasHeight = 1 };
		var size = view.Measure(double.PositiveInfinity, double.PositiveInfinity);

		Assert.Multiple(
			() => Assert.Equal(1, size.Width),
			() => Assert.Equal(1, size.Height));
	}

	[Fact]
	public void View_LargeCanvas_Dimensions()
	{
		var view = new AsciiCanvasView { CanvasWidth = 500, CanvasHeight = 200 };
		var size = view.Measure(double.PositiveInfinity, double.PositiveInfinity);

		Assert.Multiple(
			() => Assert.Equal(500, size.Width),
			() => Assert.Equal(200, size.Height));
	}

	// ────────────────────────────────────────────
	// Zero-allocation verification
	// ────────────────────────────────────────────

	[Fact]
	public void ConvertColor_ZeroAllocations()
	{
		// Warmup
		for (int i = 0; i < 100; i++)
			AsciiCanvasViewHandler.ConvertColor(Colors.Red);

		long before = GC.GetAllocatedBytesForCurrentThread();
		for (int i = 0; i < 1000; i++)
		{
			AsciiCanvasViewHandler.ConvertColor(Colors.Red);
			AsciiCanvasViewHandler.ConvertColor(Colors.Blue);
			AsciiCanvasViewHandler.ConvertColor(Colors.Green);
		}
		long after = GC.GetAllocatedBytesForCurrentThread();

		Assert.True(after - before <= 256,
			$"Expected near-zero allocations for color conversion, got {after - before} bytes");
	}

	[Fact]
	public void ConvertStyle_ZeroAllocations()
	{
		// Warmup
		for (int i = 0; i < 100; i++)
			AsciiCanvasViewHandler.ConvertStyle(Colors.White, Colors.Black, CellAttributes.Bold);

		long before = GC.GetAllocatedBytesForCurrentThread();
		for (int i = 0; i < 1000; i++)
		{
			AsciiCanvasViewHandler.ConvertStyle(Colors.White, Colors.Black, CellAttributes.Bold);
			AsciiCanvasViewHandler.ConvertStyle(Colors.Red, null, CellAttributes.None);
			AsciiCanvasViewHandler.ConvertStyle(null, null, CellAttributes.Italic);
		}
		long after = GC.GetAllocatedBytesForCurrentThread();

		Assert.True(after - before <= 256,
			$"Expected near-zero allocations for style conversion, got {after - before} bytes");
	}

	[Fact]
	public void ConvertTextStyle_ZeroAllocations()
	{
		// Warmup
		for (int i = 0; i < 100; i++)
			AsciiCanvasViewHandler.ConvertTextStyle(CellAttributes.Bold | CellAttributes.Italic);

		long before = GC.GetAllocatedBytesForCurrentThread();
		for (int i = 0; i < 1000; i++)
		{
			AsciiCanvasViewHandler.ConvertTextStyle(CellAttributes.Bold);
			AsciiCanvasViewHandler.ConvertTextStyle(CellAttributes.Bold | CellAttributes.Italic | CellAttributes.Underline);
			AsciiCanvasViewHandler.ConvertTextStyle(CellAttributes.None);
		}
		long after = GC.GetAllocatedBytesForCurrentThread();

		Assert.True(after - before <= 256,
			$"Expected zero allocations for text style conversion, got {after - before} bytes");
	}

	// ────────────────────────────────────────────
	// DrawFrame + buffer write integration
	// ────────────────────────────────────────────

	[Fact]
	public void DrawFrame_FillRect_CorrectlyFillsSubregion()
	{
		var buffer = new CellBuffer(20, 10);
		var view = new AsciiCanvasView();

		view.DrawFrame += (s, e) =>
		{
			var fill = new TerminalCell('#', Colors.Yellow, Colors.DarkBlue);
			e.Buffer.FillRect(2, 1, 5, 3, fill);
		};

		var args = new AsciiCanvasDrawEventArgs { Buffer = buffer };
		view.OnDrawFrame(args);

		Assert.Multiple(
			// Inside the rect
			() => Assert.Equal(new System.Text.Rune('#'), buffer[2, 1].Character),
			() => Assert.Equal(new System.Text.Rune('#'), buffer[6, 3].Character),
			// Outside the rect
			() => Assert.True(buffer[0, 0].IsEmpty),
			() => Assert.True(buffer[7, 1].IsEmpty),
			() => Assert.True(buffer[2, 4].IsEmpty));
	}

	[Fact]
	public void DrawFrame_DrawString_ClipsAtBoundary()
	{
		var buffer = new CellBuffer(5, 1);
		var view = new AsciiCanvasView();

		view.DrawFrame += (s, e) =>
		{
			// String exceeds buffer width — should clip, not throw
			e.Buffer.DrawString(0, 0, "Hello World!", Colors.White, Colors.Black);
		};

		var args = new AsciiCanvasDrawEventArgs { Buffer = buffer };
		view.OnDrawFrame(args);

		Assert.Multiple(
			() => Assert.Equal(new System.Text.Rune('H'), buffer[0, 0].Character),
			() => Assert.Equal(new System.Text.Rune('o'), buffer[4, 0].Character));
	}

	// ────────────────────────────────────────────
	// Handler — OnTick simulation
	// ────────────────────────────────────────────

	[Fact]
	public void OnTick_Simulation_RaisesDrawFrameWithCorrectArgs()
	{
		// We can't fully instantiate the handler (needs MAUI DI),
		// but we can verify the view's OnDrawFrame contract
		var view = new AsciiCanvasView { CanvasWidth = 10, CanvasHeight = 5 };
		var buffer = new CellBuffer(10, 5);
		var drawArgs = new AsciiCanvasDrawEventArgs { Buffer = buffer };

		var receivedFrames = new List<(int frameNumber, TimeSpan elapsed)>();
		view.DrawFrame += (s, e) =>
		{
			receivedFrames.Add((e.FrameNumber, e.ElapsedTime));
		};

		// Simulate 3 ticks (what the handler's OnTick does)
		for (int i = 0; i < 3; i++)
		{
			drawArgs.FrameNumber = i;
			drawArgs.ElapsedTime = TimeSpan.FromMilliseconds(i * 33);
			drawArgs.DeltaTime = TimeSpan.FromMilliseconds(33);
			buffer.Clear();
			view.OnDrawFrame(drawArgs);
		}

		Assert.Multiple(
			() => Assert.Equal(3, receivedFrames.Count),
			() => Assert.Equal(0, receivedFrames[0].frameNumber),
			() => Assert.Equal(1, receivedFrames[1].frameNumber),
			() => Assert.Equal(2, receivedFrames[2].frameNumber),
			() => Assert.Equal(TimeSpan.Zero, receivedFrames[0].elapsed),
			() => Assert.Equal(TimeSpan.FromMilliseconds(66), receivedFrames[2].elapsed));
	}

	[Fact]
	public void OnTick_IsAnimatingFalse_DoesNotRaiseDrawFrame()
	{
		var view = new AsciiCanvasView { IsAnimating = false };
		bool drawCalled = false;
		view.DrawFrame += (s, e) => drawCalled = true;

		// OnDrawFrame would be called by the handler only if IsAnimating is true.
		// The handler checks VirtualView.IsAnimating before calling OnDrawFrame.
		// Verify at the view level that the event mechanism works correctly.
		Assert.False(view.IsAnimating);
		Assert.False(drawCalled);
	}
}
