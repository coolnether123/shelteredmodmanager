# ModAPI and ShelteredAPI boundary

This reference records the completed 2.0 assembly split. Mod authors should use the [assembly boundary](README.md#assembly-boundary-canonical) to choose references.

## Ownership rule

`ModAPI` owns game-neutral framework code. `ShelteredAPI` owns code that names or implements Sheltered behavior.

Pure C# code does not automatically belong in `ModAPI`. Code belongs in `ShelteredAPI` when it encodes Sheltered gameplay rules, managers, panels, saves, items, characters, or scenarios.

## Assembly responsibilities

| Assembly | Owns |
| --- | --- |
| `ModAPI.dll` | Plugin lifecycle, discovery, logging, registry services, main-thread scheduling, neutral persistence, deterministic random streams, background work, settings metadata, input contracts, actor contracts, inter-mod events, Harmony helpers, diagnostics, and neutral scenario contracts |
| `ShelteredAPI.dll` | Sheltered content, assets, saves, events, input runtime, NGUI integration, characters, maps, queues, scenarios, runtime bootstrap, and implementations of neutral ModAPI ports |
| `ShelteredScenarioEditor.dll` | Optional interactive scenario drafts, editor commands, authoring UI, preview composition, and editor diagnostics |
| `Manager.exe` | Desktop installation, configuration, Nexus integration, updates, content tools, and manager-owned options |

The scenario dependency direction is:

```text
ShelteredScenarioEditor -> ShelteredAPI -> ModAPI
```

`ShelteredAPI` must build and run without the editor assembly.

## Shared contracts

Some systems split a neutral contract from a Sheltered implementation:

| System | ModAPI | ShelteredAPI |
| --- | --- | --- |
| Game access | `IGameHelper` with neutral IDs and opaque handles | Sheltered manager and `FamilyMember` adapters |
| Actors | Registry, components, bindings, events, simulation, and serialization | Family, party, encounter, and live-runtime adapters |
| Persistence | `ISaveSystem`, `IModSaveContext`, `ISaveRuntimeAdapter`, and per-mod JSON storage | Slot routing, `SaveManager` hooks, expanded saves, manifests, and save UI |
| Settings | Metadata, scanning, definitions, and providers | NGUI rendering and Sheltered controls integration |
| Input | Actions, bindings, registry, scroll, and touch contracts | Vanilla actions, `PlatformInput_PC` patches, persistence, conflicts, and controls UI |
| Content | Opaque content-resolution contract | Items, recipes, loot, localization, assets, inventory, and runtime injection |
| Events | `ModEventBus` | Sheltered day, session, combat, party, UI, faction, save, and time events |
| Scenarios | Registration, lifecycle, catalog metadata, dependencies, and validation results | XML models, `ScenarioDef` creation, playback, saves, triggers, scoring, and runtime application |
| Runtime startup | Loader sequence and `IGameRuntimeBootstrap` discovery | Sheltered bootstrap and neutral service registration |

## Public replacements

Use the current facades instead of older helper classes:

| Need | Public API |
| --- | --- |
| Game and manager state | `ShelteredGameState` |
| Persisted Sheltered collections | `ShelteredPersistence`, `ShelteredPersistentList<T>`, `ShelteredPersistentDictionary<TValue>` |
| Characters and parties | `ShelteredActors`, `ShelteredCharacters` |
| Game and UI events | `ShelteredEvents` |
| Object interactions | `ObjectButtonInjector` |
| Content and inventory | `ShelteredContent` |
| UI helpers | `ShelteredUI`, `ShelteredRuntimeUI` |
| Save slots and lifecycle | `ShelteredSaves`, `ShelteredSaveEvents` |
| Scenarios | `ShelteredScenarios`, `ShelteredScenarioAuthoring`, `ShelteredScenarioRuntime` |

Do not add compatibility aliases for unreleased pre-2.0 helper names. The 2.0 line uses the facade names above.

## Place new code

Before adding a type, answer these questions:

1. Does it name a Sheltered manager, panel, game type, save format, item, character, or scenario rule? Put it in `ShelteredAPI`.
2. Can another game host implement the contract without referencing Sheltered? The contract may belong in `ModAPI`.
3. Does it implement a neutral contract with Sheltered runtime objects? Keep the contract in `ModAPI` and the implementation in `ShelteredAPI`.
4. Is it interactive scenario-authoring UI or draft state? Put it in `ShelteredScenarioEditor`.

Do not add a reflection bridge whose only purpose is to let `ModAPI` call `ShelteredAPI`. Register the Sheltered implementation through a neutral contract instead.

## Verify the boundary

Run:

```cmd
tools\verify-modapi-boundary.cmd
```

The verifier rejects:

- game or Sheltered assembly references in `ModAPI.csproj`;
- Sheltered managers, panels, game types, NGUI widgets, and `ShelteredAPI` symbols under `ModAPI`;
- Sheltered-specific filenames or namespaces under `ModAPI`.

The expected baseline is empty. [`ModAPI_Boundary_Baseline.tsv`](ModAPI_Boundary_Baseline.tsv) exists only for an explicitly reviewed engine-level exception. Do not add a baseline row to hide Sheltered behavior in `ModAPI`.
