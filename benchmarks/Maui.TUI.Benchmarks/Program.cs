using BenchmarkDotNet.Running;
using Maui.TUI.Benchmarks;

BenchmarkSwitcher
	.FromAssembly(typeof(CellBufferBenchmarks).Assembly)
	.Run(args);
