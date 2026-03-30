# XenoAtom can hit 30 fps — and it's architected for it

**Yes, the XenoAtom terminal stack can achieve 30–60 fps ASCII animation in C#/.NET**, and it is arguably the only .NET terminal library today with the architecture to do so out of the box. XenoAtom.Terminal.UI already implements cell-buffer diffing, dirty-region tracking, synchronized output (DEC mode 2026), and batched ANSI writes — the exact four pillars required for flicker-free high-frame-rate terminal rendering. The real bottleneck is never the .NET application; it is the terminal emulator's rendering pipeline. On GPU-accelerated terminals like kitty, Alacritty, Windows Terminal, and Ghostty, **30 fps full-screen and 60 fps partial-screen animation are practical targets**. On older terminals like GNOME Terminal (VTE) or iTerm2 at 4K resolution, even 15 fps can stutter. The Copilot CLI banner that inspired this question actually runs at only ~6–7 fps, making XenoAtom's architecture dramatically overqualified for the task.

---

## The XenoAtom stack is a three-layer rendering pipeline

Alexandre Mutel (xoofx) — known for Markdig, SharpDX, and a long demoscene background — designed XenoAtom as a layered terminal stack targeting .NET 10 with NativeAOT compatibility. The architecture splits into three distinct packages, each handling a different abstraction level.

**XenoAtom.Ansi** (v1.6.0) is the foundation layer. It provides allocation-friendly VT100/ECMA-48/ISO-6429 escape sequence generation, parsing via a tokenizer, and ANSI-aware text utilities. The library explicitly advertises "fast, allocation-friendly" design — meaning `Span<T>`-based APIs, no per-call heap allocations, and pre-encoded escape sequence fragments. It supports OSC 8 hyperlinks and the full SGR attribute set.

**XenoAtom.Terminal** (v1.8.0) replaces `System.Console` entirely. It provides unified input events (keyboard, mouse, resize), thread-safe atomic writes to stdout, CI-friendly color detection, and an in-memory terminal backend for testing. The "atomic writes" detail matters: output from concurrent threads never interleaves mid-frame. The in-memory backend confirms the I/O layer is abstracted and pluggable — on Linux, it likely writes to raw file descriptors through the broader XenoAtom.Interop.musl P/Invoke layer, bypassing managed stream overhead.

**XenoAtom.Terminal.UI** (v1.13.1) is the retained-mode UI framework and the layer most relevant to animation. Its rendering architecture hits every checkbox on the high-performance terminal animation requirements list:

- **Cell-buffer renderer with frame diffing** — maintains an internal grid of character cells with style metadata, diffs between the current and previous frame, and emits only changed cells
- **Dirty-region tracking** — only visuals marked as invalidated are re-rendered into the cell buffer, not the entire visual tree
- **Batched ANSI output** — escape sequences are coalesced and written in a single I/O operation per frame
- **Synchronized output (DEC mode 2026)** — wraps frame output in `\x1b[?2026h` / `\x1b[?2026l` markers so the terminal holds rendering until the complete frame arrives
- **Built-in FPS overlay** — press F12 in any fullscreen app to see real-time FPS, dirty-region counts, diff statistics, and per-pass timing

The three rendering modes map directly to animation use cases. `Terminal.Write(visual)` renders a visual once inline. `Terminal.Live(visual, onUpdate)` creates a live inline region that updates in place — ideal for a Copilot-style banner that animates within scrolling output. `Terminal.Run(visual, onUpdate)` enters a full-screen event loop with continuous rendering — the mode for full-screen ASCII art animation. The loop is controlled by returning `TerminalLoopResult.Continue` or `TerminalLoopResult.StopAndKeepVisual`.

The reactive binding system (`State<T>`) means you don't manually trigger redraws. Change a state value and only the visuals that read that value during their last render pass are invalidated. For animation, you'd update a `State<int>` frame counter on a timer, and the framework handles minimal redraws automatically.

---

## What the Copilot CLI banner actually requires is modest

The GitHub Engineering blog post reveals that the Copilot CLI's animated ASCII banner is far less demanding than it appears. Cameron Foxly designed ~**20 frames of hand-crafted ASCII art** at **11 rows × 78 columns** each, playing over ~3 seconds for an effective frame rate of **6–7 fps**. The animation shows a 3D Copilot mascot flying in to reveal a block-letter "COPILOT" logo. Frames were created in ASCII Motion (ascii-motion.app), a React/TypeScript tool Foxly built for layer-based ASCII animation.

