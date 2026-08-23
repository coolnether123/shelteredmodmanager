# FluentTranspiler guide

A fluent wrapper around Harmony's `CodeMatcher` for rewriting game and mod IL.
It handles labels, branch fixups, and stack validation for common patch operations.

This guide explains when to use a transpiler, how to locate an edit, which recipes to use,
what validation runs, and how to test a patch.

---

## 1. Decide whether you need a transpiler

Use the least intrusive patch type that can make the change:

| Need | Use |
| --- | --- |
| Run code before or after a method, or read or replace its arguments or return value | **Prefix or postfix** (`HarmonyPrefix` or `HarmonyPostfix`) |
| Skip the original entirely | Prefix returning `false` |
| Change a value the method computes internally, or redirect a call **inside** a method | **Transpiler (this framework)** |
| Redirect the same call in many methods | **Transpiler**, with one shared body applied to a list of targets |

Use a transpiler only when a prefix or postfix cannot make the change. A transpiler edits
raw IL, so game updates can invalidate its matches.

---

## 2. Use `Execute` for most patches

`FluentTranspiler.Execute` opens a session, runs the transform, and calls `Build` to
validate the result and collect diagnostics.

```csharp
[HarmonyPatch(typeof(TargetClass), nameof(TargetClass.Method))]
public static class TargetClass_Method_Patch
{
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> Transpiler(
        IEnumerable<CodeInstruction> instructions, MethodBase original, ILGenerator il)
    {
        return FluentTranspiler.Execute(instructions, original, il, t =>
        {
            t.ForCall(typeof(TargetClass), "ComputeValue")
             .ReplaceAllWith(typeof(MyHooks), "ComputeValue");
        });
    }
}
```

- **Always pass `original`.** Stack analysis, the linter, and diagnostics use it.
- **Pass `il`** whenever you may declare a local or define a label; otherwise `null` is fine.
- If you want the original IL returned instead of a throw when a patch throws, wrap with
  `FluentTranspilerExecution.ExecuteOrOriginal(...)` and supply an `onFailure` fallback.

Other entry points and when they are appropriate:

| Entry | Use it when |
| --- | --- |
| `FluentTranspiler.Execute(...)` | Normal Harmony transpiler. |
| `FluentTranspilerExecution.ExecuteOrOriginal(...)` | You want a guaranteed fallback to original IL on failure. |
| `FluentTranspiler.For(...)` and manual `.Build(profile)` | You need multi-stage control or an explicit build profile. |
| `CooperativePatcher.RegisterTranspiler(...)` | Several mods patch the same method and require ordering and conflict checks. |
| `TranspilerTestHarness.FromInstructions(...)` | Unit-testing a transform off a running game. |

---

## 3. Find where to patch

Use either of these workflows:

**Runtime IL Inspector.** Press F10 in the game. `ModAPI.Inspector.RuntimeILInspector`
lets you search for a type and method, then dump its live IL after other mods'
transpilers have run. Use it to read the exact opcodes and operands around the site you
want, and to confirm which mods already patched the method.

**Cartographer.** During development, call the anchor mapper to find stable,
low-frequency anchor points instead of hard-coding indices:

```csharp
FluentTranspiler.Execute(instructions, original, il, t =>
{
    t.ExportAnchors();          // logs a ranked list of stable anchors to MMLog
    // ...author your edit against a high-score anchor...
});
```

`t.MapAnchors()` returns the ranked anchors programmatically; `t.FindNextAnchor()` moves
the cursor to the next high-uniqueness instruction. Prefer anchoring on a unique string
load or a specific method/field reference over anchoring on a bare opcode.

When you are unsure a sequence still exists after a game update, use `MatchIntent`:

```csharp
t.MatchIntent("water-level compare",
    new CodeMatch(OpCodes.Ldfld, waterField),
    new CodeMatch(OpCodes.Ldc_R4));
```

On failure, it records a soft failure and suggests the nearest remaining
anchor, so a broken patch tells you where the code moved.

---

## 4. Use a recipe for common edits

All of the recipe entry points below return a `FluentReplacementResult`
such as `PatternReplaced`, `NoMatch`, `AmbiguousMatch`, `UnsafeMatch`, or `Failed`. Call
`.Succeeded()` on it if you want a bool. They validate the hook signature **before**
mutating IL and refuse unsafe edits.

