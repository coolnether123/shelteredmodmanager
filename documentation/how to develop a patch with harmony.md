# How to Develop a Harmony Patch | Sheltered Mod Manager v1.3

This guide covers practical Harmony usage with the current ModAPI stack.

Exact API signatures: `documentation/API_Signatures_Reference.md`.

## Compatibility Matrix

| Scope | Applies To | Status |
|-------|------------|--------|
| Harmony patch workflow and safety practices | Current `ModAPI.dll` | Supported |
| Fluent transpiler helpers | Current `ModAPI.dll` | Supported |
| Detailed IL debugging tools | Current `ModAPI.dll` | Supported |

## 1. Reference Setup

Add references to:
- `ModAPI.dll`
- `0Harmony.dll`
- `Assembly-CSharp.dll`
- `UnityEngine.dll`

Add `ShelteredAPI.dll` only if your patch code uses `ShelteredAPI.*` namespaces directly.

## 2. Apply Patches in `Start(...)`

Patch in `Start(...)`, not in constructors, so loader context and logging are ready.

```csharp
using ModAPI.Core;
using HarmonyLib;
using System.Reflection;

public class MyPlugin : IModPlugin
{
    private IModLogger _log;
    private Harmony _harmony;

    public void Initialize(IPluginContext ctx)
    {
        _log = ctx.Log;
    }

    public void Start(IPluginContext ctx)
    {
        _harmony = new Harmony("yourname.myplugin");
        _harmony.PatchAll(Assembly.GetExecutingAssembly());
        _log.Info("Harmony patches applied");
    }
}
```

## 3. Prefix/Postfix Template

```csharp
using HarmonyLib;

[HarmonyPatch(typeof(SomeGameType), "MethodName")]
public static class SomeGameType_MethodName_Patch
{
    public static void Prefix()
    {
    }

    public static void Postfix()
    {
    }
}
```

## 4. Fluent Transpiler Template

Prefer ModAPI's fluent transpiler surface over raw opcode surgery when possible.

```csharp
using HarmonyLib;
using ModAPI.Harmony;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

[HarmonyPatch(typeof(SomeGameType), "MethodName")]
public static class SomeGameType_MethodName_Transpiler
{
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> Transpiler(
        IEnumerable<CodeInstruction> instructions,
        MethodBase original,
        ILGenerator il)
    {
        return FluentTranspiler.For(instructions, original, il)
            .FindCall(typeof(SomeGameType), "OldCall", SearchMode.Start)
            .ReplaceWithCall(typeof(MyHooks), "NewCall")
            .Build();
    }
}
```

## 5. Patch Design Rules

- Use a unique Harmony ID such as `author.modname`.
- Prefer Prefix/Postfix over transpilers when they are sufficient.
- Keep each patch focused on one behavior change.
- Use stable anchors such as method calls and known patterns instead of brittle opcode offsets.
- In development, keep validation on with `Build(strict: true, validateStack: true)`.

## 6. Multi-Mod Compatibility

If several mods need to transpile the same method, prefer `CooperativePatcher` over isolated transpilers.

Benefits:
- explicit patch order
- declared dependencies
- declared conflicts
- better failure isolation

## 7. Debugging

Useful tools in the current stack:
- `RuntimeILInspector` (`F10`) for in-game IL inspection
- `TranspilerDebugger` for before/after dumps
- `TranspilerTestHarness` for isolated transform tests
- `UIDebugInspector` (`F11`) for UI-oriented runtime investigation

For a deeper IL workflow, see `documentation/Transpiler_and_Debugging_Guide.md`.

## 8. Common Failure Modes

- `No match for call ...`: target method changed or overload mismatch
- stack validation failures: replacement logic left the evaluation stack unbalanced
- patch appears inactive: wrong target signature or patch assembly never loaded

When this happens:
1. Confirm the target method signature.
2. Dump before/after IL.
3. Replace brittle instruction chains with stronger anchors.
4. Reduce the patch to the smallest working edit.
