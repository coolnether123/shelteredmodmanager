# How to Develop a Harmony Patch | Sheltered Mod Manager v1.2

This guide covers practical Harmony usage with ModAPI.

## 1. Reference Setup

Add `0Harmony.dll` from your SMM install (`SMM/bin/0Harmony.dll`).

Also reference:
- `ModAPI.dll`
- `Assembly-CSharp.dll`
- `UnityEngine.dll`

## 2. Apply Patches in Plugin `Start`

Patch in `Start(...)`, not constructor, so context/logging are ready.

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
        // Runs before original method
    }

    public static void Postfix()
    {
        // Runs after original method
    }
}
```

## 4. Transpiler Template (Fluent)

Use ModAPI's fluent transpiler instead of raw instruction surgery when possible.

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

- Use a unique Harmony ID (`author.modname`).
- Prefer Prefix/Postfix over Transpiler when enough.
- Keep each patch focused on one behavioral change.
- Fail loudly in development (`Build(strict: true)`).
- Add logging around risky branches and patch setup.

## 6. Multi-Mod Compatibility

If multiple mods touch the same target method heavily, use `CooperativePatcher` instead of standalone transpilers.

Benefits:
- explicit order (`PatchPriority`)
- dependency checks (`dependsOn`)
- conflict guards (`conflictsWith`)

## 7. Debugging

Use these tools:
- `RuntimeILInspector` (`F10`) to inspect patched IL in-game.
- `TranspilerDebugger.DumpWithDiff(...)` for before/after files.
- `TranspilerTestHarness` for isolated transform tests.

For detailed IL workflow, see:
- `documentation/Transpiler_and_Debugging_Guide.md`

## 8. Common Failure Modes

- `No match for call ...`: game method changed or overload mismatch.
- Stack validation warning/exception: replace logic unbalanced stack.
- Patch silently ineffective: wrong target signature or patch class not loaded by `PatchAll`.

When this happens:
1. Confirm target method signature.
2. Dump before/after IL.
3. Replace brittle opcode chains with stronger anchors (`FindCall`, `ReplaceAllPatterns`).
