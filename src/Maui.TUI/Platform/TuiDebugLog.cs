#nullable enable
namespace Maui.TUI.Platform;

/// <summary>
/// Simple diagnostic logger that writes timestamped, thread-tagged lines to a fixed log file.
/// All writes are fire-and-forget — any IO error is silently swallowed so the logger
/// never crashes the host process.
/// </summary>
internal static class TuiDebugLog
{
    private const string LogPath = "/tmp/maui-tui-debug.log";

    static TuiDebugLog()
    {
        try { File.Delete(LogPath); } catch { }
    }

    internal static void Log(string message)
    {
        try
        {
            var line = $"[{DateTime.Now:HH:mm:ss.fff}] [{Environment.CurrentManagedThreadId:D3}] {message}\n";
            File.AppendAllText(LogPath, line);
        }
        catch { }
    }
}
