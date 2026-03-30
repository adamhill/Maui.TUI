# MAUI.Tui Animation Pipeline — Implementation Spec for Claude Code

> **Purpose:** This document is the task-by-task implementation plan for adding 30–60 fps ASCII animation support to [Redth/MAUI.Tui](https://github.com/Redth/MAUI.Tui), which renders .NET MAUI apps in the terminal via [XenoAtom.Terminal.UI](https://github.com/XenoAtom/XenoAtom.Terminal.UI).
>
> **How to use:** Feed tasks to Claude Code one at a time, in order. Each task lists its dependencies, inputs, outputs, files to create/modify, acceptance criteria, and recommended model. Complete Task 0 (project setup) first, then work through Tasks 1–7 sequentially.
>
> **Tech stack:** .NET 10, C# 14, XenoAtom.Terminal.UI 1.13.1+, MAUI handler architecture (`ViewHandler<TVirtualView, TPlatformView>`)

---

## Architecture Overview

The animation pipeline uses a **dual-tier rendering** approach:

```
┌─────────────────────────────────────────────────────┐
│  TIER 1: MAUI Layout Pipeline (static UI shell)     │
│  Grid / StackLayout / FlexLayout → Handlers →       │
│  XenoAtom Visuals → normal measure/arrange/render   │
├─────────────────────────────────────────────────────┤
│  TIER 2: Direct Cell-Buffer Pipeline (animation)    │
│  AsciiCanvasView → bypasses MAUI layout →           │
│  writes directly to XenoAtom Canvas visual →        │
│  cell-buffer diff → batched ANSI output             │
└─────────────────────────────────────────────────────┘
```

**Why two tiers?** MAUI's measure/arrange cascade costs 2–8ms per pass depending on tree depth. At 60 fps (16.7ms budget), that's 12–48% of the frame. Animation regions that change every frame must bypass this. Static UI (navigation, status bars, controls) uses MAUI normally.

**Key invariant:** All rendering ultimately flows through XenoAtom.Terminal.UI's cell-buffer → diff → synchronized output pipeline. We never bypass XenoAtom's renderer — we bypass MAUI's *layout* engine for animated regions, but still render through XenoAtom visuals.

---

## Reference: Existing MAUI.Tui Code Structure

Before starting, familiarize yourself with the repo layout:

```
src/Maui.TUI/
├── Handlers/                    # 25+ MAUI → XenoAtom handler mappings
│   ├── ButtonHandler.cs         # ViewHandler<IButton, Button>
│   ├── LabelHandler.cs          # ViewHandler<ILabel, TextBlock>
│   ├── EntryHandler.cs          # ViewHandler<IEntry, TextBox>
│   └── ...
├── Platform/
│   ├── MauiTuiApplication.cs    # Entry point, calls Terminal.Run()
│   ├── TuiDispatcherProvider.cs # Marshals MAUI work to terminal thread
│   ├── TuiMauiContext.cs        # IMauiContext implementation
│   └── TuiAlertManager.cs       # Modal dialogs
├── Hosting/
│   └── AppHostBuilderExtensions.cs  # UseMauiAppTUI<T>() registration
└── Maui.TUI.csproj
```

**Critical integration point:** `MauiTuiApplication.Run()` calls `Terminal.Run(visual, onUpdate)` which is XenoAtom's fullscreen event loop. The `onUpdate` delegate fires once per frame iteration — this is where animation state must advance.

---

## Task 0: Project Scaffolding & Configuration

**Model:** Sonnet
**Effort:** 15 minutes
**Dependencies:** None

### Objective
Set up the project structure, CLAUDE.md, agent configuration, and MCP servers for the animation work. This task creates the foundation every subsequent task builds on.

### Steps

1. **Fork and clone** `https://github.com/Redth/Maui.TUI`

2. **Create `CLAUDE.md`** in the repo root:

```markdown
# CLAUDE.md — MAUI.Tui Animation Layer

## Project
Adding 30–60 fps ASCII animation support to MAUI.Tui.
MAUI.Tui renders .NET MAUI apps in the terminal via XenoAtom.Terminal.UI.

## Tech Stack
- .NET 10, C# 14 (extension members, primary constructors, NativeAOT-oriented)
- XenoAtom.Terminal.UI 1.13.1+ (retained-mode reactive TUI framework)
- XenoAtom.Ansi (allocation-friendly VT100 escape sequence generation)
- XenoAtom.Terminal (cross-platform terminal I/O with atomic writes)
- MAUI handler architecture: ViewHandler<TVirtualView, TPlatformView>

## Commands
- Build: `dotnet build src/Maui.TUI/Maui.TUI.csproj`
- Test: `dotnet test tests/Maui.TUI.Animation.Tests/`
- Run demo: `dotnet run --project samples/AnimationDemo/`
- Format: `dotnet format src/Maui.TUI/Maui.TUI.csproj`

## Architecture Rules
- Animation rendering goes through XenoAtom's cell-buffer diffing — NEVER write ANSI escape sequences directly to stdout
- Animated regions bypass MAUI's measure/arrange pipeline but still render through XenoAtom visuals
- Zero heap allocations in the per-frame render hot path — use Span<T>, stackalloc, ArrayPool, pre-allocated buffers
- State changes use XenoAtom's State<T> reactive system for automatic dirty-region tracking
- All new public APIs must have XML doc comments
- Follow existing handler patterns in src/Maui.TUI/Handlers/ for consistency

## Key Patterns
- Handlers: static Map* methods in PropertyMapper, e.g. `static void MapText(LabelHandler h, ILabel v)`
- Platform views: XenoAtom.Terminal.UI control types (TextBlock, Button, Canvas, etc.)
- Layout: MAUI's CrossPlatformMeasure/CrossPlatformArrange translate double → integer cells
- Invalidation: property change → PropertyMapper → handler updates XenoAtom visual → reactive system marks dirty

## Reference Docs
- Implementation spec: docs/tasks/animation-pipeline-spec.md
- XenoAtom research: docs/research/xenoatom-animation-analysis.md
- MAUI.Tui analysis: docs/research/maui-tui-animation-plan.md

## Querying Documentation
Use `microsoft_docs_search` and `microsoft_code_sample_search` MCP tools
when working with .NET MAUI APIs. Use web search for XenoAtom.Terminal.UI
docs at https://xenoatom.github.io/terminal/
```

3. **Create `.mcp.json`** in the repo root:

```json
{
  "mcpServers": {
    "microsoft-learn": {
      "type": "http",
      "url": "https://learn.microsoft.com/api/mcp"
    }
  }
}
```

4. **Create `docs/tasks/`** directory and save this spec file there as `animation-pipeline-spec.md`

5. **Create `docs/research/`** directory and save the three research artifacts there

6. **Create test project:**
```bash
dotnet new xunit -n Maui.TUI.Animation.Tests -o tests/Maui.TUI.Animation.Tests
cd tests/Maui.TUI.Animation.Tests
dotnet add reference ../../src/Maui.TUI/Maui.TUI.csproj
```

7. **Create sample project:**
```bash
dotnet new console -n AnimationDemo -o samples/AnimationDemo
cd samples/AnimationDemo
dotnet add reference ../../src/Maui.TUI/Maui.TUI.csproj
```

### Acceptance Criteria
- [ ] `dotnet build` succeeds for main project, test project, and sample project
- [ ] CLAUDE.md exists and references all spec/research docs
- [ ] .mcp.json configured with Microsoft Learn MCP
- [ ] Directory structure matches the layout above

---

## Task 1: TuiTicker — Animation Tick Source

**Model:** Opus (architectural decision) → Sonnet (implementation)
**Effort:** 1–2 hours
**Dependencies:** Task 0
**Priority:** CRITICAL — everything else depends on this

### Objective
Implement `ITicker` for the TUI backend that hooks into XenoAtom's `Terminal.Run()` frame loop. This unlocks MAUI's entire animation system: `Animation.Commit()`, `ViewExtensions` (FadeTo, TranslateTo, ScaleTo), and `IDispatcherTimer`.

### Background
MAUI's animation system is driven by an `ITicker` that fires at a configurable rate. On iOS it's backed by `CADisplayLink`, on Android by `Choreographer`. For TUI, we need to fire it from XenoAtom's `onUpdate` callback in `Terminal.Run()`.

The `ITicker` interface:
```csharp
public interface ITicker
{
    bool IsRunning { get; }
    bool SystemEnabled { get; } // accessibility: reduced motion
    int MaxFps { get; set; }
    void Start();
    void Stop();
    event EventHandler<EventArgs>? Fire;
}
```

### Files to Create

**`src/Maui.TUI/Platform/TuiTicker.cs`**

```csharp
// Implement ITicker that:
// 1. Tracks elapsed time via Stopwatch (NOT DateTime — monotonic clock)
// 2. Fires at MaxFps rate (default 30, configurable)
// 3. Has a Tick() method called from the onUpdate delegate
// 4. Respects Start/Stop state
// 5. ZERO allocations per tick — pre-allocate EventArgs
//
// Key design decisions:
// - MaxFps defaults to 30 (not 60) because terminal emulators are the bottleneck
// - SystemEnabled should check for NO_MOTION or TERM_PROGRAM hints for accessibility
// - Fire event uses a cached EventArgs.Empty, never allocates
// - Frame pacing: skip Fire if less than (1000/MaxFps) ms elapsed since last fire
```

### Files to Modify

**`src/Maui.TUI/Platform/MauiTuiApplication.cs`**
- Store a reference to the `TuiTicker` instance
- In the `Terminal.Run()` onUpdate callback, call `_ticker.Tick()`
- The ticker must be created BEFORE `Terminal.Run()` starts

**`src/Maui.TUI/Hosting/AppHostBuilderExtensions.cs`**
- Register `TuiTicker` as the `ITicker` service: `services.AddSingleton<ITicker, TuiTicker>()`
- Register `IDispatcherTimer` factory if not already present

### Test Criteria

Write tests in `tests/Maui.TUI.Animation.Tests/TuiTickerTests.cs`:

```csharp
// Test 1: Ticker fires at approximately the configured FPS
// - Create ticker, set MaxFps = 30
// - Call Tick() in a loop with Thread.Sleep(1) for 1 second
// - Assert Fire event was raised 28–32 times (tolerance for timing)

// Test 2: Ticker respects Start/Stop
// - Create ticker, DON'T call Start()
// - Call Tick() 100 times
// - Assert Fire event was never raised
// - Call Start(), Tick() 100 more times
// - Assert Fire event was raised

// Test 3: Ticker has zero allocations per tick
// - Use GC.GetAllocatedBytesForCurrentThread() before/after 1000 Tick() calls
// - Assert delta is 0 (or very close — allow for JIT warmup)

// Test 4: MaxFps can be changed at runtime
// - Start at MaxFps=30, verify ~30 fires/sec
// - Change to MaxFps=60, verify ~60 fires/sec
```

### Acceptance Criteria
- [ ] `ITicker` implementation registered in DI
- [ ] MAUI's `Animation.Commit()` calls propagate to terminal rendering
- [ ] `IDispatcherTimer` works (create timer, verify callback fires)
- [ ] No per-tick heap allocations (verified by test)
- [ ] MaxFps defaults to 30, configurable via property

---

## Task 2: CellBuffer Abstraction

**Model:** Sonnet
**Effort:** 1–2 hours
**Dependencies:** Task 0
**Priority:** HIGH — Task 3 depends on this

### Objective
Create a lightweight, allocation-free abstraction over a rectangular grid of terminal cells. This is the data structure that `AsciiCanvasView` (Task 3) will write frames into, and that the handler will transfer to XenoAtom's canvas visual.

### Background
A terminal cell is: one character (Rune) + foreground color + background color + text attributes (bold, italic, underline, etc.). XenoAtom.Terminal.UI already has internal cell representations, but we need a MAUI-side buffer that the user's animation code writes into before the handler transfers it to XenoAtom.

### Files to Create

**`src/Maui.TUI/Animation/TerminalCell.cs`**

```csharp
// Value type (struct) — MUST be a struct for array performance
// Fields:
//   Rune Character  (System.Text.Rune, handles Unicode correctly)
//   Color Foreground (Microsoft.Maui.Graphics.Color — maps to ANSI later)
//   Color Background
//   CellAttributes Attributes (flags enum: None, Bold, Italic, Underline, Strikethrough, Dim)
//
// Include a static readonly TerminalCell Empty = new(' ', Colors.White, Colors.Transparent)
// Include bool IsEmpty => Character == ' ' && Attributes == CellAttributes.None
```

**`src/Maui.TUI/Animation/CellBuffer.cs`**

```csharp
// A fixed-size 2D grid of TerminalCell values
// Backed by a single TerminalCell[] array (row-major order) — NOT TerminalCell[,]
// Single array = cache-friendly, Span-sliceable
//
// Constructor: CellBuffer(int width, int height)
//   - Pre-allocates the array once, reuses forever
//
// Core API:
//   ref TerminalCell this[int col, int row]  // by-ref indexer for zero-copy writes
//   void SetCell(int col, int row, Rune ch, Color fg, Color bg)
//   void SetCell(int col, int row, Rune ch, Color fg)  // transparent bg
//   void DrawString(int col, int row, ReadOnlySpan<char> text, Color fg, Color bg)
//   void DrawString(int col, int row, ReadOnlySpan<char> text, Color fg) // transparent bg
//   void FillRect(int col, int row, int width, int height, TerminalCell cell)
//   void Clear()  // fills with TerminalCell.Empty
//   void Clear(int col, int row, int width, int height)  // clear subregion
//   void CopyFrom(CellBuffer source)  // blit entire buffer
//   void CopyFrom(CellBuffer source, int srcCol, int srcRow, int destCol, int destRow, int w, int h)
//   Span<TerminalCell> GetRow(int row) // returns span over one row for bulk ops
//   int Width { get; }
//   int Height { get; }
//
// CRITICAL: No bounds-check exceptions in release — use [MethodImpl(AggressiveInlining)]
//   and conditional Debug.Assert. Out-of-bounds writes are silently clipped (like GPU rasterizers).
//
// Resize(int newWidth, int newHeight):
//   - Allocates new array only if size increased
//   - Copies existing content that fits
//   - Returns old array to ArrayPool if it came from there
```

**`src/Maui.TUI/Animation/CellAttributes.cs`**

```csharp
[Flags]
public enum CellAttributes : byte
{
    None = 0,
    Bold = 1,
    Italic = 2,
    Underline = 4,
    Strikethrough = 8,
    Dim = 16,
    Blink = 32,      // some terminals support this
    Reverse = 64,    // swap fg/bg
}
```

### Test Criteria

Write tests in `tests/Maui.TUI.Animation.Tests/CellBufferTests.cs`:

```csharp
// Test 1: SetCell and indexer roundtrip
// Test 2: DrawString writes correct characters at correct positions
// Test 3: DrawString clips at buffer boundary (no exception)
// Test 4: SetCell with out-of-bounds coordinates is silently ignored
// Test 5: Clear resets all cells to TerminalCell.Empty
// Test 6: FillRect fills the correct subregion
// Test 7: CopyFrom transfers cell data correctly
// Test 8: CopyFrom with subregion clips at boundaries
// Test 9: GetRow returns correct span length and content
// Test 10: Zero allocations during 10,000 SetCell + DrawString calls
//          (measure with GC.GetAllocatedBytesForCurrentThread)
```

### Acceptance Criteria
- [ ] `TerminalCell` is a value type (struct), ≤ 32 bytes
- [ ] `CellBuffer` uses single flat array, row-major
- [ ] All drawing operations clip silently at boundaries
- [ ] Zero heap allocations for all drawing operations (verified by test)
- [ ] By-ref indexer allows direct cell mutation without copying

---

## Task 3: AsciiCanvasView and Handler

**Model:** Opus (handler architecture) → Sonnet (implementation)
**Effort:** 2–3 hours
**Dependencies:** Task 1 (TuiTicker), Task 2 (CellBuffer)
**Priority:** CRITICAL — this is the core animation primitive

### Objective
Create a custom MAUI View that maps to an XenoAtom Canvas visual, bypasses MAUI's layout system for its interior, and exposes a `CellBuffer`-based drawing API. This is the view users place in their MAUI page to render animations.

### Background
Think of this as the TUI equivalent of `SKCanvasView` in SkiaSharp — a rectangular region where the user has pixel-level (cell-level) control, embedded within a MAUI layout.

### Files to Create

**`src/Maui.TUI/Controls/IAsciiCanvasView.cs`** (the MAUI interface)

```csharp
// Extends IView (standard MAUI virtual view interface)
//
// Properties:
//   int CanvasWidth { get; }           // width in cells (columns)
//   int CanvasHeight { get; }          // height in cells (rows)
//   bool IsAnimating { get; set; }     // when true, requests continuous redraw
//   int TargetFps { get; set; }        // desired frame rate (default 30)
//
// Events:
//   event EventHandler<AsciiCanvasDrawEventArgs> DrawFrame;
//     // Fired each frame when IsAnimating=true
//     // The handler writes into the provided CellBuffer
//
// The event args:
// public class AsciiCanvasDrawEventArgs : EventArgs
// {
//     public CellBuffer Buffer { get; }    // pre-allocated, cleared before each frame
//     public TimeSpan ElapsedTime { get; } // total time since animation started
//     public TimeSpan DeltaTime { get; }   // time since last frame
//     public int FrameNumber { get; }      // monotonically increasing frame counter
// }
//
// NOTE: AsciiCanvasDrawEventArgs should be pooled/reused, not allocated per frame.
// Create ONE instance at handler creation time, update its fields each frame.
```

**`src/Maui.TUI/Controls/AsciiCanvasView.cs`** (the MAUI concrete view)

```csharp
// Extends Microsoft.Maui.Controls.View, implements IAsciiCanvasView
//
// BindableProperties:
//   CanvasWidthProperty (int, default 80)
//   CanvasHeightProperty (int, default 24)
//   IsAnimatingProperty (bool, default false)
//   TargetFpsProperty (int, default 30)
//
// CRITICAL LAYOUT BYPASS:
//   Override protected override Size MeasureOverride(double widthConstraint, double heightConstraint)
//   to return new Size(CanvasWidth, CanvasHeight) immediately WITHOUT calling base.
//   This prevents MAUI's layout engine from walking children or cascading invalidation.
//
//   Override protected override Size ArrangeOverride(Rect bounds)
//   to simply return new Size(CanvasWidth, CanvasHeight) — no-op arrange.
//
// Methods:
//   void Invalidate() — marks the canvas for redraw on next frame
//   void StartAnimation() — sets IsAnimating = true
//   void StopAnimation() — sets IsAnimating = false
```

**`src/Maui.TUI/Handlers/AsciiCanvasViewHandler.cs`** (the handler)

```csharp
// ViewHandler<IAsciiCanvasView, XenoAtomCanvasVisual>
//
// where XenoAtomCanvasVisual is whatever XenoAtom.Terminal.UI visual
// best supports direct cell writing. Investigate:
//   1. XenoAtom.Terminal.UI.Canvas (custom drawing surface) — preferred
//   2. A custom Visual subclass if Canvas doesn't expose cell-level writes
//   3. A Panel with manually positioned TextBlock children — fallback only
//
// PropertyMapper:
//   [nameof(IAsciiCanvasView.CanvasWidth)] = MapCanvasSize,
//   [nameof(IAsciiCanvasView.CanvasHeight)] = MapCanvasSize,
//   [nameof(IAsciiCanvasView.IsAnimating)] = MapIsAnimating,
//
// ANIMATION LOOP (the critical path):
//   When IsAnimating is true, the handler registers a callback with TuiTicker
//   (or uses IDispatcherTimer). Each tick:
//     1. Update ElapsedTime and DeltaTime on the REUSED AsciiCanvasDrawEventArgs
//     2. Clear the CellBuffer (or don't — let user decide via a ClearBeforeDraw property)
//     3. Raise DrawFrame event — user code writes into CellBuffer
//     4. Transfer CellBuffer contents to the XenoAtom visual
//        (this is the handler's core job — mapping CellBuffer cells to XenoAtom cells)
//     5. Mark the XenoAtom visual as dirty via its invalidation API
//     6. XenoAtom's renderer handles the rest (diff, batch, synchronized output)
//
// CELL TRANSFER STRATEGY:
//   The handler must translate:
//     TerminalCell.Foreground (MAUI Color) → XenoAtom RGBA
//     TerminalCell.Background (MAUI Color) → XenoAtom RGBA
//     TerminalCell.Character (Rune) → XenoAtom Rune
//     TerminalCell.Attributes → XenoAtom text attributes
//   Pre-compute color conversions where possible. MAUI Color is float RGBA,
//   XenoAtom likely uses byte RGBA — do the conversion once, cache it.
//
// Register this handler in AppHostBuilderExtensions.cs:
//   handlers.AddHandler<AsciiCanvasView, AsciiCanvasViewHandler>();
```

### Files to Modify

**`src/Maui.TUI/Hosting/AppHostBuilderExtensions.cs`**
- Add handler registration for `AsciiCanvasView` → `AsciiCanvasViewHandler`

### Research Required (use Opus for this)
Before implementing the handler, you MUST understand XenoAtom.Terminal.UI's Canvas visual:
1. Read the XenoAtom.Terminal.UI source for the `Canvas` control
2. Determine how to write individual cells (character + color) to a Canvas
3. Check if Canvas supports a `Render(Action<ICellWriter>)` pattern or similar
4. If Canvas doesn't support cell-level writes, check if a custom `Visual` subclass can override rendering to write cells directly into the cell buffer

Use `microsoft_docs_search` MCP tool for MAUI handler patterns. Search: "create custom handler .NET MAUI"

### Test Criteria

```csharp
// Test 1: AsciiCanvasView.MeasureOverride returns CanvasWidth × CanvasHeight
//         without calling base (verify no layout cascade)

// Test 2: Handler creates platform view successfully

// Test 3: DrawFrame event fires when IsAnimating = true
//         (may need to mock/simulate the ticker)

// Test 4: DrawFrame event does NOT fire when IsAnimating = false

// Test 5: CellBuffer provided in DrawFrame has correct dimensions

// Test 6: Changing CanvasWidth/CanvasHeight resizes the CellBuffer

// Test 7: AsciiCanvasDrawEventArgs is reused (same instance each frame)
//         Verify with ReferenceEquals

// Test 8: Color conversion from MAUI Color to XenoAtom format is correct
//         Test edge cases: transparent, named colors, hex colors
```

### Acceptance Criteria
- [ ] `AsciiCanvasView` can be placed in any MAUI layout (Grid, StackLayout, etc.)
- [ ] Layout bypass verified: changing animation content does NOT trigger `InvalidateMeasure` on parent
- [ ] DrawFrame event fires at approximately TargetFps when IsAnimating = true
- [ ] CellBuffer contents render correctly in the terminal
- [ ] Zero per-frame allocations in the handler's animation loop
- [ ] Handler registered in DI and works with `UseMauiAppTUI<T>()`

---

## Task 4: AsciiAnimation — Pre-computed Frame Pipeline

**Model:** Sonnet
**Effort:** 1–2 hours
**Dependencies:** Task 2 (CellBuffer)
**Priority:** HIGH

### Objective
Create data structures for pre-computed ASCII art animation sequences — the .NET equivalent of the Copilot CLI's frame data format. This is how users define animations: as a sequence of pre-rendered frames with timing metadata.

### Background
The GitHub Copilot CLI stores ~20 frames of 11×78 ASCII art as string arrays with a separate color map. We want something similar but more structured, supporting both simple frame sequences and sprite-sheet style layouts.

### Files to Create

**`src/Maui.TUI/Animation/AsciiFrame.cs`**

```csharp
// Represents a single frame of ASCII animation
//
// Properties:
//   CellBuffer Cells { get; }          // the frame's content
//   TimeSpan Duration { get; set; }    // how long to display this frame (default 100ms)
//   int Width => Cells.Width;
//   int Height => Cells.Height;
//
// Static factory methods:
//   static AsciiFrame FromString(string content, Color fg, Color bg)
//     // Splits string by newlines, creates CellBuffer, fills with chars
//     // All chars get the same fg/bg colors
//
//   static AsciiFrame FromString(string content, Func<int, int, char, (Color fg, Color bg)> colorizer)
//     // Same as above but the colorizer function provides per-cell colors
//     // Parameters: column, row, character → (foreground, background)
//
//   static AsciiFrame FromStringWithColorMap(string content, Dictionary<string, Color> colorMap,
//                                             string[,] roleMap)
//     // Copilot CLI style: roleMap[row,col] = "block_text", colorMap["block_text"] = cyan
//     // This is the content-role-theme separation pattern
```

**`src/Maui.TUI/Animation/AsciiAnimation.cs`**

```csharp
// A sequence of AsciiFrames with playback control
//
// Constructor: AsciiAnimation(IReadOnlyList<AsciiFrame> frames)
//   All frames MUST have the same Width and Height (throw if not)
//
// Properties:
//   int Width { get; }
//   int Height { get; }
//   int FrameCount { get; }
//   int CurrentFrameIndex { get; }
//   AsciiFrame CurrentFrame { get; }
//   bool IsLooping { get; set; }       // default true
//   bool IsComplete { get; }           // true if non-looping and past last frame
//   TimeSpan TotalDuration { get; }    // sum of all frame durations
//   TimeSpan ElapsedTime { get; }
//   PlaybackState State { get; }       // Playing, Paused, Stopped
//
// Methods:
//   AsciiFrame Advance(TimeSpan deltaTime)
//     // Accumulates elapsed time, returns the frame that should display NOW
//     // If looping, wraps around. If not looping, clamps to last frame.
//     // Returns the SAME frame object if we haven't advanced — handler can check
//     // ReferenceEquals to skip redundant cell transfers.
//
//   void Reset()           // back to frame 0, elapsed = 0
//   void Pause()
//   void Resume()
//   void SeekTo(int frameIndex)
//   void SeekTo(TimeSpan time)
//
// Static factory:
//   static AsciiAnimation FromFrameStrings(string[] frameStrings, TimeSpan frameDuration,
//                                          Color fg, Color bg)
//     // Convenience: each string is one frame, all same duration and colors
//
//   static AsciiAnimation FromSpriteSheet(CellBuffer sheet, int frameWidth, int frameHeight,
//                                          int framesPerRow, int totalFrames, TimeSpan frameDuration)
//     // Extracts frames from a large CellBuffer arranged in a grid
```

**`src/Maui.TUI/Animation/AnimationTheme.cs`**

```csharp
// The Copilot CLI "content-role-theme" pattern
//
// A theme maps semantic role names to colors, enabling user customization
// and terminal-adaptive color schemes.
//
// Dictionary<string, (Color Foreground, Color Background)> Roles
//
// Static factory presets:
//   static AnimationTheme Dark { get; }   // sensible dark-terminal defaults
//   static AnimationTheme Light { get; }  // light-terminal defaults
//   static AnimationTheme Ansi4Bit { get; } // 4-bit ANSI only (maximum compatibility)
//
// Method:
//   (Color fg, Color bg) GetColors(string role)
//   (Color fg, Color bg) GetColorsOrDefault(string role, Color defaultFg, Color defaultBg)
```

### Test Criteria

```csharp
// Test 1: AsciiFrame.FromString creates correct CellBuffer dimensions
// Test 2: AsciiFrame.FromString with colorizer applies per-cell colors
// Test 3: AsciiAnimation.Advance returns correct frame at each time point
// Test 4: AsciiAnimation loops correctly (frame N+1 after last frame = frame 0)
// Test 5: AsciiAnimation with IsLooping=false stops at last frame
// Test 6: AsciiAnimation.Advance returns same frame reference if no advance needed
// Test 7: All frames must have same dimensions (constructor throws otherwise)
// Test 8: SeekTo(frameIndex) and SeekTo(time) position correctly
// Test 9: FromSpriteSheet extracts correct subregions
// Test 10: AnimationTheme.GetColorsOrDefault returns defaults for unknown roles
```

### Acceptance Criteria
- [ ] Can represent the Copilot CLI banner as an `AsciiAnimation` (20 frames, 11×78, themed colors)
- [ ] `Advance()` is allocation-free
- [ ] Frame data is immutable after construction (thread-safe reads)
- [ ] `FromString` factory handles multi-line strings with mixed line endings
- [ ] Sprite sheet extraction works for irregularly-sized last rows

---

## Task 5: End-to-End Integration — Wire It All Together

**Model:** Opus
**Effort:** 2–3 hours
**Dependencies:** Tasks 1, 2, 3, 4
**Priority:** CRITICAL

### Objective
Wire `TuiTicker` → `AsciiCanvasView` → `AsciiAnimation` → XenoAtom rendering into a working end-to-end pipeline. Build the sample app that proves it works.

### Files to Create

**`samples/AnimationDemo/App.cs`**

```csharp
// A minimal MAUI app that displays an animated ASCII banner
//
// Structure:
//   - MauiTuiApplication subclass as entry point
//   - Single page with:
//     - A Label at the top: "MAUI.Tui Animation Demo"
//     - An AsciiCanvasView in the center showing a looping animation
//     - A Label at the bottom showing current FPS
//
// The animation should be something simple but visually verifiable:
//   Option A: A bouncing ASCII art ball (easiest to implement)
//   Option B: A cycling color gradient across the canvas
//   Option C: A scrolling text marquee
//   Option D: A Copilot-CLI-style frame sequence
//
// The FPS label should update every second showing actual measured frame rate.
//
// Keyboard controls:
//   Space = pause/resume
//   +/- = increase/decrease TargetFps
//   Q = quit
```

**`samples/AnimationDemo/Animations/BouncingBall.cs`**

```csharp
// A procedural animation (not pre-computed frames)
// Demonstrates using DrawFrame event with real-time computation
//
// Implements the DrawFrame handler:
//   void OnDrawFrame(object sender, AsciiCanvasDrawEventArgs e)
//   {
//       // Calculate ball position based on e.ElapsedTime
//       // Use sin/cos for smooth motion (you know calc — make it nice)
//       // Draw ball as a small ASCII art circle: ( o )
//       // Draw trail with diminishing brightness
//       // Draw border around the canvas
//   }
```

**`samples/AnimationDemo/Animations/CopilotBanner.cs`**

```csharp
// A pre-computed frame animation mimicking the Copilot CLI style
// Use AsciiAnimation.FromFrameStrings() with a few hand-crafted frames
// Apply an AnimationTheme for coloring
//
// This proves the full pipeline: pre-computed frames → AsciiAnimation →
// AsciiCanvasView → XenoAtom → terminal
```

### Files to Modify

**`src/Maui.TUI/Platform/MauiTuiApplication.cs`**
- Ensure the TuiTicker.Tick() call in onUpdate works correctly with multiple AsciiCanvasViews
- The ticker should fire BEFORE XenoAtom processes its own rendering pass

### Integration Test Criteria

```csharp
// Integration Test 1: Full pipeline smoke test
//   Create MauiTuiApplication with one AsciiCanvasView
//   Run for 100 frames
//   Verify DrawFrame was called ~100 times (±5)
//   Verify no exceptions

// Integration Test 2: Multiple AsciiCanvasViews on same page
//   Create two canvases with different TargetFps (15 and 30)
//   Run for 2 seconds
//   Verify each received approximately the right number of DrawFrame calls

// Integration Test 3: Animation with MAUI controls
//   Create page with Grid containing: Label + AsciiCanvasView + Button
//   Verify Label and Button still render correctly while canvas animates
//   Verify Label text changes do NOT cause canvas to re-layout

// Integration Test 4: Memory pressure test
//   Run animation for 10 seconds at 60 fps
//   Measure total allocations via GC.GetAllocatedBytesForCurrentThread()
//   Assert < 1KB total (excluding startup)
```

### Acceptance Criteria
- [ ] Sample app runs and displays visible animation in the terminal
- [ ] F12 (XenoAtom debug overlay) shows FPS ≥ 25 for a 40×12 animation region
- [ ] Static MAUI controls (Label, Button) render correctly alongside animation
- [ ] Changing Label.Text during animation does not cause animation stutter
- [ ] Pause/resume works via keyboard
- [ ] No visible flicker on Windows Terminal, kitty, or Alacritty

---

## Task 6: Direct Property Bypass for Simple Animations

**Model:** Sonnet
**Effort:** 1–2 hours
**Dependencies:** Task 1 (TuiTicker)
**Priority:** MEDIUM — enhances existing controls, not new functionality

### Objective
For simple animations on standard controls (color transitions on Labels, progress bar animations), provide an optimized path that updates XenoAtom visual properties directly WITHOUT triggering MAUI's `InvalidateMeasure()` cascade.

### Background
When you animate a Label's TextColor from red to blue, the full MAUI path is:
`TextColor changed → InvalidateMeasure → parent measure → parent arrange → handler.MapTextColor → XenoAtom updates`

The optimized path should be:
`TextColor changed → handler detects "frozen layout" mode → directly updates XenoAtom TextBlock.Foreground → XenoAtom marks dirty`

This is safe when the property change doesn't affect the control's size — color changes, opacity changes, and attribute changes never change size in a terminal context (unlike GUI where font color could theoretically affect text shaping).

### Files to Create

**`src/Maui.TUI/Handlers/AnimationBypassExtensions.cs`**

```csharp
// Extension methods on ViewHandler that enable frozen-layout property updates
//
// void UpdatePropertyDirect<THandler, TVirtualView>(
//     this THandler handler,
//     string propertyName,
//     Action<THandler, TVirtualView> updateAction)
//   where THandler : ViewHandler
//   where TVirtualView : IView
//
// This method:
// 1. Calls the updateAction (which updates the XenoAtom visual)
// 2. Marks the XenoAtom visual as dirty
// 3. Does NOT call InvalidateMeasure or InvalidateArrange on the MAUI view
//
// Safe properties for bypass (create a HashSet):
//   - TextColor, BackgroundColor, any color property
//   - Opacity
//   - FontAttributes (bold/italic — doesn't change cell size in terminal)
//   - IsVisible (in terminal, visibility is just clearing cells)
```

### Files to Modify

**`src/Maui.TUI/Handlers/LabelHandler.cs`** (example — apply pattern to other handlers)
- In `MapTextColor`, check if the property change is animation-driven
- If so, use the bypass path instead of normal property mapping
- Detection: check if MAUI's AnimationManager has active animations targeting this element

### Acceptance Criteria
- [ ] Animating Label.TextColor at 30fps does NOT trigger measure/arrange
- [ ] Visual result is identical to non-bypass path
- [ ] Bypass only activates for "safe" properties (size-invariant)
- [ ] Size-affecting properties (Text, FontSize) still trigger full layout

---

## Task 7: Frame Scheduler Service

**Model:** Sonnet
**Effort:** 1 hour
**Dependencies:** Task 1 (TuiTicker)
**Priority:** MEDIUM — quality of life improvement

### Objective
Expose a `RequestAnimationFrame`-style API (like the browser's `requestAnimationFrame`) that gives users a clean way to schedule one-shot or continuous frame callbacks without directly managing `IDispatcherTimer` or `IsAnimating` state.

### Files to Create

**`src/Maui.TUI/Animation/IFrameScheduler.cs`**

```csharp
public interface IFrameScheduler
{
    // One-shot: callback fires on the next frame, then is automatically removed
    IDisposable RequestFrame(Action<FrameInfo> callback);

    // Continuous: callback fires every frame until disposed
    IDisposable RequestAnimationLoop(Action<FrameInfo> callback);

    // Current frame info (valid only during a frame callback)
    FrameInfo CurrentFrame { get; }
}

public readonly record struct FrameInfo(
    TimeSpan ElapsedTime,    // since scheduler started
    TimeSpan DeltaTime,      // since last frame
    int FrameNumber,         // monotonically increasing
    double Fps               // measured FPS (rolling average)
);
```

**`src/Maui.TUI/Animation/TuiFrameScheduler.cs`**

```csharp
// Implements IFrameScheduler
// Hooks into TuiTicker
//
// Uses a pre-allocated list of callbacks (List<FrameCallback>)
// where FrameCallback is a struct containing:
//   - Action<FrameInfo> callback
//   - bool isOneShot
//   - bool isDisposed
//
// On each tick:
//   1. Build FrameInfo (allocation-free — it's a readonly record struct)
//   2. Iterate callbacks, invoke non-disposed ones
//   3. Remove disposed and completed one-shots
//
// The IDisposable returned by Request* methods sets isDisposed = true
// Use a pooled disposable wrapper to avoid allocating IDisposable per request
```

### Files to Modify

**`src/Maui.TUI/Hosting/AppHostBuilderExtensions.cs`**
- Register `IFrameScheduler` → `TuiFrameScheduler` as singleton

### Test Criteria

```csharp
// Test 1: RequestFrame fires exactly once then stops
// Test 2: RequestAnimationLoop fires continuously until disposed
// Test 3: Disposing the returned IDisposable stops callbacks
// Test 4: FrameInfo.DeltaTime is approximately correct
// Test 5: FrameInfo.Fps stabilizes to approximately TuiTicker.MaxFps
// Test 6: Multiple simultaneous RequestAnimationLoop callbacks all fire each frame
// Test 7: Zero allocations per frame in steady state
```

### Acceptance Criteria
- [ ] `IFrameScheduler` available via DI
- [ ] One-shot and continuous modes work correctly
- [ ] Disposing stops callbacks without exceptions
- [ ] Zero per-frame allocations in the scheduler
- [ ] FPS measurement is accurate (rolling average over ~60 frames)

---

## Task Dependency Graph

```
Task 0: Project Setup
   │
   ├──→ Task 1: TuiTicker
   │       │
   │       ├──→ Task 6: Property Bypass (extends existing handlers)
   │       │
   │       └──→ Task 7: Frame Scheduler
   │
   └──→ Task 2: CellBuffer
           │
           ├──→ Task 4: AsciiAnimation (uses CellBuffer)
           │       │
           └───────┤
                   │
                   └──→ Task 3: AsciiCanvasView + Handler (uses CellBuffer, TuiTicker)
                           │
                           └──→ Task 5: End-to-End Integration (uses everything)
```

**Parallelization:** Tasks 1 and 2 can be done simultaneously. Tasks 6 and 7 can be done after Task 1, independently of Tasks 2–5. Task 4 can be done after Task 2, in parallel with Task 3 (as long as Task 1 is also done).

---

## Model Selection Quick Reference

| Phase | Model | Why |
|---|---|---|
| Reading MAUI.Tui source to understand handler patterns | **Haiku** | Fast codebase exploration, symbol lookup |
| Designing CellBuffer struct layout and memory strategy | **Opus** | Alignment, cache lines, allocation patterns |
| Implementing CellBuffer, AsciiFrame, AnimationTheme | **Sonnet** | Straightforward data structures |
| Designing AsciiCanvasViewHandler ↔ XenoAtom integration | **Opus** | Cross-library API bridge, multiple concerns |
| Implementing handler Map* methods | **Sonnet** | Pattern-following from existing handlers |
| Writing tests | **Sonnet** | Good at generating comprehensive test cases |
| End-to-end debugging when things don't render | **Opus** | Needs full pipeline reasoning |
| Performance profiling / allocation hunting | **Opus** | Deep analysis of hot paths |
| Writing XML doc comments | **Haiku** | Fast, low-complexity text generation |

**General rule:** Use Opus when reasoning across more than 2 files simultaneously or when the task requires understanding the interaction between MAUI's pipeline and XenoAtom's pipeline. Use Sonnet for single-file implementation work. Use Haiku for exploration and documentation.

---

## Non-Goals (Explicitly Out of Scope)

These are things we are NOT building in this phase:

1. **MAUI GraphicsView/ICanvas support** — Full `Microsoft.Maui.Graphics` canvas-to-terminal translation is a separate, much larger project
2. **MAUI's built-in animation system working on arbitrary properties** — We support it for color/opacity but not for TranslationX/Y/Scale/Rotation (these don't have terminal equivalents)
3. **Hot-reload support for animation frames** — Nice to have, but not in scope
4. **Sixel / Kitty image protocol support** — ASCII art only for now
5. **Sound/audio synchronization** — Terminal animations are silent
6. **Network-streamed animation data** — All frames are local/embedded
7. **Animation editor/designer tool** — Users create frames in code or external tools

---

## Success Criteria (The Whole Project)

When all tasks are complete, the following must be true:

1. A MAUI page containing an `AsciiCanvasView` renders a smooth 30fps animation in Windows Terminal, kitty, and Alacritty
2. Static MAUI controls (Labels, Buttons, Progress Bars) on the same page render correctly and respond to input during animation
3. XenoAtom's F12 debug overlay shows ≥ 25 measured FPS for a 40×12 animated canvas
4. Total heap allocations during 10 seconds of animation at 30fps are < 1KB (excluding startup)
5. The Copilot CLI banner can be replicated using `AsciiAnimation` + `AsciiCanvasView` with zero flicker
6. The sample app demonstrates both procedural (bouncing ball) and pre-computed (frame sequence) animation
7. All new code has XML doc comments and passes `dotnet format`
8. Test coverage exists for all public APIs