### 4.1 Redirect a static/instance call to a replacement (everywhere in the method)

```csharp
t.ForCall(typeof(SomeType), "ComputeValue")
 .ReplaceAllWith(typeof(MyHooks), "ComputeValue");
```

The replacement must be static and consume the same stack shape as the original call.
For an instance call, the first hook parameter is the instance type. The replacement must
return a compatible type. Use `.ReplaceWith(...)` when you expect exactly one call site.
It returns `AmbiguousMatch` if it finds more than one.

To disambiguate by surrounding IL:

```csharp
t.ForCall(typeof(SomeType), "ComputeValue")
 .WhenSurroundedBy((instrs, callIndex) => instrs[callIndex - 1].IsLdcI4(4))
 .ReplaceWith(typeof(MyHooks), "ComputeValue");
```

For an exact overload, pass the resolved `MethodInfo`:

```csharp
var src = AccessTools.Method(typeof(UnityEngine.Random), "Range", new[]{ typeof(int), typeof(int) });
t.ReplaceCalls(src).WithCall(bridgeRangeIntInt);
```

**Current and legacy redirects.** Use `t.ForCall(...).ReplaceAllWith(...)` or
`t.ReplaceCalls(mi).WithCall(mi)` for new code. The older `t.RedirectCall(...)`,
`t.RedirectCallAll(...)`, and `t.ReplaceAllCalls(...)` methods remain available but produce
an obsolete warning. They forward to the current implementation.

**Batch redirects that append a tag argument.** When you redirect the same call across many methods
and each redirect must pass an extra constant, such as a domain or stream tag, to the
replacement, use the batch helper instead of hand-walking indices:

```csharp
// Redirects every UnityEngine.Random.Range(int,int) to Bridge.Range(int,int,string), pushing "map"
// as the trailing argument at each site. Also handles generic call sites: when the source is a
// generic method definition, each site's type arguments are applied to the replacement.
t.RedirectCallsAppendingLiteral(RangeII, BridgeRangeWithDomain, "map");
t.RedirectCallsAppendingLiteral(ShuffleDef, BridgeShuffleDef, "map");   // generic ExtensionMethods.Shuffle<T>
```

The recipe validates every matched signature, including the original stack shape and the
literal's type, before it changes IL. A mismatch returns `UnsafeMatch` and leaves the stream
unchanged. Supported literal types are
string, bool, integral types, char, float, double, long.

### 4.2 Wrap the return value of a call

```csharp
t.ForCall(typeof(SomeType), "ComputeValue")
 .WrapReturnValue(typeof(MyHooks), "AdjustValue");   // int AdjustValue(int original)
```

### 4.3 Inject a side-effect hook before/after a call

```csharp
t.ForCall(typeof(TargetUi), "DrawPanel")
 .InjectBefore(typeof(MyHooks), "BeforeDrawPanel");  // static void, no params
```

### 4.4 Guard a call and skip the original when a condition holds

```csharp
t.BeforeCall(AccessTools.Method(typeof(SomeType), "DoThing"))
 .SkipOriginalWhen(g => g.RequireCallTrue(AccessTools.Method(typeof(MyHooks), "IsEnabled")));
```

The guard builder emits branch-safe control flow and defines its own labels; you never
touch `Label`s by hand.

### 4.5 Adjust or replace a method's return value

```csharp
t.Returns<int>().WrapAll(typeof(MyHooks), "AdjustReturn");          // wrap every return
t.Returns<bool>().ReplaceConstant(false, typeof(MyHooks), "ShouldReturnTrue");
t.Returns<int>().InsertGuardBeforeReturn(typeof(MyHooks), "OnExit"); // static void guard
```

### 4.6 Retune a magic number

```csharp
t.ChangeConstant(4f, 8f);        // first occurrence
t.ChangeConstantAll(4, 8);       // every occurrence (NOP-padded, index-stable)
```

### 4.7 Remove a call

```csharp
t.RemoveCall(typeof(Analytics), "Track");   // pops args, pushes a safe default if needed
```

### 4.8 Inject at method entry or before every return

```csharp
t.InsertAtStart(new CodeInstruction(OpCodes.Call, myInitHook));
t.InsertAtExit (new CodeInstruction(OpCodes.Call, myCleanupHook));
```

### 4.9 Replace bounds and range checks