The rendering architecture uses a **content-role-theme separation pattern**. Frame content is stored as plain text strings. A color map associates `"row,col"` coordinates with semantic element names (`block_text`, `eyes`, `goggles`, `stars`). At runtime, a theme object maps these semantic roles to **4-bit ANSI color codes** — not 256-color or truecolor. This deliberate constraint ensures users can customize colors through their terminal preferences, which is critical for accessibility.

```typescript
const ANIMATION_ANSI_DARK: AnimationTheme = {
  block_text: "cyan",
  block_shadow: "white",
  eyes: "greenBright",
  head: "magentaBright",
  goggles: "cyanBright",
};
```

The CLI is a Node.js SEA (Single Executable Application) using Ink (React for terminals) for the overall UI, but the animation logic is handcrafted because Ink's asynchronous re-rendering model doesn't support precise frame-by-frame timing. Output goes to raw stdout with ANSI escape sequences and Node.js `readline` module cursor control. The implementation has **known flickering issues** — heavy use of `clearScreenDown` (CSI 0J) causes visible flicker in iTerm2 and Visual Studio's terminal during streaming responses. Users report 30% CPU usage on M4 Max MacBooks in iTerm2.

The engineering lesson is clear: **Copilot's banner animation is achievable with far less optimization than XenoAtom provides**. A cell-buffer-with-diffing approach would eliminate the flickering Copilot currently suffers from.

---

## Four strategies for high-fps .NET terminal animation, ranked

**Strategy 1: XenoAtom.Terminal.UI's built-in rendering loop (recommended).** Use `Terminal.Run()` with a frame-counter `State<int>` incremented by a `Stopwatch`-based timer. Pre-compute all ASCII art frames as string arrays. Create a custom visual that reads the current frame index, writes the appropriate characters and colors into the cell buffer. The framework handles diffing, batching, synchronized output, and cursor management. Expected overhead per frame: **2–5 ms for diffing + 1–2 ms for I/O**, well within the 16.7 ms budget for 60 fps. This approach requires .NET 10 and C# 14.

**Strategy 2: Custom double-buffered renderer on XenoAtom.Terminal + XenoAtom.Ansi.** If you need control below the UI framework level, build a custom cell buffer on top of the lower two layers. Allocate two `Cell[rows, cols]` arrays at startup (each cell: `char` + foreground + background + attributes as value types). Render into the back buffer, diff against the front buffer, use XenoAtom.Ansi's writer to emit minimal escape sequences into a pre-allocated `byte[]` from `ArrayPool<byte>.Shared`, wrap in synchronized output markers, write to `Console.OpenStandardOutput()` as a single flush, swap buffers. This gives maximum control at the cost of reimplementing what Terminal.UI already provides.

**Strategy 3: Raw `StreamWriter` on `Console.OpenStandardOutput()` with manual ANSI.** The zero-dependency approach. `Console.Write()` auto-flushes on every call — **4.7× slower** than buffered `StreamWriter` output. Instead:

```csharp
using var stdout = new StreamWriter(
    Console.OpenStandardOutput(), 
    Encoding.UTF8, 
    bufferSize: 32768);
stdout.AutoFlush = false;

// Per frame:
stdout.Write("\x1b[?2026h");  // begin synchronized output
stdout.Write("\x1b[?25l");     // hide cursor
stdout.Write("\x1b[H");        // cursor home
// ... write frame content with ANSI color codes ...
stdout.Write("\x1b[?25h");     // show cursor
stdout.Write("\x1b[?2026l");  // end synchronized output
stdout.Flush();
```

This works but you must implement your own dirty-tracking or accept full-screen redraws. Without diffing, a **200×50 terminal at 60 fps generates ~18–24 MB/s** of escape sequence data — within modern terminal throughput limits but wasteful.

**Strategy 4: P/Invoke to native write syscalls.** On Linux, bypass managed streams entirely with `write(STDOUT_FILENO, buffer, length)` via P/Invoke or XenoAtom.Interop.musl. On Windows, use `WriteConsoleW` or `WriteFile` to the console output handle. This eliminates all managed stream overhead but provides marginal improvement over Strategy 3's buffered `StreamWriter` — **the syscall itself is not the bottleneck**. Only justified if profiling shows managed stream overhead is significant in your specific scenario.

---

## The real bottleneck is always the terminal emulator

Application-side frame generation in .NET takes **3–8 ms** with proper optimization. The remaining ~9–14 ms of the 16.7 ms frame budget belongs to the terminal emulator. Terminal rendering performance varies dramatically:

