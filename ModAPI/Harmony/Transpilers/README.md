# FluentTranspiler — the transpiler framework

A fluent wrapper around Harmony's `CodeMatcher` for rewriting game and mod IL safely.
The goal is that a developer (or an AI agent) can go from *"I want call X redirected to Y
inside methods Z"* to a working patch in a few readable lines — without hand-managing
labels, branch fixups, or stack balance, and without reading the whole framework.

This document is the contract for the framework: when to reach for it, how to find where
to patch, the canonical patterns for the common intents, the safety rules, the debugging
workflow, and the performance budget.

---

## 1. When to use a transpiler (and when not to)

Prefer the cheapest tool that does the job:

| Need | Use |
| --- | --- |
| Run code before/after a method, or read/replace its args & return | **Prefix / Postfix** (`HarmonyPrefix` / `HarmonyPostfix`) |
| Skip the original entirely | Prefix returning `false` |
| Change a value the method computes internally, or redirect a call **inside** a method | **Transpiler (this framework)** |
| Redirect *the same call in many methods* consistently | **Transpiler**, ideally one shared transpiler body applied to a list of targets |

Reach for a transpiler only when a prefix/postfix cannot express the intent, because a
transpiler edits raw IL and is inherently more fragile across game updates. The framework
exists to make that fragility manageable, not to make transpilers the default.

---

## 2. The one entry point

For 95% of patches, use `FluentTranspiler.Execute`. It opens the session, runs your
transform, and finishes with `Build` (validation + diagnostics) automatically.

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

- **Always pass `original`** — it powers stack analysis, the linter, and diagnostics.
- **Pass `il`** whenever you may declare a local or define a label; otherwise `null` is fine.
- If you want the original IL returned instead of a throw when a patch throws, wrap with
  `FluentTranspilerExecution.ExecuteOrOriginal(...)` and supply an `onFailure` fallback.

Other entry points and when they are appropriate:

| Entry | Use it when |
| --- | --- |
| `FluentTranspiler.Execute(...)` | **Canonical.** Normal Harmony transpiler. |
| `FluentTranspilerExecution.ExecuteOrOriginal(...)` | You want a guaranteed fallback to original IL on failure. |
| `FluentTranspiler.For(...)` + manual `.Build(profile)` | You need multi-stage control or an explicit build profile. |
| `CooperativePatcher.RegisterTranspiler(...)` | Several mods patch the same method and must be ordered / conflict-checked. |
| `TranspilerTestHarness.FromInstructions(...)` | Unit-testing a transform off a running game. |

---

## 3. Finding *where* to patch

Two supported workflows:

**Runtime IL Inspector (in-game, press F10).** `ModAPI.Inspector.RuntimeILInspector`
lets you search a type/method and dump its live IL — including the IL *after* other mods'
transpilers have run. Use it to read the exact opcodes and operands around the site you
want, and to confirm which mods already patched the method.

**Cartographer (in code).** During development, call the anchor mapper to find robust,
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

On failure it records a readable soft failure **and** suggests the nearest surviving
anchor, so a broken patch tells you where the code moved.

---

## 4. Canonical patterns for the common intents

All of the recipe entry points below return a `FluentReplacementResult`
(`PatternReplaced`, `NoMatch`, `AmbiguousMatch`, `UnsafeMatch`, `Failed`, ...). Call
`.Succeeded()` on it if you want a bool. They validate the hook signature **before**
mutating IL and refuse unsafe edits.

### 4.1 Redirect a static/instance call to a replacement (everywhere in the method)

```csharp
t.ForCall(typeof(SomeType), "ComputeValue")
 .ReplaceAllWith(typeof(MyHooks), "ComputeValue");
```

The replacement must be **static** and consume the same stack shape as the original call
(for an instance call that means the first hook parameter is the instance type), returning
the same/compatible type. Use `.ReplaceWith(...)` (singular) when you expect exactly one
call site — it fails with `AmbiguousMatch` if there are several, which is a feature.

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

