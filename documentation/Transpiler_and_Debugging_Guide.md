# ModAPI Transpiler and Debugging Guide (Current v1.3 Line)

This guide focuses on the current transpiler stack under `ModAPI.Harmony.Transpilers`.

Canonical signatures: `documentation/API_Signatures_Reference.md`.

## Compatibility Matrix

| Scope | Applies To | Status |
|-------|------------|--------|
| Transpiler workflow and diagnostics | Current `ModAPI.dll` | Supported |
| Intent API helpers | Current `ModAPI.dll` | Supported |
| Cooperative transpiler pipeline | Current `ModAPI.dll` | Supported |

## 1. Recommended Workflow

1. Start with `FluentTranspiler.For(...)`.
2. Match using explicit anchors.
3. Apply the smallest safe edit.
4. Build with validation.
5. Inspect runtime/diff diagnostics if behavior changes unexpectedly.

Minimal template:

```csharp
[HarmonyPatch(typeof(SomeType), "TargetMethod")]
public static class TargetMethod_Patch
{
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> Transpiler(
        IEnumerable<CodeInstruction> instructions,
        MethodBase original,
        ILGenerator il)
    {
        return FluentTranspiler.For(instructions, original, il)
            .FindCall(typeof(SomeType), "OldCall", SearchMode.Start)
            .ReplaceWithCall(typeof(MyHooks), "NewCall")
            .Build();
    }
}
```

## 2. Core Types

- `FluentTranspiler`: primary match/edit API
- `IntentAPI`: high-level helpers such as `RedirectCall` and `ChangeConstant`
- `StackSentinel`: stack validation during build
- `CooperativePatcher`: ordered multi-mod transpiler composition
- `TranspilerDebugger`: before/after dumps and diagnostics
- `TranspilerTestHarness`: isolated testing without launching the game

## 3. Matching Correctly

Use the strongest anchor available:
- method calls
- field loads/stores
- distinctive constant patterns
- short instruction sequences

`SearchMode` matters:
- `Start`: reset matcher to index 0 and search forward
- `Current`: search from the current index
- `Next`: advance one instruction and search forward

Typical pattern:

```csharp
var t = FluentTranspiler.For(instructions, original)
    .FindCall(typeof(A), "First", SearchMode.Start)
    .FindCall(typeof(A), "Second", SearchMode.Next);
```

## 4. Edit APIs

Common calls:
- `ReplaceWith(...)`
- `ReplaceWithCall(...)`
- `ReplaceSequence(...)`
- `ReplaceAll(...)`
- `ReplaceAllCalls(...)`
- `ReplaceAllPatterns(...)`
- `WithTransaction(...)`

`ReplaceAllPatterns(...)` is especially useful when you need to replace every known IL shape for the same behavior.

## 5. Intent API

Intent helpers reduce opcode choreography:

```csharp
t.RedirectCall(typeof(GameModeManager), "OnDayPassed", typeof(Hooks), "OnDayPassedHook");
t.RedirectCallAll(typeof(GameModeManager), "OnDayPassed", typeof(Hooks), "OnDayPassedHook");
t.ChangeConstant(1.0f, 1.5f);
t.ChangeConstantAll(5, 10);
t.RemoveCall(typeof(Analytics), "TrackEvent");
```

Use them whenever an intent helper expresses the change clearly.

## 6. Validation

Default guidance:
- development: `Build(strict: true, validateStack: true)`
- compatibility-sensitive patches: keep stack validation on
- only relax strictness when you have a concrete reason and have inspected the result

## 7. Cooperative Patching

When multiple mods need to transpile the same method, prefer `CooperativePatcher`.

Benefits:
- ordered registration
- dependency constraints
- conflict declarations
- easier diagnostics when one participant fails

## 8. Debugging

Useful tools:
- `RuntimeILInspector` (`F10`)
- `TranspilerDebugger`
- `TranspilerTestHarness`
- `UIDebugInspector` (`F11`) when patch results surface through UI

When a transpiler misbehaves:
1. dump original IL
2. confirm the anchor still exists
3. reduce the edit to the smallest reproducer
4. re-enable validation if it was disabled

## 9. Common Failure Modes

- no match found: target method changed or overload mismatch
- invalid stack: replacement left pushes/pops unbalanced
- wrong branch behavior: replaced region contained branch destinations
- silent no-op: wrong target method or patch assembly never loaded

In practice, the safest fixes are usually:
- use stronger anchors
- edit a smaller region
- replace a call instead of rewriting a broad block