```csharp
t.ForArgument(2).InRangeCheck(0, 4).ReplaceUpperBoundWithCall(getMaxPriority);
```

These understand the several compiler shapes of a range check (`blt/ble`, inverted
`bgt/bge`, `>= upper+1`, NOP gaps) and refuse to patch when the match is ambiguous or a
branch target lands inside the edited span.

---

## 5. Follow the validation rules

Validation is **on by default** (`ModPrefs.TranspilerSafeMode = true`,
`TranspilerFailFastCritical = true`). Every `Build` runs:

- **StackSentinel.** Runs basic-block stack-depth and type analysis. A stack imbalance becomes a
  `Stack Error:` which is *critical* and aborts the build by default.
- **Linter.** Flags null operands on call and field instructions, branch operands that are not
  `Label`s (a classic native-crash cause), out-of-range local/argument indices, and
  `castclass` with a non-`Type` operand. All of these are `[CRITICAL LINT]` and abort.
- **Recipe signature validation.** The call, return, and range-check
  recipes verify the hook is static and stack-compatible before touching IL. A mismatch
  returns `UnsafeMatch` or `Failed` and leaves the original IL untouched.

The framework also protects structural edits: `ReplaceSequence`, `ReplaceAll`,
`ReplaceAllPatterns` capture and re-attach labels/exception blocks, roll back on any
exception, refuse to relocate exception-handler boundaries, and abort if a branch targets
the interior of a removed range.

You are still responsible for these checks:

- **Match the stack shape.** A replacement for a call to an instance method
  `int Foo.Bar(float)` must be `static int Hook(Foo self, float x)`.
- **Keep hooks static.** Instance hooks are rejected.
- **Prefer `original`-aware sessions.** Without `original`, stack validation and the
  linter's local/argument checks are limited.
- **Methods with exception handlers are constrained.** StackSentinel cannot model exception
  regions, so structural edits require exact index-aligned replacement
  (`removeCount == insertCount`); otherwise the edit is refused.

Relevant `ModPrefs` toggles (all default to the safe value):

| Pref | Default | Effect |
| --- | --- | --- |
| `TranspilerSafeMode` | `true` | Forces `preserveInstructionCount` for multi-instruction pattern edits. |
| `TranspilerForcePreserveInstructionCount` | `true` | Backs the above. |
| `TranspilerFailFastCritical` | `true` | Critical warnings throw at `Build`. |
| `TranspilerCooperativeStrictBuild` | `false` | Run cooperative pipeline in Strict profile. |
| `TranspilerQuarantineOnFailure` | `false` | Disable a mod's cooperative patches after a critical failure. |
| `DebugTranspilers` | `false` | Verbose tracing and snapshots. Leave **off** in production. |

---

## 6. Debug a transpiler

1. **Test the transform without the game** by using `TranspilerTestHarness`:

   ```csharp
   var t = TranspilerTestHarness.FromInstructions(instructions, originalMethod);
   t.ForCall(typeof(SomeType), "Foo").ReplaceAllWith(typeof(MyHooks), "Foo");
   var result = TranspilerTestHarness.RunTest(t, strict: true);
   TranspilerTestHarness.AssertInstruction(result, index, OpCodes.Call, expectedHook);
   ```

2. **Run the built-in cases** after changing FluentTranspiler:

   ```csharp
   TranspilerTestHarness.AssertAllHarnessCasesPass();   // throws on any FAIL
   // or inspect: foreach (var line in TranspilerTestHarness.RunAllHarnessCases()) ...
   ```

3. **Turn on tracing** (`ModPrefs.DebugTranspilers = true`) and use the Debug build
   profile to get snapshots via `TranspilerDebugger`. `t.DumpAll()`, `t.Log()`, and
   `t.DumpDiffFrom(original)` print IL state during development.

4. **Read the diagnostics.** `t.Warnings`, `t.SoftFailures`, `t.Notes`, and the structured
   `t.PatchDiagnostics` explain the expected shape, the actual shape, and the action taken.
   Check the `FluentReplacementResult` returned by each recipe.

Successful production patches are silent by design: snapshots and warning logs only fire
on critical warnings or when `DebugTranspilers` is on.

---

## 7. Limit load-time work

Transpilers run once per patched method at patch time. The dominant cost is Harmony's IL
re-emit and JIT, which happens regardless of this framework. Framework overhead per patch
is small and linear:

