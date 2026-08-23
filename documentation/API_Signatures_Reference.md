# API reference

Use this page to find the assembly, namespace, facade, and task guide for a public API. Use Visual Studio IntelliSense or Object Browser for exact overloads. The build places XML documentation beside each assembly:

- `Dist/SMM/ModAPI.dll` and `Dist/SMM/ModAPI.xml`
- `Dist/SMM/bin/ShelteredAPI.dll` and `Dist/SMM/bin/ShelteredAPI.xml`

The source is the final authority. The public-surface verifier checks `ShelteredAPI` against [`ShelteredAPI_PublicSurface_Baseline.tsv`](ShelteredAPI_PublicSurface_Baseline.tsv). Run it with:

```powershell
tools\verify-shelteredapi-public-surface.cmd
```

## Choose an assembly

| Need | Assembly | Start with |
| --- | --- | --- |
| Plugin lifecycle, logging, settings, persistence, input contracts, actors, Harmony, random streams, or background work | `ModAPI.dll` | [Core ModAPI basics](ModAPI_Developer_Guide.md) |
| Sheltered content, saves, events, UI, characters, scenarios, maps, or queues | `ShelteredAPI.dll` and `ModAPI.dll` | [ShelteredAPI guide](ShelteredAPI_Guide.md) |
| A vanilla type such as `FamilyMember`, `ItemManager.ItemType`, or `ScenarioDef` | `Assembly-CSharp.dll`, plus the API assemblies used by the mod | [Assembly boundary](README.md#assembly-boundary-canonical) |
| Harmony patches | `0Harmony.dll` and `ModAPI.dll` | [Harmony patch guide](how%20to%20develop%20a%20patch%20with%20harmony.md) |

## ModAPI namespaces

| Namespace | Main public types | Guide or source |
| --- | --- | --- |
| `ModAPI.Core` | `IModPlugin`, `IPluginContext`, `IModSaveContext`, `ModManagerBase`, `ModManagerBase<T>`, `ModRandom`, `ModThreads`, `ManagerBooleanOptions`, `ModAPIRegistry` | [Core ModAPI basics](ModAPI_Developer_Guide.md), [`ModAPI/Core`](../ModAPI/Core) |
| `ModAPI.Persistence` | `IModPersistenceLifecycle` | [Settings and persistence](SETTINGS.md), [`ModAPI/Persistence`](../ModAPI/Persistence) |
| `ModAPI.InputActions` | `ModInputAction`, `InputBinding`, `InputActionRegistry` | [Input keybindings](Input_Keybindings_Guide.md), [`ModAPI/Input`](../ModAPI/Input) |
| `ModAPI.Actors` | `IActorSystem`, `ActorId`, actor components, queries, adapters, simulation, and serialization contracts | [Actors guide](ShelteredAPI_Characters_Guide.md), [`ModAPI/Actors`](../ModAPI/Actors) |
| `ModAPI.Spine`, `ModAPI.Attributes` | `ISettingsProvider`, `SpineSettingsHelper`, `SettingDefinition`, `ModSettingAttribute` | [Spine settings](Spine_Settings_Guide.md), [`ModAPI/Spine`](../ModAPI/Spine) |
| `ModAPI.Events` | `ModEventBus` | [Events guide](Events_Guide.md), [`ModAPI/Events`](../ModAPI/Events) |
| `ModAPI.Harmony` | `FluentTranspiler`, intent helpers, cooperative patching, patch reports, and safety controls | [Transpiler and debugging guide](Transpiler_and_Debugging_Guide.md), [`ModAPI/Harmony`](../ModAPI/Harmony) |
| `ModAPI.Scenarios` | Neutral scenario registration, lifecycle, dependency, catalog, and validation contracts | [Custom scenarios](Custom_Scenarios_Guide.md), [`ModAPI/Scenarios`](../ModAPI/Scenarios) |

## ShelteredAPI namespaces

| Namespace | Main public types | Guide or source |
| --- | --- | --- |
| `ShelteredAPI.Content` | `ShelteredContent`, item and recipe definitions, patches, assets, loot, and `VanillaItems` | [Content guide](ShelteredAPI_Content_Guide.md), [`ShelteredAPI/Content`](../ShelteredAPI/Content) |
| `ShelteredAPI.Events` | `ShelteredEvents`, time-trigger types | [Events guide](Events_Guide.md), [`ShelteredAPI/Events`](../ShelteredAPI/Events) |
| `ShelteredAPI.Input` | `ShelteredInput`, `ShelteredInputActions` | [Input keybindings](Input_Keybindings_Guide.md), [`ShelteredAPI/Input`](../ShelteredAPI/Input) |
| `ShelteredAPI.Actors`, `ShelteredAPI.Characters` | `ShelteredActors`, `ShelteredCharacters`, character proxies, queries, effects, and attributes | [Actors guide](ShelteredAPI_Characters_Guide.md), [`ShelteredAPI/Actors`](../ShelteredAPI/Actors), [`ShelteredAPI/Characters`](../ShelteredAPI/Characters) |
| `ShelteredAPI.Scenarios` | `ShelteredScenarios`, `ShelteredScenarioAuthoring`, `ShelteredScenarioRuntime`, XML models, builders, triggers, and scoring snapshots | [Custom scenarios](Custom_Scenarios_Guide.md), [`ShelteredAPI/Scenarios`](../ShelteredAPI/Scenarios) |
| `ShelteredAPI.Saves` | `ShelteredSaves`, `ShelteredSaveEvents`, save entries, slot manifests, and verification models | [ShelteredAPI guide](ShelteredAPI_Guide.md), [`ShelteredAPI/Saves`](../ShelteredAPI/Saves) |
| `ShelteredAPI.UI`, `ShelteredAPI.UI.Runtime` | Focused UI helpers and mod-owned runtime panels | [Runtime UI and stores](ShelteredAPI_Runtime_UI_Stores_Guide.md), [`ShelteredAPI/UI`](../ShelteredAPI/UI) |
| `ShelteredAPI.Storage`, `ShelteredAPI.Workstations` | Item stores, reservations, character assignments, and cooking stations | [Runtime UI and stores](ShelteredAPI_Runtime_UI_Stores_Guide.md), [`ShelteredAPI/Storage`](../ShelteredAPI/Storage), [`ShelteredAPI/Workstations`](../ShelteredAPI/Workstations) |
| `ShelteredAPI.Map` | `ShelteredMap`, `ShelteredMapMarkers`, map snapshots, policies, markers, and home-shelter providers | [ShelteredAPI guide](ShelteredAPI_Guide.md), [`ShelteredAPI/Map`](../ShelteredAPI/Map) |
| `ShelteredAPI.Queues` | `ShelteredQueues`, queue snapshots, restore results, and change events | [ShelteredAPI guide](ShelteredAPI_Guide.md), [`ShelteredAPI/Queues`](../ShelteredAPI/Queues) |
| `ShelteredAPI.Debugging` | `ShelteredSupportBundle` and support-bundle models | [`ShelteredAPI/Debugging`](../ShelteredAPI/Debugging) |
| `ShelteredAPI.Harmony` | Sheltered-specific Harmony match patterns | [Harmony patch guide](how%20to%20develop%20a%20patch%20with%20harmony.md), [`ShelteredAPI/Harmony`](../ShelteredAPI/Harmony) |
| `ShelteredAPI.Interactions` | Object-button builders and injection helpers | [`ShelteredAPI/Interactions`](../ShelteredAPI/Interactions) |

## Manager runtime options

Use `ModAPI.Core.ManagerBooleanOptions` to register and read desktop-manager boolean options. Do not read or write `manager_options.json` directly. The manager and ModAPI share the option contract in [`Shared/ManagerOptions`](../Shared/ManagerOptions).

## Plugin lifecycle and context

Implement `IModPlugin.Initialize(IPluginContext)` and `IModPlugin.Start(IPluginContext)`. Optional lifecycle interfaces add updates, shutdown, scene events, and session events. See [`IPlugin.cs`](../ModAPI/Core/IPlugin.cs).

## Persistence and Sheltered saves

Use `IPluginContext.SaveSystem` for ordinary mod-owned data in the active save. Use `ShelteredSaves` only to inspect or operate on Sheltered slots and descriptors. See [Settings and persistence](SETTINGS.md).

## ModRandom deterministic streams (`ModAPI.Core`)

Use `ModRandom.GetStream(modId, featureId)` for save-replayable choices. Named streams isolate one feature's draw order from another. See [`ModRandom.cs`](../ModAPI/Core/ModRandom.cs) and [Core ModAPI basics](ModAPI_Developer_Guide.md#deterministic-random-streams).

## Background work (SMM 2.0)

Use `ModThreads` for non-Unity calculations. Background delegates must not access Unity objects. Keyed options add cancellation, throttling, and stale-result handling. See [`ModThreads.cs`](../ModAPI/Core/ModThreads.cs).

## Expedition map context (SMM 2.0)

Use `ShelteredMap` for detached map snapshots, coordinate conversion, route distance, generation policies, and home-shelter placement providers. The facade may report that map data is unavailable before the runtime has created a map. See [`ShelteredMap.cs`](../ShelteredAPI/Map/ShelteredMap.cs).

## Map markers (SMM 2.0)

Use `ShelteredMapMarkers` for detached expedition marker and actor projections, or to register mod-owned markers. See [`ShelteredMapMarkers.cs`](../ShelteredAPI/Map/ShelteredMapMarkers.cs).

## Player queues (SMM 2.0)

Use `ShelteredQueues` to read detached player-queue snapshots and request conservative restore operations. The API does not expose live vanilla jobs. See [`ShelteredQueues.cs`](../ShelteredAPI/Queues/ShelteredQueues.cs).

## UI extensions (SMM 2.0)

Use `ShelteredUI` for focused clone, bind, color, and lifecycle helpers. Use `ShelteredRuntimeUI` for mod-owned panels. These APIs do not replace vanilla panel controllers. See [`ShelteredUI.cs`](../ShelteredAPI/UI/ShelteredUI.cs) and [Runtime UI and stores](ShelteredAPI_Runtime_UI_Stores_Guide.md).

## Save manifest and support bundle (SMM 2.0)

Use `ShelteredSupportBundle` to capture current runtime, mod, save-manifest, diagnostic, and log facts for a bug report. Missing optional data is reported as unavailable. See [`ShelteredSupportBundle.cs`](../ShelteredAPI/Debugging/ShelteredSupportBundle.cs).

## API status

The 2.0 assembly split is the supported API line. Runtime UI, stores, cooking stations, map helpers, queues, and support-bundle APIs remain in preview. Scenario registration, XML and code authoring, playback, runtime bindings, and scoring snapshots are supported. `ShelteredScenarioEditor.dll` is an optional editor application and is not a mod dependency.
