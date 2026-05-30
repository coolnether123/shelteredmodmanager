# Sheltered Mod Manager Documentation

This index is the mod-author entry point for SMM v2.0. Start with the shortest path below, then open a task or reference guide only when your mod needs it.

> **Upgrade safety:** Back up saves before testing mods or framework upgrades. Custom scenarios and expanded vanilla save modes should first be smoke tested with disposable saves.

## Start Here / First Mod

For a first mod:

1. Create a C# class library targeting **.NET Framework 3.5**.
2. Reference `ModAPI.dll`, implement `IModPlugin`, and package a required `About/About.json` plus your DLL under `Assemblies/`.
3. Build and load the minimal plugin before adding Sheltered-specific features.
4. Use the boundary table below to decide whether to add `ShelteredAPI.dll` or `Assembly-CSharp.dll`.

| Need | Read This |
|------|-----------|
| Build and package a first plugin | [How to Develop a Plugin](how%20to%20develop%20a%20plugin.md) |
| Understand the minimal lifecycle and context | [Core ModAPI Basics](ModAPI_Developer_Guide.md) |
| Build SMM itself from source | [Root README build section](../readme.md#building-from-source) |

SMM itself is a legacy solution: build it with Visual Studio 2022 MSBuild, not `dotnet build`, and install .NET Framework 3.5 targeting support. A mod project targeting the framework should also target .NET Framework 3.5 unless its deployment strategy deliberately provides otherwise.

## Core ModAPI Basics

Use `ModAPI.dll` for the host-neutral framework:

- plugin lifecycle, logging, and loader context
- settings and ordinary per-mod persistence through `ctx.SaveSystem`
- inter-mod events and registry services
- neutral input-action, actor-contract, Harmony, diagnostics, and background-work surfaces that exist in the current build

| Task | Guide |
|------|-------|
| Lifecycle, context, and choosing the next guide | [ModAPI Developer Guide](ModAPI_Developer_Guide.md) |
| Settings, per-mod persisted state, and save lifecycle hooks | [Settings and Persistence](SETTINGS.md) |
| Harmony patches | [Harmony Patch Guide](how%20to%20develop%20a%20patch%20with%20harmony.md) |
| Transpiler debugging and safety | [Transpiler and Debugging](Transpiler_and_Debugging_Guide.md) |

## When To Use ShelteredAPI

### Assembly Boundary (Canonical)

This is the canonical mod-author rule for the SMM 2.0 assembly split. Other guides link here rather than restating it.

| Your mod does this | Compile references |
|--------------------|--------------------|
| Uses only lifecycle, settings, `ctx.SaveSystem`, inter-mod APIs, neutral input actions, neutral actor contracts, or neutral Harmony helpers | `ModAPI.dll` |
| Registers Sheltered items/recipes/assets, accesses Sheltered save slots, listens for Sheltered game/UI events, adds Sheltered input/UI behavior, works with Sheltered characters, or registers custom scenarios | `ModAPI.dll` and `ShelteredAPI.dll` |
| Directly names vanilla game types such as `FamilyMember`, `ItemManager.ItemType`, `ObjectManager.ObjectType`, `ScenarioDef`, or a Harmony patch target in game code | `ModAPI.dll`, `ShelteredAPI.dll` when using its facades, and `Assembly-CSharp.dll` |
| Applies Harmony patches without naming Sheltered game types | `ModAPI.dll` and `0Harmony.dll` |

Rules:

- `ModAPI.dll` owns neutral contracts. `ShelteredAPI.dll` owns Sheltered integrations and supplies game-specific runtime implementations behind neutral contracts.
- Prefer public facades such as `ShelteredContent`, `ShelteredSaves`, `ShelteredEvents`, `ShelteredInput`, `ShelteredRuntimeUI`, `ShelteredActors`, `ShelteredCharacters`, `ShelteredScenarios`, `ShelteredMap`, and `ShelteredMapMarkers`.
- Treat implementation services, serializers, patch hosts, repositories, and controllers as internal even if their source is visible.
- A typed Sheltered escape hatch is appropriate only when a facade explicitly exposes a vanilla type for deliberate game-object integration, for example `FindFamilyMember(...)`. Do not make raw vanilla types the default data model for ordinary mod logic.

For the internal ownership history and verifier policy, see [ModAPI/ShelteredAPI Boundary Refactor](ModAPI_Sheltered_Boundary_Refactor.md). For facade selection examples, see [ShelteredAPI Guide](ShelteredAPI_Guide.md).

## Common Tasks

| I want to... | Use... | Guide |
|--------------|--------|-------|
| Register items, recipes, loot, assets, or localization | `ShelteredContent` | [Content Guide](ShelteredAPI_Content_Guide.md) |
| Store ordinary mod state with a save | `ctx.SaveSystem` | [Settings and Persistence](SETTINGS.md) |
| Inspect or operate on Sheltered save slots | `ShelteredSaves`, `ShelteredSaveEvents` | [ShelteredAPI Guide](ShelteredAPI_Guide.md#facade-chooser) |
| Listen for game, UI, faction, or scheduled time events | `ShelteredEvents` | [Events Guide](Events_Guide.md) |
| Add configurable keybindings | `InputActionRegistry`, optionally `ShelteredInput` | [Input Keybindings Guide](Input_Keybindings_Guide.md) |
| Work with actors or actual Sheltered characters | `ctx.Actors`, `ShelteredActors`, `ShelteredCharacters` | [Actors Guide](ShelteredAPI_Characters_Guide.md) |
| Query expedition map context or project map markers | `ShelteredMap`, `ShelteredMapMarkers` | [ShelteredAPI Guide](ShelteredAPI_Guide.md), [map context signatures](API_Signatures_Reference.md#expedition-map-context-smm-20), and [marker signatures](API_Signatures_Reference.md#map-markers-smm-20) |

## Advanced Systems

These surfaces are useful after a first plugin is working. Preview or experimental status matters when publishing a mod.

| System | Status | Guide |
|--------|--------|-------|
| Deterministic feature streams | Current neutral API | [`ModRandom` signatures](API_Signatures_Reference.md#modrandom-deterministic-streams-modapicore) |
| Background work, cancellation, stale-result handling, and diagnostics | Current neutral API | [`ModThreads` signatures](API_Signatures_Reference.md#background-work-smm-20) |
| Mod-owned panels, storage, item reservations, character item assignments, and cooking stations | API preview | [Runtime UI, Stores, and Cooking Stations](ShelteredAPI_Runtime_UI_Stores_Guide.md) |
| Focused UI clone/bind/color/lifecycle helpers | API preview | [`ShelteredUI` signatures](API_Signatures_Reference.md#ui-extensions-smm-20) |
| Expedition map context, generation intent, markers, and actor projections | API preview | [`ShelteredMap` signatures](API_Signatures_Reference.md#expedition-map-context-smm-20) and [`ShelteredMapMarkers` signatures](API_Signatures_Reference.md#map-markers-smm-20) |
| Player queue snapshots, conservative restore, and change notification | API preview | [`ShelteredQueues` signatures](API_Signatures_Reference.md#player-queues-smm-20) |
| Custom scenario XML/code registration, authoring, runtime triggers, and scoring snapshots | Supported 2.0 surface | [Custom Scenarios Guide](Custom_Scenarios_Guide.md) |
| Spine settings UI | Supported | [Spine Settings Guide](Spine_Settings_Guide.md) |
| Patch metadata, conflict reports, and cooperative patching | Current neutral API | [Patch Governance](Patch_Governance.md) |
| Save manifests and structured support-bundle capture | API preview / support tooling | [`ShelteredSupportBundle` signatures](API_Signatures_Reference.md#save-manifest--support-bundle-smm-20) |
| Loader and runtime internals | Maintainer/advanced | [Architecture Guide](ModAPI_Architecture_guide.md) and [Project Map](ModAPI_Documentation.md) |

Services should be treated as public authoring surfaces only when they appear in a guide and the signature reference.

## API Reference

| Need | Document |
|------|----------|
| Exact public type and method signatures | [API Signatures Reference](API_Signatures_Reference.md) |
| Sheltered facade selection | [ShelteredAPI Guide](ShelteredAPI_Guide.md) |
| Module ownership and runtime design | [ModAPI Project Map](ModAPI_Documentation.md) |
| Internal boundary/refactor record | [ModAPI/ShelteredAPI Boundary Refactor](ModAPI_Sheltered_Boundary_Refactor.md) |

The signature reference is a lookup sheet, not a tutorial. Begin with a task guide, then use it when you need exact names and overloads.

## Migration / Troubleshooting

| Situation | Document |
|-----------|----------|
| Player upgrading to 2.0 | [SMM 2.0 Migration](SMM_2.0_Migration.md) |
| Mod author rebuilding a 1.x mod | [For Modders: 2.0 API Migration](For_Modders_2.0_API_Migration.md) |
| Runtime/log failure investigation | [API Troubleshooting](API_Troubleshooting.md) |
| Known release issues and reporting data | [Known Issues](Known_Issues.md) |
| 2.0 release scope/checklist | [2.0 Release Notes](Release_2.0.md) |
