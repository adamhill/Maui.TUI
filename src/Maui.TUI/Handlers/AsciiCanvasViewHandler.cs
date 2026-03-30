#nullable enable
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using Maui.TUI.Animation;
using Maui.TUI.Controls;
using Maui.TUI.Platform;
using Microsoft.Maui.Animations;
using Microsoft.Maui.Handlers;
using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Controls;
using XenoAtom.Terminal.UI.Styling;
using TuiCanvas = XenoAtom.Terminal.UI.Controls.Canvas;
using TuiColor = XenoAtom.Terminal.UI.Color;
using TuiStyle = XenoAtom.Terminal.UI.Style;

namespace Maui.TUI.Handlers;

/// <summary>
/// Handles mapping between <see cref="IAsciiCanvasView"/> and the XenoAtom
/// <see cref="Canvas"/> visual for terminal rendering. This is the core animation
/// handler that bridges MAUI's CellBuffer to XenoAtom's rendering pipeline.
/// </summary>
/// <remarks>
/// <para>
/// Each tick, the handler: (1) updates the reused event args, (2) optionally
/// clears the CellBuffer, (3) raises DrawFrame so user code writes into it,
/// (4) transfers CellBuffer contents to the XenoAtom Canvas via the Painter callback.
/// </para>
/// <para>
/// The Painter callback runs during XenoAtom's render pass and "blits" the
/// pre-computed cell data into the terminal's rendering cell buffer using
/// <c>CanvasContext.SetPixel</c> per cell.
/// </para>
/// </remarks>
public partial class AsciiCanvasViewHandler : TuiViewHandler<IAsciiCanvasView, TuiCanvas>
{
	/// <summary>Property mapper for AsciiCanvasView properties.</summary>
	public static IPropertyMapper<IAsciiCanvasView, AsciiCanvasViewHandler> Mapper =
		new PropertyMapper<IAsciiCanvasView, AsciiCanvasViewHandler>(ViewMapper)
		{
			[nameof(IAsciiCanvasView.CanvasWidth)] = MapCanvasSize,
			[nameof(IAsciiCanvasView.CanvasHeight)] = MapCanvasSize,
			[nameof(IAsciiCanvasView.IsAnimating)] = MapIsAnimating,
			[nameof(IAsciiCanvasView.TargetFps)] = MapTargetFps,
		};

	/// <summary>Command mapper for AsciiCanvasView commands.</summary>
	public static CommandMapper<IAsciiCanvasView, AsciiCanvasViewHandler> CommandMapper =
		new(ViewCommandMapper);

	private CellBuffer? _buffer;
	private AsciiCanvasDrawEventArgs? _drawEventArgs;
	private readonly Stopwatch _animationStopwatch = new();
	private TimeSpan _lastFrameTime;
	private int _frameNumber;
	private bool _isSubscribedToTicker;
	private TuiTicker? _ticker;

	// Reactive state that drives XenoAtom's re-render pipeline.
	// BlitToCanvas reads _frameState.Value to establish a reactive dependency;
	// OnTick writes a new value to trigger canvas invalidation each frame.
	private readonly State<int> _frameState = new(0);

	// Cached action delegate to avoid per-tick allocations
	private Action? _tickCallback;

	/// <summary>
	/// Initializes a new instance of <see cref="AsciiCanvasViewHandler"/>.
	/// </summary>
	public AsciiCanvasViewHandler() : base(Mapper, CommandMapper) { }

	/// <summary>
	/// Initializes with custom mappers for testing or subclassing.
	/// </summary>
	public AsciiCanvasViewHandler(IPropertyMapper? mapper, CommandMapper? commandMapper = null)
		: base(mapper ?? Mapper, commandMapper ?? CommandMapper) { }

	/// <inheritdoc/>
	protected override TuiCanvas CreatePlatformView()
	{
		var canvas = new TuiCanvas();
		// Register the Painter callback — this runs during XenoAtom's render pass.
		// It reads from our CellBuffer and writes to the CanvasContext.
		canvas.Painter(BlitToCanvas);
		return canvas;
	}

	/// <inheritdoc/>
	protected override void ConnectHandler(TuiCanvas platformView)
	{
		base.ConnectHandler(platformView);

		// Resolve the TuiTicker from DI (safe lookup — some mock IServiceProvider
		// implementations throw on missing keys instead of returning null)
		try { _ticker = MauiContext?.Services.GetService(typeof(TuiTicker)) as TuiTicker; }
		catch (KeyNotFoundException) { _ticker = null; }

		// Cache the tick callback delegate
		_tickCallback = OnTick;

		EnsureBuffer();
	}

