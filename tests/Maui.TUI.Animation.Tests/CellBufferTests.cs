#nullable enable
using System.Runtime.CompilerServices;
using System.Text;
using Maui.TUI.Animation;
using Microsoft.Maui.Graphics;

namespace Maui.TUI.Animation.Tests;

public class CellBufferTests
{
	// ── TerminalCell basics ──────────────────────────────────────

	/// <summary>
	/// TerminalCell.Empty is a space with white foreground, transparent background, no attributes.
	/// </summary>
	[Fact]
	public void TerminalCell_Empty_HasExpectedDefaults()
	{
		var empty = TerminalCell.Empty;

		Assert.Multiple(
			() => Assert.Equal(new Rune(' '), empty.Character),
			() => Assert.Equal(Colors.White, empty.Foreground),
			() => Assert.Equal(Colors.Transparent, empty.Background),
			() => Assert.Equal(CellAttributes.None, empty.Attributes),
			() => Assert.True(empty.IsEmpty));
	}

	/// <summary>
	/// TerminalCell with a visible character is not considered empty.
	/// </summary>
	[Fact]
	public void TerminalCell_WithCharacter_IsNotEmpty()
	{
		var cell = new TerminalCell('A', Colors.Red, Colors.Black);
		Assert.False(cell.IsEmpty);
	}

	/// <summary>
	/// TerminalCell with space but attributes is not empty.
	/// </summary>
	[Fact]
	public void TerminalCell_SpaceWithAttributes_IsNotEmpty()
	{
		var cell = new TerminalCell(' ', Colors.White, Colors.Transparent, CellAttributes.Bold);
		Assert.False(cell.IsEmpty);
	}

	/// <summary>
	/// TerminalCell equality works for identical cells.
	/// </summary>
	[Fact]
	public void TerminalCell_Equality_Works()
	{
		var a = new TerminalCell('X', Colors.Red, Colors.Blue, CellAttributes.Bold);
		var b = new TerminalCell('X', Colors.Red, Colors.Blue, CellAttributes.Bold);
		var c = new TerminalCell('Y', Colors.Red, Colors.Blue, CellAttributes.Bold);

		Assert.Multiple(
			() => Assert.Equal(a, b),
			() => Assert.True(a == b),
			() => Assert.NotEqual(a, c),
			() => Assert.True(a != c));
	}

	/// <summary>
	/// TerminalCell is a value type (struct) as required by the spec.
	/// </summary>
	[Fact]
	public void TerminalCell_IsValueType()
	{
		Assert.True(typeof(TerminalCell).IsValueType);
	}

	/// <summary>
	/// TerminalCell struct size must be ≤ 32 bytes as required by the spec.
	/// </summary>
	[Fact]
	public void TerminalCell_StructSize_LessThanOrEqual32Bytes()
	{
		var size = Unsafe.SizeOf<TerminalCell>();
		Assert.True(size <= 32, $"TerminalCell is {size} bytes, expected ≤ 32");
	}

	// ── CellBuffer construction & Clear ─────────────────────────

	/// <summary>
	/// New buffer is filled with TerminalCell.Empty.
	/// </summary>
	[Fact]
	public void Buffer_NewBuffer_FilledWithEmpty()
	{
		var buf = new CellBuffer(10, 5);

		Assert.Multiple(
			() => Assert.Equal(10, buf.Width),
			() => Assert.Equal(5, buf.Height));

		for (int r = 0; r < buf.Height; r++)
			for (int c = 0; c < buf.Width; c++)
				Assert.Equal(TerminalCell.Empty, buf[c, r]);
	}

	/// <summary>
	/// Constructor rejects zero or negative dimensions.
	/// </summary>
	[Theory]
	[InlineData(0, 5)]
	[InlineData(5, 0)]
	[InlineData(-1, 5)]
	[InlineData(5, -1)]
	public void Buffer_Constructor_ThrowsOnInvalidDimensions(int w, int h)
	{
		Assert.Throws<ArgumentOutOfRangeException>(() => new CellBuffer(w, h));
	}

