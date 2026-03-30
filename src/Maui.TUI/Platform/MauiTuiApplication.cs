using Maui.TUI.Hosting;
using Maui.TUI.Platform;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Animations;
using Microsoft.Maui.Hosting;
using Serilog;
using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Controls;
using XenoAtom.Terminal.UI.Layout;
using XenoAtom.Terminal.UI.Rendering;

namespace Maui.TUI;

/// <summary>
/// Base class for .NET MAUI TUI applications. 
/// Subclass and implement CreateMauiApp() to configure handlers and services.
/// Call Run() to start the fullscreen terminal application.
/// </summary>
public abstract class MauiTuiApplication : IPlatformApplication
{
	private static readonly ILogger Logger = Log.ForContext<MauiTuiApplication>();

	TerminalApp? _terminalApp;
	Panel _rootPanel = new VStack();
	TuiWindowRootContainer? _windowRoot;

	protected abstract MauiApp CreateMauiApp();

	public IServiceProvider Services { get; protected set; } = null!;
	public IApplication Application { get; protected set; } = null!;

	internal void SetWindowRoot(TuiWindowRootContainer windowRoot)
	{
		_windowRoot = windowRoot;
		_rootPanel.Children.Add(windowRoot);
	}

	/// <summary>
	/// Initializes the MAUI app and builds the visual tree without running the terminal loop.
	/// Useful for testing and diagnostics.
	/// </summary>
	public Panel Initialize()
	{
		Logger.Information("Initializing MAUI TUI application");

		TuiDispatcher.SetUIThread();
		IPlatformApplication.Current = this;

		var mauiApp = CreateMauiApp();
		var rootContext = new TuiMauiContext(mauiApp.Services);
		var applicationContext = rootContext.MakeApplicationScope(this);

		Services = applicationContext.Services;
		Application = Services.GetRequiredService<IApplication>();

		this.SetApplicationHandler(Application, applicationContext);

		// Create the platform window (triggers MAUI to create Window + Page + content)
		ApplicationExtensions.CreatePlatformWindow(this, Application);

		_rootPanel.VerticalAlignment = Align.Stretch;
		_rootPanel.HorizontalAlignment = Align.Stretch;

		Logger.Information("MAUI TUI application initialized successfully");

		return _rootPanel;
	}

	public void Run()
	{
		Logger.Information("Starting MAUI TUI application run loop");

		var rootPanel = Initialize();

		// Resolve the TuiTicker so we can wire it to the TerminalApp
		var ticker = Services.GetService<TuiTicker>();

		// Ensure Ctrl+C is passed as input rather than handled as SIGINT
		var terminal = XenoAtom.Terminal.Terminal.Instance;
		terminal.Options.TreatControlCAsInput = true;

		// Create the terminal app with the fully-built visual tree
		_terminalApp = new TerminalApp(rootPanel, terminal,
			new TerminalAppOptions
			{
				HostKind = XenoAtom.Terminal.UI.Hosting.TerminalHostKind.Fullscreen,
				// Use Ctrl+Q as exit gesture (Ctrl+C is unreliable across terminals)
				ExitGesture = new XenoAtom.Terminal.UI.Input.KeyGesture(
					'q', XenoAtom.Terminal.TerminalModifiers.Ctrl),
			});

		Logger.Debug("TerminalApp created with fullscreen mode, Ctrl+Q exit gesture");

		// Also try to handle Ctrl+C via Console.CancelKeyPress as fallback
		Console.CancelKeyPress += (s, e) =>
		{
			e.Cancel = true;
			Logger.Information("Ctrl+C received, stopping application");
			_terminalApp?.Stop();
		};

		// Wire up dispatcher to use TerminalApp.Post() for background thread dispatch
		TuiDispatcher.SetTerminalApp(_terminalApp);

		// Wire the TuiTicker to the TerminalApp so timer callbacks
		// are marshaled to the UI thread
		if (ticker is not null)
		{
			ticker.TerminalApp = _terminalApp;
			Logger.Debug("TuiTicker wired to TerminalApp at {MaxFps} fps", ticker.MaxFps);
		}

		Logger.Information("Entering terminal run loop");

		// Run the terminal app loop (blocks until exit)
		_terminalApp.Run();

		Logger.Information("Terminal run loop exited");

		// Clean up ticker when the app exits
		if (ticker is not null)
		{
			ticker.Stop();
			ticker.TerminalApp = null;
			Logger.Debug("TuiTicker stopped and disconnected");
		}

		Log.CloseAndFlush();
	}

	/// <summary>
	/// Renders the current visual tree to SVG without starting the terminal loop.
	/// Useful for testing and diagnostics.
	/// </summary>
	public string RenderSvg(int width = 80, int height = 24)
	{
		var rootPanel = Initialize();
		return TerminalAppSnapshotRenderer.RenderSvg(rootPanel, width: width, height: height);
	}

	/// <summary>
	/// Dumps the visual tree to the console (stderr) for debugging.
	/// </summary>
	public static void DumpVisualTree(Visual visual, int depth = 0)
	{
		var sb = new System.Text.StringBuilder();
		BuildVisualTreeString(visual, sb, depth);
		Console.Error.Write(sb);
	}

	/// <summary>
	/// Returns the visual tree as a formatted string.
	/// Useful for logging or assertions in tests.
	/// </summary>
	public static string GetVisualTreeString(Visual visual)
	{
		var sb = new System.Text.StringBuilder();
		BuildVisualTreeString(visual, sb, depth: 0);
		return sb.ToString();
	}

	private static void BuildVisualTreeString(Visual visual, System.Text.StringBuilder sb, int depth)
	{
		var indent = new string(' ', depth * 2);
		var name = visual.GetType().Name;
		var extra = visual is TextBlock tb ? $" Text=\"{tb.Text}\"" : "";
		sb.AppendLine($"{indent}{name}{extra}");

		if (visual is Panel panel)
		{
			foreach (var child in panel.Children)
				BuildVisualTreeString(child, sb, depth + 1);
		}
		else if (visual is ContentVisual cv && cv.Content is not null)
		{
			BuildVisualTreeString(cv.Content, sb, depth + 1);
		}
		else if (visual is ScrollViewer sv && sv.Content is not null)
		{
			BuildVisualTreeString(sv.Content, sb, depth + 1);
		}
		else if (visual is TabControl tc)
		{
			foreach (var tab in tc.Tabs)
			{
				sb.AppendLine($"{indent}  TabPage");
				if (tab.Header is not null)
					BuildVisualTreeString(tab.Header, sb, depth + 2);
				if (tab.Content is not null)
					BuildVisualTreeString(tab.Content, sb, depth + 2);
			}
		}
	}
}