	/// <inheritdoc/>
	protected override void DisconnectHandler(TuiCanvas platformView)
	{
		UnsubscribeFromTicker();
		_ticker = null;
		_tickCallback = null;
		base.DisconnectHandler(platformView);
	}

	/// <summary>
	/// Gets the current cell buffer. Exposed for testing.
	/// </summary>
	internal CellBuffer? Buffer => _buffer;

	/// <summary>
	/// Gets the reused draw event args. Exposed for testing.
	/// </summary>
	internal AsciiCanvasDrawEventArgs? DrawEventArgs => _drawEventArgs;

	/// <summary>
	/// Maps CanvasWidth or CanvasHeight changes: resizes the CellBuffer
	/// and updates the platform view's fixed size.
	/// </summary>
	public static void MapCanvasSize(AsciiCanvasViewHandler handler, IAsciiCanvasView view)
	{
		handler.EnsureBuffer();

		// Resize buffer if dimensions changed
		if (handler._buffer is { } buffer &&
			(buffer.Width != view.CanvasWidth || buffer.Height != view.CanvasHeight))
		{
			buffer.Resize(view.CanvasWidth, view.CanvasHeight);
		}

		// Update the XenoAtom visual's fixed size hints
		handler.PlatformView.MinWidth = view.CanvasWidth;
		handler.PlatformView.MinHeight = view.CanvasHeight;
		handler.PlatformView.MaxWidth = view.CanvasWidth;
		handler.PlatformView.MaxHeight = view.CanvasHeight;
	}

	/// <summary>
	/// Maps IsAnimating changes: subscribes/unsubscribes from the ticker.
	/// </summary>
	public static void MapIsAnimating(AsciiCanvasViewHandler handler, IAsciiCanvasView view)
	{
		if (view.IsAnimating)
			handler.SubscribeToTicker();
		else
			handler.UnsubscribeFromTicker();
	}

	/// <summary>
	/// Maps TargetFps changes. Currently informational — the ticker runs
	/// at its own MaxFps and the handler fires every tick.
	/// </summary>
	public static void MapTargetFps(AsciiCanvasViewHandler handler, IAsciiCanvasView view)
	{
		// TargetFps is available for future per-canvas frame skipping.
		// For now, all canvases share the ticker's rate.
	}

	/// <summary>
	/// Called on each ticker fire when animating. Updates event args,
	/// raises DrawFrame, then invalidates the XenoAtom Canvas by writing
	/// a new frame number into <see cref="_frameState"/>.
	/// </summary>
	internal void OnTick()
	{
		if (VirtualView is not { IsAnimating: true })
			return;

		EnsureBuffer();
		if (_buffer is null || _drawEventArgs is null)
			return;

		// Lazy-start the stopwatch on first tick (handles direct OnTick calls
		// without ticker subscription, e.g. in tests)
		if (!_animationStopwatch.IsRunning)
			_animationStopwatch.Start();

		// Update timing
		var now = _animationStopwatch.Elapsed;
		var delta = now - _lastFrameTime;
		_lastFrameTime = now;

		// Update the reused event args (zero allocation)
		_drawEventArgs.ElapsedTime = now;
		_drawEventArgs.DeltaTime = delta;
		_drawEventArgs.FrameNumber = _frameNumber++;

		// Optionally clear the buffer before user draws
		if (VirtualView.ClearBeforeDraw)
			_buffer.Clear();

		// Raise DrawFrame — user code writes into the CellBuffer
		VirtualView.OnDrawFrame(_drawEventArgs);

		// Invalidate the XenoAtom Canvas by advancing the reactive state.
		// BlitToCanvas reads _frameState.Value, establishing a reactive dependency;
		// writing a new value here marks the canvas dirty and triggers re-render.
		_frameState.Value = _frameNumber;
	}