	/// <summary>
	/// Clear resets all cells to TerminalCell.Empty.
	/// </summary>
	[Fact]
	public void Buffer_Clear_ResetsAllCells()
	{
		var buf = new CellBuffer(5, 3);
		buf.SetCell(0, 0, new Rune('A'), Colors.Red, Colors.Black);
		buf.SetCell(4, 2, new Rune('Z'), Colors.Green, Colors.Blue);

		buf.Clear();

		for (int r = 0; r < buf.Height; r++)
			for (int c = 0; c < buf.Width; c++)
				Assert.Equal(TerminalCell.Empty, buf[c, r]);
	}

	/// <summary>
	/// Clear subregion only clears the specified rectangle.
	/// </summary>
	[Fact]
	public void Buffer_ClearSubregion_OnlyClearsSpecifiedArea()
	{
		var buf = new CellBuffer(5, 3);
		var cell = new TerminalCell('X', Colors.Red, Colors.Black);
		buf.FillRect(0, 0, 5, 3, cell);

		buf.Clear(1, 1, 2, 1);

		// Cleared region
		Assert.Multiple(
			() => Assert.Equal(TerminalCell.Empty, buf[1, 1]),
			() => Assert.Equal(TerminalCell.Empty, buf[2, 1]));

		// Surrounding cells unchanged
		Assert.Multiple(
			() => Assert.Equal(cell, buf[0, 0]),
			() => Assert.Equal(cell, buf[4, 2]),
			() => Assert.Equal(cell, buf[0, 1]),
			() => Assert.Equal(cell, buf[3, 1]));
	}

	// ── SetCell and indexer roundtrip ────────────────────────────

	/// <summary>
	/// Test 1: SetCell and indexer roundtrip.
	/// </summary>
	[Fact]
	public void Buffer_SetCell_Indexer_Roundtrip()
	{
		var buf = new CellBuffer(10, 10);
		buf.SetCell(3, 7, new Rune('Q'), Colors.Cyan, Colors.Magenta, CellAttributes.Bold | CellAttributes.Italic);

		var cell = buf[3, 7];
		Assert.Multiple(
			() => Assert.Equal(new Rune('Q'), cell.Character),
			() => Assert.Equal(Colors.Cyan, cell.Foreground),
			() => Assert.Equal(Colors.Magenta, cell.Background),
			() => Assert.Equal(CellAttributes.Bold | CellAttributes.Italic, cell.Attributes));
	}

	/// <summary>
	/// Indexer ref return allows direct mutation.
	/// </summary>
	[Fact]
	public void Buffer_Indexer_RefReturnAllowsMutation()
	{
		var buf = new CellBuffer(5, 5);
		buf[2, 3] = new TerminalCell('W', Colors.Yellow, Colors.Black);

		Assert.Equal(new Rune('W'), buf[2, 3].Character);
	}

	/// <summary>
	/// Test 4: SetCell with out-of-bounds coordinates is silently ignored.
	/// </summary>
	[Theory]
	[InlineData(-1, 0)]
	[InlineData(0, -1)]
	[InlineData(10, 0)]
	[InlineData(0, 5)]
	[InlineData(100, 100)]
	[InlineData(-100, -100)]
	public void Buffer_SetCell_OutOfBounds_SilentlyIgnored(int col, int row)
	{
		var buf = new CellBuffer(10, 5);
		// Should not throw
		buf.SetCell(col, row, new Rune('X'), Colors.Red, Colors.Black);

		// Buffer should still be all empty
		for (int r = 0; r < buf.Height; r++)
			for (int c = 0; c < buf.Width; c++)
				Assert.Equal(TerminalCell.Empty, buf[c, r]);
	}

	/// <summary>
	/// Indexer out of bounds returns a discardable ref (writes are lost).
	/// </summary>
	[Fact]
	public void Buffer_Indexer_OutOfBounds_ReturnsDiscardRef()
	{
		var buf = new CellBuffer(5, 5);

		// Reading out of bounds should not throw
		var cell = buf[-1, 0];
		Assert.True(true); // If we get here, no exception was thrown

		// Writing out of bounds should not throw or affect the buffer
		buf[-1, 0] = new TerminalCell('Z', Colors.Red, Colors.Black);
		// Verify buffer is still clean
		Assert.Equal(TerminalCell.Empty, buf[0, 0]);
	}

	// ── DrawString ──────────────────────────────────────────────

