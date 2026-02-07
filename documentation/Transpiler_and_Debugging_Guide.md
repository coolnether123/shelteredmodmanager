# ModAPI Transpilation System (V1.2) - Complete Guide

The ModAPI Transpilation System (located in `ModAPI.Harmony.Transpilers` namespace) provides a comprehensive suite of tools for safe, effective, and maintainable IL manipulation in Sheltered.

This system replaces raw Harmony `CodeInstruction` manipulation with a fluent, type-safe, and self-validating pipeline.

---

## 📚 Core Components Overview

| Component | Responsibility | Usage Pattern |
|---|---|---|
| **FluentTranspiler** | The primary engine. Wraps `CodeMatcher` with safe navigation, specific matching, and bulk operations. | `FluentTranspiler.For(instructions).Find...().Build()` |
| **IntentAPI** | **High-Level Operations**. Express intent (Redirect, Change Constant, Inject) without touching IL opcodes directly. | `t.RedirectCall(...)`, `t.ChangeConstant(...)` |
| **StackSentinel** | **Safety Monitor**. Tracks stack depth across branches (Control Flow Graph analysis) to prevent invalid programs. | Automatically run by `FluentTranspiler.Build()`. |
| **CooperativePatcher** | **Conflict Resolution**. Allows multiple mods to patch the same method safely by sequencing them in a pipeline. | `CooperativePatcher.RegisterTranspiler(...)` |
| **Cartographer** | **Pattern Discovery**. Analyzes methods to find "safe anchors" (unique instruction patterns). | `FluentTranspiler.For(...).MapAnchors()` |
| **ShelteredPatterns** | **Game-Specific Helpers**. Shortcuts for common Sheltered tasks (fixing `Vector2(0,0)`, manager usage). | Extension methods for `FluentTranspiler`. |
| **TranspilerDebugger** | **Diagnostics**. Dumps IL before/after patching and generates diff reports. | `t.DumpWithDiff("MyPatchLabel", ...)` |
| **TestHarness** | **Unit Testing**. Allows testing transpiler logic in isolation without a running game instance. | `TranspilerTestHarness.FromInstructions(...)` |
| **RuntimeILInspector** | **Live Inspection**. In-game tool (F10) to view current method IL and active patches. | Automatic (Press F10 in-game). |

---

## 1. FluentTranspiler: The Engine

This is your primary tool. It wrappers standard Harmony operations with safety checks and fluent syntax.

### Key Features
*   **Intelligent Matching**: Supports type-safe matching of method calls, field access, and properties.
*   **Branch-Safe Insertions**: Automatically handles label transfers when inserting/replacing (mostly).
*   **Stack Validation**: Integrated `StackSentinel` checks every build.

### Basic Usage
```csharp
[HarmonyPatch(typeof(SomeClass), "SomeMethod")]
[HarmonyTranspiler]
public static IEnumerable<CodeInstruction> MyTranspiler(IEnumerable<CodeInstruction> instructions, MethodBase original)
{
    return FluentTranspiler.For(instructions, original)
        .FindCall(typeof(SomeClass), "OldMethod", SearchMode.Start)
        .ReplaceWithCall(typeof(MyMod), "NewMethod")
        .Build();
}
```

### Search Modes
The Find* methods (FindCall, FindOpCode, FindString, etc.) use a `SearchMode` to control where the search begins:

| Mode | Behavior | Use Case |
|---|---|---|
| **`SearchMode.Start`** | Resets the matcher to instruction 0 before searching. | Finding the first occurrence or resetting context. |
| **`SearchMode.Current`** | Searches forward from the current position (inclusive). | Looking for a pattern that follows a previous match. |
| **`SearchMode.Next`** | Advances 1 instruction then searches forward. | Sequential matching (e.g., matching the next call after this one). |

> **Note**: Legacy `MatchCall(...)` and `MatchCallNext(...)` methods are still supported as aliases for `FindCall` with `Start` and `Next` modes respectively.

### Safe Modification Methods
These methods are designed to be "label-safe," meaning they preserve jump targets (labels) from original code.

