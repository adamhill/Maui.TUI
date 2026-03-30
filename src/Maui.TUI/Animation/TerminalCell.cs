#nullable enable
using System.Text;
using Microsoft.Maui.Graphics;

namespace Maui.TUI.Animation;

/// <summary>
/// Represents a single terminal cell: one character with foreground color,
/// background color, and text attributes.
/// </summary>
/// <remarks>
/// This is a value type for array-of-struct performance. The flat array
/// layout in <see cref="CellBuffer"/> ensures cache-friendly iteration.
/// </remarks>
public readonly struct TerminalCell : IEquatable<TerminalCell>
{
	/// <summary>An empty cell: space character, white foreground, transparent background, no attributes.</summary>
	public static readonly TerminalCell Empty = new(' ', Colors.White, Colors.Transparent, CellAttributes.None);

	/// <summary>The Unicode character displayed in this cell.</summary>
	public Rune Character { get; }

	/// <summary>Foreground (text) color.</summary>
	public Color Foreground { get; }

	/// <summary>Background color.</summary>
	public Color Background { get; }

	/// <summary>Text rendering attributes (bold, italic, etc.).</summary>
	public CellAttributes Attributes { get; }

	/// <summary>
	/// Creates a new terminal cell.
	/// </summary>
	/// <param name="character">The character to display.</param>
	/// <param name="foreground">Foreground color.</param>
	/// <param name="background">Background color.</param>
	/// <param name="attributes">Text attributes.</param>
	public TerminalCell(Rune character, Color foreground, Color background, CellAttributes attributes = CellAttributes.None)
	{
		Character = character;
		Foreground = foreground;
		Background = background;
		Attributes = attributes;
	}

	/// <summary>
	/// Creates a new terminal cell from a char.
	/// </summary>
	/// <param name="character">The character to display.</param>
	/// <param name="foreground">Foreground color.</param>
	/// <param name="background">Background color.</param>
	/// <param name="attributes">Text attributes.</param>
	public TerminalCell(char character, Color foreground, Color background, CellAttributes attributes = CellAttributes.None)
		: this(new Rune(character), foreground, background, attributes)
	{
	}

	/// <summary>
	/// Returns <see langword="true"/> if this cell is visually empty:
	/// a space character with no special attributes.
	/// </summary>
	public bool IsEmpty => Character.Value == ' ' && Attributes == CellAttributes.None;

	/// <inheritdoc />
	public bool Equals(TerminalCell other) =>
		Character == other.Character &&
		Attributes == other.Attributes &&
		Equals(Foreground, other.Foreground) &&
		Equals(Background, other.Background);

	/// <inheritdoc />
	public override bool Equals(object? obj) => obj is TerminalCell other && Equals(other);

	/// <inheritdoc />
	public override int GetHashCode() => HashCode.Combine(Character, Foreground, Background, Attributes);

	/// <summary>Equality operator.</summary>
	public static bool operator ==(TerminalCell left, TerminalCell right) => left.Equals(right);

	/// <summary>Inequality operator.</summary>
	public static bool operator !=(TerminalCell left, TerminalCell right) => !left.Equals(right);

	/// <inheritdoc />
	public override string ToString() => $"'{(char)Character.Value}' fg={Foreground} bg={Background} attr={Attributes}";
}