- Matching (`ForCall`, `Find*`, `MapAnchors`) is O(instructions).
- `Build` runs one StackSentinel pass (basic-block analysis, O(instructions)) plus one
  linter pass. Explicit `ExpectStack`/`EnsureStack` expectations trigger extra analysis
  passes. Use them only where you need them.
- To keep many patches cheap:
  - **Resolve hook `MethodInfo`s once** (static readonly fields) and pass them to the
    `MethodInfo` overloads (`ReplaceCalls(mi).WithCall(mi)`), avoiding per-patch reflection
    and ambiguous-overload lookups.
  - **Stay on the Runtime build profile** (the `Execute` default). Do not ship on the Debug
    profile because it records snapshots.
  - Keep `DebugTranspilers` off in production.

Measure total patch time when applying the same transpiler to many target methods.

---

## 8. Pitfalls

- **Anchoring on bare opcodes.** `Ldc_I4_4` appears everywhere. Anchor on a unique string,
  a specific method/field, or a short distinctive sequence (`MatchIntent`, `MapAnchors`).
- **Assuming one call site.** `ReplaceWith` (singular) returns `AmbiguousMatch` if there
  are several. Use `ReplaceAllWith` or `WhenSurroundedBy` to be explicit.
- **Wrong stack shape on the hook.** The commonest cause of `UnsafeMatch`. Remember the
  implicit `this` for instance calls.
- **Editing methods with exception handlers.** Only index-aligned replacements are allowed;
  structural edits are refused.
- **Branch operands as integers.** Never pass an `int` where a `Label` is expected. The
  linter rejects it because the runtime requires a label operand.
- **Ignoring the result.** Recipes never throw on a clean miss; they return `NoMatch`. If
  your patch silently does nothing, check the returned `FluentReplacementResult` and
  `t.SoftFailures`.

---

## 9. Redirect vanilla RNG to `ModRandom`

This example redirects supported `UnityEngine.Random` and `System.Random` call sites to
`ModAPI.Core.ModRandom`. Outside a custom scenario, the bridge calls the original random
implementation.

**Recipe choice.** Use the call recipe for call-site redirects. `UnityEngine.Random`
members are static, so `Random.Range(int,int)` maps directly to a static
`Range(int,int)` hook. Redirect to a bridge so one method owns the scenario gate:

```csharp
public static class ModRandomBridge
{
    // One resolved MethodInfo per signature, cached once.
    public static int Range(int min, int max)
        => ModRandomGate.Active ? ModRandom.Range(min, max) : UnityEngine.Random.Range(min, max);

    public static float Range(float min, float max)
        => ModRandomGate.Active ? ModRandom.Range(min, max) : UnityEngine.Random.Range(min, max);

    public static float Value()
        => ModRandomGate.Active ? ModRandom.Value() : UnityEngine.Random.value;
}
```

`ModRandom.Range(int,int)` already matches Unity semantics (`[min, max)` for ints,
`[min, max]` for floats) and `Value()` returns `[0,1]`, so the bridge is a thin gate.
`Random.value` decompiles to a call to `get_value`. Redirect it to `Value()`.

**Per-site redirect.** Put these calls in one shared transpiler body:

```csharp
static readonly MethodInfo RangeII = AccessTools.Method(typeof(UnityEngine.Random), "Range", new[]{typeof(int),typeof(int)});
static readonly MethodInfo BridgeII = AccessTools.Method(typeof(ModRandomBridge), "Range", new[]{typeof(int),typeof(int)});
// ...one pair per signature...

t.ReplaceCalls(RangeII).WithCall(BridgeII);
t.ReplaceCalls(RangeFF).WithCall(BridgeFF);
t.ForCall(typeof(UnityEngine.Random), "get_value").ReplaceAllWith(typeof(ModRandomBridge), "Value");
```

`System.Random` methods are instance calls, so a redirect hook must take the
`Random` instance as its first parameter (`static int NextGated(System.Random self, int max)`)
and decide whether to defer to `self` or to `ModRandom`. Prefer replacing the construction
of `System.Random` with a gated factory where feasible; fall back to instance-shaped hooks
only at the specific sites that need it.

**Batching strategy.** Write one shared transpiler method and apply it to the catalogued
target methods:

