#nullable enable
using Maui.TUI.Hosting;
using Maui.TUI.Platform;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;
using Serilog;
using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Layout;

namespace Maui.TUI.Handlers;

public partial class WindowHandler : ElementHandler<IWindow, TuiWindowRootContainer>
{
	private static readonly ILogger Logger = Log.ForContext<WindowHandler>();

	public static IPropertyMapper<IWindow, WindowHandler> Mapper =
		new PropertyMapper<IWindow, WindowHandler>(ElementHandler.ElementMapper)
		{
			[nameof(IWindow.Content)] = MapContent,
		};

	public static CommandMapper<IWindow, IWindowHandler> CommandMapper =
		new(ElementCommandMapper)
		{
		};

	public WindowHandler()
		: base(Mapper, CommandMapper)
	{
	}

	public WindowHandler(IPropertyMapper? mapper)
		: base(mapper ?? Mapper, CommandMapper)
	{
	}

	public WindowHandler(IPropertyMapper? mapper, CommandMapper? commandMapper)
		: base(mapper ?? Mapper, commandMapper ?? CommandMapper)
	{
	}

	protected override TuiWindowRootContainer CreatePlatformElement()
	{
		Logger.Debug("Creating TuiWindowRootContainer platform element");

		// Try getting from services first, otherwise create new
		return MauiContext?.Services.GetService<TuiWindowRootContainer>()
			?? new TuiWindowRootContainer();
	}

	protected override void ConnectHandler(TuiWindowRootContainer platformView)
	{
		base.ConnectHandler(platformView);

		if (VirtualView is Microsoft.Maui.Controls.Window window)
		{
			window.ModalPushed += OnModalPushed;
			window.ModalPopped += OnModalPopped;
			Logger.Information("WindowHandler connected, modal events subscribed");
		}
	}

	protected override void DisconnectHandler(TuiWindowRootContainer platformView)
	{
		if (VirtualView is Microsoft.Maui.Controls.Window window)
		{
			window.ModalPushed -= OnModalPushed;
			window.ModalPopped -= OnModalPopped;
			Logger.Information("WindowHandler disconnecting, modal events unsubscribed");
		}

		base.DisconnectHandler(platformView);
	}

	void OnModalPushed(object? sender, ModalPushedEventArgs e)
	{
		if (MauiContext is null)
			return;

		var modalType = e.Modal.GetType().Name;
		Logger.Information("Modal pushed: {ModalType}", modalType);

		var platformModal = e.Modal.ToPlatform(MauiContext);
		if (platformModal is Visual visual)
		{
			visual.HorizontalAlignment = Align.Stretch;
			visual.VerticalAlignment = Align.Stretch;
			PlatformView.PushModal(visual);

			Logger.Debug("Modal {ModalType} added to window root as {VisualType}",
				modalType, visual.GetType().Name);
		}
	}

	void OnModalPopped(object? sender, ModalPoppedEventArgs e)
	{
		var modalType = e.Modal.GetType().Name;
		Logger.Information("Modal popped: {ModalType}", modalType);

		PlatformView.PopModal();
	}

	public static void MapContent(WindowHandler handler, IWindow window)
	{
		_ = handler.MauiContext ?? throw new InvalidOperationException($"{nameof(MauiContext)} should have been set by base class.");

		if (handler.PlatformView is TuiWindowRootContainer container && window.Content is not null)
		{
			var contentType = window.Content.GetType().Name;

			using (TuiLogging.PushChildContext("Window", contentType, 0))
			{
				Logger.Information("Mapping window content: {ContentType}", contentType);

				var platformContent = window.Content.ToPlatform(handler.MauiContext);
				if (platformContent is Visual visual)
				{
					container.SetPage(visual);
					Logger.Debug("Window content set to {VisualType}", visual.GetType().Name);
				}
			}
		}
	}
}
