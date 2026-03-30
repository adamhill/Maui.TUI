---
applyTo: "src/Maui.TUI/Handlers/**"
---

# Handler Authoring Guide

All handlers map a MAUI virtual view interface to an XenoAtom.Terminal.UI platform visual.
Read the [XenoAtom.Terminal.UI docs](https://xenoatom.github.io/terminal/) before picking a platform view type — every `TPlatformView` must be a `Visual` subclass from that library.

## Anatomy of a Handler

### 1. File location and naming

```
src/Maui.TUI/Handlers/{ControlName}Handler.cs
```

One file per handler. No partial splits unless auto-generated code is involved.

### 2. Class declaration

```csharp
#nullable enable
// Import only the XenoAtom namespace(s) you need
using XenoAtom.Terminal.UI.Controls;

namespace Maui.TUI.Handlers;

public partial class {ControlName}Handler : TuiViewHandler<I{ControlName}, {TuiVisualType}>
```

- Use a type alias (`using Tui{Name} = ...`) when the XenoAtom type name clashes with a MAUI type (see `ButtonHandler` — `TuiButton` / `TuiTextBlock`).
- `partial` is required; MAUI tooling may generate companion files.

### 3. Mappers

```csharp
public static IPropertyMapper<I{ControlName}, {ControlName}Handler> Mapper =
    new PropertyMapper<I{ControlName}, {ControlName}Handler>(ViewMapper)
    {
        [nameof(I{ControlName}.SomeProperty)] = MapSomeProperty,
    };

public static CommandMapper<I{ControlName}, {ControlName}Handler> CommandMapper =
    new(ViewCommandMapper);
```

- Always chain from `ViewMapper` / `ViewCommandMapper` (inherited from `TuiViewHandler`) so base mappings (size, visibility, etc.) are preserved.
- Key is `nameof(Interface.Property)`, never a raw string.
- Map methods are `static void Map{Property}({ControlName}Handler handler, I{ControlName} view)`.

### 4. Constructors (standard boilerplate)

```csharp
public {ControlName}Handler() : base(Mapper, CommandMapper) { }

public {ControlName}Handler(IPropertyMapper? mapper, CommandMapper? commandMapper = null)
    : base(mapper ?? Mapper, commandMapper ?? CommandMapper) { }
```

Provide both overloads so subclasses and tests can inject alternative mappers.

### 5. CreatePlatformView

```csharp
protected override {TuiVisualType} CreatePlatformView() => new {TuiVisualType}();
```

Return a freshly constructed XenoAtom control. Do not configure state here — that is the mappers' job.

### 6. ConnectHandler / DisconnectHandler (only when needed)

Override these **only** when subscribing to platform events:

```csharp
protected override void ConnectHandler({TuiVisualType} platformView)
{
    base.ConnectHandler(platformView);
    platformView.SomeEvent += OnSomeEvent;
}

protected override void DisconnectHandler({TuiVisualType} platformView)
{
    platformView.SomeEvent -= OnSomeEvent;
    base.DisconnectHandler(platformView);
}
```

Always call `base` first in `ConnectHandler` and last in `DisconnectHandler`. Unsubscribe every event you subscribe to — XenoAtom visuals are long-lived and leaks accumulate.

### 7. Map methods

```csharp
public static void Map{Property}({ControlName}Handler handler, I{ControlName} view)
{
    handler.PlatformView.TuiProperty = view.MauiProperty ?? defaultValue;
}
```

- `static` — MAUI's mapper infrastructure requires it.
- Null-coalesce when the MAUI interface allows null but the TUI control does not.
- Keep each method focused on a single property.

## Cell-based layout

`TuiViewHandler` already overrides `PlatformArrange` and `GetDesiredSize` to convert floating-point MAUI coordinates to integer terminal cells. **Do not re-implement these in individual handlers.**

## Registration

After creating the handler, add one line in `AppHostBuilderExtensions.AddMauiControlsHandlers()`:

```csharp
handlersCollection.AddHandler<Microsoft.Maui.Controls.{ControlName}, {ControlName}Handler>();
```

Use the fully-qualified MAUI type to avoid ambiguity with handler class names.

## Checklist

- [ ] File at `src/Maui.TUI/Handlers/{ControlName}Handler.cs`
- [ ] Inherits `TuiViewHandler<I{Control}, {TuiVisualType}>`
- [ ] `Mapper` chains from `ViewMapper`; `CommandMapper` chains from `ViewCommandMapper`
- [ ] Both constructor overloads present
- [ ] `CreatePlatformView()` returns a fresh XenoAtom `Visual`
- [ ] Event subscriptions balanced in `Connect`/`DisconnectHandler`
- [ ] Registered in `AppHostBuilderExtensions.AddMauiControlsHandlers()`
- [ ] `dotnet build Maui.TUI.slnx` passes