	/// <summary>
	/// Test 2: DrawString writes correct characters at correct positions.
	/// </summary>
	[Fact]
	public void Buffer_DrawString_WritesCorrectCharacters()
	{
		var buf = new CellBuffer(10, 3);
		buf.DrawString(2, 1, "Hello", Colors.Green, Colors.Black);

		Assert.Multiple(
			() => Assert.Equal(new Rune('H'), buf[2, 1].Character),
			() => Assert.Equal(new Rune('e'), buf[3, 1].Character),
			() => Assert.Equal(new Rune('l'), buf[4, 1].Character),
			() => Assert.Equal(new Rune('l'), buf[5, 1].Character),
			() => Assert.Equal(new Rune('o'), buf[6, 1].Character));

		// Characters should have the correct colors
		Assert.Multiple(
			() => Assert.Equal(Colors.Green, buf[2, 1].Foreground),
			() => Assert.Equal(Colors.Black, buf[2, 1].Background));

		// Adjacent cells should be empty
		Assert.Multiple(
			() => Assert.Equal(TerminalCell.Empty, buf[1, 1]),
			() => Assert.Equal(TerminalCell.Empty, buf[7, 1]));
	}

	/// <summary>
	/// Test 3: DrawString clips at buffer boundary (no exception).
	/// </summary>
	[Fact]
	public void Buffer_DrawString_ClipsAtBoundary()
	{
		var buf = new CellBuffer(5, 1);
		buf.DrawString(3, 0, "Hello", Colors.White, Colors.Black);

		// Only first 2 chars should fit (columns 3,4)
		Assert.Multiple(
			() => Assert.Equal(new Rune('H'), buf[3, 0].Character),
			() => Assert.Equal(new Rune('e'), buf[4, 0].Character));
	}

	/// <summary>
	/// DrawString with negative start column clips leading characters.
	/// </summary>
	[Fact]
	public void Buffer_DrawString_NegativeStartCol_ClipsLeading()
	{
		var buf = new CellBuffer(5, 1);
		buf.DrawString(-2, 0, "Hello", Colors.White, Colors.Black);

		// "Hello" starts at -2, so 'H' at -2, 'e' at -1 are clipped
		// 'l' at 0, 'l' at 1, 'o' at 2 should be visible
		Assert.Multiple(
			() => Assert.Equal(new Rune('l'), buf[0, 0].Character),
			() => Assert.Equal(new Rune('l'), buf[1, 0].Character),
			() => Assert.Equal(new Rune('o'), buf[2, 0].Character));
	}

	/// <summary>
	/// DrawString on out-of-bounds row is silently ignored.
	/// </summary>
	[Fact]
	public void Buffer_DrawString_OutOfBoundsRow_SilentlyIgnored()
	{
		var buf = new CellBuffer(10, 3);
		buf.DrawString(0, 5, "Hello", Colors.White, Colors.Black);
		buf.DrawString(0, -1, "Hello", Colors.White, Colors.Black);

		// Buffer should be all empty
		for (int r = 0; r < buf.Height; r++)
			for (int c = 0; c < buf.Width; c++)
				Assert.Equal(TerminalCell.Empty, buf[c, r]);
	}

	/// <summary>
	/// DrawString with transparent background overload works.
	/// </summary>
	[Fact]
	public void Buffer_DrawString_TransparentBgOverload()
	{
		var buf = new CellBuffer(10, 1);
		buf.DrawString(0, 0, "Hi", Colors.Red);

		Assert.Multiple(
			() => Assert.Equal(Colors.Red, buf[0, 0].Foreground),
			() => Assert.Equal(Colors.Transparent, buf[0, 0].Background));
	}

	// ── FillRect ────────────────────────────────────────────────

	/// <summary>
	/// Test 6: FillRect fills the correct subregion.
	/// </summary>
	[Fact]
	public void Buffer_FillRect_FillsCorrectSubregion()
	{
		var buf = new CellBuffer(10, 5);
		var cell = new TerminalCell('#', Colors.Red, Colors.Black);

		buf.FillRect(2, 1, 3, 2, cell);

		// Inside the rect
		for (int r = 1; r <= 2; r++)
			for (int c = 2; c <= 4; c++)
				Assert.Equal(cell, buf[c, r]);

		// Outside the rect
		Assert.Multiple(
			() => Assert.Equal(TerminalCell.Empty, buf[1, 1]),
			() => Assert.Equal(TerminalCell.Empty, buf[5, 1]),
			() => Assert.Equal(TerminalCell.Empty, buf[2, 0]),
			() => Assert.Equal(TerminalCell.Empty, buf[2, 3]));
	}