### 4.4 Guard a call — skip the original when a condition holds

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

### 4.7 Remove a call (e.g. analytics/logging)

```csharp
t.RemoveCall(typeof(Analytics), "Track");   // pops args, pushes a safe default if needed
```

### 4.8 Inject at method entry / before every return

```csharp
t.InsertAtStart(new CodeInstruction(OpCodes.Call, myInitHook));
t.InsertAtExit (new CodeInstruction(OpCodes.Call, myCleanupHook));
```

### 4.9 Bounds/range-check recipes (for clamp / `0..N` validation sites)

```csharp
t.ForArgument(2).InRangeCheck(0, 4).ReplaceUpperBoundWithCall(getMaxPriority);
```

These understand the several compiler shapes of a range check (`blt/ble`, inverted
`bgt/bge`, `>= upper+1`, NOP gaps) and refuse to patch when the match is ambiguous or a
branch target lands inside the edited span.

---

## 5. Safety rules (what the framework enforces, and what you must still do)

Validation is **on by default** (`ModPrefs.TranspilerSafeMode = true`,
`TranspilerFailFastCritical = true`). Every `Build` runs:

- **StackSentinel** — basic-block stack-depth/type analysis. A stack imbalance becomes a
  `Stack Error:` which is *critical* and aborts the build by default.
- **Linter** — flags null operands on `call/ldfld/...`, branch operands that aren't
  `Label`s (a classic native-crash cause), out-of-range local/argument indices, and
  `castclass` with a non-`Type` operand. All of these are `[CRITICAL LINT]` and abort.
- **Recipe signature validation** — the `ForCall`/`Returns`/`ReplaceCalls`/range-check
  recipes verify the hook is static and stack-compatible before touching IL. A mismatch
  returns `UnsafeMatch`/`Failed` and leaves the original IL untouched.

The framework also protects structural edits: `ReplaceSequence`, `ReplaceAll`,
`ReplaceAllPatterns` capture and re-attach labels/exception blocks, roll back on any
exception, refuse to relocate exception-handler boundaries, and abort if a branch targets
the interior of a removed range.

What you are still responsible for:

- **Match the stack shape.** A replacement for a call to an instance method
  `int Foo.Bar(float)` must be `static int Hook(Foo self, float x)`.
- **Keep hooks static.** Instance hooks are rejected.
- **Prefer `original`-aware sessions.** Without `original`, stack validation and the
  linter's local/argument checks are limited.
- **Methods with try/catch are constrained.** StackSentinel cannot model exception
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
| `DebugTranspilers` | `false` | Verbose tracing + snapshots. Leave **off** in production. |

---

## 6. Debugging workflow

1. **Unit-test the transform off-game** with `TranspilerTestHarness`:

   ```csharp
   var t = TranspilerTestHarness.FromInstructions(instructions, originalMethod);
   t.ForCall(typeof(SomeType), "Foo").ReplaceAllWith(typeof(MyHooks), "Foo");
   var result = TranspilerTestHarness.RunTest(t, strict: true);
   TranspilerTestHarness.AssertInstruction(result, index, OpCodes.Call, expectedHook);
   ```

2. **Smoke-test the framework itself** after any change to it:

   ```csharp
   TranspilerTestHarness.AssertAllHarnessCasesPass();   // throws on any FAIL
   // or inspect: foreach (var line in TranspilerTestHarness.RunAllHarnessCases()) ...
   ```

3. **Turn on tracing** (`ModPrefs.DebugTranspilers = true`) and use the Debug build
   profile to get snapshots via `TranspilerDebugger`. `t.DumpAll()`, `t.Log()`, and
   `t.DumpDiffFrom(original)` print IL state during development.

