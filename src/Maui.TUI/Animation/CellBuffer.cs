#nullable enable
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.Maui.Graphics;
using Serilog;

namespace Maui.TUI.Animation;

/// <summary>
/// A fixed-size 2D grid of <see cref="TerminalCell"/> values backed by a single
/// flat array in row-major order.
/// </summary>
/// <remarks>
/// <para>
/// The flat layout is cache-friendly and Span-sliceable. All drawing operations
/// silently clip at buffer boundaries — no exceptions for out-of-bounds coordinates,
/// matching GPU rasterizer semantics.
/// </para>
/// <para>
/// This type is designed for zero-allocation drawing in animation hot paths.
/// The backing array is allocated once and reused. <see cref="Resize"/> only
/// allocates when the new size exceeds the current capacity.
/// </para>
/// </remarks>
public sealed class CellBuffer
{
	private static readonly ILogger Logger = Log.ForContext<CellBuffer>();

	private TerminalCell[] _cells;
	private bool _fromPool;

	/// <summary>Width of the buffer in columns.</summary>
	public int Width { get; private set; }

	/// <summary>Height of the buffer in rows.</summary>
	public int Height { get; private set; }

	/// <summary>
	/// Creates a new cell buffer with the specified dimensions, filled with
	/// <see cref="TerminalCell.Empty"/>.
	/// </summary>
	/// <param name="width">Width in columns. Must be &gt; 0.</param>
	/// <param name="height">Height in rows. Must be &gt; 0.</param>
	/// <exception cref="ArgumentOutOfRangeException">
	/// <paramref name="width"/> or <paramref name="height"/> is &lt;= 0.
	/// </exception>
	public CellBuffer(int width, int height)
	{
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

		Width = width;
		Height = height;
		_cells = new TerminalCell[width * height];
		_cells.AsSpan().Fill(TerminalCell.Empty);

		Logger.Debug("CellBuffer created: {Width}x{Height} ({CellCount} cells, {ByteSize} bytes)",
			width, height, width * height, width * height * Unsafe.SizeOf<TerminalCell>());
	}

