<img src="/documentation/logo.png">

Original Author: benjaminfoo
Maintainer: Coolnether123

# Legacy # | Coolnether123
This project do to lack of interest to actually create a modding scene in Sheltered. In 2019 it was left to github and asking the community to pick it up. On reddit UnicubeSheltered, a dev, said they wanted to add one but didn't build the framework. On the mod loader Github Tiller4363 attempted to reach out to benjaminfoo for help on understanding the project but from everywhere I look benjaminfoo never answered and simply left the sheltered community. In 2025 Coolnether123 (myself) picked up the project. Finding it only after getting Sheltered in Febuary. Finding a love for the game I looked for mods and found this. 


# Sheltered Mod Manager v0.6

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

## Installation (Coolnether note: Will be streamlining soon)
- Backup your game directory (zip the entire Sheltered folder)
- Clone this repository, open `ShelteredModManager.sln` in Visual Studio 2017+ or JetBrains Rider
- Build the whole solution in `Release` configuration
- After build, a `Dist/` folder appears at the project root; copy the entire contents of `Dist/` to your Sheltered installation root (e.g., `D:\Epic Games\Sheltered\`)
- Launch `SMM\\Manager.exe` from your game folder

## Compilation
- Target Framework: .NET Framework 3.5
- Architecture: Doorstop builds x64 to match the 64‑bit (Epic) game; older Steam 32‑bit assemblies are referenced where applicable
- Harmony: Lib.Harmony 2.4.1

## Mod Structure
Mods can be legacy loose DLLs or folder-based with metadata currently for v0.6:

```
Sheltered/
└─ mods/
   ├─ enabled/
   │  └─ MyCoolMod/                ← one folder per mod
   │     ├─ About/                 ← metadata & preview
   │     │  ├─ About.json          ← JSON details for the mod (renamed)
   │     │  └─ preview.png         ← for the manager UI could also have an icon png
   │     ├─ Assemblies/            ← compiled code (pick the best TFM at runtime)
   │     │  └─ MyCoolMod.dll
   │     ├─ Assets/                ← optional data the mod defines
   │     │  ├─ Textures/
   │     │  ├─ Audio/
   │     │  └─ Localization/
   │     └─ Config/                ← default cfg the mod can read
   └─ disabled/
      └─ ...
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

## Plugin Authoring

Create a class that implements `IModPlugin` and use the provided context:

```csharp
using UnityEngine;

public class MyPlugin : IModPlugin
{
    public void Initialize(IPluginContext ctx)
    {
        // optional: pre-load resources, register services, read settings
    }

    public void Start(IPluginContext ctx)
    {
        // attach behaviours under a per-plugin root
        ctx.PluginRoot.AddComponent<MyMonoBehaviour>();

        // read/write settings (merged defaults + user overrides)
        int maxCount = ctx.Settings.GetInt("maxCount", 10);
        ctx.Settings.SetInt("maxCount", maxCount);
        ctx.Settings.SaveUser();

        // simple logging, prefixed with your mod id
        ctx.Log.Info($"MyPlugin started. maxCount={maxCount}");
    }
}
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
