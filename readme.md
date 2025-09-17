<img src="/documentation/logo.png">

Original Author: benjaminfoo
Maintainer: Coolnether123

# Legacy
This project is considered legacy because the original Sheltered mod-loader effort from 2019 was left unmaintained and never grew into an active modding framework. At the time, a Unicube developer (on Reddit as UnicubeSheltered) expressed interest in mod support, but no official framework was shipped. On the original mod-loader GitHub repo, Tiller4363 attempted in 2023 to contact benjaminfoo for guidance, but from what I can find, there was no reply and benjaminfoo deleted his reddit account.

In 2025, I (Coolnether123) discovered Sheltered and went looking for mods. The only thing I found was the abandoned mod loader, so I decided to pick it up and continue development to enable modding for the game.


# Sheltered Mod Manager v0.7

This project enables modding support for the game [Sheltered](https://store.steampowered.com/app/356040/Sheltered/) by Team17.\
It acts as a drop-in application for a regular installation of Sheltered — no original game files are modified.

## Architecture
**Doorstop**\
Bootstraps the Mod API inside the game using Unity Doorstop and starts the plugin system.

**ModAPI**\
Defines the plugin interfaces and loader. Plugins implement `IModPlugin` and receive an `IPluginContext` with:
- `PluginRoot` GameObject for attaching behaviours
- `Mod` (About.json metadata and paths)
- `Settings` (typed access to Config/default.json + Config/user.json)
- `Log` (mod-prefixed logger)
The loader currently resolves dependencies (`dependsOn`), ordering (`loadBefore`/`loadAfter`), and honors `loadorder.json`. This will be moved to more user facing with a sort button.

# Example Plugins

**ManagerGUI**\
Windows UI to locate the game, manage mod enable/disable, and set load order.

### Plugins
**PluginInitializer**\
Displays an early on-screen label so you can confirm the Mod API booted before the console is available.

**PluginConsole**\
In-game console for interacting with the loader and game objects (help/clear/sceneinfo, WIP).

**PluginHarmony**\
Includes Harmony (https://github.com/pardeike/Harmony) and demo patches to verify runtime patching.

**PluginDebugGUI**\
Simple UI exposing loader state and loaded plugins while in-game.

## Installation
- Backup your game directory (zip the entire Sheltered folder)
- Download [Release v0.7](TODO CREATE)  
- Copy its content in the same space as the games exe
- Launch `SMM\\Manager.exe` from your game folder
- If sheltered exe is not found use the browse for sheltered exe button and navigate to the exe

## Compilation
- Target Framework: .NET Framework 3.5
- Architecture: Doorstop 4.4.1 64‑bit (Epic) / 32-bit (Steam)
- Harmony: Lib.Harmony 2.4.1

## Mod Structure
Mods can be legacy loose DLLs or folder-based with metadata currently for v0.7:

```
Sheltered/
└─ mods/
    └─ MyCoolMod/                ← one folder per mod
         ├─ About/                 ← metadata & preview
         │  ├─ About.json          ← JSON details for the mod (renamed)
         │  └─ preview.png         ← for the manager UI could also have an icon png
         ├─ Assemblies/            ← compiled code (pick the best TFM at runtime)
         │  └─ MyCoolMod.dll
         ├─ Assets/                ← optional data the mod defines
         │  ├─ Textures/
         │  ├─ Audio/
         │  └─ Localization/
         └─ Config/                ← default cfg the mod can read 
```

Example About.json
------------------
{
  "id": "com.devname.mycoolmod",        // Required
  "name": "My Cool Mod",                 // Required
  "version": "1.2.3",                    // Required
  "authors": ["You"],                     // Required
  "entryType": "MyCoolMod.Entry",         // Optional
  "dependsOn": ["com.other.mod>=2.0.0"],  // Optional
  "loadBefore": ["com.some.mod"],         // Optional
  "loadAfter": ["com.core.api"],          // Optional
  "description": "Adds X to Sheltered.",  // Required
  "tags": ["QoL","UI"],                  // Optional
  "website": "https://example.com"        // Optional
}

## Developing Plugins

To create a mod, you'll write a plugin by creating a class that implements the `IModPlugin` interface. The loader will automatically discover and run your plugin if it's structured correctly and enabled in the manager.

Here is an example of a simple plugin that adds a label to the main menu:

```csharp
using HarmonyLib;
using UnityEngine;

/// <summary>
/// Example plugin that adds a simple cyan label to the Main Menu
/// 
/// This shows the full lifecycle of a ModAPI plugin:
///  - Initialize() is called before the game starts (read settings here)
///  - Start() is called once Unity is running (safe to patch here)
///  - Harmony is used to patch into game methods (here: MainMenu.OnShow)
///  - UIUtil helper is used to safely create an NGUI UILabel
/// 
/// Why is Harmony used at all?
///   - Sheltered doesn't add menu panels until runtime
///   - The menu GameObjects exist but are inactive during scene load
///   - Harmony lets us “hook” the panel’s OnShow() so we run code right when
///     the game activates it, ensuring the label is visible.
/// 
/// </summary>
public class MyMenuPlugin : IModPlugin
{
    private IModLogger _log;

    /// <summary>
    /// Called very early, before Unity scene is running.
    /// Use this to read settings/config values.
    /// </summary>
    public void Initialize(IPluginContext ctx)
    {
        _log = ctx.Log;
        _log.Info("MyMenuPlugin Initializing...");
    }

    /// <summary>
    /// Called once Unity is ready and safe to run coroutines/patches.
    /// Creates a Harmony instance and patch all targets in this assembly.
    /// </summary>
    public void Start(IPluginContext ctx)
    {
        _log.Info("MyMenuPlugin Starting...");
        var h = new Harmony("com.plugin.mymenu"); // unique ID for this patch set
        h.PatchAll(typeof(MyMenuPlugin).Assembly);
        _log.Info("Harmony patches for MyMenuPlugin applied.");
    }
}

/// <summary>
/// Patch handler for when the MainMenu panel is shown.
/// Note: Sheltered has two menu variants: MainMenu and MainMenuX.
/// To be safe, you can patch both. Just MainMenu works here though.
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
        // Always null-check the instance; Harmony will pass null if the patch misfires
        if (__instance == null || __instance.gameObject == null) return;

        // Our own marker component prevents us from adding the label twice
        if (__instance.GetComponent<MyMenuLabelMarker>() != null) return;

        __instance.gameObject.AddComponent<MyMenuLabelMarker>();

        // Build label options:
        // - Cyan text
        // - Anchored top-right with a 10px inset
        // - Slight outline effect so it stands out
        // - Relative depth +50 to ensure it renders above most default widgets
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

        // Actually create the label via the ModAPI helper.
        // Use UIUtil.CreateLabel instead of new UILabel manually to
        //   - Ensures label is under a UIPanel
        //   - Picks a working font automatically (bitmap or TTF fallback)
        //   - Computes safe depth above siblings
        //   - Honors UIRoot activeHeight → resolution-scaled placement
        UIUtil.CreateLabel(__instance.gameObject, opts, out used);
    }
}

/// <summary>
/// Simple empty marker component to ensure it doesn't inject multiple times.
/// Many BasePanel classes in Sheltered can call OnShow() more than once per session. 
/// </summary>
public class MyMenuLabelMarker : MonoBehaviour { }
```

Dependencies and order are declared in `About.json` via `dependsOn`, `loadBefore`, and `loadAfter`. Version constraints are supported (e.g., `"com.example.mod >= 1.2.0"`). To use another mod’s public API, reference its DLL and declare a matching `dependsOn` entry. 

## Screenshots
<img src="/documentation/manager_gui.png">

<img src="/documentation/ingame.png">

<img src="/documentation/ingame_2.png">

## Credits
- [Team 17 for Sheltered](https://store.steampowered.com/app/356040/Sheltered/)
- [NeighTools for UnityDoorstop](https://github.com/NeighTools/UnityDoorstop)
- [Pardeike for Harmony](https://github.com/pardeike/Harmony)

### Testing
Validated on:
- Windows 10, 64‑bit
- Doorstop v4 config (shipped), x64
- Sheltered 1.8 (Epic, 64‑bit)
- Lib.Harmony 2.4.1

## License
MIT — see `LICENSE`.
