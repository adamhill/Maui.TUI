using Maui.TUI;
using Maui.TUI.Sample;
using Serilog;

var app = new MauiTuiSampleApp();

if (args.Contains("--dump"))
{
	var rootPanel = app.Initialize();
	var tree = MauiTuiApplication.GetVisualTreeString(rootPanel);
	Log.Information("Visual tree dump:\n{VisualTree}", tree);
	MauiTuiApplication.DumpVisualTree(rootPanel, 3);
}
else if (args.Contains("--svg"))
{
	var svg = app.RenderSvg(80, 24);
	Log.Information("SVG render ({Width}x{Height}, {Bytes} bytes):\n{Svg}", 80, 24, svg.Length, svg);
	Console.WriteLine(svg);
}
else
{
	Log.Information("Running in interactive TUI mode");
	app.Run();
}
