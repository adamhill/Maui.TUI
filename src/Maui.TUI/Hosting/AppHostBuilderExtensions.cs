using System.Diagnostics.CodeAnalysis;
using Maui.TUI.Controls;
using Maui.TUI.Handlers;
using Maui.TUI.Platform;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Maui.Animations;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Hosting;
using Microsoft.Maui.Dispatching;
using Microsoft.Maui.Hosting;
using Serilog;

namespace Maui.TUI.Hosting;

public static class AppHostBuilderExtensions
{
	public static MauiAppBuilder UseMauiAppTUI<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TApp>(
		this MauiAppBuilder builder)
		where TApp : class, IApplication
	{
		builder.UseMauiApp<TApp>();
		builder.SetupDefaults();
		return builder;
	}

	public static MauiAppBuilder UseMauiAppTUI<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TApp>(
		this MauiAppBuilder builder,
		Func<IServiceProvider, TApp> implementationFactory)
		where TApp : class, IApplication
	{
		builder.UseMauiApp(implementationFactory);
		builder.SetupDefaults();
		return builder;
	}

	static IMauiHandlersCollection AddMauiControlsHandlers(this IMauiHandlersCollection handlersCollection)
	{
		handlersCollection.AddHandler<Application, ApplicationHandler>();
		handlersCollection.AddHandler<Microsoft.Maui.Controls.Window, WindowHandler>();
		handlersCollection.AddHandler<Microsoft.Maui.Controls.Label, LabelHandler>();
		handlersCollection.AddHandler<Microsoft.Maui.Controls.Button, Handlers.ButtonHandler>();
		handlersCollection.AddHandler<ContentPage, PageHandler>();
		handlersCollection.AddHandler<ContentView, ContentViewHandler>();
		handlersCollection.AddHandler<TabbedPage, TabbedPageHandler>();
		handlersCollection.AddHandler<NavigationPage, NavigationPageHandler>();
		handlersCollection.AddHandler<FlyoutPage, FlyoutPageHandler>();
		handlersCollection.AddHandler<Layout, LayoutHandler>();

		// Tier 1 — Core Input Controls
		handlersCollection.AddHandler<Microsoft.Maui.Controls.Entry, EntryHandler>();
		handlersCollection.AddHandler<Microsoft.Maui.Controls.Editor, EditorHandler>();
		handlersCollection.AddHandler<Microsoft.Maui.Controls.CheckBox, CheckBoxHandler>();
		handlersCollection.AddHandler<Microsoft.Maui.Controls.Switch, SwitchHandler>();
		handlersCollection.AddHandler<Microsoft.Maui.Controls.Slider, Handlers.SliderHandler>();
		handlersCollection.AddHandler<Microsoft.Maui.Controls.ProgressBar, Handlers.ProgressBarHandler>();
		handlersCollection.AddHandler<Microsoft.Maui.Controls.ActivityIndicator, ActivityIndicatorHandler>();

		// Tier 2 — Layout & Container Controls
		handlersCollection.AddHandler<Grid, LayoutHandler>();
		handlersCollection.AddHandler<AbsoluteLayout, LayoutHandler>();
		handlersCollection.AddHandler<FlexLayout, LayoutHandler>();
		handlersCollection.AddHandler<Microsoft.Maui.Controls.Border, BorderHandler>();
#pragma warning disable CS0618 // Frame is obsolete but we support it for backward compatibility
		handlersCollection.AddHandler<Microsoft.Maui.Controls.Frame, FrameHandler>();
#pragma warning restore CS0618
		handlersCollection.AddHandler<Microsoft.Maui.Controls.ScrollView, ScrollViewHandler>();

		// Tier 3 — Selection & Advanced Controls
		handlersCollection.AddHandler<Microsoft.Maui.Controls.Picker, PickerHandler>();
		handlersCollection.AddHandler<Microsoft.Maui.Controls.RadioButton, RadioButtonHandler>();
		handlersCollection.AddHandler<Microsoft.Maui.Controls.CollectionView, CollectionViewHandler>();

		// Tier 4 — Composite Controls
		handlersCollection.AddHandler<Microsoft.Maui.Controls.DatePicker, DatePickerHandler>();
		handlersCollection.AddHandler<Microsoft.Maui.Controls.TimePicker, TimePickerHandler>();
		handlersCollection.AddHandler<Microsoft.Maui.Controls.Stepper, StepperHandler>();

		// Animation Controls
		handlersCollection.AddHandler<AsciiCanvasView, AsciiCanvasViewHandler>();

		return handlersCollection;
	}

	static MauiAppBuilder SetupDefaults(this MauiAppBuilder builder)
	{
		// Ensure Serilog is initialized and wire it into DI as the ILogger provider
		TuiLogging.EnsureInitialized();
		builder.Services.AddSerilog();

		Log.Information("Configuring MAUI TUI services");

		builder.Services.AddSingleton<IDispatcherProvider>(svc => new TuiDispatcherProvider());

		// Register TuiTicker as a singleton so the same instance is shared across the app.
		// Using TryAdd* ensures we don't conflict if the user registers their own.
		// MUST be registered before ConfigureAnimations() which uses TryAddScoped for ITicker.
		builder.Services.AddSingleton<TuiTicker>();
		builder.Services.AddSingleton<ITicker>(svc => svc.GetRequiredService<TuiTicker>());
		builder.Services.AddSingleton<IAnimationManager>(svc =>
			new AnimationManager(svc.GetRequiredService<ITicker>()));

		builder.Services.AddScoped(svc =>
		{
			var provider = svc.GetRequiredService<IDispatcherProvider>();
			if (DispatcherProvider.SetCurrent(provider))
			{
				// Replaced dispatcher provider
			}
			return Dispatcher.GetForCurrentThread()!;
		});

		// Register alert subscription via DI so AlertManager.Subscribe() picks it up
		TuiAlertManager.RegisterAlertSubscription(builder.Services);

		builder.ConfigureMauiHandlers(handlers =>
		{
			handlers.AddMauiControlsHandlers();
		});

		Log.Debug("MAUI TUI service registration complete: {HandlerCount} handlers registered", 28);

		return builder;
	}
}
