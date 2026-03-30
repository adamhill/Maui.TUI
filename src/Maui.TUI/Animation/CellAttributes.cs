#nullable enable

namespace Maui.TUI.Animation;

/// <summary>
/// Text rendering attributes for a terminal cell.
/// Combine with bitwise OR for multiple attributes (e.g. <c>Bold | Italic</c>).
/// </summary>
[Flags]
public enum CellAttributes : byte
{
	/// <summary>No special attributes.</summary>
	None = 0,

	/// <summary>Bold / increased intensity.</summary>
	Bold = 1,

	/// <summary>Italic text.</summary>
	Italic = 2,

	/// <summary>Underlined text.</summary>
	Underline = 4,

	/// <summary>Strikethrough text.</summary>
	Strikethrough = 8,

	/// <summary>Dim / faint intensity.</summary>
	Dim = 16,

	/// <summary>Blinking text (terminal support varies).</summary>
	Blink = 32,

	/// <summary>Swap foreground and background colors.</summary>
	Reverse = 64,
}
