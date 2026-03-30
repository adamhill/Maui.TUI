#nullable enable
using Maui.TUI.Hosting;
using Maui.TUI.Platform;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;
using Serilog;
using XenoAtom.Terminal.UI;

namespace Maui.TUI.Handlers;

public partial class LayoutHandler : TuiViewHandler<Layout, TuiLayoutPanel>
{
	private static readonly ILogger Logger = Log.ForContext<LayoutHandler>();

	public static IPropertyMapper<Layout, LayoutHandler> Mapper =
		new PropertyMapper<Layout, LayoutHandler>(ViewMapper)
		{
		};

	public static CommandMapper<Layout, LayoutHandler> CommandMapper =
		new(ViewCommandMapper)
		{
			[nameof(ILayoutHandler.Add)] = MapAdd,
			[nameof(ILayoutHandler.Remove)] = MapRemove,
			[nameof(ILayoutHandler.Clear)] = MapClear,
			[nameof(ILayoutHandler.Insert)] = MapInsert,
			[nameof(ILayoutHandler.Update)] = MapUpdate,
			[nameof(ILayoutHandler.UpdateZIndex)] = MapUpdateZIndex,
		};

	public LayoutHandler() : base(Mapper, CommandMapper)
	{
	}

	public LayoutHandler(IPropertyMapper? mapper, CommandMapper? commandMapper = null)
		: base(mapper ?? Mapper, commandMapper ?? CommandMapper)
	{
	}

	protected override TuiLayoutPanel CreatePlatformView()
	{
		_ = VirtualView ?? throw new InvalidOperationException($"{nameof(VirtualView)} must be set.");

		Logger.Debug("Creating TuiLayoutPanel for {LayoutType}", VirtualView.GetType().Name);

		return new TuiLayoutPanel
		{
			CrossPlatformMeasure = VirtualView.CrossPlatformMeasure,
			CrossPlatformArrange = VirtualView.CrossPlatformArrange,
		};
	}

	public override void SetVirtualView(IView view)
	{
		base.SetVirtualView(view);

		_ = PlatformView ?? throw new InvalidOperationException($"{nameof(PlatformView)} should have been set.");
		_ = VirtualView ?? throw new InvalidOperationException($"{nameof(VirtualView)} should have been set.");
		_ = MauiContext ?? throw new InvalidOperationException($"{nameof(MauiContext)} should have been set.");

		PlatformView.CrossPlatformMeasure = VirtualView.CrossPlatformMeasure;
		PlatformView.CrossPlatformArrange = VirtualView.CrossPlatformArrange;

		var layoutType = VirtualView.GetType().Name;
		var childCount = 0;

		PlatformView.Children.Clear();
		foreach (var child in VirtualView)
		{
			var childType = child.GetType().Name;

			using (TuiLogging.PushChildContext(layoutType, childType, childCount))
			{
				Logger.Debug("Setting up child {ChildIndex}: {ChildType} in {ParentType}",
					childCount, childType, layoutType);

				var platformChild = child.ToPlatform(MauiContext);
				if (platformChild is Visual visual)
					PlatformView.Children.Add(visual);
			}

			childCount++;
		}

		Logger.Information("LayoutHandler set virtual view {LayoutType} with {ChildCount} children",
			layoutType, childCount);
	}

	public void Add(IView child)
	{
		_ = PlatformView ?? throw new InvalidOperationException($"{nameof(PlatformView)} should have been set.");
		_ = VirtualView ?? throw new InvalidOperationException($"{nameof(VirtualView)} should have been set.");
		_ = MauiContext ?? throw new InvalidOperationException($"{nameof(MauiContext)} should have been set.");

		var targetIndex = VirtualView.IndexOf(child);
		var childType = child.GetType().Name;
		var parentType = VirtualView.GetType().Name;

		using (TuiLogging.PushChildContext(parentType, childType, targetIndex))
		{
			Logger.Debug("Adding child {ChildType} at index {ChildIndex} to {ParentType}",
				childType, targetIndex, parentType);

			var platformChild = child.ToPlatform(MauiContext);
			if (platformChild is Visual visual)
				PlatformView.Children.Insert(targetIndex, visual);
		}
	}