| Method | Purpose | Label Strategy |
|---|---|---|
| **`ReplaceWith(OpCode, operand)`** | Replaces a single instruction. | Transfers labels from the old instruction to the new one. |
| **`ReplaceWithCall(Type, name)`** | Replaces a call (or any instruction) with a static call. | Transfers labels from the old instruction to the new one. |
| **`ReplaceSequence(count, code)`** | Replaces a block of N instructions with new code. | Captures labels from the **first** removed instruction and anchors them to the **first** replacement instruction. |
| **`ReplaceAll(code)`** | Completely overwrites the method body. | Preserves labels on the method entry point (index 0). |
| **`ReplaceAllCalls(...)`** | Finds every instance of a call and replaces it. | Uses resilient type matching (by Name/FullName) and preserves labels on every replacement. |

### Advanced Usage: Bulk Replacement
Replace **ALL** occurrences of a pattern safely.

```csharp
// Scenario: Replace all "x = new Vector2(0,0)" with "x = MyHelper.GetPos()"
t.ReplaceAllPatterns(
    patternPredicates: new Func<CodeInstruction, bool>[] {
        i => i.opcode == OpCodes.Ldc_R4 && (float)i.operand == 0f,
        i => i.opcode == OpCodes.Ldc_R4 && (float)i.operand == 0f,
        i => i.IsNewobjVector2()
    },
    replaceWith: new[] {
        new CodeInstruction(OpCodes.Call, myHelperMethod)
    },
    preserveInstructionCount: true // Use NOPs to keep labels intact
);
```

