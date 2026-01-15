# Sheltered Mod Manager v1.0

<img src="/documentation/logo.png" width="600">

**A comprehensive modding framework for [Sheltered](https://store.steampowered.com/app/356040/Sheltered/) by Team17**

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![ModAPI Version](https://img.shields.io/badge/ModAPI-v1.0.0-blue)]()

> **Credit:** Originally created by benjaminfoo (2019)  
> **Maintained by:** Coolnether123 (2025-Present)

## License & attribution

This project is licensed under the MIT License (see LICENSE).

The original 2019 Sheltered mod loader foundation was created by benjaminfoo. Continued development and public redistribution are performed with the original author’s permission.

Third-party components retain their own licenses (see the Credits section).

## Legacy

This project is considered legacy because the original Sheltered mod-loader effort from 2019 was left unmaintained and never grew into an active modding framework. At the time, a Unicube developer (on Reddit as UnicubeSheltered) expressed interest in mod support, but no official framework was shipped. On the original mod-loader GitHub repo, Tiller4363 attempted in 2023 to contact benjaminfoo for guidance, but from what I can find, there was no reply and benjaminfoo deleted his reddit account.

In 2025, I (Coolnether123) discovered Sheltered and went looking for mods. The only thing I found was the abandoned mod loader, so I decided to pick it up and continue development to enable modding for the game. 

## About

Sheltered Mod Manager (SMM) adds files alongside the game. It does not overwrite core game assets. It does add a loader DLL (winhttp.dll) and config files to hook at startup. It includes a plugin API, unlimited save slots with mod tracking, and tools for players and mod creators.

The goal of SMM is to provide a stable, extensible foundation for Sheltered mods, for both players and mod authors. The game is harder to mod in, the ModAPI system is being built to allow developers access to more of Sheltered. Custom save systems are built in with helpers to help devs saving their data in saves.

## Core features

* A native plugin API for mods
* Unlimited save slots with safety checks
* Mod tracking per save file
* A desktop mod manager
* An in-game mod manager
* Developer tools for creating and debugging mods


## Installation

Steam users: install the 32-bit package named Steam.

Epic users: install the 64-bit package named Epic.

1. **Back up your Sheltered folder**
2.  Copy the files into the Sheltered game directory
   (same folder as `Sheltered.exe` or `ShelteredWindows64_EOS.exe`)
3. Run `SMM\Manager.exe`
4. Enable mods and launch the game

If your exe is Sheltered.exe, you’re on Steam. If it’s ShelteredWindows64_EOS.exe, you’re on Epic

### Installing mods
1. Download mods from *Nexus(Link)*
2. Move file into mods folder
3. If the mod is zipped unzip the folder
4. Enable in Manager.exe

# Features
### Save protection

Each save records which mods were active when it was created.

* Warns if required mods are missing
* Warns on version mismatches
* Visual status icons per save:
  * ✓ All mods match
  * ~ Version mismatch
  * ✗ Missing mods
* Save Details window shows differences
* One-click “Reload with Save Mods” option

### Unlimited save slots

Removes the vanilla 3-slot limit.

* Paging UI for unlimited saves
* Works alongside vanilla saves
### In-game mod manager

A “Mods” button is added to the main menu.

* View installed mods
* See versions, authors, and dependencies

# Support

## Uninstall

1. Disable all mods in the manager
2. Delete the `mods` and `SMM` folders
3. Remove doorstop_config.ini, mod_manager.log, and winhttp.dll
4. Verify game files via Steam/Epic if needed

Your vanilla save files are not deleted.
Custom saves are stored in mods/ModAPI/... (back this folder up if you want to keep them).

## Vanilla launch note

If winhttp.dll is present, Sheltered will always start with ModAPI enabled, even when launched directly.

To start the game fully vanilla, temporarily move winhttp.dll out of the game directory, then move it back to re-enable mods.

## Compatibility

* **Game:** Sheltered 1.8+
* **Platforms:** Steam, Epic
* **Architecture:**
  * Steam: 32-bit
  * Epic: 64-bit
* **OS:** Windows 10 / 11
* **Unity:** 5.3 and 5.6+ supported

## Developer tools

### Runtime inspector

Press **F9** in-game.

* Scene hierarchy viewer
* Object picker
* Component and field inspection
* Bounds visualization

## Mod Structure

Mods follow a standardized folder layout:

```
Sheltered/
└─ mods/
    └─ MyCoolMod/                ← Mod root folder
         ├─ About/                 
         │  ├─ About.json         ← Mod metadata (REQUIRED)
         │  ├─ preview.png        ← Preview image for Manager
         │  └─ icon.png           ← Optional icon
         ├─ Assemblies/           ← Compiled mod code
         │  └─ MyCoolMod.dll
         ├─ Assets/               ← Custom content
         │  ├─ Textures/
         │  ├─ Audio/
         │  └─ Localization/
         └─ Config/               ← Configuration files
              ├─ default.json     ← Default settings
              └─ user.json        ← User overrides
```

### About.json Format

```json
{
  "id": "com.yourname.mycoolmod",
  "name": "My Cool Mod",
  "version": "1.0.0",
  "authors": ["Your Name"],
  "description": "Adds cool features to Sheltered!",
  "entryType": "MyCoolMod.MyPlugin",
  "dependsOn": ["com.other.mod>=2.0.0"],
  "loadBefore": ["com.some.mod"],
  "loadAfter": ["com.core.api"],
  "tags": ["QoL", "Items"],
  "website": "https://nexusmods.com/sheltered/mods/123",
  "missingModWarning": "This save uses custom items that will be lost!"
}
```

**Required Fields:** `id`, `name`, `version`, `authors`, `description`

**Optional Fields:**
- `entryType` - Fully qualified class name implementing `IModPlugin`
- `dependsOn` - Array of mod IDs with optional version constraints (e.g., `">=1.0.0"`)
- `loadBefore` / `loadAfter` - Load order hints for compatibility
- `tags` - Categories for filtering (e.g., `"QoL"`, `"UI"`, `"Content"`)
- `website` - Link to your mod page or documentation
- `missingModWarning` - Custom message shown when loading a save that used this mod but it's now disabled/missing. Use this to warn players about potential data loss or gameplay issues.


## For mod authors

The ModAPI uses a context first approach. Currently all documentation on the API and how to mod Sheltered is in the documentation. Future updates will move this to be more standard.

Current APIs provide:
1. Creating custom items and food
2. Hooking into common GameEvent triggers
3. UI Utilities and Helpers
4. Creating icons
5. Tools for Debugging NGUI

This framework is intended to be the base for all future Sheltered mods. 

---

## Credits

- **[Team17](https://www.team17.com/)** - For creating Sheltered
- **benjaminfoo** — Original 2019 mod loader foundation (used with permission)
- **[NeighTools](https://github.com/NeighTools)** - UnityDoorstop injection framework
- **[Andreas Pardeike](https://github.com/pardeike)** - Harmony runtime patching library
- **Coolnether123** - 2025 Active Maintainer

## Support & Community

- **Issues:** [GitHub Issues](https://github.com/coolnether123/shelteredmodmanager/issues)
- **Nexus Comments:** [Sheltered Mod Manager](https://nexusmods.com/sheltered/mods/1)
- **Documentation:** [API_DOCUMENTATION.md](API_DOCUMENTATION.md)
