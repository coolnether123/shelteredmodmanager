# How to Develop a Plugin | Sheltered Mod Manager v1.0.1

## Prerequisites
- Visual Studio 2017+ or JetBrains Rider
- .NET Framework 3.5 SDK
- Sheltered Mod Manager installed (provides ModAPI.dll and 0Harmony.dll)

## Steps

### 1. Create a new C# Class Library project targeting .NET Framework 3.5

### 2. Add references to:
- `ModAPI.dll` from your Sheltered installation's `SMM` folder
- `0Harmony.dll` from `SMM/bin/` (if using Harmony patches)
- `Assembly-CSharp.dll` from `<Sheltered>/Sheltered_Data/Managed` (Steam) or `Windows64_EOS_Data/Managed` (Epic)
- `UnityEngine.dll` from the same folder (if needed)

### 3. Write your plugin code

**Important:** Your plugin class must implement `IModPlugin` which requires **both** `Initialize()` and `Start()` methods.

```csharp
using ModAPI.Core;
using UnityEngine;

/// <summary>
/// Minimal plugin example showing the required lifecycle methods.
/// 
/// IModPlugin requires:
///  - Initialize(ctx) - Called first, before Unity is fully ready. Use for reading settings.
///  - Start(ctx)      - Called after Initialize when Unity context is ready. Safe to use Harmony here.
/// </summary>
public class MyPlugin : IModPlugin
{
    private IModLogger _log;

    /// <summary>
    /// Called very early, before Unity scene is running.
    /// Use this to read settings/config values and store references.
    /// </summary>
    public void Initialize(IPluginContext ctx)
    {
        _log = ctx.Log;
        _log.Info("MyPlugin initializing...");
        
        // Read settings if you have a Config/default.json
        // var maxCount = ctx.Settings.GetInt("maxCount", 5);
    }

    /// <summary>
    /// Called once Unity is ready and safe to run coroutines/patches.
    /// Use this for Harmony patches and subscribing to events.
    /// </summary>
    public void Start(IPluginContext ctx)
    {
        _log.Info("MyPlugin starting...");
        
        // Your mod logic here
    }
}
```

### 4. Example: Adding UI to the Main Menu with Harmony

```csharp
using ModAPI.Core;
using HarmonyLib;
using UnityEngine;

/// <summary>
/// Example plugin that adds a cyan label to the Main Menu.
/// 
/// Why use Harmony here?
///   - Sheltered doesn't add menu panels until runtime
///   - The menu GameObjects exist but are inactive during scene load
///   - Harmony lets us "hook" the panel's OnShow() so we run code right when
///     the game activates it, ensuring the label is visible.
/// </summary>
public class MyMenuPlugin : IModPlugin
{
    private IModLogger _log;

    public void Initialize(IPluginContext ctx)
    {
        _log = ctx.Log;
        _log.Info("MyMenuPlugin initializing...");
    }

    public void Start(IPluginContext ctx)
    {
        _log.Info("MyMenuPlugin starting...");
        var h = new Harmony("com.plugin.mymenu"); // unique ID for this patch set
        h.PatchAll(typeof(MyMenuPlugin).Assembly);
        _log.Info("Harmony patches applied.");
    }
}

/// <summary>
/// Patch handler for when the MainMenu panel is shown.
/// Note: Sheltered has two menu variants: MainMenu and MainMenuX.
/// </summary>
[HarmonyPatch(typeof(MainMenu), "OnShow")]
public static class MainMenu_OnShow_Patch
{
    /// <summary>
    /// Postfix is called after the real OnShow() runs.
    /// At this point the panel is active and safe to add children to.
    /// </summary>
    static void Postfix(MainMenu __instance)
    {
        if (__instance == null || __instance.gameObject == null) return;

        // Marker component prevents adding the label twice
        if (__instance.GetComponent<MyMenuLabelMarker>() != null) return;
        __instance.gameObject.AddComponent<MyMenuLabelMarker>();

        // Create a label using UIUtil helper
        UIPanel used;
        var opts = new UIUtil.UILabelOptions
        {
            text = "MyMenuPlugin is active!",
            color = Color.cyan,
            fontSize = 22,
            alignment = NGUIText.Alignment.Right,
            effect = UILabel.Effect.Outline,
            effectColor = new Color(0, 0, 0, 0.85f),
            anchor = UIUtil.AnchorCorner.TopRight,
            pixelOffset = new Vector2(-10, -10),
            relativeDepth = 50
        };

        UIUtil.CreateLabel(__instance.gameObject, opts, out used);
    }
}

/// <summary>
/// Empty marker component to prevent injecting multiple times.
/// </summary>
public class MyMenuLabelMarker : MonoBehaviour { }
```

