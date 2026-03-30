#nullable enable
using Maui.TUI.Hosting;
using Maui.TUI.Platform;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;
using Serilog;
using XenoAtom.Terminal.UI;

namespace Maui.TUI.Handlers;

public partial class ContentViewHandler : TuiViewHandler<IContentView, TuiContentPanel>
{
	private static readonly ILogger Logger = Log.ForContext<ContentViewHandler>();

	public static IPropertyMapper<IContentView, ContentViewHandler> Mapper =
		new PropertyMapper<IContentView, ContentViewHandler>(ViewMapper)
		{
			[nameof(IContentView.Content)] = MapContent
		};

	public static CommandMapper<IContentView, ContentViewHandler> CommandMapper =
		new(ViewCommandMapper);

	public ContentViewHandler() : base(Mapper, CommandMapper)
	{
	}

	public ContentViewHandler(IPropertyMapper? mapper)
		: base(mapper ?? Mapper, CommandMapper)
	{
	}

	public ContentViewHandler(IPropertyMapper? mapper, CommandMapper? commandMapper)
		: base(mapper ?? Mapper, commandMapper ?? CommandMapper)
	{
	}

	protected override TuiContentPanel CreatePlatformView()
	{
		_ = VirtualView ?? throw new InvalidOperationException($"{nameof(VirtualView)} must be set.");
		Logger.Debug("Creating TuiContentPanel for {ViewType}", VirtualView.GetType().Name);
		return new TuiContentPanel
		{
			CrossPlatformMeasure = VirtualView.CrossPlatformMeasure,
			CrossPlatformArrange = VirtualView.CrossPlatformArrange
		};
	}

	public override void SetVirtualView(IView view)
	{
		base.SetVirtualView(view);

		_ = PlatformView ?? throw new InvalidOperationException($"{nameof(PlatformView)} should have been set.");
		_ = VirtualView ?? throw new InvalidOperationException($"{nameof(VirtualView)} should have been set.");

		PlatformView.CrossPlatformMeasure = VirtualView.CrossPlatformMeasure;
		PlatformView.CrossPlatformArrange = VirtualView.CrossPlatformArrange;
	}

	public static void MapContent(ContentViewHandler handler, IContentView page)
	{
		_ = handler.PlatformView ?? throw new InvalidOperationException($"{nameof(PlatformView)} should have been set.");
		_ = handler.VirtualView ?? throw new InvalidOperationException($"{nameof(VirtualView)} should have been set.");
		_ = handler.MauiContext ?? throw new InvalidOperationException($"{nameof(MauiContext)} should have been set.");

		handler.PlatformView.Children.Clear();

		if (handler.VirtualView.PresentedContent is IView view)
		{
			var contentType = view.GetType().Name;
			using (TuiLogging.PushChildContext("ContentView", contentType, 0))
			{
				Logger.Debug("Mapping content: {ContentType}", contentType);
				var platformView = view.ToPlatform(handler.MauiContext);
				if (platformView is Visual visual)
					handler.PlatformView.Children.Add(visual);
			}
		}
	}
}
