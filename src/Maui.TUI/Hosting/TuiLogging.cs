#nullable enable
using Serilog;
using Serilog.Context;
using Serilog.Events;

namespace Maui.TUI.Hosting;

/// <summary>
/// Configures Serilog logging for the TUI application.
/// The file sink is always enabled; the console sink is available
/// for non-fullscreen modes (e.g. <c>--dump</c>, <c>--svg</c>).
/// </summary>
public static class TuiLogging
{
    private const string DefaultLogPath = "logs/maui-tui-.log";

    // File output includes ThreadId and all enriched properties for structured analysis
    private const string FileOutputTemplate =
        "[{Timestamp:HH:mm:ss.fff} {Level:u3}] [{SourceContext}] [T{ThreadId}] {Message:lj}" +
        "{Properties:j}{NewLine}{Exception}";

    // Console output is simpler — structured viewers handle properties natively
    private const string ConsoleOutputTemplate =
        "[{Timestamp:HH:mm:ss.fff} {Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}";

    /// <summary>
    /// Creates the default Serilog logger with File + optional Console sinks.
    /// Call this early in the application lifecycle, before building the MAUI app.
    /// </summary>
    /// <param name="enableConsole">
    /// When <see langword="true"/>, also writes to stderr via the Console sink.
    /// Avoid in fullscreen mode — XenoAtom owns stdout.
    /// </param>
    /// <param name="logPath">Override the default log file path.</param>
    /// <param name="minimumLevel">Override the minimum log level (default: Debug).</param>
    public static LoggerConfiguration CreateDefaultConfiguration(
        bool enableConsole = false,
        string? logPath = null,
        LogEventLevel minimumLevel = LogEventLevel.Debug)
    {
        var config = new LoggerConfiguration()
            .MinimumLevel.Is(minimumLevel)
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.Maui", LogEventLevel.Information)
            .Enrich.FromLogContext()
            .Enrich.WithThreadId()
            .Enrich.WithProperty("Application", "Maui.TUI")
            .WriteTo.File(
                path: logPath ?? DefaultLogPath,
                outputTemplate: FileOutputTemplate,
                rollingInterval: RollingInterval.Day,
                rollOnFileSizeLimit: true,
                fileSizeLimitBytes: 10 * 1024 * 1024, // 10 MB
                retainedFileCountLimit: 7,
                shared: false,
                flushToDiskInterval: TimeSpan.FromSeconds(1));

        if (enableConsole)
        {
            // Use stderr so we don't corrupt terminal output
            config.WriteTo.Console(
                outputTemplate: ConsoleOutputTemplate,
                standardErrorFromLevel: LogEventLevel.Verbose);
        }

        return config;
    }

    /// <summary>
    /// Initializes the global Serilog logger with default settings.
    /// Safe to call multiple times — subsequent calls are no-ops if a logger is already configured.
    /// </summary>
    public static void EnsureInitialized(bool enableConsole = false)
    {
        if (Log.Logger is not Serilog.Core.Logger)
        {
            Log.Logger = CreateDefaultConfiguration(enableConsole).CreateLogger();
        }
    }

    /// <summary>
    /// Pushes a parent-child context scope onto the Serilog <see cref="LogContext"/>.
    /// All log entries within the returned scope will carry <c>ParentView</c>,
    /// <c>ChildView</c>, and <c>ChildIndex</c> properties for structured log viewers.
    /// </summary>
    /// <returns>An <see cref="IDisposable"/> that removes the properties when disposed.</returns>
    public static IDisposable PushChildContext(string parentViewType, string childViewType, int childIndex)
    {
        // Each Push returns a disposable; we chain them so all three are removed together.
        var d1 = LogContext.PushProperty("ParentView", parentViewType);
        var d2 = LogContext.PushProperty("ChildView", childViewType);
        var d3 = LogContext.PushProperty("ChildIndex", childIndex);
        return new CompositeDisposable(d1, d2, d3);
    }

    /// <summary>
    /// Pushes a navigation context scope for page stack operations.
    /// </summary>
    public static IDisposable PushNavigationContext(string handlerType, string operation, int stackDepth)
    {
        var d1 = LogContext.PushProperty("HandlerType", handlerType);
        var d2 = LogContext.PushProperty("NavOperation", operation);
        var d3 = LogContext.PushProperty("StackDepth", stackDepth);
        return new CompositeDisposable(d1, d2, d3);
    }

    /// <summary>
    /// Pushes a handler context scope that enriches all log entries with the handler
    /// and virtual view types.
    /// </summary>
    public static IDisposable PushHandlerContext(string handlerType, string virtualViewType)
    {
        var d1 = LogContext.PushProperty("HandlerType", handlerType);
        var d2 = LogContext.PushProperty("VirtualViewType", virtualViewType);
        return new CompositeDisposable(d1, d2);
    }

    /// <summary>
    /// Combines multiple <see cref="IDisposable"/> instances into one.
    /// </summary>
    private sealed class CompositeDisposable(params IDisposable[] disposables) : IDisposable
    {
        public void Dispose()
        {
            for (int i = disposables.Length - 1; i >= 0; i--)
                disposables[i].Dispose();
        }
    }
}