	/// <summary>
	/// Gets a reference to the cell at the specified column and row.
	/// </summary>
	/// <remarks>
	/// Returns a <see langword="ref"/> for zero-copy mutation. Callers can write
	/// directly: <c>buffer[col, row] = new TerminalCell(...);</c>
	/// Out-of-bounds access returns a ref to a static discard cell.
	/// </remarks>
	public ref TerminalCell this[int col, int row]
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get
		{
			if ((uint)col >= (uint)Width || (uint)row >= (uint)Height)
				return ref _discardCell;
			return ref _cells[row * Width + col];
		}
	}

	// Static discard cell for out-of-bounds ref returns.
	// Writes to this are harmlessly lost.
	private static TerminalCell _discardCell;

	/// <summary>
	/// Sets a cell at the specified position. Out-of-bounds writes are silently ignored.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void SetCell(int col, int row, Rune ch, Color fg, Color bg, CellAttributes attributes = CellAttributes.None)
	{
		if ((uint)col >= (uint)Width || (uint)row >= (uint)Height)
			return;
		_cells[row * Width + col] = new TerminalCell(ch, fg, bg, attributes);
	}

	/// <summary>
	/// Sets a cell with a transparent background.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void SetCell(int col, int row, Rune ch, Color fg)
	{
		SetCell(col, row, ch, fg, Colors.Transparent);
	}

	/// <summary>
	/// Draws a string starting at the specified position. Characters that extend
	/// beyond the buffer boundary are silently clipped.
	/// </summary>
	/// <param name="col">Starting column.</param>
	/// <param name="row">Starting row.</param>
	/// <param name="text">Text to draw.</param>
	/// <param name="fg">Foreground color for all characters.</param>
	/// <param name="bg">Background color for all characters.</param>
	/// <param name="attributes">Text attributes for all characters.</param>
	public void DrawString(int col, int row, ReadOnlySpan<char> text, Color fg, Color bg, CellAttributes attributes = CellAttributes.None)
	{
		// Row out of bounds — nothing to draw
		if ((uint)row >= (uint)Height)
			return;

		int x = col;
		var enumerator = text.EnumerateRunes();
		foreach (var rune in enumerator)
		{
			if (x >= Width)
				break;
			if (x >= 0)
				_cells[row * Width + x] = new TerminalCell(rune, fg, bg, attributes);
			x++;
		}
	}

	/// <summary>
	/// Draws a string with a transparent background.
	/// </summary>
	public void DrawString(int col, int row, ReadOnlySpan<char> text, Color fg)
	{
		DrawString(col, row, text, fg, Colors.Transparent);
	}

	/// <summary>
	/// Fills a rectangular region with the specified cell value.
	/// The region is clipped to the buffer boundaries.
	/// </summary>
	public void FillRect(int col, int row, int width, int height, TerminalCell cell)
	{
		// Clip to buffer bounds
		int startCol = Math.Max(col, 0);
		int startRow = Math.Max(row, 0);
		int endCol = Math.Min(col + width, Width);
		int endRow = Math.Min(row + height, Height);

		for (int r = startRow; r < endRow; r++)
		{
			var rowSpan = _cells.AsSpan(r * Width + startCol, endCol - startCol);
			rowSpan.Fill(cell);
		}
	}

	/// <summary>
	/// Clears the entire buffer, filling all cells with <see cref="TerminalCell.Empty"/>.
	/// </summary>
	public void Clear()
	{
		_cells.AsSpan(0, Width * Height).Fill(TerminalCell.Empty);
	}

	/// <summary>
	/// Clears a rectangular subregion, filling it with <see cref="TerminalCell.Empty"/>.
	/// The region is clipped to the buffer boundaries.
	/// </summary>
	public void Clear(int col, int row, int width, int height)
	{
		FillRect(col, row, width, height, TerminalCell.Empty);
	}

	/// <summary>
	/// Copies the entire contents of <paramref name="source"/> into this buffer.
	/// The source must have the same dimensions.
	/// </summary>
	/// <exception cref="ArgumentException">
	/// Source dimensions do not match this buffer.
	/// </exception>
	public void CopyFrom(CellBuffer source)
	{
		ArgumentNullException.ThrowIfNull(source);
		if (source.Width != Width || source.Height != Height)
			throw new ArgumentException(
				$"Source dimensions ({source.Width}x{source.Height}) must match destination ({Width}x{Height}).");

		source._cells.AsSpan(0, Width * Height).CopyTo(_cells.AsSpan());
	}

	/// <summary>
	/// Copies a rectangular region from <paramref name="source"/> into this buffer.
	/// Both source and destination regions are clipped to their respective buffer boundaries.
	/// </summary>
	public void CopyFrom(CellBuffer source, int srcCol, int srcRow, int destCol, int destRow, int width, int height)
	{
		ArgumentNullException.ThrowIfNull(source);

		// Clip source region
		int sCol = Math.Max(srcCol, 0);
		int sRow = Math.Max(srcRow, 0);
		int sEndCol = Math.Min(srcCol + width, source.Width);
		int sEndRow = Math.Min(srcRow + height, source.Height);

		// Adjust dest to account for source clipping
		int dCol = destCol + (sCol - srcCol);
		int dRow = destRow + (sRow - srcRow);

		// Clip destination region
		int effectiveWidth = sEndCol - sCol;
		int effectiveHeight = sEndRow - sRow;

		if (dCol < 0)
		{
			int clip = -dCol;
			sCol += clip;
			dCol = 0;
			effectiveWidth -= clip;
		}
		if (dRow < 0)
		{
			int clip = -dRow;
			sRow += clip;
			dRow = 0;
			effectiveHeight -= clip;
		}

		effectiveWidth = Math.Min(effectiveWidth, Width - dCol);
		effectiveHeight = Math.Min(effectiveHeight, Height - dRow);

		if (effectiveWidth <= 0 || effectiveHeight <= 0)
			return;

		for (int r = 0; r < effectiveHeight; r++)
		{
			var srcSpan = source._cells.AsSpan((sRow + r) * source.Width + sCol, effectiveWidth);
			var dstSpan = _cells.AsSpan((dRow + r) * Width + dCol, effectiveWidth);
			srcSpan.CopyTo(dstSpan);
		}
	}

	/// <summary>
	/// Returns a span over one row for bulk operations.
	/// </summary>
	/// <param name="row">Row index (0-based).</param>
	/// <returns>A span of <see cref="TerminalCell"/> values for the row, or an empty span if out of bounds.</returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public Span<TerminalCell> GetRow(int row)
	{
		if ((uint)row >= (uint)Height)
			return Span<TerminalCell>.Empty;
		return _cells.AsSpan(row * Width, Width);
	}

	/// <summary>
	/// Resizes the buffer. Existing content that fits within the new dimensions is preserved.
	/// New cells are filled with <see cref="TerminalCell.Empty"/>.
	/// </summary>
	/// <param name="newWidth">New width. Must be &gt; 0.</param>
	/// <param name="newHeight">New height. Must be &gt; 0.</param>
	public void Resize(int newWidth, int newHeight)
	{
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(newWidth);
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(newHeight);

		if (newWidth == Width && newHeight == Height)
			return;

		Logger.Debug("CellBuffer resizing: {OldWidth}x{OldHeight} → {NewWidth}x{NewHeight}",
			Width, Height, newWidth, newHeight);

		int newLength = newWidth * newHeight;
		int copyWidth = Math.Min(Width, newWidth);
		int copyHeight = Math.Min(Height, newHeight);

		if (newLength > _cells.Length)
		{
			// Need a larger array — allocate from pool, copy rows, return old
			Logger.Debug("CellBuffer pool allocation: {OldCapacity} → {NewCapacity} cells (growth)",
				_cells.Length, newLength);
			var newCells = ArrayPool<TerminalCell>.Shared.Rent(newLength);
			newCells.AsSpan(0, newLength).Fill(TerminalCell.Empty);

			for (int r = 0; r < copyHeight; r++)
			{
				_cells.AsSpan(r * Width, copyWidth)
					.CopyTo(newCells.AsSpan(r * newWidth, copyWidth));
			}

			if (_fromPool)
				ArrayPool<TerminalCell>.Shared.Return(_cells);

			_cells = newCells;
			_fromPool = true;
		}
		else
		{
			// Reuse same array — reindex rows in place.
			// When width shrinks, rows shift left: copy top-to-bottom (no overlap issue).
			// When width grows, rows shift right: copy bottom-to-top to avoid overwriting.
			if (newWidth <= Width)
			{
				for (int r = 0; r < copyHeight; r++)
				{
					_cells.AsSpan(r * Width, copyWidth)
						.CopyTo(_cells.AsSpan(r * newWidth, copyWidth));
				}
			}
			else
			{
				for (int r = copyHeight - 1; r >= 0; r--)
				{
					_cells.AsSpan(r * Width, copyWidth)
						.CopyTo(_cells.AsSpan(r * newWidth, copyWidth));
					// Fill the extra columns
					_cells.AsSpan(r * newWidth + copyWidth, newWidth - copyWidth)
						.Fill(TerminalCell.Empty);
				}
			}

			// Clear any new rows beyond the old height
			if (newHeight > copyHeight)
			{
				_cells.AsSpan(copyHeight * newWidth, (newHeight - copyHeight) * newWidth)
					.Fill(TerminalCell.Empty);
			}
		}

		Width = newWidth;
		Height = newHeight;
	}
}
