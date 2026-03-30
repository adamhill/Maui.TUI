#nullable enable
using Serilog;

namespace Maui.TUI.Platform;

/// <summary>
/// Legacy diagnostic logger — now delegates to Serilog.
/// Retained for API compatibility. Prefer injecting <c>ILogger</c> or using
/// <c>Log.ForContext&lt;T&gt;()</c> directly.
/// </summary>
[Obsolete("Use Serilog Log.ForContext<T>() instead. This class delegates to Serilog internally.")]
internal static class TuiDebugLog
{
    private static readonly ILogger Logger = Serilog.Log.ForContext(typeof(TuiDebugLog));

    internal static void Log(string message)
    {
        Logger.Debug("{Message}", message);
    }
}
