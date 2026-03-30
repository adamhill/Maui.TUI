#nullable enable
using Maui.TUI.Hosting;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Platform;
using Serilog;
using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Controls;

namespace Maui.TUI.Handlers;

public partial class TabbedPageHandler : TuiViewHandler<TabbedPage, TabControl>
{
	private static readonly ILogger Logger = Log.ForContext<TabbedPageHandler>();

	public static IPropertyMapper<TabbedPage, TabbedPageHandler> Mapper =
		new PropertyMapper<TabbedPage, TabbedPageHandler>(ViewMapper);

	public static CommandMapper<TabbedPage, TabbedPageHandler> CommandMapper = new(ViewCommandMapper);

	public TabbedPageHandler() : base(Mapper, CommandMapper) { }
	public TabbedPageHandler(IPropertyMapper? mapper, CommandMapper? commandMapper = null)
		: base(mapper ?? Mapper, commandMapper ?? CommandMapper) { }

	protected override TabControl CreatePlatformView()
	{
		Logger.Debug("Creating TabControl");
		return new TabControl();
	}

	public override void SetVirtualView(IView view)
	{
		base.SetVirtualView(view);
		BuildTabs();
	}

	protected override void ConnectHandler(TabControl platformView)
	{
		base.ConnectHandler(platformView);

		if (VirtualView is TabbedPage tabbedPage)
		{
			Logger.Debug("Connecting TabbedPageHandler with {TabCount} tabs", tabbedPage.Children.Count);
			tabbedPage.PagesChanged += OnPagesChanged;
		}
	}

	protected override void DisconnectHandler(TabControl platformView)
	{
		Logger.Debug("Disconnecting TabbedPageHandler");
		if (VirtualView is TabbedPage tabbedPage)
			tabbedPage.PagesChanged -= OnPagesChanged;

		base.DisconnectHandler(platformView);
	}

	void OnPagesChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
	{
		BuildTabs();
	}

	void BuildTabs()
	{
		if (PlatformView is null || VirtualView is not TabbedPage tabbedPage || MauiContext is null)
			return;

		Logger.Information("Building tabs: {TabCount} children", tabbedPage.Children.Count);

		// TabControl doesn't have a Clear/Remove, so we rebuild by creating a new one
		// For now, only build tabs on initial load
		int tabIndex = 0;
		foreach (var page in tabbedPage.Children)
		{
			var tabTitle = page.Title ?? "Tab";
			using (TuiLogging.PushChildContext("TabbedPage", page.GetType().Name, tabIndex))
			{
				Logger.Debug("Adding tab {TabIndex}: {TabTitle} ({PageType})",
					tabIndex, tabTitle, page.GetType().Name);
				var header = new TextBlock(tabTitle);
				var content = ((IView)page).ToPlatform(MauiContext);

				if (content is Visual visual)
					PlatformView.AddTab(header, visual);
			}
			tabIndex++;
		}
	}
}