| Terminal | Renderer | Animation suitability |
|----------|----------|-----------------------|
| **kitty** | OpenGL, async render thread, configurable `repaint_delay` (default 10 ms) | Excellent. Best throughput. Tune `repaint_delay 2`, `input_delay 0` for lowest latency |
| **Alacritty** | OpenGL, GPU glyph caching | Excellent. **6.9 ms** measured latency. Renders at monitor refresh rate |
| **Ghostty** | Metal/Vulkan/DirectX (platform-native) | Excellent. 2–5× better throughput than WezTerm |
| **Windows Terminal** | DirectX AtlasEngine, vsync | Good. Previously locked at 60 fps, improved in v1.22+. Supports synchronized output |
| **iTerm2** | Core Text (macOS) | Poor for animation. Can take **>150 ms per frame** at 4K on older hardware |
| **GNOME Terminal (VTE)** | CPU-based rendering, writes scrollback to disk | Poor. No synchronized output support. Higher latency |
| **Windows conhost** | GDI-based | Marginal. VT support requires explicit `SetConsoleMode` flag. Not recommended |

**Synchronized output (mode 2026) is the single most impactful optimization.** Without it, the terminal may repaint mid-frame, causing tearing. With it, throughput improves **20–50%** because the terminal avoids wasted partial renders. It's supported by kitty, Alacritty, Windows Terminal, WezTerm, iTerm2, foot, and mintty — but notably **not** by GNOME Terminal/VTE.

---

## Avoiding GC pauses requires discipline, not magic

At 60 fps, a single .NET Gen 2 garbage collection pause of **30+ ms** drops two frames visibly. The defense is straightforward: **zero allocations in the render loop**. XenoAtom's allocation-friendly design philosophy aligns with this requirement. The practical techniques are:

- **Pre-allocate all buffers at startup**: cell grids, output byte arrays, escape sequence scratch buffers. Reuse every frame.
- **Use `ArrayPool<byte>.Shared`** for any dynamically-sized buffers. Return after each frame.
- **Use `Span<T>` and `stackalloc`** for per-frame temporaries under ~1 KB. These are stack-allocated, invisible to GC.
- **Set `GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency`** to suppress full blocking collections except under memory pressure.
- **Avoid hidden allocations**: string interpolation, LINQ expressions, lambda captures, and boxing all silently allocate. Use `[MemoryDiagnoser]` in BenchmarkDotNet to audit the hot path.

XenoAtom.Terminal.UI's retained-mode reactive model naturally minimizes allocations — state changes invalidate specific visuals rather than rebuilding the entire tree, and the framework's cell buffer is long-lived and reused across frames. The ecosystem's related libraries (XenoAtom.Collections with struct-based collections, XenoAtom.Allocators with TLSF allocation, XenoAtom.Logging with zero-alloc hot paths) confirm this is a core design principle throughout Mutel's stack.

---

## No other .NET library comes close for animation

**Spectre.Console** uses cursor repositioning for live updates — no cell diffing, no synchronized output, no dirty-region tracking. Its `LiveDisplay` works for progress bars but is architecturally unsuitable for frame-based animation. **Terminal.Gui** (gui.cs) has damage-region tracking but is an event-driven widget toolkit with documented performance issues on Linux/curses; its v2 rewrite acknowledges "deep flaws and poor performance" in the ConsoleDriver architecture. **ConsoleRenderer** explicitly documents that "these examples generally don't achieve framerates north of **10 fps**" for full-screen updates. **SadConsole** achieves 60+ fps easily but renders to a graphical window via MonoGame/SFML — it doesn't output to an actual terminal.

XenoAtom.Terminal.UI is unique in the .NET ecosystem in combining cell-buffer diffing, dirty-region invalidation, synchronized output, and batched writes in a single framework with an explicit rendering loop and built-in performance instrumentation.

---

## Conclusion

The XenoAtom stack is not just capable of Copilot-style terminal animation — it's overengineered for it. The Copilot banner runs at 6–7 fps on a tiny 11×78 grid in Node.js with known flickering. XenoAtom.Terminal.UI's cell-diffing renderer with synchronized output and dirty-region tracking could replicate this with zero flicker and headroom for **4–5× higher frame rates**. For a full-screen animation targeting 30 fps on kitty, Alacritty, or Windows Terminal, the practical approach is: pre-compute frames, load them into a `Terminal.Run()` loop, use `State<int>` for frame advancement with a `Stopwatch`-based timer, and let the framework handle the rest. The .NET 10 / C# 14 requirement is the main adoption barrier — not performance. The terminal emulator is the ceiling, and on any GPU-accelerated terminal built after 2020, that ceiling comfortably accommodates 30 fps full-screen and 60 fps partial-screen animation from a .NET process.