4. **Read the diagnostics.** `t.Warnings`, `t.SoftFailures`, `t.Notes`, and the structured
   `t.PatchDiagnostics` explain what shape was expected vs found and what action was taken.
   A failed recipe returns a `FluentReplacementResult` — check it.

Successful production patches are silent by design: snapshots and warning logs only fire
on critical warnings or when `DebugTranspilers` is on.

---

## 7. Performance guidance (load-time budget)

Transpilers run once per patched method at patch time. The dominant cost is Harmony's IL
re-emit and JIT, which happens regardless of this framework. Framework overhead per patch
is small and linear:

- Matching (`ForCall`, `Find*`, `MapAnchors`) is O(instructions).
- `Build` runs one StackSentinel pass (basic-block analysis, O(instructions)) plus one
  linter pass. Explicit `ExpectStack`/`EnsureStack` expectations trigger extra analysis
  passes — use them only where you need them.
- To keep many patches cheap:
  - **Resolve hook `MethodInfo`s once** (static readonly fields) and pass them to the
    `MethodInfo` overloads (`ReplaceCalls(mi).WithCall(mi)`), avoiding per-patch reflection
    and ambiguous-overload lookups.
  - **Stay on the Runtime build profile** (the `Execute` default). Do not ship on the Debug
    profile — it records expensive snapshots.
  - Keep `DebugTranspilers` off in production.

For dozens of target methods, the total added load cost is a handful of milliseconds and
should not meaningfully move game load times.

---

## 8. Pitfalls

- **Anchoring on bare opcodes.** `Ldc_I4_4` appears everywhere. Anchor on a unique string,
  a specific method/field, or a short distinctive sequence (`MatchIntent`, `MapAnchors`).
- **Assuming one call site.** `ReplaceWith` (singular) returns `AmbiguousMatch` if there
  are several — use `ReplaceAllWith` or `WhenSurroundedBy` to be explicit.
- **Wrong stack shape on the hook.** The commonest cause of `UnsafeMatch`. Remember the
  implicit `this` for instance calls.
- **Editing methods with exception handlers.** Only index-aligned replacements are allowed;
  structural edits are refused.
- **Branch operands as integers.** Never pass an `int` where a `Label` is expected — the
  linter flags it, but it is a native-crash class of bug.
- **Ignoring the result.** Recipes never throw on a clean miss; they return `NoMatch`. If
  your patch silently does nothing, check the returned `FluentReplacementResult` and
  `t.SoftFailures`.

---

## 9. Design note — redirecting vanilla RNG to `ModRandom` (flagship consumer)

Goal: redirect vanilla RNG call sites — `UnityEngine.Random.Range` (int and float
overloads), `Random.value`, `Random.insideUnitCircle`, etc., plus `System.Random` usage —
across ~76 decompiled game files to `ModAPI.Core.ModRandom`, **gated** so that outside a
custom scenario the behavior still mimics Unity randomness.

**Recipe choice.** These are call-site redirects, so the canonical tool is the call recipe.
`UnityEngine.Random` members are *static*, which makes them the easy, exact case: a static
`Random.Range(int,int)` maps 1:1 to a static hook `Range(int,int)` with no `this` to model,
so signature validation passes cleanly. Redirect to a **bridge**, not to `ModRandom`
directly, so the gating lives in one place:

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
`Random.value` decompiles to a call to `get_value` — redirect it to `Value()`.

**Per-site redirect** (inside one shared transpiler body):

```csharp
static readonly MethodInfo RangeII = AccessTools.Method(typeof(UnityEngine.Random), "Range", new[]{typeof(int),typeof(int)});
static readonly MethodInfo BridgeII = AccessTools.Method(typeof(ModRandomBridge), "Range", new[]{typeof(int),typeof(int)});
// ...one pair per signature...

t.ReplaceCalls(RangeII).WithCall(BridgeII);
t.ReplaceCalls(RangeFF).WithCall(BridgeFF);
t.ForCall(typeof(UnityEngine.Random), "get_value").ReplaceAllWith(typeof(ModRandomBridge), "Value");
```