### 5. Set up the mod folder structure

Place your mod in `Sheltered/mods/MyPlugin/`:

```
Sheltered/
└─ mods/
    └─ MyPlugin/                  ← Mod root folder
         ├─ About/                 
         │  ├─ About.json         ← Mod metadata (REQUIRED)
         │  ├─ preview.png        ← Preview image for Manager
         │  └─ icon.png           ← Optional icon
         ├─ Assemblies/           ← Compiled mod code
         │  └─ MyPlugin.dll
         └─ Config/               ← Configuration files (optional)
              └─ default.json     ← Default settings
```

### 6. Create About.json (required)

```json
{
  "id": "YourName.MyPlugin",
  "name": "My Plugin",
  "version": "1.0.0",
  "authors": ["Your Name"],
  "description": "What this mod does",
  "entryType": "MyPlugin"
}
```

**Required Fields:** `id`, `name`, `version`, `authors`, `description`

**Optional Fields:**
- `entryType` - Class name implementing `IModPlugin` (if different from auto-detection)
- `dependsOn` - Array of mod IDs with optional version constraints
- `loadBefore` / `loadAfter` - Load order hints
- `missingModWarning` - Custom message shown when loading a save that used this mod

### 7. Enable your mod in the Manager GUI

Run `SMM/Manager.exe`, enable your mod, and launch the game.
![Mod Manager](screenshots/mod_manager_gui_mods.png)

---

## Optional Interfaces

Your plugin can implement additional interfaces for more functionality:

```csharp
// Per-frame updates
public interface IModUpdate
{
    void Update();  // Called every frame
}

// Cleanup on shutdown
public interface IModShutdown
{
    void Shutdown();  // Called when loader tears down
}

// Scene change notifications
public interface IModSceneEvents
{
    void OnSceneLoaded(string sceneName);
    void OnSceneUnloaded(string sceneName);
}

// Session Lifecycle (v1.0.1)
public interface IModSessionEvents
{
    void OnSessionStarted(); // Called when session starts (Load/New Game)
    void OnNewGame();        // Called specifically for New Games
}
```

Example:
```csharp
public class MyPlugin : IModPlugin, IModShutdown
{
    public void Initialize(IPluginContext ctx) { /* ... */ }
    public void Start(IPluginContext ctx) { /* ... */ }
    
    public void Shutdown()
    {
        // Clean up resources, unsubscribe events, etc.
    }
}
```

---

## Context API Reference

The `IPluginContext` gives you access to:

| Property/Method | Description |
|-----------------|-------------|
| `Log` | Logger that prefixes with your mod ID |
| `Settings` | Read/write mod settings |
| `PluginRoot` | Per-plugin GameObject for your components |
| `LoaderRoot` | Global loader GameObject |
| `Mod` | Your mod's metadata (About.json) |
| `Game` | Unified game state helper (v1.0.1) |
| `GameRoot` | Path to Sheltered install directory |
| `ModsRoot` | Path to mods folder |
| `IsModernUnity` | True if running Unity 5.4+ (Epic version) |
| `RunNextFrame(action)` | Queue an action for next frame |
| `StartCoroutine(routine)` | Start a coroutine |
| `FindPanel(name)` | Find a UI panel by name or path |
| `AddComponentToPanel<T>(name)` | Add component to a UI panel |

---

## Tips

- Avoid blocking the main thread during `Start`; use `ctx.RunNextFrame()` or `ctx.StartCoroutine()` for heavy work
- Use `ctx.Log.Info("...")` for logging (automatically prefixed with your mod ID)
- Store your `IModLogger` reference in `Initialize` for use throughout your plugin
- Use the Runtime Inspector (F9) to explore the game's UI hierarchy