```csharp
static IEnumerable<CodeInstruction> RngTranspiler(IEnumerable<CodeInstruction> instrs, MethodBase original)
    => FluentTranspiler.Execute(instrs, original, null, t => ApplyRngRedirects(t));

foreach (var target in RngTargetMethods)                 // curated, generated once
    harmony.Patch(target, transpiler: new HarmonyMethod(typeof(RngPatches), nameof(RngTranspiler)));
```

Keep the target list data-driven (generated from a scan of the decompiled call sites) so it
can be audited and regenerated after a game update. Resolve every `RangeII/BridgeII`
`MethodInfo` **once** into static readonly fields (as above) so 76 applications share the
lookups.

**Load-time work.** Each application performs O(method length) matching and one
StackSentinel pass during `Build`. Stay on the Runtime profile and keep `DebugTranspilers`
off in production.

**Verification.** Before shipping, add test cases that feed
representative IL and assert behavior, then gate the build on them:

```csharp
var t = TranspilerTestHarness.FromInstructions(
    new CodeInstruction(OpCodes.Ldc_I4_0),
    new CodeInstruction(OpCodes.Ldc_I4_5),
    new CodeInstruction(OpCodes.Call, RangeII));
var res = t.ReplaceCalls(RangeII).WithCall(BridgeII);           // expects PatternReplaced
Assert(res == FluentReplacementResult.PatternReplaced);
Assert(t.Instructions().Any(i => i.Calls(BridgeII)));           // bridge is now called
// A bridge with the wrong signature must return UnsafeMatch or Failed
// and leave the original call intact.
```

Wire these into `TranspilerTestHarness.RunAllHarnessCases()` (extend it the same way the
built-in cases are) and call `AssertAllHarnessCasesPass()` from your test entry. A signature
regression then fails the test before release.

### Determinism contract for fixed custom scenarios

When `ModRandomBridge.ScenarioFixedSeedActive` is true, the bridge captures the scenario's
fixed seed as the root of eight isolated streams: `map`, `characters`, `encounters`,
`weather`, `visits`, `combat`, `items`, and `misc`. Each stream seed is a stable FNV-1a
composition of `(scenarioFixedSeed, domainName)`. The manifest transpiler assigns a whole
declaring-type batch to one domain and emits the domain name at each redirected call site.
Draw-order changes in another domain therefore cannot perturb the current domain.
Calls to vanilla's generic `ExtensionMethods.Shuffle<T>` inside a patched batch are also
redirected to the same domain stream; with the gate inactive the bridge executes the
original shuffle loop against Unity RNG, preserving its draw count and bounds.

`ExpeditionMap.CreateMap` and `CreateStasisMap` still redirect their vanilla
`Random.InitState(randomSeed)` calls through `InitScenarioState(int)`, but the active-gate
path deliberately ignores that argument. Vanilla initializes `ExpeditionMap.randomSeed`
from `DateTime.Now.Ticks` when it is zero, so accepting it would replace the fixed scenario
root on every entry. The bridge instead resets only the `map` sub-stream from the captured
scenario seed and logs the scenario seed, derived domain seed, and reset origin. Gate
activation resets and logs all eight domains, making replay boundaries auditable.

As a result, fresh or restarted generation produces the same immutable map tuple
`(gridX, gridY, regionName, topography, category)` for the same fixed seed, while a
different fixed seed selects different domain streams. Do not include visibility,
discovery, encounter chance, or item counts in the map identity; those fields are mutable
gameplay state.

The retained `ProceduralTile.rnd` System.Random field and initializer rows outside the
catalogued redirect scope remain vanilla-owned. Isolation is per declared domain, not a
promise that ordering changes within one domain are irrelevant. A restart is a
regeneration proof only when the route actually invokes map creation. With the gate
inactive, every bridge target calls Unity directly, including `InitState`, so normal
vanilla games retain the original global RNG behavior.

---

## 10. Find the implementation file

All files in this section live in `ModAPI/Harmony/Transpilers/` unless noted.

### Session

- `FluentTranspiler.cs` contains the session type, `Execute`, `For`, `Build`, the find and match methods,
  the insert and replace methods, structural replacement methods, and
  the legacy `ReplaceAllCalls` shim.
