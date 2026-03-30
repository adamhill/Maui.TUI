# MAUI.Tui Handler Conventions

> This file is auto-loaded by Claude Code. Follow these patterns when creating
> or modifying handlers in src/Maui.TUI/Handlers/.

## Handler Architecture

MAUI.Tui uses the standard MAUI handler pattern. Each handler bridges a MAUI
virtual view interface to an XenoAtom.Terminal.UI platform view (visual control).

```
MAUI Interface (ILabel, IButton, etc.)
    ↓ PropertyMapper
Handler (ViewHandler<TVirtualView, TPlatformView>)
    ↓ Creates and updates
XenoAtom Visual (TextBlock, Button, etc.)
```

## Creating a New Handler

Follow this template — it matches the pattern used by all existing handlers
in `src/Maui.TUI/Handlers/`:

```csharp
using Microsoft.Maui.Handlers;
using XenoAtom.Terminal.UI;

namespace Maui.TUI.Handlers;

public partial class MyControlHandler : ViewHandler<IMyControl, XenoAtomControl>
{
    // 1. Static PropertyMapper — maps MAUI properties to handler update methods
    public static readonly IPropertyMapper<IMyControl, MyControlHandler> Mapper =
        new PropertyMapper<IMyControl, MyControlHandler>(ViewMapper)
        {
            [nameof(IMyControl.Text)] = MapText,
            [nameof(IMyControl.TextColor)] = MapTextColor,
            // Add all mapped properties here
        };

    // 2. Constructor — pass mapper to base
    public MyControlHandler() : base(Mapper) { }

    // 3. CreatePlatformView — instantiate the XenoAtom visual
    protected override XenoAtomControl CreatePlatformView()
    {
        return new XenoAtomControl();
    }

    // 4. ConnectHandler — subscribe to XenoAtom events if needed
    protected override void ConnectHandler(XenoAtomControl platformView)
    {
        base.ConnectHandler(platformView);
        // Subscribe to platform view events here
    }

    // 5. DisconnectHandler — unsubscribe from events
    protected override void DisconnectHandler(XenoAtomControl platformView)
    {
        // Unsubscribe from platform view events here
        base.DisconnectHandler(platformView);
    }

    // 6. Static Map methods — update platform view from virtual view
    static void MapText(MyControlHandler handler, IMyControl view)
    {
        handler.PlatformView.Text = view.Text;
    }

    static void MapTextColor(MyControlHandler handler, IMyControl view)
    {
        handler.PlatformView.Foreground = view.TextColor.ToXenoAtomColor();
    }
}
```

## Registering Handlers

All handlers must be registered in `src/Maui.TUI/Hosting/AppHostBuilderExtensions.cs`
inside the `UseMauiAppTUI<TApp>()` method:

```csharp
handlers.AddHandler<MyControl, MyControlHandler>();
```

## PropertyMapper Rules

- Map methods are `static void` — they receive the handler and virtual view
- Map methods should be fast — they fire on every property change
- Map methods should NOT call `InvalidateMeasure()` unless the property genuinely
  affects the control's measured size (e.g., Text changes size, TextColor does not)
- Use the existing `ViewMapper` as the base mapper to inherit common properties
  (Background, Opacity, IsEnabled, IsVisible, etc.)

## Color Conversion

MAUI uses `Microsoft.Maui.Graphics.Color` (float RGBA, 0.0–1.0).
XenoAtom uses its own color type (likely byte RGBA, 0–255).

Convert using a helper or extension method — do NOT allocate per conversion:

```csharp
// Create an extension method for reuse across handlers
static class ColorExtensions
{
    public static XenoAtomColor ToXenoAtomColor(this Color color)
    {
        return new XenoAtomColor(
            (byte)(color.Red * 255),
            (byte)(color.Green * 255),
            (byte)(color.Blue * 255),
            (byte)(color.Alpha * 255));
    }
}
```

## Layout Integration

MAUI.Tui delegates layout to MAUI's cross-platform engine. Handlers translate
the `double`-precision layout results to XenoAtom's integer cell coordinates.

- `CrossPlatformMeasure(widthConstraint, heightConstraint)` → desired size in cells
- `CrossPlatformArrange(bounds)` → final position and size in cells

For controls with fixed terminal sizes (like `AsciiCanvasView`), override
`MeasureOverride` in the MAUI View to short-circuit the layout pipeline:

```csharp
// In the MAUI View (NOT the handler):
protected override Size MeasureOverride(double widthConstraint, double heightConstraint)
{
    return new Size(CanvasWidth, CanvasHeight);
    // Do NOT call base — this bypasses MAUI's layout cascade
}
```

## Threading

All handler work runs on the terminal thread via `TuiDispatcherProvider`.
MAUI's `Dispatcher.Dispatch()` and `Dispatcher.DispatchAsync()` marshal
work to this thread. Never update XenoAtom visuals from background threads.

```csharp
// If you need to update from a background thread:
Dispatcher.Dispatch(() =>
{
    PlatformView.Text = newValue;
});
```

## Animation-Aware Handlers

For handlers that support animation (color transitions, opacity changes),
provide a "direct bypass" path that updates the XenoAtom visual without
triggering MAUI's layout invalidation cascade:

```csharp
// Properties that NEVER affect cell size can bypass layout:
// - TextColor, BackgroundColor (any color property)
// - Opacity
// - FontAttributes (bold/italic — same cell width in terminal)
//
// Properties that DO affect cell size must trigger full layout:
// - Text (different string length)
// - FontSize (if supported — generally 1 cell = 1 char in terminal)
// - Padding, Margin
// - WidthRequest, HeightRequest
```

## Existing Handler Reference

Check these existing handlers for patterns to follow:

| Handler | MAUI Interface | XenoAtom Visual | Notes |
|---------|---------------|-----------------|-------|
| `LabelHandler` | `ILabel` | `TextBlock` | Text + color mapping |
| `ButtonHandler` | `IButton` | `Button` | Click events |
| `EntryHandler` | `IEntry` | `TextBox` | Text input |
| `EditorHandler` | `IEditor` | `TextArea` | Multi-line input |
| `ProgressBarHandler` | `IProgressBar` | `ProgressBar` | Progress animation |
| `CheckBoxHandler` | `ICheckBox` | `CheckBox` | Toggle state |

## XML Documentation

All public APIs on new handlers must have XML doc comments:

```csharp
/// <summary>
/// Handles mapping between <see cref="IAsciiCanvasView"/> and
/// the XenoAtom Canvas visual for terminal rendering.
/// </summary>
public partial class AsciiCanvasViewHandler : ViewHandler<IAsciiCanvasView, Canvas>
```
