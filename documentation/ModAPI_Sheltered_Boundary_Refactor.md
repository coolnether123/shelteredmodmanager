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
| Scenario registration contracts, serializer infrastructure, and pure schema tooling where the schema is kept game-neutral | Candidate framework layer only after Sheltered-specific schema vocabulary is separated. |

### Split Between ModAPI And ShelteredAPI

These surfaces need neutral contracts or framework pieces in `ModAPI`, with Sheltered behavior implemented in `ShelteredAPI`:

| Surface | ModAPI Owns | ShelteredAPI Owns |
| --- | --- | --- |
| `IGameHelper` and `IPluginContext.Game` | Game-helper abstraction using neutral IDs/contracts | Sheltered `FamilyMember`, `ItemManager`, and manager-backed implementation/adapters. |
| Actor system | Registry/component/binding/event/simulation contracts | Sheltered family, party, encounter, and live-sync adapters. |
| Character abstractions/models | Neutral character identity/effect contracts if still needed by framework | `FamilyMember`, party, effect runtime, and Sheltered character proxy implementations. |
| Save system | `ISaveSystem` contract and mod data registration model | `SaveManager` hooks, slot routing, expanded save storage, UI, and migration runtime. |
| UI hooks | Neutral hook registration contracts and lifecycle abstractions | NGUI widgets, `BasePanel`, `UIPanelManager`, mod-manager panels, settings panels, and injection runtime. |
| Input | Neutral binding/action contracts | Sheltered vanilla actions, Unity legacy/touchpad readers, keybind persistence, and conflict UI. |
| Content | `IContentResolutionService` for resolving mod-facing IDs to opaque host runtime keys; future neutral content extension point contracts if needed | Sheltered item, recipe, loot, localization, asset, inventory integrations, and host key resolution. |
| Scenarios | Neutral registration service contracts and portable scenario metadata where possible | `ScenarioDef`, bunker/family/inventory/quest/weather/runtime apply services, authoring UI, and in-game lifecycle patches. |
| Events | Neutral event bus | Sheltered day/session/combat/party/UI/faction/time-trigger hooks. |
| Runtime bootstrap | Game-neutral plugin loader sequence | Sheltered startup bootstrap and runtime API registrations. |

### Move To ShelteredAPI

These current `ModAPI` surfaces encode Sheltered runtime concepts and should move in later phases:

| Surface | Why |
| --- | --- |
| `Characters/PartyHelper.cs`, `Characters/PartyPatches.cs` | `FamilyManager`, `ExplorationManager`, `ExplorationParty`, and Sheltered party semantics. |
| `Items/InventoryHelper.cs` | `ItemManager` and `InventoryManager` integration. |
| `GameState/ManagerStateHelper.cs` | Manager runtime state is host-specific. |
| `Events/GameEvents.cs`, `Events/FactionEvents.cs`, `Events/UIEvents.cs`, `Events/GameTimeTriggerHelper.cs` | Sheltered gameplay and UI event hooks. |
| `Interactions/InteractionRegistry.cs`, `Hooks/WorldHooks.cs`, `Hooks/UIHooks.cs` | Sheltered world/UI hook vocabulary and runtime targets. |
| `Util/GameUtil.cs`, `Util/PersistentDataAPI.cs` | Sheltered exploration/save manager helpers. |
| Remaining typed compatibility bridge in `Core/ShelteredContentBridge.cs` | It still exposes Sheltered item runtime types for 1.3 compatibility, but Prompt 2 removed its direct reflection dependency on ShelteredAPI. Future phases should delete this adapter when item/content helpers move. |
| `Core/SaveSystemImpl.cs`, `Core/SaveProtection.cs`, `Core/SaveExitTracer.cs` where they depend on Sheltered save/quit flow | Sheltered save/runtime implementation, not a neutral framework contract. |
| `Custom Saves/**` | Sheltered `SaveManager`, slot selection UI, save verification, and expanded vanilla save implementation. |
| NGUI/UI implementation files under `UI/**` | NGUI widgets, panel injection, settings panel, mod-manager panel, and UI patch runtime. |
| `Harmony/MainMenuPatches.cs` and `Harmony/Transpilers/ShelteredPatterns.cs` | Sheltered menu/runtime patch targets and Sheltered-specific IL helpers. |
| `Debugging/CrashCorridorMapDiagnostics.cs` | Sheltered map/panel diagnostics and manager patches. |
| Scenario bunker/family/inventory/quest/weather/runtime application and authoring UI vocabulary currently under `Scenarios/**` | Sheltered scenario domain and runtime integration. |

### Delete Or Replace After 1.3 Compatibility

These are compatibility debts, not long-term framework surfaces:

| Surface | Replacement Direction |
| --- | --- |
| v1.2 compatibility helpers retained in `ModAPI` (`GameEvents`, `GameTimeTriggerHelper`, `UIEvents`, `FactionEvents`, `PartyHelper`, `InteractionRegistry`, `GameUtil`, `PersistentDataAPI`) | Move to `ShelteredAPI` and leave only explicit migration documentation or obsolete forwarding shims in the next breaking line. |
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

## Prompt 1 Scope Lock

Prompt 1 intentionally does not:

- move scenario implementations,
- move save implementations,
- move event, character, UI, input, or content implementations,
- remove `Assembly-CSharp` from `ModAPI.csproj`,
- remove `Manager` from `ModAPI.csproj`.

Those changes are implementation phases that should remove baseline debt as they move code.
