---
applyTo: "tests/**"
---

# Test Authoring Guide

## Framework

Tests use xUnit 2.9.3 with the `Microsoft.NET.Test.Sdk` runner.

## MultiAssert Pattern

When a test checks multiple independent conditions, use `Assert.Multiple()` so all failures are reported at once instead of stopping at the first:

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

### When to use

- Multiple asserts verifying independent properties/state of the same object at a single point in time.
- Checking several outputs of a single operation.

### When NOT to use

- Sequential asserts that depend on actions between them (e.g. assert state before an action, perform the action, assert state after). These are separate logical steps and must remain sequential.
- A test with only a single assert.

## Conventions

- One test class per production class, named `{ClassName}Tests`.
- Test methods follow `{Unit}__{Scenario}` or `{Unit}_{Behavior}` naming.
- Use `[Fact]` for single cases, `[Theory]` with `[InlineData]` for parameterized cases.
- Keep tests focused — prefer many small tests over few large ones.
- Add `/// <summary>` XML doc comments describing what each test verifies.

## Zero-Allocation Testing

For hot-path code, measure allocations with `GC.GetAllocatedBytesForCurrentThread()`:

```csharp
// Warmup (JIT)
for (int i = 0; i < 100; i++) { /* exercise code path */ }

long before = GC.GetAllocatedBytesForCurrentThread();
for (int i = 0; i < 1000; i++) { /* exercise code path */ }
long after = GC.GetAllocatedBytesForCurrentThread();

Assert.True(after - before <= 256, $"Allocated {after - before} bytes");
```
