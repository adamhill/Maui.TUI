# CLAUDE.md - MAUI.Tui Animation Layer

## Overview
Animation subsystem for MAUI.Tui, rendering .NET MAUI animations
in terminal via XenoAtom.Terminal.UI's cell-buffer renderer.

## Tech Stack
- .NET 10, C# 14 (extension members, primary constructors, NativeAOT-oriented)
- XenoAtom.Terminal.UI 1.13.1+ (retained-mode reactive TUI framework)
- XenoAtom.Ansi (allocation-friendly VT100 escape sequence generation)
- XenoAtom.Terminal (cross-platform terminal I/O with atomic writes)
- MAUI handler pipeline: ViewHandler<TVirtualView, TPlatformView>

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
- Animation frame timing must use XenoAtom's State<T> invalidation
- All new public APIs must have XML doc comments
- Follow existing handler patterns in src/Maui.TUI/Handlers/ for consistency - Handler pattern: TUI platform handlers map MAUI animation properties

## Key Patterns
- Handlers: static Map* methods in PropertyMapper, e.g. `static void MapText(LabelHandler h, ILabel v)`
- Platform views: XenoAtom.Terminal.UI control types (TextBlock, Button, Canvas, etc.)
- Layout: MAUI's CrossPlatformMeasure/CrossPlatformArrange translate double → integer cells
- Invalidation: property change → PropertyMapper → handler updates XenoAtom visual → reactive system marks dirty

## Reference Docs
- Implementation spec: docs/tasks/animation-pipeline-spec.md
- XenoAtom research: docs/research/xenoatom-animation-analysis.md
- MAUI.Tui analysis: docs/research/maui-tui-animation-plan.md

@.claude/rules/xenoatom-patterns.md
@.claude/rules/maui-handler-conventions.md

## Current Task
See docs/research/maui-tui-animation-plan.md for the full architecture.
Implementation spec: docs/tasks/maui-tui-animation-spec.md

## Querying Microsoft Documentation
Always use `microsoft_docs_search` and `microsoft_code_sample_search` tools
when working with .NET MAUI, XenoAtom, or Microsoft APIs. These tools provide
more current information than training data.