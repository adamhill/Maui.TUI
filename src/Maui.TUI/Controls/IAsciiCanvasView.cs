#nullable enable

namespace Maui.TUI.Controls;

/// <summary>
/// Virtual view interface for a terminal canvas that supports direct cell-level
/// drawing, bypassing MAUI's layout system for its interior. This is the TUI
/// equivalent of <c>SKCanvasView</c> in SkiaSharp.
/// </summary>
public interface IAsciiCanvasView : IView
{
	/// <summary>Width of the canvas in terminal columns.</summary>
	int CanvasWidth { get; }

	/// <summary>Height of the canvas in terminal rows.</summary>
	int CanvasHeight { get; }

	/// <summary>
	/// When <see langword="true"/>, the canvas requests continuous redraw
	/// at <see cref="TargetFps"/> frames per second.
	/// </summary>
	bool IsAnimating { get; set; }

	/// <summary>Desired animation frame rate. Defaults to 30.</summary>
	int TargetFps { get; set; }

	/// <summary>
	/// When <see langword="true"/>, the buffer is cleared to
	/// <see cref="Animation.TerminalCell.Empty"/> before each frame.
	/// Defaults to <see langword="true"/>.
	/// </summary>
	bool ClearBeforeDraw { get; set; }

	/// <summary>
	/// Raised each frame when <see cref="IsAnimating"/> is <see langword="true"/>.
	/// The handler writes into the provided <see cref="AsciiCanvasDrawEventArgs.Buffer"/>.
	/// </summary>
	event EventHandler<AsciiCanvasDrawEventArgs>? DrawFrame;

	/// <summary>
	/// Raises the <see cref="DrawFrame"/> event. Called by the handler each tick.
	/// </summary>
	void OnDrawFrame(AsciiCanvasDrawEventArgs e);
}
