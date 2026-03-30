#nullable enable
using Maui.TUI.Hosting;
using Maui.TUI.Platform;
using Microsoft.Maui.Platform;
using Serilog;
using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Controls;

namespace Maui.TUI.Handlers;

/// <summary>
/// NavigationPage handler that uses a simple content-swap approach.
/// Pushes replace the visible content; pops restore the previous page.
/// </summary>
public partial class NavigationPageHandler : TuiViewHandler<IStackNavigationView, TuiNavigationContainer>
{
	private static readonly ILogger Logger = Log.ForContext<NavigationPageHandler>();

	public static IPropertyMapper<IStackNavigationView, NavigationPageHandler> Mapper =
		new PropertyMapper<IStackNavigationView, NavigationPageHandler>(ViewMapper);

	public static CommandMapper<IStackNavigationView, NavigationPageHandler> CommandMapper =
		new(ViewCommandMapper)
		{
			[nameof(IStackNavigation.RequestNavigation)] = MapRequestNavigation,
		};

	public NavigationPageHandler() : base(Mapper, CommandMapper) { }
	public NavigationPageHandler(IPropertyMapper? mapper, CommandMapper? commandMapper = null)
		: base(mapper ?? Mapper, commandMapper ?? CommandMapper) { }

	protected override TuiNavigationContainer CreatePlatformView()
	{
		Logger.Debug("Creating TuiNavigationContainer");
		return new TuiNavigationContainer();
	}

	static void MapRequestNavigation(NavigationPageHandler handler, IStackNavigationView view, object? args)
	{
		if (args is not NavigationRequest request)
			return;

		handler.HandleNavigation(request);
	}

	void HandleNavigation(NavigationRequest request)
	{
		if (MauiContext is null || VirtualView is null)
			return;

		var newStack = request.NavigationStack;
		var operation = request.Animated ? "AnimatedNavigation" : "Navigation";

		using (TuiLogging.PushNavigationContext(nameof(NavigationPageHandler), operation, newStack.Count))
		{
			Logger.Information("Navigation requested: stack depth {StackDepth}, animated={IsAnimated}",
				newStack.Count, request.Animated);

			PlatformView.Children.Clear();

			// Show the top of the navigation stack
			if (newStack.Count > 0)
			{
				var topPage = newStack[newStack.Count - 1];
				var pageType = topPage.GetType().Name;

				using (TuiLogging.PushChildContext("NavigationStack", pageType, newStack.Count - 1))
				{
					Logger.Debug("Navigating to top page: {PageType} (stack position {Position}/{Total})",
						pageType, newStack.Count - 1, newStack.Count);

					var platformView = topPage.ToPlatform(MauiContext);
					if (platformView is Visual visual)
						PlatformView.Children.Add(visual);
				}

				// Log the full stack for diagnostic purposes
				for (int i = 0; i < newStack.Count; i++)
				{
					Logger.Verbose("  Stack[{Index}]: {PageType}", i, newStack[i].GetType().Name);
				}
			}

			// Tell MAUI navigation is complete
			VirtualView.NavigationFinished(newStack);
			Logger.Debug("Navigation completed, MAUI notified");
		}
	}
}
