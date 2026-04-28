# ModAPI Sheltered Boundary Refactor

This is the baseline document for the full refactor to remove Sheltered-specific code from `ModAPI`.

Prompt 1 does not move implementation code. It establishes ownership, visible debt, and a verifier so later phases can move code in small commits without adding hidden exceptions.

## Boundary Rule

- `ModAPI` owns only game-neutral modding framework code.
- `ShelteredAPI` owns Sheltered developer hooks, adapters, Harmony patches, runtime manager integrations, NGUI/UI integrations, content injection, scenario runtime integration, and save/runtime implementations.
- Pure C# is not enough to stay in `ModAPI`. If it encodes Sheltered vocabulary or Sheltered gameplay rules, it belongs in `ShelteredAPI` or must be split into neutral `ModAPI` contracts plus Sheltered-owned implementations.
- Compatibility surfaces kept in `ModAPI` for the 1.3 line must be explicit, obsolete where appropriate, and tracked in the boundary baseline.

## Current Ownership Map

### Keep In ModAPI

These surfaces are intended to remain in `ModAPI` after Sheltered references are removed:

| Surface | Reason |
| --- | --- |
| `Core` plugin lifecycle contracts (`IModPlugin`, optional lifecycle interfaces), mod metadata, discovery, logging, registry helpers, and main-thread scheduling contracts | Framework behavior that applies to any game host. |
| `ModAPIRegistry`, shared assembly resolution, and basic plugin host wiring | Neutral runtime infrastructure, once Sheltered bootstrap details are moved behind host adapters. |
| `Persistence` containers and generic mod persistence models | Framework persistence primitives that do not require Sheltered managers. |
| `Spine` settings metadata, scanning, and neutral settings definitions | Game-neutral settings contract/model layer. |
| `Input` binding models, action registry, scroll query/source contracts | Neutral input description and dispatch contracts. |
| `Actors/Abstractions` and most `Actors/Models` | Neutral actor registry/component contracts, after Sheltered-specific enum values or adapters are split. |
| `Events/ModEventBus` | Game-neutral event bus. |
| `Harmony` fluent transpiler, cooperative patcher, safety policy, and diagnostics | General patching framework, excluding Sheltered pattern helpers and game-menu patches. |
| `Reflection`, neutral inspector helpers, and reusable debugger infrastructure | Framework diagnostics, provided they do not patch or name Sheltered runtime types. |
| Scenario registration contracts, lifecycle state/event contracts, catalog metadata, mod-folder source contracts, dependency manifest conversion, and neutral validation result containers | Framework behavior that describes custom scenario registration and metadata without Sheltered gameplay vocabulary. |

### Split Between ModAPI And ShelteredAPI

These surfaces need neutral contracts or framework pieces in `ModAPI`, with Sheltered behavior implemented in `ShelteredAPI`:

| Surface | ModAPI Owns | ShelteredAPI Owns |
| --- | --- | --- |
| `IGameHelper` and `IPluginContext.Game` | Game-helper abstraction using neutral IDs/contracts | Sheltered `FamilyMember`, `ItemManager`, and manager-backed implementation/adapters. |
| Actor system | Registry/component/binding/event/simulation contracts | Sheltered family, party, encounter, and live-sync adapters. |
| Character abstractions/models | Future neutral character identity/effect contracts only if separated from host runtime types | 1.3 Sheltered character effect/proxy surface, `FamilyMember`, party, effect runtime, and Sheltered character proxy implementations. |
| Save system | `ISaveSystem` contract and mod data registration model | `SaveManager` hooks, slot routing, expanded save storage, UI, and migration runtime. |
| UI hooks | Neutral hook registration contracts and lifecycle abstractions | NGUI widgets, `BasePanel`, `UIPanelManager`, mod-manager panels, settings panels, and injection runtime. |
| Input | Neutral binding/action contracts | Sheltered vanilla actions, Unity legacy/touchpad readers, keybind persistence, and conflict UI. |
| Content | `IContentResolutionService` for resolving mod-facing IDs to opaque host runtime keys; future neutral content extension point contracts if needed | Sheltered item, recipe, loot, localization, asset, inventory integrations, and host key resolution. |
| Scenarios | Neutral registration service contracts, lifecycle state/event args, opaque definition factory boundaries, portable catalog metadata, mod-folder source contracts, dependency manifest conversion, and neutral validation result containers | Sheltered scenario definitions, XML serializers, validators, runtime catalog/loader, runtime binding, `ScenarioDef` creation, bunker/family/inventory/quest/weather/runtime apply services, authoring UI, and in-game lifecycle patches. |
| Events | Neutral event bus | Sheltered day/session/combat/party/UI/faction/time-trigger hooks. |
| Runtime bootstrap | Game-neutral plugin loader sequence | Sheltered startup bootstrap and runtime API registrations. |