	public void Remove(IView child)
	{
		var childType = child.GetType().Name;
		Logger.Debug("Removing child {ChildType} from layout", childType);

		if ((child.Handler?.ContainerView ?? child.Handler?.PlatformView) is Visual visual)
			PlatformView?.Children.Remove(visual);
	}

	public void Clear()
	{
		var parentType = VirtualView?.GetType().Name ?? "Unknown";
		var count = PlatformView?.Children.Count ?? 0;

		Logger.Debug("Clearing all {ChildCount} children from {ParentType}", count, parentType);

		PlatformView?.Children.Clear();
	}

	public void Insert(int index, IView child)
	{
		_ = PlatformView ?? throw new InvalidOperationException($"{nameof(PlatformView)} should have been set.");
		_ = VirtualView ?? throw new InvalidOperationException($"{nameof(VirtualView)} should have been set.");
		_ = MauiContext ?? throw new InvalidOperationException($"{nameof(MauiContext)} should have been set.");

		var targetIndex = VirtualView.IndexOf(child);
		var childType = child.GetType().Name;
		var parentType = VirtualView.GetType().Name;

		using (TuiLogging.PushChildContext(parentType, childType, targetIndex))
		{
			Logger.Debug("Inserting child {ChildType} at index {ChildIndex} into {ParentType}",
				childType, targetIndex, parentType);

			var platformChild = child.ToPlatform(MauiContext);
			if (platformChild is Visual visual)
				PlatformView.Children.Insert(targetIndex, visual);
		}
	}

	public void Update(int index, IView child)
	{
		_ = PlatformView ?? throw new InvalidOperationException($"{nameof(PlatformView)} should have been set.");
		_ = MauiContext ?? throw new InvalidOperationException($"{nameof(MauiContext)} should have been set.");

		var childType = child.GetType().Name;
		var parentType = VirtualView?.GetType().Name ?? "Unknown";

		using (TuiLogging.PushChildContext(parentType, childType, index))
		{
			Logger.Debug("Updating child at index {ChildIndex}: {ChildType} in {ParentType}",
				index, childType, parentType);

			var platformChild = child.ToPlatform(MauiContext);
			if (platformChild is Visual visual)
				PlatformView.Children[index] = visual;
		}
	}

	public void UpdateZIndex(IView child)
	{
		// Z-index reordering not needed for MVP TUI
	}

	protected override void DisconnectHandler(TuiLayoutPanel platformView)
	{
		var parentType = VirtualView?.GetType().Name ?? "Unknown";
		Logger.Debug("LayoutHandler disconnecting for {ParentType}", parentType);

		platformView.Children.Clear();
		base.DisconnectHandler(platformView);
	}

	public static void MapAdd(LayoutHandler handler, Layout layout, object? arg)
	{
		if (arg is LayoutHandlerUpdate args)
			handler.Add(args.View);
	}

	public static void MapRemove(LayoutHandler handler, Layout layout, object? arg)
	{
		if (arg is LayoutHandlerUpdate args)
			handler.Remove(args.View);
	}

	public static void MapInsert(LayoutHandler handler, Layout layout, object? arg)
	{
		if (arg is LayoutHandlerUpdate args)
			handler.Insert(args.Index, args.View);
	}

	public static void MapClear(LayoutHandler handler, Layout layout, object? arg)
	{
		handler.Clear();
	}

	static void MapUpdate(LayoutHandler handler, Layout layout, object? arg)
	{
		if (arg is LayoutHandlerUpdate args)
			handler.Update(args.Index, args.View);
	}

	static void MapUpdateZIndex(LayoutHandler handler, Layout layout, object? arg)
	{
		if (arg is IView view)
			handler.UpdateZIndex(view);
	}
}
