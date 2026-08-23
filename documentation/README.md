# Sheltered Mod Manager documentation

This index separates mod-author tasks from maintainer and release work. Start with the shortest guide that matches your task.

Back up saves before testing a framework upgrade, a custom scenario, or a mod that changes save behavior.

## Start here: first mod

1. Create a C# class library that targets .NET Framework 3.5.
2. Reference `ModAPI.dll` and implement `IModPlugin`.
3. Package `About/About.json` and your DLL under `Assemblies/`.
4. Load the minimal plugin before you add Sheltered-specific code.

Use these guides in order:

1. [Develop a plugin](how%20to%20develop%20a%20plugin.md)
2. [Core ModAPI basics](ModAPI_Developer_Guide.md)
3. [API reference](API_Signatures_Reference.md) when you need a namespace or exact symbol

Build SMM with Visual Studio 2022 MSBuild and the .NET Framework 3.5 targeting pack. Do not use `dotnet build`. See the [root build instructions](../readme.md#build-from-source).

## Assembly boundary: canonical

| Your mod uses | Compile references |
| --- | --- |
| Lifecycle, settings, `ctx.SaveSystem`, inter-mod APIs, neutral input actions, neutral actors, or neutral Harmony helpers | `ModAPI.dll` |
| Sheltered content, saves, events, input, UI, characters, maps, queues, or scenarios | `ModAPI.dll` and `ShelteredAPI.dll` |
| Vanilla types such as `FamilyMember`, `ItemManager.ItemType`, `ObjectManager.ObjectType`, or `ScenarioDef` | The API assemblies used by the mod and `Assembly-CSharp.dll` |
| Harmony without Sheltered types | `ModAPI.dll` and `0Harmony.dll` |

`ModAPI.dll` owns game-neutral contracts. `ShelteredAPI.dll` owns Sheltered integrations. `ShelteredScenarioEditor.dll` is an optional editor application; mods do not reference it.

Prefer public facades such as `ShelteredContent`, `ShelteredSaves`, `ShelteredEvents`, `ShelteredInput`, `ShelteredRuntimeUI`, `ShelteredActors`, `ShelteredCharacters`, `ShelteredScenarios`, `ShelteredMap`, and `ShelteredMapMarkers`. Treat serializers, repositories, patch hosts, controllers, and other implementation services as internal.

Use a vanilla type only when a documented facade exposes it for direct game-object integration. Do not make vanilla runtime objects the default data model for a mod.

## Mod-author tasks

| Task | Guide |
| --- | --- |
| Add settings or persisted mod data | [Settings and persistence](SETTINGS.md) |
| Register items, recipes, loot, assets, or localization | [Content guide](ShelteredAPI_Content_Guide.md) |
| Build a data-only content pack | [Content Workshop](Content_Workshop_Guide.md) |
| Subscribe to game, UI, save, or time events | [Events guide](Events_Guide.md) |
| Add rebindable controls | [Input keybindings](Input_Keybindings_Guide.md) |
| Work with actors or Sheltered characters | [Actors guide](ShelteredAPI_Characters_Guide.md) |
| Add mod-owned panels, stores, or cooking stations | [Runtime UI and stores](ShelteredAPI_Runtime_UI_Stores_Guide.md) |
| Create custom scenarios | [Custom scenarios](Custom_Scenarios_Guide.md) |
| Patch game code | [Harmony patch guide](how%20to%20develop%20a%20patch%20with%20harmony.md) |
| Debug a transpiler | [Transpiler and debugging guide](Transpiler_and_Debugging_Guide.md) |
| Choose a Sheltered facade | [ShelteredAPI guide](ShelteredAPI_Guide.md) |
| Diagnose a runtime failure | [API troubleshooting](API_Troubleshooting.md) |

The [API reference](API_Signatures_Reference.md) is an index for lookup. The task guides contain the examples and constraints needed to use each API.

## API status

The 2.0 assembly split is the supported line. Runtime UI, stores, cooking stations, map helpers, queues, and support-bundle APIs remain in preview. Custom scenario registration, XML and code authoring, playback, runtime bindings, and scoring snapshots are supported. The separate in-game scenario editor remains optional and defaults off.

## Migration and release information

| Situation | Document |
| --- | --- |
| A player is upgrading to 2.0 | [SMM 2.0 migration](SMM_2.0_Migration.md) |
| A mod author is rebuilding a 1.x mod | [Modder 2.0 API migration](For_Modders_2.0_API_Migration.md) |
| A player needs current limitations | [Known issues](Known_Issues.md) |
| A maintainer is preparing the 2.0 release | [2.0 release notes](Release_2.0.md) |

## Maintainer documents

| Work | Document |
| --- | --- |
| Find the owner of a subsystem | [Architecture ownership](Architecture_Ownership_Guide.md) |
| Follow loader and runtime startup | [ModAPI architecture](ModAPI_Architecture_guide.md) |
| Find source by module | [Project map](ModAPI_Documentation.md) |
| Enforce the ModAPI and ShelteredAPI split | [Assembly boundary record](ModAPI_Sheltered_Boundary_Refactor.md) |
| Extend scenario authoring | [Scenario authoring architecture](Scenario_Authoring_Architecture.md) |
| Review patch ownership and conflicts | [Patch governance](Patch_Governance.md) |
| Maintain comment style | [Developer commenting standard](Developer_Commenting_Standard.md) |
| Review OAuth security and behavior | [Nexus OAuth implementation](Nexus_OAuth_Implementation.md) |
| Prepare Nexus application review | [Nexus registration submission](Nexus_Registration_Submission.md) |
