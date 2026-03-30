# XenoAtom.Terminal.UI — Patterns and Conventions

> This file is auto-loaded by Claude Code. Follow these patterns when writing code
> that interacts with XenoAtom.Terminal.UI, XenoAtom.Terminal, or XenoAtom.Ansi.

## Reactive State System

XenoAtom uses `State<T>` for reactive data binding. When a `State<T>.Value` changes,
only visuals that read that state during their last render pass are automatically
invalidated. Never manually force redraws — update state and let the framework react.

```csharp
// CORRECT: update state, framework handles invalidation
var frameIndex = new State<int>(0);
frameIndex.Value = nextFrame; // only visuals reading frameIndex re-render

// WRONG: manually marking things dirty or calling redraw methods
visual.Invalidate(); // don't do this unless you have a specific reason
```

## Cell Buffer and Rendering

All terminal output goes through XenoAtom's cell-buffer → diff → batched ANSI pipeline.
Never write ANSI escape sequences directly to Console or stdout.

```csharp
// WRONG — bypasses the renderer, causes flicker, breaks synchronized output
Console.Write("\x1b[31mRed text\x1b[0m");

// CORRECT — render through XenoAtom visuals
var label = new TextBlock { Text = "Red text", Foreground = Color.Red };
```

## Zero-Allocation Hot Paths

XenoAtom is designed for allocation-free rendering. Match this in all code that
runs per-frame (inside onUpdate callbacks, State<T> change handlers, render methods):

- Use `Span<T>` and `ReadOnlySpan<T>` instead of allocating arrays
- Use `stackalloc` for small temporary buffers (< 1KB)
- Use `ArrayPool<T>.Shared` for larger buffers, return after use
- Avoid string interpolation (`$"..."`) in hot paths — it allocates
- Avoid LINQ in hot paths — extension methods allocate enumerators
- Avoid lambda captures that close over local variables — they allocate
- Pre-allocate EventArgs instances and reuse them
- Use `struct` for small data types that appear in arrays or collections

```csharp
// WRONG — allocates string every frame
label.Text = $"FPS: {currentFps:F1}";

// CORRECT — use a pre-allocated char buffer or update only when value changes
if (currentFps != lastFps)
{
    label.Text = string.Create(null, stackalloc char[16], $"FPS: {currentFps:F1}");
    lastFps = currentFps;
}
```

## Terminal.Run() Event Loop

The fullscreen rendering loop is:

```csharp
Terminal.Run(rootVisual, (terminal) =>
{
    // This fires once per frame iteration
    // 1. Advance animation state here
    // 2. Update State<T> values (triggers reactive invalidation)
    // 3. Return Continue or StopAndKeepVisual

    return TerminalLoopResult.Continue;
});
```

The framework then runs: measure → arrange → render dirty visuals → diff cell buffer → batched ANSI output with synchronized output protocol (DEC mode 2026).

## Terminal.Live() for Inline Animation

For animations embedded in scrolling output (like the Copilot CLI banner),
use `Terminal.Live()` instead of `Terminal.Run()`:

```csharp
Terminal.Live(visual, (terminal) =>
{
    // Updates the visual in-place without entering fullscreen mode
    return TerminalLoopResult.Continue;
});
```

## Color System

XenoAtom uses RGBA colors with alpha blending support. Colors can be specified as:

- Named: `Color.Red`, `Color.Cyan`, `Color.Transparent`
- RGBA: `new Color(255, 128, 0, 255)`
- Gradients: `Brush.LinearGradient()` with `GradientStop` and `ColorMixSpace.Oklab`

For terminal compatibility, prefer 4-bit ANSI colors for maximum portability,
or use full RGBA when targeting modern terminals (Windows Terminal, kitty, Alacritty, Ghostty).

## Built-in Controls Relevant to Animation

- `Canvas` — custom drawing surface (Visualization category)
- `TextFiglet` — FIGlet ASCII art text with gradient brush support
- `Spinner` — built-in frame-cycling animation control
- `Sparkline`, `LineChart`, `BarChart` — data visualization controls
- `ProgressBar` — animated progress with multiple styles

## Debug Overlay

Press F12 in any fullscreen app to see real-time diagnostics:
FPS, dirty-region visualization, diff statistics, per-pass timing
(update, measure, arrange, render, diff, output).

## NativeAOT Compatibility

XenoAtom targets `net10.0` and is NativeAOT-oriented. Avoid:
- `System.Reflection` usage (use source generators instead)
- Dynamic type loading
- Unbounded generic instantiation

## Package Dependencies

The XenoAtom stack layers are:

```
XenoAtom.Terminal.UI  (controls, layout, styling, binding)
    └── XenoAtom.Terminal  (cross-platform terminal I/O, atomic writes)
        └── XenoAtom.Ansi  (VT100/ECMA-48 escape sequence generation/parsing)
```

Related packages: `XenoAtom.Collections` (struct-based collections),
`XenoAtom.Allocators` (TLSF allocation), `XenoAtom.Logging` (zero-alloc logging).