	/// <summary>
	/// FillRect clips at buffer boundaries.
	/// </summary>
	[Fact]
	public void Buffer_FillRect_ClipsAtBoundary()
	{
		var buf = new CellBuffer(5, 3);
		var cell = new TerminalCell('*', Colors.White, Colors.Black);

		// Fill rect that extends beyond the right and bottom
		buf.FillRect(3, 2, 10, 10, cell);

		// Only cells within bounds should be filled
		Assert.Multiple(
			() => Assert.Equal(cell, buf[3, 2]),
			() => Assert.Equal(cell, buf[4, 2]),
			() => Assert.Equal(TerminalCell.Empty, buf[2, 2]));
	}

	/// <summary>
	/// FillRect with completely out-of-bounds region is a no-op.
	/// </summary>
	[Fact]
	public void Buffer_FillRect_CompletelyOutOfBounds_NoOp()
	{
		var buf = new CellBuffer(5, 3);
		var cell = new TerminalCell('X', Colors.Red, Colors.Black);

		buf.FillRect(10, 10, 5, 5, cell);
		buf.FillRect(-10, -10, 5, 5, cell);

		// Buffer should be all empty
		for (int r = 0; r < buf.Height; r++)
			for (int c = 0; c < buf.Width; c++)
				Assert.Equal(TerminalCell.Empty, buf[c, r]);
	}

	// ── CopyFrom ────────────────────────────────────────────────

	/// <summary>
	/// Test 7: CopyFrom transfers cell data correctly.
	/// </summary>
	[Fact]
	public void Buffer_CopyFrom_TransfersCellData()
	{
		var src = new CellBuffer(3, 2);
		src.SetCell(0, 0, new Rune('A'), Colors.Red, Colors.Black);
		src.SetCell(1, 0, new Rune('B'), Colors.Green, Colors.Black);
		src.SetCell(2, 1, new Rune('C'), Colors.Blue, Colors.Black);

		var dst = new CellBuffer(3, 2);
		dst.CopyFrom(src);

		Assert.Multiple(
			() => Assert.Equal(new Rune('A'), dst[0, 0].Character),
			() => Assert.Equal(new Rune('B'), dst[1, 0].Character),
			() => Assert.Equal(new Rune('C'), dst[2, 1].Character),
			() => Assert.Equal(Colors.Red, dst[0, 0].Foreground));
	}

	/// <summary>
	/// CopyFrom throws when dimensions don't match.
	/// </summary>
	[Fact]
	public void Buffer_CopyFrom_ThrowsOnDimensionMismatch()
	{
		var src = new CellBuffer(3, 2);
		var dst = new CellBuffer(5, 5);

		Assert.Throws<ArgumentException>(() => dst.CopyFrom(src));
	}

	/// <summary>
	/// Test 8: CopyFrom with subregion clips at boundaries.
	/// </summary>
	[Fact]
	public void Buffer_CopyFrom_Subregion_TransfersCorrectly()
	{
		var src = new CellBuffer(5, 5);
		src.DrawString(0, 0, "ABCDE", Colors.Red, Colors.Black);
		src.DrawString(0, 1, "FGHIJ", Colors.Green, Colors.Black);

		var dst = new CellBuffer(10, 10);
		dst.CopyFrom(src, srcCol: 1, srcRow: 0, destCol: 3, destRow: 2, width: 3, height: 2);

		// Should have copied BCD at (3,2) and GHI at (3,3)
		Assert.Multiple(
			() => Assert.Equal(new Rune('B'), dst[3, 2].Character),
			() => Assert.Equal(new Rune('C'), dst[4, 2].Character),
			() => Assert.Equal(new Rune('D'), dst[5, 2].Character),
			() => Assert.Equal(new Rune('G'), dst[3, 3].Character),
			() => Assert.Equal(new Rune('H'), dst[4, 3].Character),
			() => Assert.Equal(new Rune('I'), dst[5, 3].Character));

		// Adjacent cells should be empty
		Assert.Multiple(
			() => Assert.Equal(TerminalCell.Empty, dst[2, 2]),
			() => Assert.Equal(TerminalCell.Empty, dst[6, 2]));
	}

