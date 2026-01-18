# How to Develop a Harmony Patch | Sheltered Mod Manager v1.0

## Harmony Reference
- This project references Lib.Harmony 2.4.1
- You can reference `0Harmony.dll` from your SMM installation at `SMM/bin/0Harmony.dll`
- Alternatively, add Harmony via NuGet in your project

## Quick Start

### 1. Reference Harmony
Reference `0Harmony.dll` from your Sheltered installation's `SMM/bin/` folder.

### 2. Create a patch class in your plugin assembly

```csharp
using HarmonyLib;

[HarmonyPatch(typeof(SomeGameType), "MethodName")] 
public static class SomeGameType_MethodName_Patch
{
    // Prefix runs BEFORE the original method
    public static void Prefix(/* original args, ref bool __runOriginal, etc. */)
    {
        // run before original
    }

    // Postfix runs AFTER the original method
    public static void Postfix(/* original args, return value, etc. */)
    {
        // run after original
    }
}
```

### 3. Apply patches in your plugin

**Important:** Your plugin must implement both `Initialize()` and `Start()`. Apply Harmony patches in `Start()`:

```csharp
using ModAPI.Core;
using HarmonyLib;

public class MyPlugin : IModPlugin
{
    private IModLogger _log;
    
    public void Initialize(IPluginContext ctx)
    {
        _log = ctx.Log;
        _log.Info("MyPlugin initializing...");
    }
    
    public void Start(IPluginContext ctx)
    {
        _log.Info("MyPlugin starting...");
        
        var harmony = new Harmony("com.yourname.myplugin");
        harmony.PatchAll(System.Reflection.Assembly.GetExecutingAssembly());
        
        _log.Info("Harmony patches applied.");
    }
}
```

## Tips
- Use unique Harmony IDs (reverse domain style) to avoid conflicts
- Use `Traverse` in Harmony for private field/property access when needed
- Keep patches focused; log with `ctx.Log.Info("...")` during development
- Harmony is powerful, but in v1.0 you don't always need it:
  - Use Harmony patches when you want to change game logic
  - Use UIUtil & context helpers to simply add UI elements
  
- Read https://harmony.pardeike.net/ for more information
