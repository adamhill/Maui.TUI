# Making MAUI.Tui animate at 30–60 fps

**MAUI.Tui can reach 30–60 fps ASCII animation, but it requires building a custom rendering bypass layer.** The framework today is purely event-driven with no frame loop, no canvas primitive, and no way to short-circuit MAUI's measure/arrange pipeline for animation regions. XenoAtom.Terminal.UI underneath already supports diff-based rendering at terminal-native frame rates — the bottleneck is the MAUI handler bridge, not the terminal. The path forward involves a dedicated `AsciiCanvasView` handler that writes directly to XenoAtom's cell buffer, a timer-driven animation tick source integrated into the `TerminalApp` loop, and selective bypass of MAUI's layout system for animated regions.

---

## How MAUI.Tui maps GUI concepts to terminal cells

MAUI.Tui (by Jonathan Dick / Redth, MIT, 4 commits, .NET 10 + C# 14) implements the standard MAUI handler architecture with XenoAtom.Terminal.UI visuals as the "platform views." The generic signature `ViewHandler<TVirtualView, TPlatformView>` maps each MAUI interface to a terminal control: `ILabel` → `TextBlock`, `IButton` → `Button`, `IEntry` → `TextBox`, `IEditor` → `TextArea`, `IProgressBar` → `ProgressBar`, and so on across **25+ handlers** in `src/Maui.TUI/Handlers/`.

The platform infrastructure lives in `src/Maui.TUI/Platform/` with four key classes. **`MauiTuiApplication`** is the entry point — users subclass it, override `CreateMauiApp()`, and call `Run()`, which bootstraps the MAUI DI pipeline and starts XenoAtom's `Terminal.Run()` fullscreen event loop. **`TuiDispatcherProvider`** implements `IDispatcherProvider` to marshal all MAUI work onto XenoAtom's single terminal thread. **`TuiMauiContext`** implements `IMauiContext` to provide services and the handler factory. **`TuiAlertManager`** intercepts MAUI's `AlertManager` via DI to render `DisplayAlert`/`DisplayActionSheet`/`DisplayPromptAsync` as modal `Dialog` controls.

The hosting extension `UseMauiAppTUI<TApp>()` in `src/Maui.TUI/Hosting/AppHostBuilderExtensions.cs` registers all handlers and services. Layout delegates entirely to MAUI's cross-platform engine — `CrossPlatformMeasure` and `CrossPlatformArrange` work unchanged, with handlers translating the `double`-precision results to XenoAtom's integer cell coordinates. This means Grid, FlexLayout, AbsoluteLayout, and stack layouts all function without reimplementation, but every layout pass traverses the full MAUI pipeline.

The rendering flow is: MAUI property change → PropertyMapper invokes static `Map*` method → handler updates XenoAtom visual property → XenoAtom's reactive system marks visual dirty → next frame runs measure → arrange → render to cell buffer → diff against previous buffer → batched ANSI output with synchronized output protocol (DEC 2026). **No explicit frame loop exists in MAUI.Tui itself** — it rides entirely on XenoAtom's `Terminal.Run()` event loop, which processes input, calls `onUpdate`, and renders when dirty.

---

## XenoAtom.Terminal.UI already supports the rendering speed needed

XenoAtom.Terminal.UI (v1.13.1, by Alexandre Mutel / xoofx, BSD-2-Clause, 833 commits) is a retained-mode reactive terminal UI framework built on three layers: `XenoAtom.Ansi` (allocation-friendly ANSI/VT primitives), `XenoAtom.Terminal` (cross-platform terminal hosting + I/O), and `XenoAtom.Terminal.UI` (60+ controls, layout, styling, binding). All target `net10.0` and are NativeAOT-oriented.

The framework's render pipeline is the critical enabler. It maintains a **double-buffered cell buffer** where each cell stores a `Rune`, foreground RGBA, background RGBA, and text attributes. After rendering dirty visuals, a diff algorithm compares the current buffer against the previous frame and emits only changed cells as batched ANSI escape sequences. The **synchronized output protocol** wraps each frame's output in begin/end markers so the terminal holds rendering until complete, eliminating tearing. The F12 debug overlay in fullscreen apps shows real-time **FPS, dirty region visualization, diff statistics, and per-pass timing breakdowns** (update, measure, arrange, render, diff, output).

The `Terminal.Run(visual, onUpdate)` method drives the fullscreen loop: process input → call `onUpdate` delegate → if state is dirty, run measure/arrange/render/diff/output → check `TerminalLoopResult` return value. The `onUpdate` callback fires once per frame iteration and is where animation state should be advanced. The reactive `State<T>` system provides automatic dependency tracking — when a `State<T>.Value` changes, only visuals that read that state during their last render pass are invalidated.

Key controls relevant to animation include **`Canvas`** (custom drawing surface in the Visualization category), **`TextFiglet`** (FIGlet ASCII art text with gradient brush support), and **`Spinner`** (built-in frame-cycling animation). The `Brush.LinearGradient()` API supports `GradientStop` with `ColorMixSpace.Oklab` for perceptually smooth color transitions. RGBA alpha blending enables layered visual effects. The entire ecosystem is designed for **zero/low allocation hot paths**, consistent with xoofx's work on XenoAtom.Allocators and zero-alloc logging.

---

## The MAUI rendering pipeline adds measurable overhead per frame

MAUI's standard rendering pipeline introduces several layers between a property change and screen output. The handler pattern itself is efficient — `PropertyMapper` is a static `Dictionary<string, Action<THandler, TVirtualView>>` with direct dictionary lookup and delegate invocation, no reflection, no boxing. Handler allocation is 1:1 (one handler per view, one platform view per handler), and unlike Xamarin.Forms renderers, no wrapper elements are created.

The layout system is the primary performance concern. Each `InvalidateMeasure()` call cascades upward through the visual tree, potentially triggering re-measurement of every ancestor. `InvalidateArrange()` similarly cascades. For a modestly nested tree with Grid → StackLayout → children, a single property change can trigger **multiple measure passes** before `ArrangeChildren()` runs (the platform may call `Measure` speculatively). Known MAUI performance data from Jonathan Peppers' profiling shows `GetDesiredSizeFromHandler` consuming **4–5% of total time** during scrolling scenarios, and `DispatchDraw` interop (on Android) consuming 35% — though the TUI backend avoids native interop overhead entirely.

MAUI's animation system uses an `AnimationManager` driven by an `ITicker` that fires at a configurable rate (default **16ms / ~62.5 fps**). The `Animation.Commit()` method accepts a `rate` parameter in milliseconds. Animations modify `VisualElement` properties (Opacity, TranslationX/Y, Scale, Rotation), which flow through PropertyMapper to update platform views. **Transform-type animations should not trigger layout**, but a known iOS bug (issue #24996) causes `TranslationX/Y` changes to trigger full measure/arrange cascades. In the TUI backend, this bug's impact depends on how the handlers translate these properties — if they map to XenoAtom positional properties that don't participate in XenoAtom's layout, the cascade stays within MAUI's layer only.

The `GraphicsView` / `IDrawable` pattern in MAUI provides a custom drawing surface where `Draw(ICanvas canvas, RectF dirtyRect)` is called on invalidation. This is software-rendered via `Microsoft.Maui.Graphics`. For the TUI backend, no `GraphicsView` handler exists, and `ICanvas` operations (DrawRect, DrawLine, DrawString) would need translation to terminal cell writes — a significant but tractable implementation.

---

## Five critical gaps between MAUI.Tui today and 30–60 fps animation

**Gap 1: No frame/render loop or animation tick source.** MAUI.Tui is purely event-driven. XenoAtom's `Terminal.Run()` has an `onUpdate` callback that fires each frame, but MAUI.Tui doesn't expose this or connect it to MAUI's `ITicker`. There is no `IDispatcherTimer`-equivalent driving periodic state updates. Without a tick source, nothing advances animation state between user input events.

**Gap 2: No custom drawing or canvas primitive.** XenoAtom has a `Canvas` control and `TextFiglet` for ASCII art, but MAUI.Tui has no handler that exposes direct cell-buffer writing. There's no way to render pre-computed ASCII art frames, arbitrary characters at arbitrary positions, or per-cell color data through the MAUI layer. Every visual must be one of the 25 supported controls.

**Gap 3: Every property change triggers MAUI layout.** Even when animating a single label's text or color, the change flows through `InvalidateMeasure()` → full tree walk. For animation at 30–60 fps, this means 30–60 layout passes per second through MAUI's cross-platform engine, each converting `double` measurements to integer cells. While XenoAtom's diff rendering handles the output efficiently, the MAUI layout overhead accumulates with tree depth.

**Gap 4: Memory allocation patterns are unknown but likely problematic.** MAUI's binding system, property change notifications, and handler dispatch involve delegate invocations and potential allocations (e.g., `Task<bool>` per animation, `EventArgs` per property change). At 60 fps, even small per-frame allocations trigger GC pressure. XenoAtom is allocation-conscious, but the MAUI bridge layer may not be.

**Gap 5: Threading model coupling is incomplete.** `TuiDispatcherProvider` marshals MAUI work to the terminal thread, but the integration between MAUI's `Dispatcher.CreateTimer()` and XenoAtom's frame loop is unverified. If `IDispatcherTimer` posts work to a queue that's only drained during input processing (not during `onUpdate`), timer-driven animations could experience frame-rate inconsistency.

---

## Lessons from terminal animation at scale

The GitHub Copilot CLI animated ASCII banner — the inspiration for this project — required **~6,000 lines of TypeScript** built on Ink (React for terminals). Their engineering blog describes it as "one of the most constrained UI engineering problems you can take on." Key technical decisions: pre-rendered frame data converted to ANSI sequences, a color roles system for cross-terminal compatibility, graceful degradation when animation isn't visible, and custom preview tooling because no existing tool accurately simulates terminal color remapping.

Other frameworks confirm that **60 fps is achievable in modern terminals**. Will McGugan (Textual/Python) demonstrated "60 frames per second animation... it looked silky smooth." Ratatui (Rust) benchmarks show "60+ FPS even with complex layouts." The VS Code integrated terminal moved from DOM to canvas rendering specifically because DOM layout could exceed **16.6ms per frame** — the same threshold relevant here.

The universal optimization pattern across all high-performance terminal frameworks is: **double-buffered cell array → diff → batched output → synchronized output protocol**. XenoAtom.Terminal.UI already implements all four. The critical insight from Consolonia (Avalonia rendered to terminal) is that a full GUI framework *can* render through a terminal backend with animation support — Consolonia supports Avalonia's animation system in the terminal context.

---

## Concrete implementation plan for 30–60 fps ASCII animation

The following components, listed in dependency order, would enable smooth animation through MAUI.Tui. Each includes the specific files, classes, and architectural changes needed.

**Component 1: Animation tick integration** (`src/Maui.TUI/Platform/TuiTicker.cs`). Implement `ITicker` for the TUI backend that hooks into XenoAtom's `onUpdate` callback cycle. The ticker fires once per frame iteration of `Terminal.Run()`, providing the animation pulse. Register this in `AppHostBuilderExtensions.UseMauiAppTUI<T>()` as the `ITicker` service. This unlocks MAUI's entire `Animation` class, `ViewExtensions` animations (FadeTo, TranslateTo), and `IDispatcherTimer`. The `MaxFps` property should default to 30 and be configurable.

**Component 2: AsciiCanvasView and handler** (`src/Maui.TUI/Controls/AsciiCanvasView.cs`, `src/Maui.TUI/Handlers/AsciiCanvasViewHandler.cs`). A custom MAUI `View` subclass that maps to XenoAtom's `Canvas` control (or a custom `Visual` subclass). It exposes a `Draw` callback or `IDrawable`-like interface operating on a `CellBuffer` abstraction — methods like `SetCell(int col, int row, Rune character, Color fg, Color bg)`, `DrawString(int col, int row, string text, Color fg)`, and `LoadFrame(AsciiFrame frame)`. The handler creates the XenoAtom visual, and on invalidation, calls the user's draw callback. This bypasses MAUI's layout system for the canvas interior — only the canvas's own bounds participate in MAUI layout; the content is drawn directly to cells.

**Component 3: Pre-computed frame pipeline** (`src/Maui.TUI/Animation/AsciiFrame.cs`, `AsciiAnimation.cs`). A data structure for pre-rendered ASCII art frames: a 2D array of `(Rune, Color, Color)` tuples plus timing metadata (duration per frame, total frame count). `AsciiAnimation` holds a sequence of frames and an `Advance(TimeSpan deltaTime)` method that returns the current frame. This plugs into the `AsciiCanvasView` — each tick, the animation advances and the canvas draws the current frame. This mirrors the Copilot CLI approach of pre-computed frame data.

**Component 4: Layout bypass for fixed-size animation regions.** For the `AsciiCanvasView`, override `CrossPlatformMeasure` to return the fixed canvas size without walking children, and `CrossPlatformArrange` to be a no-op. The handler should set explicit `WidthRequest`/`HeightRequest` on the XenoAtom visual, removing the canvas from MAUI's layout invalidation cascade. When animation state changes, only `InvalidateArrange` (not `InvalidateMeasure`) should fire, and even that should be suppressed — instead, directly mark the XenoAtom `Canvas` visual as dirty through its own invalidation mechanism.

**Component 5: Direct XenoAtom visual property bypass** (`src/Maui.TUI/Handlers/AnimationOptimizedHandler.cs`). For simple animations (color changes, text updates on a Label), provide an optimized path that updates the XenoAtom visual's properties directly via `State<T>.Value` assignment *without* flowing through MAUI's `InvalidateMeasure()`. This requires a "frozen layout" mode where the handler caches the last layout result and suppresses upward invalidation. The XenoAtom reactive system then handles re-rendering just the affected visual.

**Component 6: Frame scheduling and pacing.** Expose a `FrameScheduler` service that wraps XenoAtom's render loop timing. It provides `RequestAnimationFrame(Action<TimeSpan> callback)` semantics — registered callbacks fire once per frame with delta time. This decouples animation logic from `IDispatcherTimer` (which may not align with frame boundaries) and provides vsync-like behavior tied to the actual render cycle.

**Component 7: Allocation-free hot path audit.** Profile the MAUI handler bridge for per-frame allocations. Key areas to examine: PropertyMapper delegate invocations, `BindableProperty` change notifications (`PropertyChanged` event args), layout `Size`/`Rect` struct boxing, and dispatcher queue node allocations. Where allocations are found, introduce object pooling or `stackalloc`-based alternatives. XenoAtom's cell buffer already uses pre-allocated arrays — ensure the MAUI bridge doesn't undermine this.

The expected performance envelope: with differential rendering, a **200×50 terminal** (10,000 cells) where animation changes ~500 cells per frame should sustain **60 fps** comfortably. Full-screen RGB color changes on every cell will drop to **15–30 fps** depending on the terminal emulator's ANSI parsing throughput. The MAUI layout bypass for the canvas region eliminates the most expensive per-frame cost, leaving XenoAtom's optimized diff → output pipeline as the only bottleneck.

---

## What the architecture looks like end-to-end

The target data flow for a 60-fps ASCII animation through MAUI.Tui is:

1. `TuiTicker` fires via XenoAtom's `onUpdate` callback
2. MAUI's `AnimationManager` processes active animations, calling user callbacks with interpolated values
3. `AsciiAnimation.Advance(deltaTime)` selects the current frame
4. `AsciiCanvasView`'s draw callback writes the frame's cell data directly to XenoAtom's `Canvas` visual
5. No `InvalidateMeasure` fires — the canvas has fixed bounds and bypasses MAUI layout
6. XenoAtom's reactive system detects the canvas visual is dirty
7. Render pass writes canvas cells to the cell buffer
8. Diff finds ~500 changed cells, generates ~2–5 KB of ANSI escape sequences
9. Synchronized output protocol wraps the batch, terminal renders atomically

The critical architectural principle is **two rendering tiers**: MAUI's full pipeline handles the static UI shell (navigation, controls, layout), while the animation region bypasses MAUI entirely after initial layout and writes directly through XenoAtom. This mirrors how SkiaSharp's `SKGLView` with `HasRenderLoop = true` bypasses MAUI's rendering for its canvas area on graphical platforms, and how Consolonia successfully renders Avalonia animations through a terminal backend. The MAUI handler architecture is extensible enough to support this — the `ViewHandler<AsciiCanvasView, XenoAtomCanvas>` contract requires only `CreatePlatformView`, `ConnectHandler`, `DisconnectHandler`, and property mappers, all of which can be optimized for the animation case.