	/// <summary>
	/// CopyFrom subregion clips when source or dest would overflow.
	/// </summary>
	[Fact]
	public void Buffer_CopyFrom_Subregion_ClipsAtBoundaries()
	{
		var src = new CellBuffer(5, 3);
		src.DrawString(0, 0, "ABCDE", Colors.Red, Colors.Black);

		var dst = new CellBuffer(3, 3);
		// Copy 5 chars starting at col 0 into dst starting at col 1
		// Only 2 should fit (cols 1,2 of dst)
		dst.CopyFrom(src, srcCol: 0, srcRow: 0, destCol: 1, destRow: 0, width: 5, height: 1);

		Assert.Multiple(
			() => Assert.Equal(TerminalCell.Empty, dst[0, 0]),
			() => Assert.Equal(new Rune('A'), dst[1, 0].Character),
			() => Assert.Equal(new Rune('B'), dst[2, 0].Character));
	}

	// ── GetRow ──────────────────────────────────────────────────

	/// <summary>
	/// Test 9: GetRow returns correct span length and content.
	/// </summary>
	[Fact]
	public void Buffer_GetRow_ReturnsCorrectSpan()
	{
		var buf = new CellBuffer(5, 3);
		buf.DrawString(0, 1, "Hello", Colors.White, Colors.Black);

		var row = buf.GetRow(1);

		// Span<T> (ref struct) cannot be captured by Assert.Multiple lambdas,
		// so these asserts are sequential by necessity.
		Assert.Equal(5, row.Length);
		Assert.Equal(new Rune('H'), row[0].Character);
		Assert.Equal(new Rune('o'), row[4].Character);
	}

	/// <summary>
	/// GetRow with out-of-bounds row returns empty span.
	/// </summary>
	[Theory]
	[InlineData(-1)]
	[InlineData(3)]
	[InlineData(100)]
	public void Buffer_GetRow_OutOfBounds_ReturnsEmpty(int row)
	{
		var buf = new CellBuffer(5, 3);
		Assert.True(buf.GetRow(row).IsEmpty);
	}

	/// <summary>
	/// GetRow span allows direct mutation of buffer cells.
	/// </summary>
	[Fact]
	public void Buffer_GetRow_SpanAllowsMutation()
	{
		var buf = new CellBuffer(5, 3);
		var row = buf.GetRow(0);
		row[2] = new TerminalCell('M', Colors.Red, Colors.Black);

		Assert.Equal(new Rune('M'), buf[2, 0].Character);
	}

	// ── Resize ──────────────────────────────────────────────────

	/// <summary>
	/// Resize to larger dimensions preserves existing content.
	/// </summary>
	[Fact]
	public void Buffer_Resize_LargerPreservesContent()
	{
		var buf = new CellBuffer(3, 2);
		buf.SetCell(0, 0, new Rune('A'), Colors.Red, Colors.Black);
		buf.SetCell(2, 1, new Rune('B'), Colors.Green, Colors.Black);

		buf.Resize(5, 4);

		Assert.Multiple(
			() => Assert.Equal(5, buf.Width),
			() => Assert.Equal(4, buf.Height),
			() => Assert.Equal(new Rune('A'), buf[0, 0].Character),
			() => Assert.Equal(new Rune('B'), buf[2, 1].Character),
			// New cells should be empty
			() => Assert.Equal(TerminalCell.Empty, buf[4, 3]));
	}

	/// <summary>
	/// Resize to smaller dimensions truncates content.
	/// </summary>
	[Fact]
	public void Buffer_Resize_SmallerTruncatesContent()
	{
		var buf = new CellBuffer(5, 5);
		buf.SetCell(0, 0, new Rune('A'), Colors.Red, Colors.Black);
		buf.SetCell(4, 4, new Rune('Z'), Colors.Green, Colors.Black);

		buf.Resize(3, 3);

		Assert.Multiple(
			() => Assert.Equal(3, buf.Width),
			() => Assert.Equal(3, buf.Height),
			() => Assert.Equal(new Rune('A'), buf[0, 0].Character));
	}