### Move To ShelteredAPI

These current `ModAPI` surfaces encode Sheltered runtime concepts and should move in later phases:

| Surface | Why |
| --- | --- |
| Remaining character contracts or helpers that still mention Sheltered runtime types | Prompt 4 moved the current character/party helper surface to `ShelteredAPI`; any future `ModAPI.Characters` surface must be game-neutral. |
| `Items/InventoryHelper.cs` | `ItemManager` and `InventoryManager` integration. |
| Remaining manager-state helpers that name Sheltered managers | Prompt 4 moved `ManagerStateHelper` to `ShelteredAPI`. |
| Remaining event helpers backed by Sheltered managers or panels | Prompt 4 moved `GameEvents`, `FactionEvents`, `UIEvents`, and `GameTimeTriggerHelper` to `ShelteredAPI`. |
| `Hooks/WorldHooks.cs`, `Hooks/UIHooks.cs` and remaining interaction-style helpers outside the moved registry | Sheltered world/UI hook vocabulary and runtime targets. Prompt 4 moved `InteractionRegistry` to `ShelteredAPI`. |
| `Util/GameUtil.cs`, `Util/PersistentDataAPI.cs` | Sheltered exploration/save manager helpers. |
| Remaining typed compatibility bridge in `Core/ShelteredContentBridge.cs` | It still exposes Sheltered item runtime types for 1.3 compatibility, but Prompt 2 removed its direct reflection dependency on ShelteredAPI. Future phases should delete this adapter when item/content helpers move. |
| `Core/SaveSystemImpl.cs`, `Core/SaveProtection.cs`, `Core/SaveExitTracer.cs` where they depend on Sheltered save/quit flow | Sheltered save/runtime implementation, not a neutral framework contract. |
| `Custom Saves/**` | Sheltered `SaveManager`, slot selection UI, save verification, and expanded vanilla save implementation. |
| NGUI/UI implementation files under `UI/**` | NGUI widgets, panel injection, settings panel, mod-manager panel, and UI patch runtime. |
| `Harmony/MainMenuPatches.cs` and `Harmony/Transpilers/ShelteredPatterns.cs` | Sheltered menu/runtime patch targets and Sheltered-specific IL helpers. |
| `Debugging/CrashCorridorMapDiagnostics.cs` | Sheltered map/panel diagnostics and manager patches. |
| Remaining scenario runtime application and authoring UI vocabulary outside the neutral `ModAPI.Scenarios` contracts | Sheltered scenario domain and runtime integration. Prompt 3 moved the scenario XML/domain schema, serializers, validation pipeline, catalog/loader implementation, and runtime binding to `ShelteredAPI.Scenarios`. |

### Delete Or Replace After 1.3 Compatibility

These are compatibility debts, not long-term framework surfaces:

| Surface | Replacement Direction |
| --- | --- |
| v1.2 compatibility helpers still retained in `ModAPI` (`GameUtil`, `PersistentDataAPI`) | Move to `ShelteredAPI` and leave only explicit migration documentation or obsolete forwarding shims in the next breaking line. |
| 1.3 source migration aliases now hosted in `ShelteredAPI` (`GameEvents`, `GameTimeTriggerHelper`, `UIEvents`, `FactionEvents`, `PartyHelper`, `InteractionRegistry`, `ManagerStateHelper`) | Keep documented as Sheltered-owned aliases until a later breaking line can rename or replace them cleanly. |
| Duplicate compatibility files that already have ShelteredAPI equivalents | Keep one Sheltered-owned implementation and remove the `ModAPI` copy when the compatibility window closes. |
| Examples that compile or accidentally patch runtime behavior | Keep as documentation/sample source only, not compiled framework behavior. |
| Reflection bridges whose only purpose is to let `ModAPI` call `ShelteredAPI` | Replace with host-owned registration/composition in `ShelteredAPI`. |

## Guardrail Baseline

The boundary verifier is:

```cmd
tools\verify-modapi-boundary.cmd
```

It checks for:

- `ModAPI.csproj` references to `Assembly-CSharp`.
- `ModAPI.csproj` references to `Manager`.
- `ModAPI` source references to obvious Sheltered symbols such as `FamilyManager`, `SaveManager`, `ExplorationManager`, `QuestManager`, `ScenarioDef`, `UIPanelManager`, `ItemManager`, `InventoryManager`, `WeatherManager`, `EncounterManager`, `FamilyMember`, `ExplorationParty`, `EncounterCharacter`, NGUI widget types, and `ShelteredAPI`.
- Sheltered-specific filenames or namespaces under `ModAPI`.

Existing findings are allowed only because they are listed in:

