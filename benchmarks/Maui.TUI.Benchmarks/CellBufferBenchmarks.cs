using System.Text;
using BenchmarkDotNet.Attributes;
using Maui.TUI.Animation;
using Microsoft.Maui.Graphics;

namespace Maui.TUI.Benchmarks;

/// <summary>
/// Benchmarks for <see cref="CellBuffer"/> hot-path operations.
/// Validates zero-allocation drawing and cache-friendly iteration.
/// </summary>
[MemoryDiagnoser]
[ShortRunJob]
public class CellBufferBenchmarks
{
	private CellBuffer _buffer = null!;
	private CellBuffer _source = null!;

	[Params(80, 160, 320)]
	public int Width { get; set; }

	[Params(24, 48)]
	public int Height { get; set; }

	[GlobalSetup]
	public void Setup()
	{
		_buffer = new CellBuffer(Width, Height);
		_source = new CellBuffer(Width, Height);
		// Pre-fill source for copy benchmarks
		for (int r = 0; r < Height; r++)
			for (int c = 0; c < Width; c++)
				_source.SetCell(c, r, new Rune('X'), Colors.White, Colors.Black);
	}

	[Benchmark(Description = "SetCell (single)")]
	public void SetCell_Single()
	{
		_buffer.SetCell(Width / 2, Height / 2, new Rune('#'), Colors.Green, Colors.Black);
	}

	[Benchmark(Description = "SetCell (fill all)")]
	public void SetCell_FillAll()
	{
		var ch = new Rune('*');
		for (int r = 0; r < Height; r++)
			for (int c = 0; c < Width; c++)
				_buffer.SetCell(c, r, ch, Colors.White, Colors.Black);
	}

	[Benchmark(Description = "DrawString (short)")]
	public void DrawString_Short()
	{
		_buffer.DrawString(0, 0, "Hello, TUI!", Colors.Cyan, Colors.Black);
	}

	[Benchmark(Description = "DrawString (full row)")]
	public void DrawString_FullRow()
	{
		// Create a string that fills an entire row
		var text = new string('A', Width);
		_buffer.DrawString(0, 0, text, Colors.Yellow, Colors.Black);
	}

	[Benchmark(Description = "FillRect (full)")]
	public void FillRect_Full()
	{
		_buffer.FillRect(0, 0, Width, Height,
			new TerminalCell(' ', Colors.White, Colors.DarkBlue));
	}

	[Benchmark(Description = "FillRect (quarter)")]
	public void FillRect_Quarter()
	{
		_buffer.FillRect(0, 0, Width / 2, Height / 2,
			new TerminalCell(' ', Colors.White, Colors.DarkRed));
	}

	[Benchmark(Description = "Clear")]
	public void Clear()
	{
		_buffer.Clear();
	}

	[Benchmark(Description = "Clear (subregion)")]
	public void Clear_Subregion()
	{
		_buffer.Clear(10, 5, Width / 2, Height / 2);
	}

	[Benchmark(Description = "GetRow (iterate all rows)")]
	public int GetRow_AllRows()
	{
		int total = 0;
		for (int r = 0; r < Height; r++)
		{
			var row = _buffer.GetRow(r);
			total += row.Length;
		}
		return total;
	}

	[Benchmark(Description = "CopyFrom (full)")]
	public void CopyFrom_Full()
	{
		_buffer.CopyFrom(_source);
	}

	[Benchmark(Description = "CopyFrom (subregion)")]
	public void CopyFrom_Subregion()
	{
		_buffer.CopyFrom(_source, 0, 0, 10, 5, Width / 2, Height / 2);
	}

	[Benchmark(Description = "Indexer (read all)")]
	public int Indexer_ReadAll()
	{
		int count = 0;
		for (int r = 0; r < Height; r++)
			for (int c = 0; c < Width; c++)
				if (_buffer[c, r].Character.Value != 0)
					count++;
		return count;
	}

	[Benchmark(Description = "Resize (grow)")]
	public void Resize_Grow()
	{
		var buf = new CellBuffer(40, 12);
		buf.Resize(Width, Height);
	}

	[Benchmark(Description = "Resize (shrink)")]
	public void Resize_Shrink()
	{
		var buf = new CellBuffer(Width, Height);
		buf.Resize(40, 12);
	}
}
