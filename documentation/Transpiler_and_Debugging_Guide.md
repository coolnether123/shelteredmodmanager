# ModAPI V1.2: Transpiler & Debugging Guide

The **FluentTranspiler** and **TranspilerDebugger** systems are designed to make IL manipulation safer, more readable, and significantly easier to troubleshoot in the Unity 5.x environment used by Sheltered.

## 1. FluentTranspiler Engine

The `FluentTranspiler` class has been significantly enhanced in V1.2 ModAPI to support complex pattern matching and "safe" modifications that don't corrupt the underlying Harmony `CodeMatcher` state.

### Getting Started

```csharp
using ModAPI.Harmony;

// Standard boilerplate - passing 'original' enables context-aware stack validation
return FluentTranspiler.For(instructions, original)
    .MatchCall(typeof(SomeClass), "SomeMethod")
    .ReplaceWithCall(typeof(MyPatch), "MyReplacement")
    .Build();
```

### New Inspection Predicates

Instead of raw access to `opcode` and `operand`, use these safe helper methods:

- `IsLdcR4(float val)`: Checks for float constants.
- `IsLdcI4(int val)`: Checks for int constants (handles all short forms like `ldc.i4.0`).
- `IsCall(Type, Method)`: Checks for method calls (handles both `Call` and `Callvirt`).
- `IsNewobj(Type)`: Checks for object creation.
- `IsNewobjVector2()` / `IsNewobjVector3()`: Specialized Unity helpers.

### Context Inspection (Safe Backtracking)

V1.2 ModAPI introduces safe backtracking. You can look behind the current code position **without moving the cursor** or risking state corruption.

```csharp
// Example: Check if the previous 3 instructions form a Vector3(0,0,0) pattern
.MatchCall(typeof(ExpeditionMap), "WorldPosToGridRef")
.If(() => t.CheckBackward(3, 
    i => i.opcode == OpCodes.Ldc_R4 && (float)i.operand == 0f,
    i => i.opcode == OpCodes.Ldc_R4 && (float)i.operand == 0f,
    i => i.IsNewobjVector2()), 
    t => {
       // Only runs if the condition is met
       t.ReplaceWithCall(typeof(MyHelper), "GetShelterPos");
    })
```

### Safe Modification Methods

Traditional `Remove()` calls shift array indices and can break subsequent matches. FluentTranspiler in V1.2 ModAPI provides safe alternatives:

*   **`RemoveWithPrevious(n)`**: Removes the current instruction and `n` previous ones in a single atomic operation.
*   **`ReplaceSequence(removeCount, newInstructions)`**: Replaces a block of code with a new sequence without invalidating the matcher.
*   **`ReplaceAllPatterns(...)`**: Finds **ALL** occurrences of a pattern and replaces them, handling index shifts automatically.

---

## 2. The "Sheltered Patterns" Encyclopedia

V1.2 ModAPI includes specialized logic for common game-specific IL patterns found in `Assembly-CSharp.dll`.

### Pattern: Singleton Managers
Sheltered relies on the `instance` property for almost all systems.
*   **IL**: `call static T T::get_instance()`
*   **Match**: `.MatchManager(typeof(GameModeManager))`

### Pattern: Vector Fallbacks (World to Grid)
The game often creates a `new Vector2(0,0)` only to immediately pass it to a coordinate conversion method.
*   **IL**: `ldc.r4 0, ldc.r4 0, newobj Vector2, call WorldPosToGridRef`
*   **Match**: `.ReplaceVectorZeroThenMethodCall(...)`

### Pattern: DontDestroyOnLoad
Game managers are often protected from scene unloads. ModAPI provides helpers to correctly target these.
*   **Helper**: `.NukeDontDestroyOnLoad()`

---

## 3. High-Level Instruction Patterns

The `UnityPatterns` and `ShelteredPatterns` classes provide high-level helpers for common tasks.

### Replacing Complex Property Access
Property access like `Vector2.zero` can be a property call or a field access depending on the Unity version. V1.2 ModAPI handles this automatically.

```csharp
// Replaces ANY version of Vector2.zero access with an optimized static call
t.ReplaceVectorZeroWithCall(typeof(BunkerHelper), "GetStartPos");
```

**Important: `preserveInstructionCount` Parameter**

When replacing patterns, you have two modes:

1.  **`preserveInstructionCount: true` (Production Default)**:
    *   Replaces the first N-1 instructions with `Nop`.
    *   Replaces the last instruction with your new code.
    *   **Use when**: The code might have labels pointing to it. This keeps the Harmony validator happy and prevents branch target corruption.