```text
documentation/ModAPI_Boundary_Baseline.tsv
```

The baseline is an explicit debt ledger. Later phases must remove or reduce entries from this file as code moves out of `ModAPI`. Do not add hidden exceptions for new code. If a later phase appears to require increasing the baseline, stop and split the design so the new Sheltered behavior lands in `ShelteredAPI` instead.

Every phase of this refactor must end with:

1. the boundary verifier passing,
2. the most relevant build/check passing or a documented blocker,
3. `git diff --stat` reviewed, and
4. one focused commit for that phase.

## Prompt 2 Baseline Port

Prompt 2 added `ModAPI.Core.IContentResolutionService` as the first small neutral port. It is intentionally narrow:

- it resolves mod-facing item IDs to opaque host runtime keys,
- it enumerates registered host runtime item keys,
- `ModAPI` does not interpret those keys except in existing 1.3 compatibility adapters that already expose Sheltered item types,
- `ShelteredAPI.Content.ShelteredContentResolutionService` owns the Sheltered `ItemManager.ItemType` implementation and is registered by `ShelteredApiRuntimeBootstrap`.

This removed the direct `ShelteredAPI.Content.ContentInjector, ShelteredAPI` reflection bridge from `ModAPI/Core/ShelteredContentBridge.cs`. The bridge remains only as a temporary typed adapter for current `InventoryHelper` and UI runtime compatibility call sites.

Boundary baseline shrink from Prompt 2:

- removed `source-symbol ModAPI/Core/ShelteredContentBridge.cs ShelteredAPI`,
- reduced `source-symbol ModAPI/Core/ShelteredContentBridge.cs ItemManager` from `13` to `12`.

## Prompt 3 Scenario Ownership Split

Prompt 3 separated the generic scenario framework from the Sheltered scenario authoring/runtime pack:

- `ModAPI.Scenarios` keeps `ICustomScenarioService`, `CustomScenarioRegistration`, lifecycle state and event args, `CustomScenarioDefinitionFactory`, `ScenarioInfo`, `ScenarioModFolder`, `IScenarioModFolderSource`, `ModRegistryScenarioModFolderSource`, `ScenarioDependencyManifest`, and `ScenarioValidationResult`.
- `ShelteredAPI.Scenarios` owns `ScenarioDefinition`, Sheltered scenario domain types, XML section serializers, validation rules, `ScenarioValidator`, `ScenarioCatalog`, `ScenarioLoader`, `ScenarioFrameworkVerification`, `ScenarioPipelineSmokeTest`, placement definitions, and `ScenarioRuntimeBinding`.
- `ShelteredAPI` continues to register the scenario runtime through `ShelteredApiRuntimeBootstrap` and the scenario composition root.

Boundary baseline shrink from Prompt 3:

- removed `sheltered-filename ModAPI/Scenarios/ScenarioDefinition.cs ScenarioDef`,
- removed `sheltered-filename ModAPI/Scenarios/ScenarioDefinitionSerializer.cs ScenarioDef`,
- removed `source-symbol ModAPI/Scenarios/Domain/Validation/SchedulingValidationRule.cs ItemManager`.

## Prompt 4 Sheltered Hooks Split

Prompt 4 moved Sheltered-backed event, party, interaction, character, and manager-state helper surfaces out of `ModAPI`:

- `ModAPI.Events` in `ModAPI.dll` now contains only `ModEventBus`.
- `GameEvents`, `GameTimeTriggerHelper`, `UIEvents`, and `FactionEvents` are hosted by `ShelteredAPI.dll` and keep `ModAPI.Events` namespaces as 1.3 source migration aliases.
- `PartyHelper`, party patches, character effect/proxy surfaces, and character runtime models are hosted by `ShelteredAPI.dll` and keep `ModAPI.Characters` namespaces as 1.3 source migration aliases.
- `InteractionRegistry` and `ManagerStateHelper` are hosted by `ShelteredAPI.dll` and keep old namespaces as 1.3 source migration aliases.
- `ModAPI.Core.IGameLifecycleSource` and `IUiLifecycleEventSink` were added as narrow neutral runtime ports so existing ModAPI internals can receive Sheltered lifecycle/UI notifications without owning Sheltered event implementations.

Boundary baseline shrink from Prompt 4:

- removed 32 event/character/game-state/interaction entries,
- baseline count changed from `264` to `232`.

## Prompt 1 Scope Lock

Prompt 1 intentionally does not:

- move scenario implementations,
- move save implementations,
- move event, character, UI, input, or content implementations,
- remove `Assembly-CSharp` from `ModAPI.csproj`,
- remove `Manager` from `ModAPI.csproj`.

Those changes are implementation phases that should remove baseline debt as they move code.
