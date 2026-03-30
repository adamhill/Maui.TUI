#nullable enable
using Maui.TUI.Animation;

namespace Maui.TUI.Controls;

/// <summary>
/// Event arguments for the <see cref="IAsciiCanvasView.DrawFrame"/> event.
/// A single instance is reused across frames — fields are updated each tick
/// to avoid per-frame allocations.
/// </summary>
public sealed class AsciiCanvasDrawEventArgs : EventArgs
{
	/// <summary>The cell buffer to draw into. Pre-allocated at canvas dimensions.</summary>
	public CellBuffer Buffer { get; internal set; } = null!;

	/// <summary>Total time elapsed since the animation started.</summary>
	public TimeSpan ElapsedTime { get; internal set; }

	/// <summary>Time elapsed since the previous frame.</summary>
	public TimeSpan DeltaTime { get; internal set; }

	/// <summary>Monotonically increasing frame counter (starts at 0).</summary>
	public int FrameNumber { get; internal set; }
}
