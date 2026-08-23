# Project map

Use this page to find source by responsibility. For public namespaces and facades, use the [API reference](API_Signatures_Reference.md). For placement rules, use [Architecture ownership](Architecture_Ownership_Guide.md).

## ModAPI

`ModAPI.dll` contains game-neutral framework code.

| Folder | Responsibility |
| --- | --- |
| [`ModAPI/Core`](../ModAPI/Core) | Plugin contracts, context, registry services, logging, runtime bootstrap ports, manager options, deterministic random streams, and background work |
| [`ModAPI/Loading`](../ModAPI/Loading) | Mod discovery, load order, and load-plan construction |
| [`ModAPI/Persistence`](../ModAPI/Persistence) | Per-mod JSON persistence and lifecycle hooks |
| [`ModAPI/Spine`](../ModAPI/Spine) | Settings metadata, scanning, definitions, and controllers |
| [`ModAPI/Input`](../ModAPI/Input) | Input actions, bindings, registry, scroll, and touch contracts |
| [`ModAPI/Actors`](../ModAPI/Actors) | Neutral actor registry, components, bindings, adapters, simulation, events, and serialization |
| [`ModAPI/Events`](../ModAPI/Events) | Inter-mod event bus |
| [`ModAPI/Harmony`](../ModAPI/Harmony) | Harmony setup, fluent transpilers, intent helpers, cooperative patching, safety, and reports |
| [`ModAPI/Scenarios`](../ModAPI/Scenarios) | Neutral scenario registration, lifecycle, catalog metadata, dependencies, and validation results |
| [`ModAPI/Debugging`](../ModAPI/Debugging) | Runtime feedback and watcher tools |
| [`ModAPI/Inspector`](../ModAPI/Inspector) | Runtime object, IL, execution, and memory inspection |
| [`ModAPI/Reflection`](../ModAPI/Reflection) | Reflection-based inspection helpers |
| [`ModAPI/UI`](../ModAPI/UI) | Game-neutral Unity UI flow and color helpers |
| [`ModAPI/Util`](../ModAPI/Util) | Small game-neutral utility classes |

## ShelteredAPI

`ShelteredAPI.dll` contains Sheltered runtime integrations and the public facades used by mods.

| Folder | Responsibility |
| --- | --- |
| [`ShelteredAPI/Core`](../ShelteredAPI/Core) | Runtime bootstrap, service IDs, and the Sheltered mod base class |
| [`ShelteredAPI/Adapters`](../ShelteredAPI/Adapters) | Sheltered implementations of neutral ModAPI ports |
| [`ShelteredAPI/Content`](../ShelteredAPI/Content) | Items, recipes, loot, assets, localization, inventory, and runtime injection |
| [`ShelteredAPI/Saves`](../ShelteredAPI/Saves) | Sheltered slots, manifests, verification, paging, restore, and save lifecycle |
| [`ShelteredAPI/Persistence`](../ShelteredAPI/Persistence) | Sheltered-backed persistent collections |
| [`ShelteredAPI/Events`](../ShelteredAPI/Events) | Game, UI, faction, save, and time events |
| [`ShelteredAPI/Input`](../ShelteredAPI/Input) | Vanilla actions, keybind persistence, validation, conflicts, and runtime tuning |
| [`ShelteredAPI/Actors`](../ShelteredAPI/Actors) | Sheltered actor adapters and identity helpers |
| [`ShelteredAPI/Characters`](../ShelteredAPI/Characters) | Character proxies, queries, effects, attributes, parties, and vanilla escape hatches |
| [`ShelteredAPI/Scenarios`](../ShelteredAPI/Scenarios) | XML and code authoring, installed catalog, playback, triggers, scoring, saves, and runtime application |
| [`ShelteredAPI/UI`](../ShelteredAPI/UI) | NGUI integration, focused UI helpers, and mod-owned runtime panels |
| [`ShelteredAPI/Storage`](../ShelteredAPI/Storage) | Item stores, transfers, reservations, and character assignments |
| [`ShelteredAPI/Workstations`](../ShelteredAPI/Workstations) | Cooking-station registration and runtime jobs |
| [`ShelteredAPI/Map`](../ShelteredAPI/Map) | Map snapshots, coordinates, policies, markers, and home-shelter providers |
| [`ShelteredAPI/Queues`](../ShelteredAPI/Queues) | Detached player-queue snapshots, restore requests, and change events |
| [`ShelteredAPI/Interactions`](../ShelteredAPI/Interactions) | Object-button builders and injection |
| [`ShelteredAPI/Harmony`](../ShelteredAPI/Harmony) | Sheltered-specific Harmony match patterns |
| [`ShelteredAPI/Debugging`](../ShelteredAPI/Debugging) | Support bundles and Sheltered runtime diagnostics |

## Other projects

| Project | Responsibility |
| --- | --- |
| [`Manager`](../Manager) | Desktop manager UI and workflows |
| [`Shared`](../Shared) | Contracts and policies used by more than one assembly |
| [`ShelteredScenarioEditor`](../ShelteredScenarioEditor) | Optional interactive scenario editor |
| [`SMM`](../SMM) | In-game SMM runtime integration |
| [`ManagerUpdater`](../ManagerUpdater) | Manager update application |
| [`Decompiler`](../Decompiler) | Standalone decompiler command-line tool |

## Common starting points

| Task | Start at |
| --- | --- |
| Follow plugin startup | [`ModAPI/Core/PluginManager.cs`](../ModAPI/Core/PluginManager.cs), [`ModAPI/Core/PluginRunner.cs`](../ModAPI/Core/PluginRunner.cs) |
| Change mod discovery or load order | [`ModAPI/Loading`](../ModAPI/Loading) |
| Add a neutral runtime port | [`ModAPI/Core`](../ModAPI/Core), then register its Sheltered implementation from [`ShelteredAPI/Core`](../ShelteredAPI/Core) |
| Add Sheltered content behavior | [`ShelteredAPI/Content`](../ShelteredAPI/Content) |
| Change save behavior | [`ShelteredAPI/Saves`](../ShelteredAPI/Saves) |
| Change scenario playback or XML | [`ShelteredAPI/Scenarios`](../ShelteredAPI/Scenarios) |
| Change the optional scenario editor | [`ShelteredScenarioEditor`](../ShelteredScenarioEditor) |

Do not create a second owner beside an existing facade, store, renderer, or lifecycle service. Extend the current owner or add a neutral contract with one Sheltered implementation.