- `FluentTranspilerExecution.cs` contains `ExecuteOrOriginal`, which returns the original IL after a failure.
- `FluentTranspiler_Features.cs` contains the linter and session helpers.
- `FluentTranspilerFormatting.cs` contains method and type formatting for diagnostics.

### Recipes

- `FluentTranspilerCallRecipes.cs` contains `ForCall`, `ReplaceCalls`, and the batch
  `RedirectCallsAppendingLiteral` helper.
- `FluentTranspilerReturnRecipes.cs`, `FluentTranspilerWrapRecipes.cs`,
  `FluentTranspilerGuardRecipes.cs`, `FluentTranspilerClampRecipes.cs`,
  `FluentTranspilerRangeCheckRecipes.cs`, and `FluentTranspilerFieldRecipes.cs` each contain one recipe family.
- `IntentAPI.cs` contains the common edit shims (`ChangeConstant`, `RemoveCall`, `InjectBeforeCall`,
  and the now-`[Obsolete]` `RedirectCall`/`RedirectCallAll`).
- `UnityPatterns.cs`, `AdvancedExtensions.cs`, and `ControlFlowExtensions.cs` contain Unity, opcode, and control-flow
  convenience selectors.

### Matching and discovery

- `FluentTranspilerPatterns.cs` contains pattern and sequence matching methods.
- `CartographerExtensions.cs` contains O(n) anchor scoring through `MapAnchors` and `ExportAnchors`.
- `MatchIntent` in `FluentTranspiler_Features.cs` matches named sequences and suggests nearby anchors.

### Validation

- `TranspilerSafetyPolicy.cs` reads `ModPrefs`, classifies critical warnings, and resolves build profiles.
- `StackSentinel.cs` performs basic-block stack analysis through `Validate` and `Analyze`.
- `FluentTranspilerRecipeValidation.cs` validates signatures and instruction shapes before mutation,
  plus `FluentRecipeUtility` (method resolution, literal-load building, search-start indexing).

### Diagnostics

- `TranspilerDebugger.cs` creates snapshots and renders diffs.
- `FluentTranspilerCompatibilityExtensions.cs` contains `FluentReplacementResult`, `Succeeded()`, and the
  predicate-based `ReplaceMatching*`/`ReplaceAt*` primitives the recipes build on.

### Integration and tests

- `CooperativePatcher.cs` orders multi-mod patches, detects conflicts, and applies quarantine policy.
- `TranspilerTestHarness.cs` contains game-independent cases and the `RunAllHarnessCases` and `AssertAllHarnessCasesPass` entry points.
- `ModAPI/Inspector/RuntimeILInspector.cs` contains the in-game F10 live IL dump. It simulates other mods'
  transpilers against a throwaway `ILGenerator` so transpilers can declare labels and locals.

## 11. Use each verb consistently

Use these meanings when authoring or reviewing code:

| Verb | Meaning |
| --- | --- |
| **Find*** | Move the cursor to the next instruction matching a shape; records a soft failure on miss, never throws. |
| **Match*** | Same intent as `Find*` for a call/field/opcode; the `MethodInfo` overloads also record a soft failure on miss. |
| **MapAnchors and ExportAnchors** | Rank stable anchor points; do not move the cursor or edit IL. |
| **MatchIntent** | Assert a short sequence still exists; on miss, suggest the nearest surviving anchor. |
| **Replace* and RedirectCalls*** | Mutate IL. Recipes validate the hook signature before mutating and return a `FluentReplacementResult`. |
| **Validate*** | Pure predicate in `FluentTranspilerRecipeValidation`; returns a Boolean and a warning, but never mutates. |

"Locate" is not a framework verb. Use `Find*` or `Match*`. Prefer those soft-failing methods over
raw index math. When you must use an absolute index through `MoveTo` or `ReplaceAt`, recompute it from a fresh
`Instructions()` snapshot after any insertion or removal.

## 12. Resolved limitations

These earlier limitations are resolved:

- `CaptureLocal("name")` now returns a soft failure because net35 exposes no local names to a
  transpiler. Numeric indices work as before.
- `FindIfStatement` dropped its placeholder `Nop` seek and reports a soft failure when no branch is found.
- `RuntimeILInspector` simulates transpilers against a real throwaway `ILGenerator` instead of `null`.
- `Build` runs `StackSentinel.Analyze` once for the (rare) explicit stack expectations instead of twice.
