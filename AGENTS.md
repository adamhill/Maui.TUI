# AGENTS.md

Guidance for AI agents working on this repository.

## Project Overview

Maui.TUI is a terminal UI backend for .NET MAUI. It uses the standard MAUI handler architecture (`ViewHandler<TVirtualView, TPlatformView>`) to render MAUI controls as terminal UI elements via the [XenoAtom.Terminal.UI](https://xenoatom.github.io/terminal/) library.

> **Core dependency: XenoAtom.Terminal.UI** (pinned at `1.0.0` in `src/Maui.TUI/Maui.TUI.csproj`).
> All platform views must be XenoAtom `Visual` subclasses. Consult the [XenoAtom.Terminal.UI docs](https://xenoatom.github.io/terminal/) before adding any new platform control types.

## Build & Run

```bash
# Restore and build everything
dotnet build Maui.TUI.slnx

# Run the sample app
cd samples/Maui.TUI.Sample && dotnet run

# Run with diagnostics
cd samples/Maui.TUI.Sample && dotnet run -- --dump   # visual tree dump
cd samples/Maui.TUI.Sample && dotnet run -- --svg    # SVG render
```

- Target framework: `net10.0`
- Requires the MAUI workload: `dotnet workload install maui`
- MAUI version is pinned via `$(MauiVersion)` in each `.csproj` (currently `10.0.31`)

## Repository Layout

```
src/Maui.TUI/                  # Core library (Maui.TUI.csproj)
  Handlers/                    # One handler per MAUI control type
  Hosting/                     # MauiAppBuilder extensions, bootstrap
  Platform/                    # Application lifecycle, context, dispatcher, alerts

samples/Maui.TUI.Sample/      # Sample app exercising all supported controls
docs/                          # Design proposals for MAUI extensibility
```

## Architecture & Conventions

### Handler Pattern

Each handler lives in `src/Maui.TUI/Handlers/` and follows this pattern:

- Inherit from `TuiViewHandler<TVirtualView, TPlatformView>` (defined in `TuiViewHandler.cs`)
- `TVirtualView` is the MAUI interface (e.g. `ILabel`, `IButton`)
- `TPlatformView` is the XenoAtom.Terminal.UI type (e.g. `TextBlock`, `Button`)
- Map MAUI properties to terminal control properties via a `PropertyMapper`
- Register the handler in `AppHostBuilderExtensions.AddMauiControlsHandlers()`

### Platform Infrastructure

- `MauiTuiApplication` — abstract base class for TUI apps; subclass and implement `CreateMauiApp()`
- `TuiMauiContext` — implements `IMauiContext` for the TUI platform
- `TuiDispatcherProvider` — dispatches work on the terminal UI thread
- `TuiAlertManager` — hooks into MAUI's internal `AlertManager` via reflection/`DispatchProxy` to handle `DisplayAlert`, `DisplayActionSheet`, and `DisplayPromptAsync`

### Key Design Decisions

- **No MAUI fork** — everything works against the public (and some internal) MAUI APIs
- **Reflection for alerts** — MAUI's `IAlertManagerSubscription` is internal, so we use `DispatchProxy` to register a DI-based implementation (see `TuiAlertManager.cs` and the proposal in `docs/maui-extensibility-proposal.md`)
- **Cell-based layout** — terminal coordinates are integer cells, not floating-point pixels; `TuiViewHandler` converts between the two in `PlatformArrange` and `GetDesiredSize`
- **XenoAtom.Terminal.UI visuals** — all platform views are XenoAtom `Visual` subclasses (`TextBlock`, `Panel`, `Button`, etc.)

## Code Style

- C# with `LangVersion` preview, nullable enabled, implicit usings enabled (set in `Directory.Build.props`)
- Tabs for indentation
- Namespace matches folder structure (`Maui.TUI.Handlers`, `Maui.TUI.Hosting`, `Maui.TUI.Platform`)
- Minimal XML doc comments — add them on public APIs and non-obvious internal methods
- No `#region` blocks

## Adding a New Handler

1. Create `src/Maui.TUI/Handlers/{ControlName}Handler.cs`
2. Inherit from `TuiViewHandler<I{Control}, {TuiVisualType}>`
3. Define a static `PropertyMapper` mapping MAUI properties to TUI updates
4. Override `CreatePlatformView()` to return the XenoAtom control
5. Add a `handlersCollection.AddHandler<>()` line in `AppHostBuilderExtensions.AddMauiControlsHandlers()`

## Contributing (PR Workflow)

This repository is a fork of [Redth/Maui.TUI](https://github.com/Redth/Maui.TUI). Use the GitHub CLI (`gh`) to streamline submitting changes back upstream.

```bash
# Check fork/upstream state
gh repo view

# Create a feature branch and commit your work
git checkout -b feat/my-feature
# ... make changes ...
git add -A && git commit -m "feat: describe change"

# Push to your fork and open a PR against the upstream repo
git push origin feat/my-feature
gh pr create --repo Redth/Maui.TUI \
  --title "feat: describe change" \
  --body "Closes #<issue>" \
  --base main

# View open PRs on the upstream
gh pr list --repo Redth/Maui.TUI
```

Keep feature branches short-lived and scoped to a single concern so PRs are easy to review upstream.

## Testing

- Test project: `tests/Maui.TUI.Animation.Tests/`
- Run tests: `dotnet test tests/Maui.TUI.Animation.Tests/`
- Framework: xUnit 2.9.3

To verify changes manually:

1. `dotnet build Maui.TUI.slnx` — ensure it compiles
2. `cd samples/Maui.TUI.Sample && dotnet run -- --dump` — verify the visual tree
3. `cd samples/Maui.TUI.Sample && dotnet run -- --svg` — render to SVG and inspect
4. `cd samples/Maui.TUI.Sample && dotnet run` — run interactively and verify behavior (Ctrl+Q to exit)

### Test Conventions

- **MultiAssert pattern:** When a test checks multiple independent conditions, wrap them in `Assert.Multiple()` so all failures are reported together rather than stopping at the first:

  ```csharp
  // WRONG — stops at first failure, hides other problems
  Assert.False(ticker.IsRunning);
  Assert.True(ticker.SystemEnabled);
  Assert.Equal(30, ticker.MaxFps);

  // CORRECT — reports all failures at once
  Assert.Multiple(
      () => Assert.False(ticker.IsRunning),
      () => Assert.True(ticker.SystemEnabled),
      () => Assert.Equal(30, ticker.MaxFps));
  ```

  **When to use:** Multiple asserts verifying independent properties/state of the same object at a single point in time.

  **When NOT to use:** Sequential asserts that depend on actions between them (e.g., assert before start, call start, assert after start) — those are separate logical steps and should remain sequential.

## Important Notes

- The `run-sample.sh` script opens a new macOS Terminal window to run the sample (required because TUI apps need a full terminal)
- Alert handling uses reflection against MAUI internals — changes to `Microsoft.Maui.Controls.Platform.AlertManager` in new MAUI versions may require updates to `TuiAlertManager.cs`
- The project targets .NET 10 preview and MAUI 10 — ensure you have the correct SDK installed

## Developer Resources

- **MAUI & .NET docs** — Use the Microsoft Learn MCP (`@mcp-microsoft-doc`) to search official documentation for MAUI controls, APIs, and C# language questions directly from the agent.
- **XenoAtom.Terminal.UI** — [xenoatom.github.io/terminal](https://xenoatom.github.io/terminal/) — the terminal rendering library underlying all platform views. Check here first when adding new visual types.
- **Upstream repo** — [github.com/Redth/Maui.TUI](https://github.com/Redth/Maui.TUI)
