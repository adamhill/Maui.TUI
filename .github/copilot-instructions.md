# Copilot Instructions

Trust these instructions. Only search the codebase if the information here is incomplete or appears to be in error.

## What This Repository Does

Maui.TUI is a terminal UI backend for .NET MAUI. It implements the standard MAUI handler architecture (`ViewHandler<TVirtualView, TPlatformView>`) so that MAUI apps written with `ContentPage`, `Button`, `Label`, `Grid`, etc. render entirely in the terminal via the [XenoAtom.Terminal.UI](https://xenoatom.github.io/terminal/) retained-mode TUI framework.

## Repository at a Glance

| Item | Value |
|------|-------|
| Language | C# (LangVersion: preview / C# 14) |
| Target framework | `net10.0` |
| Solution file | `Maui.TUI.slnx` (root) |
| Key library | `src/Maui.TUI/Maui.TUI.csproj` |
| Sample app | `samples/Maui.TUI.Sample/Maui.TUI.Sample.csproj` |
| MAUI version | `10.0.31` (pinned via `$(MauiVersion)`) |
| Core TUI dependency | `XenoAtom.Terminal.UI` 1.0.0 |
| No test projects | Validation is via build + diagnostics modes |
| No CI/CD workflows | No `.github/workflows/` directory |

`Directory.Build.props` at the root sets `LangVersion=preview`, `Nullable=enable`, `ImplicitUsings=enable` for all projects.

## Build & Validate

### 1. Bootstrap (required once per environment)

The MAUI workload must be installed before the first build. Run:

```bash
dotnet workload restore
```

Without this step the build fails immediately with `NETSDK1147: To build this project, the following workloads must be installed: maui-tizen`.

### 2. Build

```bash
dotnet build Maui.TUI.slnx
```

A successful build prints `Build succeeded` in ~10 s and produces two warnings about nullable references in the sample (pre-existing, unrelated to library code). There are no errors.

### 3. Validate Changes (no interactive terminal needed)

```bash
# Dump the visual tree to stderr — runs without a real TTY
cd samples/Maui.TUI.Sample && dotnet run -- --dump

# Render to SVG on stdout — also runs without a TTY
cd samples/Maui.TUI.Sample && dotnet run -- --svg
```

Both modes call `MauiTuiApplication.Initialize()` and skip the `TerminalApp.Run()` loop, so they work in non-interactive environments.

### 4. Run Interactively

```bash
cd samples/Maui.TUI.Sample && dotnet run
```

This requires a real terminal (not a pipe). Press **Ctrl+Q** to exit. `run-sample.sh` is a macOS-only wrapper that opens a new Terminal.app window.

### 5. No Linter / No Formatter Tooling

There is no configured linter, formatter, or style tool. The compiler warnings (`CS8602` in the sample) are pre-existing.

## Architecture

### Directory Layout

```
Maui.TUI.slnx                   # Solution (two projects)
Directory.Build.props            # Global C# settings (preview, nullable, implicit usings)

src/Maui.TUI/
  Maui.TUI.csproj                # Core library
  Handlers/                      # One handler file per MAUI control type
    TuiViewHandler.cs            # Abstract base — converts float→cell coordinates
    LabelHandler.cs, ButtonHandler.cs … (25+ handlers)
  Hosting/
    AppHostBuilderExtensions.cs  # UseMauiAppTUI<TApp>(), registers all handlers + DI
    ApplicationExtensions.cs
  Platform/
    MauiTuiApplication.cs        # Abstract base class; call Run() or Initialize()/RenderSvg()
    TuiMauiContext.cs             # IMauiContext for TUI
    TuiDispatcherProvider.cs     # Dispatcher bound to the TerminalApp.Post() queue
    TuiAlertManager.cs           # Intercepts MAUI's internal AlertManager via DispatchProxy
    TuiContentPanel.cs / TuiLayoutPanel.cs
    TuiNavigationContainer.cs / TuiWindowRootContainer.cs

samples/Maui.TUI.Sample/
  Program.cs                     # Entry point; routes --dump / --svg / interactive
  MauiProgram.cs                 # Calls UseMauiAppTUI<App>()
  App.cs                         # Creates top-level Window
  MainPage.cs, FormDemoPage.cs … # Demo pages

docs/
  maui-extensibility-proposal.md # Design doc on alert manager hooking strategy
```

### Key Design Rules

- **Handler pattern**: inherit `TuiViewHandler<IFoo, TuiBarVisual>` → define a static `PropertyMapper` → override `CreatePlatformView()` → register in `AppHostBuilderExtensions.AddMauiControlsHandlers()`.
- **Coordinates**: terminal cells are integers. `TuiViewHandler.PlatformArrange` and `GetDesiredSize` do float→int conversion using `Math.Ceiling`.
- **Layout**: delegates to MAUI's `CrossPlatformMeasure`/`CrossPlatformArrange` — no custom layout code needed.
- **Alerts**: MAUI's `IAlertManagerSubscription` is internal; `TuiAlertManager` uses `DispatchProxy` + DI to intercept `DisplayAlert`, `DisplayActionSheet`, `DisplayPromptAsync`.
- **No MAUI fork**: only public (and some internal-via-reflection) MAUI APIs are used.
- **Tabs for indentation** in all source files.
- Namespace mirrors folder: `Maui.TUI.Handlers`, `Maui.TUI.Hosting`, `Maui.TUI.Platform`.

### Adding a New Handler (checklist)

1. Create `src/Maui.TUI/Handlers/{Control}Handler.cs`
2. `public partial class {Control}Handler : TuiViewHandler<I{Control}, {XenoAtomType}>`
3. Define static `PropertyMapper` and `CommandMapper`
4. Override `CreatePlatformView()` returning the XenoAtom control
5. Add `handlersCollection.AddHandler<Control, {Control}Handler>()` in `AppHostBuilderExtensions.AddMauiControlsHandlers()`
6. Run `dotnet build Maui.TUI.slnx` and `cd samples/Maui.TUI.Sample && dotnet run -- --dump` to verify