`System.Random` is harder: those are *instance* calls, so a redirect hook must take the
`Random` instance as its first parameter (`static int NextGated(System.Random self, int max)`)
and decide whether to defer to `self` or to `ModRandom`. Prefer replacing the construction
of `System.Random` with a gated factory where feasible; fall back to instance-shaped hooks
only at the specific sites that need it.

**Batching strategy across ~76 methods.** Do **not** write 76 transpilers. The fluent body
above is method-agnostic — it operates on whatever IL it is handed. Write it **once** as a
single shared transpiler method, then apply it to a curated list of target methods:

```csharp
static IEnumerable<CodeInstruction> RngTranspiler(IEnumerable<CodeInstruction> instrs, MethodBase original)
    => FluentTranspiler.Execute(instrs, original, null, t => ApplyRngRedirects(t));

foreach (var target in RngTargetMethods)                 // curated, generated once
    harmony.Patch(target, transpiler: new HarmonyMethod(typeof(RngPatches), nameof(RngTranspiler)));
```

Keep the target list data-driven (generated from a scan of the decompiled call sites) so it
is auditable and easy to regenerate after a game update. Resolve every `RangeII/BridgeII`
`MethodInfo` **once** into static readonly fields (as above) so 76 applications share the
lookups.

**Load-time budget.** Each application is O(method length) matching + a single StackSentinel
pass at `Build`. For ~76 methods this is a few milliseconds total, dwarfed by Harmony's IL
re-emit — negligible for load time. Stay on the Runtime profile; keep `DebugTranspilers`
off.

**Verification via the TestHarness.** Before shipping, add harness cases that feed
representative IL and assert behavior, then gate the build on them:

```csharp
var t = TranspilerTestHarness.FromInstructions(
    new CodeInstruction(OpCodes.Ldc_I4_0),
    new CodeInstruction(OpCodes.Ldc_I4_5),
    new CodeInstruction(OpCodes.Call, RangeII));
var res = t.ReplaceCalls(RangeII).WithCall(BridgeII);           // expect PatternReplaced
Assert(res == FluentReplacementResult.PatternReplaced);
Assert(t.Instructions().Any(i => i.Calls(BridgeII)));           // bridge is now called
// And a negative case: a bridge with the wrong signature must return UnsafeMatch/Failed
// and leave the original call intact.
```

Wire these into `TranspilerTestHarness.RunAllHarnessCases()` (extend it the same way the
built-in cases are) and call `AssertAllHarnessCasesPass()` from your test entry so a
signature regression fails loudly rather than silently corrupting RNG.

### Determinism contract for fixed custom scenarios

When `ModRandomBridge.ScenarioFixedSeedActive` is true, the bridge captures the scenario's
fixed seed as the root of eight isolated streams: `map`, `characters`, `encounters`,
`weather`, `visits`, `combat`, `items`, and `misc`. Each stream seed is a stable FNV-1a
composition of `(scenarioFixedSeed, domainName)`. The manifest transpiler assigns a whole
declaring-type batch to one domain and emits the domain name at each redirected call site.
Draw-order changes in another domain therefore cannot perturb the current domain.

`ExpeditionMap.CreateMap` and `CreateStasisMap` still redirect their vanilla
`Random.InitState(randomSeed)` calls through `InitScenarioState(int)`, but the active-gate
path deliberately ignores that argument. Vanilla initializes `ExpeditionMap.randomSeed`
from `DateTime.Now.Ticks` when it is zero, so accepting it would replace the fixed scenario
root on every entry. The bridge instead resets only the `map` sub-stream from the captured
scenario seed and logs the scenario seed, derived domain seed, and reset origin. Gate
activation resets and logs all eight domains, making replay boundaries auditable.

Consequently, real fresh or restart generation produces the same immutable map tuple
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
