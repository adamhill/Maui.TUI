#nullable enable
using Microsoft.Maui.Handlers;
using Serilog;

namespace Maui.TUI.Handlers;

public partial class ApplicationHandler : ElementHandler<IApplication, object>
{
	private static readonly ILogger Logger = Log.ForContext<ApplicationHandler>();

	internal const string TerminateCommandKey = "Terminate";

	public static IPropertyMapper<IApplication, ApplicationHandler> Mapper =
		new PropertyMapper<IApplication, ApplicationHandler>(ElementMapper)
		{
		};

	public static CommandMapper<IApplication, ApplicationHandler> CommandMapper =
		new(ElementCommandMapper)
		{
			[TerminateCommandKey] = MapTerminate,
			[nameof(IApplication.OpenWindow)] = MapOpenWindow,
			[nameof(IApplication.CloseWindow)] = MapCloseWindow,
		};

	public ApplicationHandler()
		: base(Mapper, CommandMapper)
	{
	}

	public ApplicationHandler(IPropertyMapper? mapper)
		: base(mapper ?? Mapper, CommandMapper)
	{
	}

	public ApplicationHandler(IPropertyMapper? mapper, CommandMapper? commandMapper)
		: base(mapper ?? Mapper, commandMapper ?? CommandMapper)
	{
	}

	protected override object CreatePlatformElement() =>
		MauiContext?.Services.GetService(typeof(object))
			?? new object();

	public static void MapTerminate(ApplicationHandler handler, IApplication application, object? args)
	{
		Logger.Information("Application terminate requested");
	}

	public static void MapOpenWindow(ApplicationHandler handler, IApplication application, object? args)
	{
		Logger.Information("Application OpenWindow requested");
	}

	public static void MapCloseWindow(ApplicationHandler handler, IApplication application, object? args)
	{
		Logger.Information("Application CloseWindow requested");
	}
}
