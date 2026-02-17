# ModAPI Transpiler and Debugging Guide (v1.2)

This guide focuses on the current transpiler stack under `ModAPI.Harmony.Transpilers`.

Canonical signatures for the APIs referenced here: `documentation/API_Signatures_Reference.md`.

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

### Exact Signatures (Most Used)

```csharp
public static FluentTranspiler For(IEnumerable<CodeInstruction> instructions, MethodBase originalMethod = null, ILGenerator generator = null);
public FluentTranspiler ReplaceAllPatterns(Func<CodeInstruction, bool>[] patternPredicates, CodeInstruction[] replaceWith, bool preserveInstructionCount = false);
public FluentTranspiler ReplaceAll(IEnumerable<CodeInstruction> newInstructions);
public FluentTranspiler WithTransaction(Action<FluentTranspiler> action);
public IEnumerable<CodeInstruction> Build(bool strict = true, bool validateStack = true);
```

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
- `StackSentinel` currently does **not** support full exception-handler analysis (`try/catch/finally/filter`) and fails validation for those methods.
- If your target method uses exception handlers, avoid complex transpilers or isolate patch points to safer Prefix/Postfix paths.
- Stack analysis now merges type state across control-flow joins and reports unresolved values as `unknown` instead of pretending they are `object`.
- `ReplaceSequence`/`ReplaceAllPatterns` now preserve Harmony exception block markers (`CodeInstruction.blocks`) and require exact index-aligned replacement on EH methods.
- `ReplaceAll` is blocked on EH methods to prevent exception-clause boundary corruption.
- `ReplaceAll` now applies transactional rollback on failures (including internal list mismatches), uses matcher-first replacement, and emits critical diagnostics with method name, old/new counts, and opcode previews.
- `FluentTranspiler` instances are not thread-safe; do not share a single instance across threads.

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
- Preserve mode now rejects unsafe NOP-padding cases when removed tail instructions are not stack-neutral.
- Critical warnings (for example stack or branch-risk warnings) can hard-fail the patch.
- Catastrophic lint findings are emitted as `[CRITICAL LINT]` (invalid branch operands, unresolved labels, bad local/arg indices, invalid castclass operands).
- Cooperative pipelines can quarantine a failing owner to prevent repeated unsafe mutations.
- Transpiler debugger history snapshots are now stored with locking and exposed as copy-on-read to avoid race conditions in debug UI.

Detailed reference: `documentation/Transpiler_Safety_Settings.md`

## 9. Debug and Inspection Tooling

In-game tools are available to help debug transpilers and inspect the runtime state.

**Key Bindings:**
- `F9`: **Runtime Inspector** (Hierarchy & Properties) - *Always Available*
- `F10`: **Runtime IL Inspector** (View live IL) - *Dev Only*
- `F11`: **UI Debugger** (UI Raycast & Structure) - *Always Available*
- `F12`: **Runtime Debugger** (Harmony/Transpiler Snapshots) - *Dev Only*

### Production vs. Development
The advanced debugging tools (**F10** and **F12**) depend on the **Decompiler** component to function. 
- **Development Builds:** Include the `bin/decompiler/` directory. All tools function normally.
- **Production/Release Builds:** Do not include the decompiler. **F10 and F12 are automatically disabled**.

### Decompiler Requirements
For F10 and F12 to work, the following must be present in your ModAPI installation:
- Directory: `Sheltered_Data/Managed/bin/decompiler/`
- Files: `Decompiler.exe` (plus dependencies)
- **Privacy Check:** The decompiler performs a local privacy check on valid game ownership before running. If this check fails, the tools will remain disabled.

### Dump Files
- `TranspilerDebugger.DumpWithDiff(...)` writes before/after/diff output under:
  - `Mods/<ModName>/Logs/TranspilerDumps/<Label>/`

### Snapshot History
- `TranspilerDebugger.RecordSnapshot(...)` stores patch history used by the runtime debug UI (F12).

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