> **💡 Pro Tip**: Always prefer `preserveInstructionCount: true` for `ReplaceAllPatterns`. This fills deleted slots with `Nop` instructions, ensuring that any branch jumping into the middle of your pattern still has a "safe" (landing spot. The system automatically transfers labels to these NOPs to maintain control flow integrity.


---

## 2. IntentAPI: High-Level Logic

For many patches, you don't need to reason about IL opcodes. The `IntentAPI` provides methods that express "what" you want to do.

### RedirectCall
Redirects a call from an original method to your hook. It automatically validates parameter types and return types, and handles instance-to-static conversion (passing `this` as the first argument).

```csharp
t.RedirectCall(
    typeof(GameModeManager), "OnDayPassed",
    typeof(MyHooks), "MyOnDayPassed",
    allOccurrences: true
);
```

### ChangeConstant
Updates a constant value (e.g., a magic number used for speed, fuel, or timers).

```csharp
t.ChangeConstant(oldValue: 1.0f, newValue: 1.5f, allOccurrences: true);
```

### RemoveCall
Removes a call and its arguments from the stack. If the method returns a value, it pushes a `default` value (like `0` or `null`) to keep the stack balanced.

```csharp
t.RemoveCall(typeof(Analytics), "TrackEvent");
```

### InjectBeforeCall
Calls your hook immediately before a target method is called. The hook receives the arguments of the **enclosing** method.

```csharp
t.InjectBeforeCall(typeof(Bunker), "Open", typeof(MyLogging), "LogBunkerOpening");
```

## 2. StackSentinel: The Safety Net

A graph-based (CFG) analyzer that validates stack height at every instruction, following branches and loops.

### Capabilities
*   **Branch Awareness**: Tracks stack height across `br`, `beq`, `bne`, etc.
*   **Error Reporting**: "Stack height mismatch at branch target Block_12: expected 2, got 1".
*   **Integration**: Runs automatically in `FluentTranspiler.Build()`.

### When to use directly
You generally don't call this directly unless writing a custom debugger or tool.
```csharp
// Manual check
if (!StackSentinel.Validate(myInstructions, myMethod, out string error))
{
    MMLog.WriteError("FATAL: " + error);
}
```

---

## 3. CooperativePatcher: Multi-Mod Harmony

Solves the "incompatible usage" problem where two mods try to transpile the same method and break each other's assumptions.

### Concept
Instead of applying a Harmony patch directly, you **register** a patch. The `CooperativePatcher` applies them in order of priority, validating the stack state between each one.

### Usage
```csharp
public class MyMod : IModPlugin
{
    public void Initialize(IPluginContext context)
    {
        CooperativePatcher.RegisterTranspiler(
            target: typeof(GameManager).GetMethod("Update"),
            anchorId: "MyMod_FixUpdateLogic",
            priority: PatchPriority.High,
            patchLogic: (t) => t
                .FindCall(typeof(GameManager), "OldLogic")
                .ReplaceWithCall(typeof(MyHooks), "NewLogic"),
            dependsOn: new[] { "OtherMod_PreFix" }, // Optional
            conflictsWith: new[] { "IncompatibleMod_Hack" } // Optional
        );
    }
}
```

### Dependency Management
*   **DependsOn**: The patch will only apply if the listed `anchorId` patches have already been applied successfully.
*   **ConflictsWith**: The patch will skip applying if any of the listed `anchorIds` are already in the pipeline.
*   **Dynamic Removal**: You can use `CooperativePatcher.UnregisterTranspiler("MyAnchorId")` to remove a patch at runtime.

---

## 4. StackSentinel: The Safety Net

A graph-based (CFG) analyzer that validates stack height at every instruction.

### Enhancements in V1.2
*   **Exception Safety**: Automatically skips validation for methods with `try/catch/finally` blocks (which current basic block analysis doesn't fully support) to avoid false positives.
*   **Instance Support**: Improved tracking for `this` parameter pops in instance calls.
*   **OpCode Coverage**: Added missing push/pop counts for `Newobj`, `Dup`, and various 3-pop opcodes.

---

## 5. IL Cartographer: The Map Maker

Analyzes methods to find "safe anchors" (unique instruction patterns).

### Usage
```csharp
// Quick log of all safe anchors during development:
FluentTranspiler.For(instructions).ExportAnchors();

// Or manual analysis:
var analysis = FluentTranspiler.For(instructions).MapAnchors(threshold: 1.2f);
MMLog.WriteInfo(analysis.ToSummary());
```
*   **Frequency Analysis**: Unique strings and method calls are scored higher than repeated ones.
*   **Context Scoring**: Sequences of unique instructions receive a bonus.

---

## 6. ShelteredPatterns: Game-Specific Logic

Specialized helpers for Sheltered's common patterns.

| Helper | Purpose |
|---|---|
| `MatchManager(Type)` | Matches the `Manager.instance` singleton pattern. |
| `ReplaceVectorZeroThenMethodCall` | Replaces `new Vector2(0,0)` + `Method(vec)` with a custom getter. |
| `ReplaceFieldAssignment` | Replaces `this.field = val` with `MyMethod(instance, val)`. |

> **Note**: Dead API surface (`ReplaceFieldAssignment<T>`) has been removed in V1.2 to reduce confusion.

---

## 7. TranspilerDebugger: The Diff Tool

### New Output Format
The `_Diff.txt` file now uses a line-based format which is much easier to read for long IL instructions:

```text
003 - callvirt System.String System.Object::ToString()
003 + call static System.String MyMod.Hooks::CustomToString(object)
```

---

## 8. Unit Testing: TranspilerTestHarness

You can now test your transpiler logic in isolation without running the game or setting up a full Harmony patch.

```csharp
[Test]
public void TestMyLogic()
{
    var original = new[] {
        new CodeInstruction(OpCodes.Ldstr, "hello"),
        new CodeInstruction(OpCodes.Call, m_Print)
    };

    var result = TranspilerTestHarness.FromInstructions(original)
        .FindCall(typeof(Console), "Print")
        .ReplaceWith(OpCodes.Pop)
        .Build();

    TranspilerTestHarness.AssertInstruction(result, 1, OpCodes.Pop);
}
```

---

## 9. RuntimeILInspector: The Live View
(F10 in-game)
- See live IL, owners of patches, and instruction counts.

---

## Best Practices Checklist

1.  **Use SearchMode**: Be explicit about whether you are starting from the beginning (`Start`) or continuing (`Next`).
2.  **Use IntentAPI**: Favor `RedirectCall` and `ChangeConstant` over manual opcode manipulation for better readability.
3.  **Validate with TestHarness**: Write unit tests for complex transpiler logic.
4.  **Check Diff Reports**: Review the dumped diff files to verify your patch applied as expected.
5.  **Use Dependencies**: Declare `DependsOn` if your patch relies on another mod's changes.