	/// <summary>
	/// Resize to same dimensions is a no-op.
	/// </summary>
	[Fact]
	public void Buffer_Resize_SameDimensions_NoOp()
	{
		var buf = new CellBuffer(5, 3);
		buf.SetCell(2, 1, new Rune('X'), Colors.Red, Colors.Black);

		buf.Resize(5, 3);

		Assert.Equal(new Rune('X'), buf[2, 1].Character);
	}

	// ── Zero-allocation verification ────────────────────────────

	/// <summary>
	/// Test 10: Zero allocations during 10,000 SetCell + DrawString calls.
	/// </summary>
	[Fact]
	public void Buffer_ZeroAllocations_DuringDrawing()
	{
		var buf = new CellBuffer(80, 24);

		// Warmup
		for (int i = 0; i < 100; i++)
		{
			buf.SetCell(i % 80, i % 24, new Rune('X'), Colors.Red, Colors.Black);
			buf.DrawString(0, i % 24, "Hello World", Colors.White, Colors.Black);
		}

		long before = GC.GetAllocatedBytesForCurrentThread();

		for (int i = 0; i < 5000; i++)
		{
			buf.SetCell(i % 80, i % 24, new Rune('A'), Colors.Red, Colors.Black);
		}
		for (int i = 0; i < 5000; i++)
		{
			buf.DrawString(i % 70, i % 24, "Hello", Colors.Green, Colors.Black);
		}

		long after = GC.GetAllocatedBytesForCurrentThread();

		long allocated = after - before;
		Assert.True(allocated <= 256,
			$"Expected zero (or near-zero) allocations during 10,000 draw calls, but got {allocated} bytes");
	}

	/// <summary>
	/// Zero allocations during FillRect and Clear operations.
	/// </summary>
	[Fact]
	public void Buffer_ZeroAllocations_DuringFillAndClear()
	{
		var buf = new CellBuffer(80, 24);
		var cell = new TerminalCell('#', Colors.Red, Colors.Black);

		// Warmup
		for (int i = 0; i < 100; i++)
		{
			buf.FillRect(0, 0, 10, 5, cell);
			buf.Clear();
		}

		long before = GC.GetAllocatedBytesForCurrentThread();

		for (int i = 0; i < 5000; i++)
		{
			buf.FillRect(i % 70, i % 19, 10, 5, cell);
			buf.Clear();
		}

		long after = GC.GetAllocatedBytesForCurrentThread();

		long allocated = after - before;
		Assert.True(allocated <= 256,
			$"Expected zero allocations during FillRect/Clear, but got {allocated} bytes");
	}

	// ── CellAttributes ─────────────────────────────────────────

	/// <summary>
	/// CellAttributes can be combined with bitwise OR.
	/// </summary>
	[Fact]
	public void CellAttributes_BitwiseCombination_Works()
	{
		var attrs = CellAttributes.Bold | CellAttributes.Italic | CellAttributes.Underline;

		Assert.Multiple(
			() => Assert.True(attrs.HasFlag(CellAttributes.Bold)),
			() => Assert.True(attrs.HasFlag(CellAttributes.Italic)),
			() => Assert.True(attrs.HasFlag(CellAttributes.Underline)),
			() => Assert.False(attrs.HasFlag(CellAttributes.Strikethrough)));
	}

	/// <summary>
	/// CellAttributes.None has value 0.
	/// </summary>
	[Fact]
	public void CellAttributes_None_IsZero()
	{
		Assert.Equal(0, (byte)CellAttributes.None);
	}

	// ── DrawString with attributes ──────────────────────────────

	/// <summary>
	/// DrawString applies attributes to all characters.
	/// </summary>
	[Fact]
	public void Buffer_DrawString_AppliesAttributes()
	{
		var buf = new CellBuffer(10, 1);
		buf.DrawString(0, 0, "AB", Colors.White, Colors.Black, CellAttributes.Bold | CellAttributes.Italic);

		Assert.Multiple(
			() => Assert.Equal(CellAttributes.Bold | CellAttributes.Italic, buf[0, 0].Attributes),
			() => Assert.Equal(CellAttributes.Bold | CellAttributes.Italic, buf[1, 0].Attributes));
	}
}