	/// <summary>
	/// The Canvas Painter callback. Runs during XenoAtom's render pass.
	/// Reads from our CellBuffer and writes to the CanvasContext using SetPixel.
	/// This is the "blit" — the core frame transfer.
	/// </summary>
	private void BlitToCanvas(CanvasContext ctx)
	{
		// Read _frameState.Value to establish a reactive dependency with XenoAtom's
		// rendering pipeline. When _frameState.Value changes in OnTick, the canvas
		// is marked dirty and this callback is re-invoked on the next render pass.
		_ = _frameState.Value;

		if (_buffer is null)
			return;

		var width = Math.Min(_buffer.Width, ctx.Size.Width);
		var height = Math.Min(_buffer.Height, ctx.Size.Height);

		for (int row = 0; row < height; row++)
		{
			var rowSpan = _buffer.GetRow(row);
			for (int col = 0; col < width; col++)
			{
				ref readonly var cell = ref rowSpan[col];

				// Skip empty cells — they're already cleared by the canvas
				if (cell.IsEmpty)
					continue;

				var style = ConvertStyle(cell.Foreground, cell.Background, cell.Attributes);
				ctx.SetPixel(col, row, cell.Character, style);
			}
		}
	}

	/// <summary>
	/// Ensures the CellBuffer and draw event args are allocated with correct dimensions.
	/// </summary>
	private void EnsureBuffer()
	{
		if (VirtualView is null)
			return;

		int w = VirtualView.CanvasWidth;
		int h = VirtualView.CanvasHeight;

		if (_buffer is null || _buffer.Width != w || _buffer.Height != h)
		{
			if (_buffer is null)
				_buffer = new CellBuffer(w, h);
			else
				_buffer.Resize(w, h);
		}

		_drawEventArgs ??= new AsciiCanvasDrawEventArgs { Buffer = _buffer };
		_drawEventArgs.Buffer = _buffer;
	}

	private void SubscribeToTicker()
	{
		if (_isSubscribedToTicker || _ticker is null || _tickCallback is null)
			return;

		_isSubscribedToTicker = true;
		_frameNumber = 0;
		_lastFrameTime = TimeSpan.Zero;
		_animationStopwatch.Restart();

		// Chain our callback onto the ticker's Fire delegate
		var existingFire = _ticker.Fire;
		_ticker.Fire = existingFire is null
			? _tickCallback
			: existingFire + _tickCallback;

		if (!_ticker.IsRunning)
			_ticker.Start();
	}

	private void UnsubscribeFromTicker()
	{
		if (!_isSubscribedToTicker || _ticker is null || _tickCallback is null)
			return;

		_isSubscribedToTicker = false;
		_animationStopwatch.Stop();

		// Remove our callback from the ticker's Fire delegate
		var existingFire = _ticker.Fire;
		if (existingFire is not null)
			_ticker.Fire = existingFire - _tickCallback;
	}

	/// <summary>
	/// Converts MAUI Color + CellAttributes to a XenoAtom Style.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static TuiStyle ConvertStyle(
		Microsoft.Maui.Graphics.Color? fg,
		Microsoft.Maui.Graphics.Color? bg,
		CellAttributes attributes)
	{
		var style = TuiStyle.None;

		if (fg is not null)
			style = style.WithForeground(ConvertColor(fg));

		if (bg is not null && bg != Microsoft.Maui.Graphics.Colors.Transparent)
			style = style.WithBackground(ConvertColor(bg));

		if (attributes != CellAttributes.None)
			style = style.WithTextStyle(ConvertTextStyle(attributes));

		return style;
	}

	/// <summary>
	/// Converts a MAUI Color (float RGBA 0.0–1.0) to a XenoAtom Color (byte RGBA).
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static TuiColor ConvertColor(Microsoft.Maui.Graphics.Color color)
	{
		byte r = (byte)(color.Red * 255f);
		byte g = (byte)(color.Green * 255f);
		byte b = (byte)(color.Blue * 255f);
		return TuiColor.Rgb(r, g, b);
	}

	/// <summary>
	/// Converts <see cref="CellAttributes"/> flags to XenoAtom <see cref="TextStyle"/> flags.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static TextStyle ConvertTextStyle(CellAttributes attributes)
	{
		var result = TextStyle.None;

		if ((attributes & CellAttributes.Bold) != 0) result |= TextStyle.Bold;
		if ((attributes & CellAttributes.Dim) != 0) result |= TextStyle.Dim;
		if ((attributes & CellAttributes.Italic) != 0) result |= TextStyle.Italic;
		if ((attributes & CellAttributes.Underline) != 0) result |= TextStyle.Underline;
		if ((attributes & CellAttributes.Blink) != 0) result |= TextStyle.Blink;
		if ((attributes & CellAttributes.Reverse) != 0) result |= TextStyle.Invert;
		if ((attributes & CellAttributes.Strikethrough) != 0) result |= TextStyle.Strikethrough;

		return result;
	}
}
