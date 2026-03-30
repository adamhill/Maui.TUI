#nullable enable
using Microsoft.Maui.Controls;
using Serilog;

namespace Maui.TUI.Controls;

/// <summary>
/// A custom MAUI View that provides a terminal canvas for direct cell-level
/// drawing. Place this in any MAUI layout (Grid, StackLayout, etc.) to create
/// an animated region that bypasses MAUI's measure/arrange pipeline for its interior.
/// </summary>
/// <remarks>
/// <para>
/// This is the TUI equivalent of <c>SKCanvasView</c> in SkiaSharp — a rectangular
/// region where the user has cell-level control, embedded within a MAUI layout.
/// </para>
/// <para>
/// The <see cref="DrawFrame"/> event fires each tick when <see cref="IsAnimating"/>
/// is <see langword="true"/>. User code writes into the provided
/// <see cref="AsciiCanvasDrawEventArgs.Buffer"/>, and the handler transfers the
/// buffer to XenoAtom's rendering pipeline.
/// </para>
/// </remarks>
public class AsciiCanvasView : View, IAsciiCanvasView
{
	private static readonly ILogger Logger = Log.ForContext<AsciiCanvasView>();
	/// <summary>Bindable property for <see cref="CanvasWidth"/>.</summary>
	public static readonly BindableProperty CanvasWidthProperty =
		BindableProperty.Create(nameof(CanvasWidth), typeof(int), typeof(AsciiCanvasView), 80);

	/// <summary>Bindable property for <see cref="CanvasHeight"/>.</summary>
	public static readonly BindableProperty CanvasHeightProperty =
		BindableProperty.Create(nameof(CanvasHeight), typeof(int), typeof(AsciiCanvasView), 24);

	/// <summary>Bindable property for <see cref="IsAnimating"/>.</summary>
	public static readonly BindableProperty IsAnimatingProperty =
		BindableProperty.Create(nameof(IsAnimating), typeof(bool), typeof(AsciiCanvasView), false);

	/// <summary>Bindable property for <see cref="TargetFps"/>.</summary>
	public static readonly BindableProperty TargetFpsProperty =
		BindableProperty.Create(nameof(TargetFps), typeof(int), typeof(AsciiCanvasView), 30);

	/// <summary>Bindable property for <see cref="ClearBeforeDraw"/>.</summary>
	public static readonly BindableProperty ClearBeforeDrawProperty =
		BindableProperty.Create(nameof(ClearBeforeDraw), typeof(bool), typeof(AsciiCanvasView), true);

	/// <inheritdoc/>
	public int CanvasWidth
	{
		get => (int)GetValue(CanvasWidthProperty);
		set => SetValue(CanvasWidthProperty, value);
	}

	/// <inheritdoc/>
	public int CanvasHeight
	{
		get => (int)GetValue(CanvasHeightProperty);
		set => SetValue(CanvasHeightProperty, value);
	}

	/// <inheritdoc/>
	public bool IsAnimating
	{
		get => (bool)GetValue(IsAnimatingProperty);
		set => SetValue(IsAnimatingProperty, value);
	}

	/// <inheritdoc/>
	public int TargetFps
	{
		get => (int)GetValue(TargetFpsProperty);
		set => SetValue(TargetFpsProperty, value);
	}

	/// <inheritdoc/>
	public bool ClearBeforeDraw
	{
		get => (bool)GetValue(ClearBeforeDrawProperty);
		set => SetValue(ClearBeforeDrawProperty, value);
	}

	/// <inheritdoc/>
	public event EventHandler<AsciiCanvasDrawEventArgs>? DrawFrame;

	/// <inheritdoc/>
	public void OnDrawFrame(AsciiCanvasDrawEventArgs e)
	{
		DrawFrame?.Invoke(this, e);
	}

	/// <summary>
	/// Marks the canvas for redraw on the next frame.
	/// </summary>
	public void InvalidateCanvas()
	{
		Logger.Verbose("Canvas invalidated ({Width}x{Height})", CanvasWidth, CanvasHeight);
		Handler?.Invoke(nameof(InvalidateCanvas));
	}

	/// <summary>
	/// Starts continuous animation at <see cref="TargetFps"/>.
	/// </summary>
	public void StartAnimation()
	{
		Logger.Information("Starting animation: {Width}x{Height} @ {Fps}fps", CanvasWidth, CanvasHeight, TargetFps);
		IsAnimating = true;
	}

	/// <summary>
	/// Stops continuous animation.
	/// </summary>
	public void StopAnimation()
	{
		Logger.Information("Stopping animation");
		IsAnimating = false;
	}

	/// <summary>
	/// Returns the fixed canvas size. Calls base so that
	/// <c>Handler.GetDesiredSize()</c> → <c>canvas.Measure()</c> runs on the
	/// XenoAtom visual, giving it non-zero layout hints.
	/// Animation content changes never trigger parent re-layout because the
	/// returned size is always the fixed canvas dimensions.
	/// </summary>
	protected override Size MeasureOverride(double widthConstraint, double heightConstraint)
	{
		// Call base so Handler.GetDesiredSize() → canvas.Measure() runs on the XenoAtom visual
		base.MeasureOverride(widthConstraint, heightConstraint);
		return new Size(CanvasWidth, CanvasHeight);
	}

	/// <summary>
	/// Returns the fixed canvas size. Calls base so that
	/// <c>Handler.PlatformArrange()</c> → <c>canvas.Arrange()</c> runs on the
	/// XenoAtom visual. This gives the Canvas non-zero <c>Bounds</c>, which is
	/// required for <c>RenderOverride</c> to execute and for the Painter
	/// callback to be invoked.
	/// </summary>
	protected override Size ArrangeOverride(Rect bounds)
	{
		// Call base so Handler.PlatformArrange() → canvas.Arrange() runs on the XenoAtom visual.
		// This gives the Canvas non-zero Bounds, which is required for RenderOverride to execute.
		base.ArrangeOverride(bounds);
		return new Size(CanvasWidth, CanvasHeight);
	}
}
