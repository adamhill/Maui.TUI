using BenchmarkDotNet.Attributes;
using Maui.TUI.Animation;
using Microsoft.Maui.Graphics;

namespace Maui.TUI.Benchmarks;

/// <summary>
/// Benchmarks for <see cref="TerminalCell"/> struct operations.
/// Validates that the struct layout stays allocation-free and equality is fast.
/// </summary>
[MemoryDiagnoser]
[ShortRunJob]
public class TerminalCellBenchmarks
{
	private TerminalCell _cellA;
	private TerminalCell _cellB;
	private TerminalCell[] _cells = null!;

	[GlobalSetup]
	public void Setup()
	{
		_cellA = new TerminalCell('A', Colors.White, Colors.Black, CellAttributes.Bold);
		_cellB = new TerminalCell('B', Colors.Red, Colors.Blue, CellAttributes.Italic);
		_cells = new TerminalCell[1920]; // 80x24
		Array.Fill(_cells, TerminalCell.Empty);
	}

	[Benchmark(Description = "Construct (char)")]
	public TerminalCell Construct_Char()
	{
		return new TerminalCell('X', Colors.Green, Colors.Black, CellAttributes.Bold);
	}

	[Benchmark(Description = "Construct (Rune)")]
	public TerminalCell Construct_Rune()
	{
		return new TerminalCell(new System.Text.Rune('X'), Colors.Green, Colors.Black, CellAttributes.Bold);
	}

	[Benchmark(Description = "Equals (same)")]
	public bool Equals_Same()
	{
		return _cellA.Equals(_cellA);
	}

	[Benchmark(Description = "Equals (different)")]
	public bool Equals_Different()
	{
		return _cellA.Equals(_cellB);
	}

	[Benchmark(Description = "GetHashCode")]
	public int GetHashCode_Cell()
	{
		return _cellA.GetHashCode();
	}

	[Benchmark(Description = "IsEmpty check")]
	public bool IsEmpty_Check()
	{
		return TerminalCell.Empty.IsEmpty;
	}

	[Benchmark(Description = "Array.Fill (1920 cells)")]
	public void ArrayFill()
	{
		Array.Fill(_cells, TerminalCell.Empty);
	}

	[Benchmark(Description = "Span.Fill (1920 cells)")]
	public void SpanFill()
	{
		_cells.AsSpan().Fill(TerminalCell.Empty);
	}
}
