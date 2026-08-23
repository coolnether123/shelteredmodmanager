# Sheltered Mod Manager v2.0

**A modding framework for [Sheltered](https://store.steampowered.com/app/356040/Sheltered/) by Unicube & Team17**

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
![ModAPI Version](https://img.shields.io/badge/ModAPI-v2.0-blue)

> **Credit:** Original loader by benjaminfoo (2019); maintained by Coolnether123 since 2025.

## Project history and license

SMM continues benjaminfoo's 2019 Sheltered mod loader. Coolnether123 resumed development in 2025 with permission to maintain and redistribute the project. SMM is licensed under the [MIT License](LICENSE); third-party components retain their own licenses.

## Overview

Sheltered Mod Manager (SMM) is a modding framework for Sheltered that installs non-destructively alongside the game.

### Highlights

- Plugin loader with dependency resolution and load order management
- Unlimited custom save slots with mod tracking and verification
- Neutral `ModAPI.dll` framework APIs plus `ShelteredAPI.dll` integration for items, recipes, events, scenarios, UI hooks, saves, input, and Harmony patching
- Custom scenario browser, XML scenario packs, trigger runtime, scheduled effects, and win/loss runtime support, with advanced authoring available as an opt-in preview
- Desktop Content Workshop for data-driven items, recipes, crafting costs, icons, validation, export, and local installation without requiring a scenario
- Rebindable Sheltered and mod-defined keybindings with conflict detection and persistence
- Attributed settings holders and the Spine settings UI
- Per-mod isolated persistence and save-backed compatibility helpers
- Desktop and in-game mod managers
- Runtime inspector (F9) for debugging

### SMM 2.0

SMM 2.0 breaks compatibility with some older mods by moving Sheltered-specific APIs out of `ModAPI.dll` and into `ShelteredAPI.dll`.

- **ModAPI/ShelteredAPI split:** `ModAPI.dll` owns neutral contracts; `ShelteredAPI.dll` owns Sheltered content, saves, input, UI, events, actors, and scenarios.
- **Custom scenarios:** XML packs and code registrations appear in the in-game scenario browser, with dependency lockout, custom save binding, triggers, scheduled effects, and win/loss outcomes.
- **Content Workshop:** the desktop manager can create content-only or hybrid mod packages with custom items, recipes, costs, recycling, and icons through a shared pixel editor.
- **Release-gated safety fixes:** custom-scenario save APIs reject built-in save ids, scenario XML saves use temp/validate/replace with backups, Unity log filtering never suppresses errors/asserts/exceptions, and Nexus installs verify copied files before success.
- **Rebindable controls:** Vanilla Sheltered actions and mod-defined input actions share one keybinding UI with persisted bindings and conflict handling.
- **Mod development:** Attribute settings, Spine settings UI, the event bus, isolated persistence, Harmony helpers, and runtime diagnostics are available in the 2.0 APIs.

> [!TIP]
> Mod authors should start with the [Documentation Index](documentation/README.md), which gives the first-mod path and the canonical ModAPI/ShelteredAPI boundary rule before linking advanced guides.

SMM 2.0 is still in prerelease review. Core APIs are documented for mod testing, while the [API status](documentation/README.md#api-status) identifies preview areas that may still change.

### Release safety notes

Back up saves before upgrading major framework versions, especially when testing custom scenarios, Stasis/Surrounded expanded saves, or mods built against 1.2.2.

Family Expansion and Deep Expansion need rebuilt/tested packages before they should be listed as compatible with 2.0. Some 1.2.2 mods may need migration because Sheltered-specific APIs moved from `ModAPI.dll` to `ShelteredAPI.dll`.

## Installation

Steam/GOG users: install the 32-bit package named Steam/GOG.

Epic users: install the 64-bit package named Epic.

1. **Back up your Sheltered folder.**
2. Copy the Steam/GOG or Epic package files into the Sheltered game directory, next to `Sheltered.exe` or `ShelteredWindows64_EOS.exe`.
3. Run `SMM\Manager.exe`.
4. Enable mods and launch the game.

If your executable is `Sheltered.exe`, you are on Steam/GOG. If it is `ShelteredWindows64_EOS.exe`, you are on Epic.

### Antivirus note

SMM uses Unity Doorstop injection through `winhttp.dll` so it can load `SMM\Doorstop.dll` before Sheltered starts. Some antivirus tools may flag this DLL injection pattern even when the file is from the official SMM release. If that happens, verify the archive source, restore the quarantined `winhttp.dll`, and allowlist the Sheltered install folder for SMM.

### Installing mods

1. Download Sheltered mods from [Nexus Mods](https://www.nexusmods.com/games/sheltered).
2. Move the mod folder or zip file into the `mods` folder.
3. If the mod is zipped, unzip it so the mod's `About` and `Assemblies` folders are inside one mod folder.
4. Enable it in `SMM\Manager.exe`.

## Features

### Save protection

Each save records which mods were active when it was created.

- Warns if required mods are missing
- Warns on version mismatches
- Visual status icons per save:
  - `OK` All mods match
  - `~` Version mismatch
  - `X` Missing mods
- Save Details window shows differences
- **AUTO-LOAD MODS** activates the save's recorded mod list

![Save Verification](documentation/screenshots/mod_ingame_modverification_menu.png)
*The verification dialog compares the active and recorded mod lists, warns about differences, and lets you decide whether to continue.*

### Unlimited save slots

Removes the vanilla 3-slot limit.

- Paging UI for unlimited saves
- Works alongside vanilla saves

### In-game mod manager

A "Mods" button is added to the main menu.

- View installed mods
- See versions, authors, and dependencies

![In-Game Mod Manager](documentation/screenshots/mod_ingame_modsmenu.png)
*Access full mod details, versions, and descriptions directly from the Sheltered main menu.*

## Uninstall

1. Back up `mods`, especially `mods/ModAPI`, if you want to keep installed mods, custom saves, or framework settings.
2. Remove `winhttp.dll`, `doorstop_config.ini`, and the `SMM` folder.
3. Remove `mods` only if you also want to erase installed mods and framework-owned data.
4. Verify the game files through Steam, GOG, or Epic if Sheltered does not start normally.

Your vanilla save files are not deleted. Custom saves are stored in `mods/ModAPI/...`; back this folder up if you want to keep them.

## Vanilla launch note

If `winhttp.dll` is present, Sheltered will always start with ModAPI enabled, even when launched directly.

To start the game fully vanilla, temporarily move `winhttp.dll` out of the game directory, then move it back to re-enable mods.

## Compatibility

- **Game:** Sheltered 1.8+
- **Platforms:** Steam/GOG, Epic
- **Architecture:**
  - Steam/GOG: 32-bit
  - Epic: 64-bit
- **OS:** Windows 10 / 11
- **Unity:** 5.3 and 5.6+ supported

## Developer tools

### Runtime inspector

Press **F9** in-game.

- Scene hierarchy viewer
- Object picker
- Component and field inspection
- Bounds visualization

### Build from source

Use Visual Studio 2022 MSBuild for this legacy solution, not `dotnet build`.

Prerequisites:

- Visual Studio 2022 with .NET desktop build tools.
- .NET Framework 3.5 targeting support for the Manager, Doorstop, ModAPI, and ShelteredAPI projects.
- .NET 8 SDK for the decompiler helper project in the solution.
- A local Sheltered install that provides `Assembly-CSharp.dll`, `UnityEngine.dll`, and `UnityEngine.UI.dll`.

Current project files contain local `HintPath` fallbacks for the maintainer's Steam/Epic installs. If your Sheltered install is elsewhere, retarget those references locally before building.

Build command used for Dev/2.0 verification:

```powershell
& "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" ShelteredModManager.sln /t:Build /p:Configuration=Debug /p:Platform="Any CPU" /v:minimal
```

Before publishing, also run:

```cmd
tools\verify-modapi-boundary.cmd
tools\verify-shelteredapi-public-surface.cmd
tools\test-shelteredapi-contracts.cmd
tools\verify-runtimecompat-rect.cmd
```

## Mod structure

Mods follow a standardized folder layout:

```text
Sheltered/
`-- mods/
    `-- MyCoolMod/                <- Mod root folder
        |-- About/
        |   |-- About.json        <- Mod metadata (REQUIRED)
        |   `-- preview.png       <- Optional Manager preview image
        |-- Assemblies/           <- Optional compiled mod code
        |   `-- MyCoolMod.dll
        |-- Assets/               <- Optional assets used by mod code
        |   |-- Textures/
        |   |-- Audio/
        |   `-- Localization/
        `-- Content/              <- Optional content-only pack
            `-- content-pack.json
```

Global per-mod Spine settings are stored at `mods/ModAPI/User/<mod-id>/settings.json`. Save-scoped settings use `mods/<mod-id>/settings.json` inside the active save slot. ModAPI's own preferences use `mods/ModAPI/User/settings.json`.

### About.json format

```json
{
  "id": "YourName.MyCoolMod",
  "name": "My Cool Mod",
  "version": "1.0.0",
  "authors": ["Your Name"],
  "description": "Adds new features to Sheltered.",
  "dependsOn": ["OtherAuthor.SomeMod>=2.0.0"],
  "loadBefore": ["SomeMod"],
  "loadAfter": ["CoreAPI"],
  "tags": ["QoL", "Items"],
  "website": "https://www.nexusmods.com/games/sheltered/mods/123",
  "missingModWarning": "This save uses custom items that may be unavailable without this mod."
}
```

Required fields: `id`, `name`, `version`, `authors`, `description`.

Optional fields:

- `dependsOn` - Array of mod IDs with optional version constraints, such as `"OtherAuthor.SomeMod>=1.0.0"`
- `loadBefore` / `loadAfter` - Load order hints for compatibility
- `tags` - Categories for filtering, such as `"QoL"`, `"UI"`, `"Content"`
- `website` - Link to your mod page or documentation
- `missingModWarning` - Custom message shown when loading a save that used this mod but it is now disabled or missing

The current loader scans every concrete `IModPlugin` implementation in the mod's assemblies. It parses the legacy `entryType` field but does not use it to select a plugin.

## For mod authors

Start with the [Documentation Index](documentation/README.md) and its canonical [assembly boundary](documentation/README.md#assembly-boundary-canonical). The API is split between the neutral framework (`ModAPI.dll`) and the Sheltered integration layer (`ShelteredAPI.dll`).

Available APIs include:

- Neutral plugin lifecycle, settings, persistence, event-bus, actor-contract, and Harmony helper APIs via `ModAPI.dll`
- Item, food, recipe, scenario, save, UI, input, event, and manager-backed hooks via `ShelteredAPI.dll`
- Event subscriptions for day cycles, save/load, UI panels, combat starts, faction events, party returns, and inter-mod messages
- Custom scenario XML packs, code registrations, trigger runtime, scheduled effects, and win/loss runtime; advanced in-game authoring is an opt-in preview
- Rebindable vanilla and mod-defined keybindings
- Runtime inspector (F9)

---

## Credits

- **Coolnether123** - 2025 maintenance and development
- **benjaminfoo** - Original 2019 mod loader foundation (used with permission)
- **[Team17](https://www.team17.com/)** - For publishing Sheltered
- **Unicube** - Original game developers
- **[NeighTools](https://github.com/NeighTools)** - UnityDoorstop injection framework
- **[Andreas Pardeike](https://github.com/pardeike)** - Harmony runtime patching library

## Support and community

- **Issues:** [GitHub Issues](https://github.com/coolnether123/shelteredmodmanager/issues)
- **Sheltered Mods:** [Nexus Mods - Sheltered](https://www.nexusmods.com/games/sheltered)
- **Nexus Comments:** [Sheltered Mod Manager](https://www.nexusmods.com/sheltered/mods/1)

## Documentation

Use [Documentation Index](documentation/README.md) for the ordered first-mod, advanced, API reference, and migration paths. This table covers common destinations.

| Task | Start Here |
|------|------------|
| Make your first mod | [Start here: first mod](documentation/README.md#start-here-first-mod) |
| Understand ModAPI/ShelteredAPI split | [Canonical Assembly Boundary](documentation/README.md#assembly-boundary-canonical) |
| Choose a Sheltered-specific facade | [When to Use ShelteredAPI](documentation/ShelteredAPI_Guide.md) |
| Add items, recipes, loot, or assets | [ShelteredAPI Content Guide](documentation/ShelteredAPI_Content_Guide.md) |
| Add settings or persisted mod data | [Settings and Persistence](documentation/SETTINGS.md) |
| Subscribe to game, UI, save, or time events | [Events Guide](documentation/Events_Guide.md) |
| Add rebindable controls | [Input Keybindings Guide](documentation/Input_Keybindings_Guide.md) |
| Author custom scenarios | [Custom Scenarios Guide](documentation/Custom_Scenarios_Guide.md) |
| Patch game code with Harmony | [Harmony Patches](documentation/how%20to%20develop%20a%20patch%20with%20harmony.md) |
| Check exact API signatures | [API Signatures Reference](documentation/API_Signatures_Reference.md) |
| Review this release | [2.0 Release Notes](documentation/Release_2.0.md) |
| Upgrade from older SMM | [SMM 2.0 Migration](documentation/SMM_2.0_Migration.md) |
| Known issues | [Known Issues](documentation/Known_Issues.md) |
| Modder migration | [For Modders: 2.0 API Migration](documentation/For_Modders_2.0_API_Migration.md) |
| Nexus application review | [Nexus registration submission](documentation/Nexus_Registration_Submission.md) |