2.  **`preserveInstructionCount: false` (Compact Mode)**:
    *   Removes all pattern instructions and inserts the new code.
    *   **Use when**: You are sure the code is standalone and no other logic jumps into the middle of it.

---

## 4. Debugging & Inspection Tools

### Automatic IL Dumping
If a transpiler fails or produces invalid code, V1.2 ModAPI allows you to dump the state to the logs.

1.  **Enable Debugging**: Set `debugTranspilers: true` in your mod settings.
2.  **Explicit Dumps**: Use these commands inside your transpiler:

```csharp
t.Log("Applying patch...");
t.DumpAll("Before Modification"); // Dumps full IL to log
// ... apply logic ...
t.DumpAll("After Modification");
```

### SmartWatcher (State Monitoring)
Reverse engineer game logic by monitoring fields or properties for changes at runtime.

```csharp
// Monitor the 'currentState' field on the GameManager
SmartWatcher.Watch<GameManager, State>("GameState", 
    instance => instance.currentState, 
    (oldVal, newVal) => MMLog.WriteInfo($"State changed: {oldVal} -> {newVal}"));
```

---
 
 ## 5. Advanced V1.2 Features
 
 V1.2 includes powerful tools for managing local variables and patching overloads.
 
 ### Local Variables & Labels
 You can now define method-local variables and labels directly within the transpiler chain (requires passing `ILGenerator`).
 
 ### Example: Loop Counter with Local Variables
 
 ```csharp
 [HarmonyTranspiler]
 public static IEnumerable<CodeInstruction> MyTranspiler(
     IEnumerable<CodeInstruction> instructions,
     ILGenerator il) // <--- REQUIRED: Add this argument!
 {
     LocalBuilder counter;
     return FluentTranspiler.For(instructions, generator: il)
         .DeclareLocal<int>(out counter)
         .MatchCall(typeof(UnityEngine.Random), "Range")
         .InsertBefore(OpCodes.Ldc_I4_0)
         .InsertBefore(OpCodes.Stloc, counter)    // Store 0 in counter
         .InsertAfter(OpCodes.Ldloc, counter)     // Load counter
         .InsertAfter(OpCodes.Ldc_I4_1)
         .InsertAfter(OpCodes.Add)                // Add 1
         .Build();
 }
 ```
 
 > **CRITICAL**: Requires `ILGenerator`!
 > If you forget to pass the generator, you will get an `InvalidOperationException`:
 > *"ILGenerator was not provided. Pass it to For(instructions, originalMethod, generator)."*
 
 ### Multi-Overload Patching
 Easily patch all overloads of a method (e.g., `Foo()`, `Foo(int)`) in one go.
 
 ```csharp
 // Scenario: Manager.SetState() has 3 overloads
 // public void SetState(int newState) { }
 // public void SetState(State state) { }
 // public void SetState(string stateName) { }
 
 // Patch ALL overloads
 HarmonyHelper.PatchAllOverloads(
     harmony, 
     typeof(Manager), 
     "SetState", 
     prefix: new HarmonyMethod(typeof(MyPatches), "SetStatePrefix"));
 
 // Patch ONLY the int overload
 HarmonyHelper.PatchAllOverloads(
     harmony, 
     typeof(Manager), 
     "SetState", 
     parameterTypes: new[] { typeof(int) },
     prefix: new HarmonyMethod(typeof(MyPatches), "SetStatePrefix"));
 
 // Patch ONLY non-generic overloads
 HarmonyHelper.PatchAllOverloads(
     harmony,
     typeof(Manager),
     "SetState",
     ignoreGenerics: true,
     prefix: new HarmonyMethod(...)
 );
 ```
 
 ---
 
 ## 6. Transpiler "Troubleshooting Matrix"
 
 | Symptom | Likely Cause | V1.2 ModAPI Solution |
 |---|---|---|
 | **`InvalidProgramException`** | Unbalanced stack (Push vs Pop mismatch). | Check your logic or use `Build(validateStack: true)` for details. |
 | **"Underflow at index X"** | False positive in instance methods. | Pass the `original` method to `For(codes, original)`. |
 | **Crash on Scene Load** | Nuked a critical `DontDestroyOnLoad` manager. | Use `.NukeDontDestroyOnLoad()` only on targeted objects. |
 | **Patch "Doesn't Apply"** | Game version used `callvirt` instead of `call`. | Use `MatchCall()` which matches both automatically. |
 | **Broken Branch Targets** | Instruction removal changed indices. | Use `preserveInstructionCount: true` to use `Nops`. |
 | **"ILGenerator was not provided"** | Used `DeclareLocal` without passing generator. | Update your patch to accept `ILGenerator` and pass it to `.For()`. |
 
 ---
 **Released with ModAPI V1.2.0**
