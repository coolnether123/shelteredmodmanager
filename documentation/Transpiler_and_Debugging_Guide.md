# ModAPI Transpiler and Debugging Guide (v1.2)

This guide focuses on the current transpiler stack under `ModAPI.Harmony.Transpilers`.

## 1. Recommended Workflow

1. Start with `FluentTranspiler.For(...)`.
2. Match with explicit `SearchMode`.
3. Apply the smallest safe edit.
4. Build with validation (`Build(...)`).
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

## 2. Core Types and When to Use Them

- `FluentTranspiler`: primary match/edit API.
- `IntentAPI`: high-level helpers (`RedirectCall`, `ChangeConstant`, etc.).
- `StackSentinel`: stack analysis used during build validation.
- `CooperativePatcher`: multi-mod transpiler pipeline sequencing.
- `TranspilerDebugger`: dump + diff files and runtime snapshots.
- `TranspilerTestHarness`: unit-style testing without launching game.

## 3. Matching Correctly

`SearchMode` matters:
- `Start`: reset matcher to index 0, then search forward.
- `Current`: search from current index.
- `Next`: advance one instruction, then search forward.

Use `Start` for first anchor and `Next` for follow-up anchors in sequence.

Example:

```csharp
var t = FluentTranspiler.For(instructions, original)
    .FindCall(typeof(A), "First", SearchMode.Start)
    .FindCall(typeof(A), "Second", SearchMode.Next);
```

## 4. Edit APIs and Safety Characteristics

Common edit calls:
- `ReplaceWith(...)`
- `ReplaceWithCall(...)`
- `ReplaceSequence(...)`
- `ReplaceAll(...)`
- `ReplaceAllCalls(...)`
- `ReplaceAllPatterns(...)`

`ReplaceAllPatterns(...)` is best for "replace every known shape" scenarios.

```csharp
t.ReplaceAllPatterns(
    new Func<CodeInstruction, bool>[] {
        i => i.opcode == OpCodes.Ldc_R4 && (float)i.operand == 0f,
        i => i.opcode == OpCodes.Ldc_R4 && (float)i.operand == 0f,
        i => i.IsNewobjVector2()
    },
    new[] { new CodeInstruction(OpCodes.Call, helperMethod) },
    preserveInstructionCount: true);
```

Use `preserveInstructionCount: true` when branch targets may land inside the replaced region.

## 5. IntentAPI Usage

Intent helpers reduce opcode choreography.

```csharp
// Replace one occurrence
t.RedirectCall(typeof(GameModeManager), "OnDayPassed", typeof(Hooks), "OnDayPassedHook");

// Replace all occurrences
t.RedirectCallAll(typeof(GameModeManager), "OnDayPassed", typeof(Hooks), "OnDayPassedHook");

// Constants
t.ChangeConstant(1.0f, 1.5f);
t.ChangeConstantAll(5, 10);

// Remove a call
// (pops arguments and provides default return where needed)
t.RemoveCall(typeof(Analytics), "TrackEvent");

// Insert a hook before call site
t.InjectBeforeCall(typeof(Bunker), "Open", typeof(Hooks), "BeforeOpen");
```

## 6. Build-Time Validation

`Build(strict: true, validateStack: true)` is default.

- `strict: true`: warnings become exceptions.
- `strict: false`: warnings are logged; patch continues.
- `validateStack: true`: runs `StackSentinel` + lint pass.

Important current behavior:
- `StackSentinel` skips validation for methods with exception handling clauses (`try/catch/finally`) and treats that as non-fatal.

If you are iterating quickly, use `strict: false` temporarily, then restore strict mode before shipping.

## 7. Cooperative Multi-Mod Patching

`CooperativePatcher` lets multiple mods patch one target in a managed order.

```csharp
CooperativePatcher.RegisterTranspiler(
    target: typeof(GameManager).GetMethod("Update"),
    anchorId: "MyMod.UpdateFix",
    priority: PatchPriority.High,
    patchLogic: t => t.FindCall(typeof(GameManager), "OldLogic").ReplaceWithCall(typeof(Hooks), "NewLogic"),
    dependsOn: new[] { "OtherMod.BaseFix" },
    conflictsWith: new[] { "IncompatibleMod.Override" });
```

Notes:
- Pipeline runs by `PatchPriority` order.
- Missing `dependsOn` anchors skip that patch.
- Matched `conflictsWith` anchors skip that patch.
- Failed patch step is skipped; prior valid instruction stream is kept.
- In current safe-mode defaults, cooperative builds use strict validation and can quarantine a patch owner after critical failures.

## 8. Safety Policy Flags (ModPrefs)

ModAPI now exposes global transpiler safety controls through `ModPrefs`:
- `TranspilerSafeMode` (default `true`)
- `TranspilerForcePreserveInstructionCount` (default `true`)
- `TranspilerFailFastCritical` (default `true`)
- `TranspilerCooperativeStrictBuild` (default `true`)
- `TranspilerQuarantineOnFailure` (default `true`)

Practical effect:
- Risky `ReplaceAllPatterns(... preserveInstructionCount: false)` calls are upgraded to preserve mode in safe mode.
- Critical warnings (for example stack or branch-risk warnings) can hard-fail the patch.
- Cooperative pipelines can quarantine a failing owner to prevent repeated unsafe mutations.

Detailed reference: `documentation/Transpiler_Safety_Settings.md`

## 9. Debug and Inspection Tooling

In-game:
- `RuntimeILInspector` toggle: `F10`
- `RuntimeInspector` toggle: `F9`

Dump files:
- `TranspilerDebugger.DumpWithDiff(...)` writes before/after/diff output under:
  - `Mods/<ModName>/Logs/TranspilerDumps/<Label>/`

Snapshot history:
- `TranspilerDebugger.RecordSnapshot(...)` stores patch history used by runtime debug UI.

## 10. Test Harness Pattern

`TranspilerTestHarness` can validate small transforms without launching Unity.

```csharp
[Test]
public void ReplacesCall()
{
    var input = new[]
    {
        new CodeInstruction(OpCodes.Ldstr, "hello"),
        new CodeInstruction(OpCodes.Call, typeof(Console).GetMethod("WriteLine", new[] { typeof(string) }))
    };

    var result = TranspilerTestHarness.FromInstructions(input)
        .FindCall(typeof(Console), "WriteLine")
        .ReplaceWith(OpCodes.Pop)
        .Build(strict: true, validateStack: true)
        .ToList();

    TranspilerTestHarness.AssertInstruction(result, 1, OpCodes.Pop);
}
```

## 11. Failure Triage Checklist

If a patch breaks after a game update:
1. Re-run with `strict: true` and capture warning/exception text.
2. Use `DumpWithDiff(...)` to verify actual instruction change.
3. Replace brittle opcode chains with stronger anchors (`FindCall`, `MapAnchors`, patterns).
4. If replacing blocks, enable `preserveInstructionCount` where branch risk exists.
5. Add or update a `TranspilerTestHarness` regression test.
