using System.Collections.Concurrent;
using Microsoft.Maui.Dispatching;
using Serilog;
using XenoAtom.Terminal.UI;

namespace Maui.TUI.Platform;

public class TuiDispatcherProvider : IDispatcherProvider
{
	private static readonly ILogger Logger = Log.ForContext<TuiDispatcherProvider>();

	[ThreadStatic]
	static IDispatcher? s_dispatcherInstance;

	public IDispatcher? GetForCurrentThread() =>
		s_dispatcherInstance ??= new TuiDispatcher();
}

public class TuiDispatcher : IDispatcher
{
	private static readonly ILogger Logger = Log.ForContext<TuiDispatcher>();

	static TerminalApp? s_terminalApp;
	static Thread? s_uiThread;

	public static void SetUIThread()
	{
		s_uiThread = Thread.CurrentThread;
		Logger.Debug("UI thread set to {ThreadId}", Thread.CurrentThread.ManagedThreadId);
	}

	/// <summary>
	/// Sets the TerminalApp instance so dispatched actions can be posted to the TUI run loop.
	/// Must be called before Run().
	/// </summary>
	public static void SetTerminalApp(TerminalApp app)
	{
		s_terminalApp = app;
		Logger.Debug("TerminalApp registered with TuiDispatcher");
	}

	public bool IsDispatchRequired => Thread.CurrentThread != s_uiThread;

	public bool Dispatch(Action action)
	{
		if (!IsDispatchRequired)
		{
			action();
			return true;
		}

		if (s_terminalApp is not null)
		{
			s_terminalApp.Post(action);
		}
		else
		{
			// Fallback: run inline (pre-Run() phase)
			Logger.Verbose("Dispatch called before TerminalApp available, executing inline on thread {ThreadId}",
				Environment.CurrentManagedThreadId);
			action();
		}
		return true;
	}

	public bool DispatchDelayed(TimeSpan delay, Action action)
	{
		Logger.Verbose("Dispatching delayed action ({DelayMs}ms)", delay.TotalMilliseconds);
		_ = Task.Run(async () =>
		{
			await Task.Delay(delay);
			Dispatch(action);
		});
		return true;
	}

	public IDispatcherTimer CreateTimer() => new TuiDispatcherTimer(this);
}

public class TuiDispatcherTimer : IDispatcherTimer
{
	private static readonly ILogger Logger = Log.ForContext<TuiDispatcherTimer>();

	readonly TuiDispatcher _dispatcher;
	Timer? _timer;

	public TuiDispatcherTimer(TuiDispatcher dispatcher)
	{
		_dispatcher = dispatcher;
	}

	public TimeSpan Interval { get; set; }
	public bool IsRepeating { get; set; }
	public bool IsRunning { get; private set; }

	public event EventHandler? Tick;

	public void Start()
	{
		if (IsRunning) return;
		IsRunning = true;

		Logger.Debug("DispatcherTimer started (interval={IntervalMs}ms, repeating={IsRepeating})",
			Interval.TotalMilliseconds, IsRepeating);

		_timer = new Timer(_ =>
		{
			_dispatcher.Dispatch(() =>
			{
				Tick?.Invoke(this, EventArgs.Empty);
				if (!IsRepeating)
					Stop();
			});
		}, null, Interval, IsRepeating ? Interval : Timeout.InfiniteTimeSpan);
	}

	public void Stop()
	{
		IsRunning = false;
		_timer?.Dispose();
		_timer = null;

		Logger.Debug("DispatcherTimer stopped");
	}
}
