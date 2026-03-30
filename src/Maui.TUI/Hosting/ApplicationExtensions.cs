using Maui.TUI.Platform;
using Microsoft.Maui.Handlers;
using Serilog;
using XenoAtom.Terminal.UI;

namespace Maui.TUI.Hosting;

public static class ApplicationExtensions
{
	private static readonly ILogger Logger = Log.ForContext(typeof(ApplicationExtensions));

	public static void CreatePlatformWindow(MauiTuiApplication tuiApp, IApplication application)
	{
		if (application.Handler?.MauiContext is not IMauiContext applicationContext)
		{
			Logger.Error("Cannot create platform window: application handler MauiContext is null");
			return;
		}

		Logger.Information("Creating platform window for {AppType}", application.GetType().Name);

		var windowRoot = new TuiWindowRootContainer();
		var mauiContext = applicationContext.MakeWindowScope(windowRoot, out _);

		var activationState = new ActivationState(mauiContext);
		var window = application.CreateWindow(activationState);

		Logger.Debug("Window created: {WindowType}", window.GetType().Name);

		var windowHandler = new Maui.TUI.Handlers.WindowHandler();
		windowHandler.SetMauiContext(mauiContext);
		windowHandler.SetVirtualView(window);

		// Get the platform view created by the handler and pass it to the TUI app
		if (windowHandler.PlatformView is TuiWindowRootContainer container)
		{
			tuiApp.SetWindowRoot(container);
			Logger.Information("Platform window root container set on TUI application");
		}
		else
		{
			Logger.Warning("WindowHandler.PlatformView is not TuiWindowRootContainer: {ActualType}",
				windowHandler.PlatformView?.GetType().Name ?? "null");
		}
	}

	internal static void SetApplicationHandler(this MauiTuiApplication tuiApp, IApplication application, IMauiContext applicationContext)
	{
		Logger.Debug("Setting application handler for {AppType}", application.GetType().Name);

		var appHandler = new Maui.TUI.Handlers.ApplicationHandler();
		appHandler.SetMauiContext(applicationContext);
		appHandler.SetVirtualView(application);
	}
}
