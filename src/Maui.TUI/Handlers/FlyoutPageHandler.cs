#nullable enable
using Maui.TUI.Hosting;
using Maui.TUI.Platform;
using Microsoft.Maui.Platform;
using Serilog;
using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Controls;
using XenoAtom.Terminal.UI.Layout;
using TuiGrid = XenoAtom.Terminal.UI.Controls.Grid;
using TuiBorder = XenoAtom.Terminal.UI.Controls.Border;
using TuiGridLength = XenoAtom.Terminal.UI.Controls.GridLength;
using TuiColumnDefinition = XenoAtom.Terminal.UI.Controls.ColumnDefinition;
using TuiRowDefinition = XenoAtom.Terminal.UI.Controls.RowDefinition;

namespace Maui.TUI.Handlers;

/// <summary>
/// Handler for FlyoutPage → TUI HSplitter with flyout panel on the left and detail on the right.
/// </summary>
public partial class FlyoutPageHandler : TuiViewHandler<IFlyoutView, TuiGrid>
{
	private static readonly ILogger Logger = Log.ForContext<FlyoutPageHandler>();

	public static IPropertyMapper<IFlyoutView, FlyoutPageHandler> Mapper =
		new PropertyMapper<IFlyoutView, FlyoutPageHandler>(ViewMapper)
		{
			[nameof(IFlyoutView.Flyout)] = MapFlyout,
			[nameof(IFlyoutView.Detail)] = MapDetail,
			[nameof(IFlyoutView.IsPresented)] = MapIsPresented,
			[nameof(IFlyoutView.FlyoutWidth)] = MapFlyoutWidth,
		};

	public static CommandMapper<IFlyoutView, FlyoutPageHandler> CommandMapper =
		new(ViewCommandMapper);

	GridCell? _flyoutCell;
	GridCell? _separatorCell;
	GridCell? _detailCell;

	public FlyoutPageHandler() : base(Mapper, CommandMapper) { }
	public FlyoutPageHandler(IPropertyMapper? mapper, CommandMapper? commandMapper = null)
		: base(mapper ?? Mapper, commandMapper ?? CommandMapper) { }

	protected override TuiGrid CreatePlatformView()
	{
		Logger.Debug("Creating FlyoutPage grid layout");
		var grid = new TuiGrid
		{
			HorizontalAlignment = Align.Stretch,
			VerticalAlignment = Align.Stretch,
		};

		// Column 0: Flyout (fixed width)
		grid.ColumnDefinitions.Add(new TuiColumnDefinition { Width = TuiGridLength.Fixed(30) });
		// Column 1: Separator (1 char)
		grid.ColumnDefinitions.Add(new TuiColumnDefinition { Width = TuiGridLength.Fixed(1) });
		// Column 2: Detail (stretch)
		grid.ColumnDefinitions.Add(new TuiColumnDefinition { Width = TuiGridLength.Star(1) });

		grid.RowDefinitions.Add(new TuiRowDefinition { Height = TuiGridLength.Star(1) });

		_flyoutCell = new GridCell { Row = 0, Column = 0, VerticalAlignment = Align.Stretch, HorizontalAlignment = Align.Stretch };
		_separatorCell = new GridCell(new TextBlock("│") { VerticalAlignment = Align.Stretch }) { Row = 0, Column = 1 };
		_detailCell = new GridCell { Row = 0, Column = 2, VerticalAlignment = Align.Stretch, HorizontalAlignment = Align.Stretch };

		grid.Cells.Add(_flyoutCell);
		grid.Cells.Add(_separatorCell);
		grid.Cells.Add(_detailCell);

		return grid;
	}

	static void MapFlyout(FlyoutPageHandler handler, IFlyoutView view)
	{
		if (handler.MauiContext is null || handler._flyoutCell is null)
			return;

		if (view.Flyout is IView flyoutView)
		{
			var flyoutType = flyoutView.GetType().Name;
			using (TuiLogging.PushChildContext("FlyoutPage", flyoutType, 0))
			{
				Logger.Debug("Mapping flyout panel: {FlyoutType}", flyoutType);
				var platformFlyout = flyoutView.ToPlatform(handler.MauiContext);
				if (platformFlyout is Visual visual)
				{
					visual.HorizontalAlignment = Align.Stretch;
					visual.VerticalAlignment = Align.Stretch;
					handler._flyoutCell.Content = visual;
				}
			}
		}
	}

	static void MapDetail(FlyoutPageHandler handler, IFlyoutView view)
	{
		if (handler.MauiContext is null || handler._detailCell is null)
			return;

		if (view.Detail is IView detailView)
		{
			var detailType = detailView.GetType().Name;
			using (TuiLogging.PushChildContext("FlyoutPage", detailType, 1))
			{
				Logger.Debug("Mapping detail panel: {DetailType}", detailType);
				var platformDetail = detailView.ToPlatform(handler.MauiContext);
				if (platformDetail is Visual visual)
				{
					visual.HorizontalAlignment = Align.Stretch;
					visual.VerticalAlignment = Align.Stretch;
					handler._detailCell.Content = visual;
				}
			}
		}
	}

	static void MapIsPresented(FlyoutPageHandler handler, IFlyoutView view)
	{
		if (handler._flyoutCell is null || handler._separatorCell is null)
			return;

		var visible = view.IsPresented;
		Logger.Debug("Flyout visibility changed: IsPresented={IsPresented}", visible);
		handler._flyoutCell.IsVisible = visible;
		handler._separatorCell.IsVisible = visible;

		// Adjust column widths
		if (handler.PlatformView.ColumnDefinitions.Count >= 3)
		{
			if (visible)
			{
				var flyoutWidth = (int)(view.FlyoutWidth > 0 ? view.FlyoutWidth : 30);
				handler.PlatformView.ColumnDefinitions[0].Width = TuiGridLength.Fixed(flyoutWidth);
				handler.PlatformView.ColumnDefinitions[1].Width = TuiGridLength.Fixed(1);
			}
			else
			{
				handler.PlatformView.ColumnDefinitions[0].Width = TuiGridLength.Fixed(0);
				handler.PlatformView.ColumnDefinitions[1].Width = TuiGridLength.Fixed(0);
			}
		}
	}

	static void MapFlyoutWidth(FlyoutPageHandler handler, IFlyoutView view)
	{
		if (handler.PlatformView.ColumnDefinitions.Count >= 1 && view.IsPresented)
		{
			var flyoutWidth = (int)(view.FlyoutWidth > 0 ? view.FlyoutWidth : 30);
			handler.PlatformView.ColumnDefinitions[0].Width = TuiGridLength.Fixed(flyoutWidth);
		}
	}
}
