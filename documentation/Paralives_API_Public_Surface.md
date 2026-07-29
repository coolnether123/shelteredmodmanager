# Paralives API Public Surface

> **Build/reference metadata**
> Research note created/reviewed: 2026-05-30.
> Game build represented: local Paralives managed assemblies from A:\SteamLibrary\steamapps\common\Paralives, DLL timestamps 2026-05-29 UTC.
> Assembly fingerprint: Assembly-CSharp.dll SHA256 885D46DF..., Paralives.dll SHA256 BEE83983..., Plugins.dll SHA256 311E9ED9.... Full hashes are in Decompiled/decompile-state.json.
> Metadata added: 2026-05-30.

Date reviewed: 2026-05-29

Scope: public declarations currently found under `ParalivesAPI` source, excluding `bin` and `obj`. This document summarizes what exists today and separates current API from proposed capability names.

## Current Summary

The current source scan finds 175 public type declarations:

| Kind | Count |
|------|-------|
| Classes | 139 |
| Enums | 18 |
| Interfaces | 18 |

Public declarations currently appear under `ParalivesAPI.Core`, `ParalivesAPI.Stable`, `ParalivesAPI.Native`, and `ParalivesAPI.Unsafe`. No public types were found under `ParalivesAPI.Patches` in the current source tree.

Primary entry point:

```csharp
ParalivesAPI.Core.ParalivesRuntimeInfo.Current
```

Current public categories:

| Category | Current public types |
|----------|----------------------|
| Runtime aggregate and bootstrap | `ParalivesRuntimeInfo`, `ParalivesRuntimeBootstrap`, `ParalivesGameFacade`, version and capability types |
| Stable contract scaffolds | `IParalivesRuntime`, `IParalivesCharacters`, `IParalivesInteractions`, `IParalivesActions`, `IParalivesOccupations`, occupation sub-facade interfaces, `IParalivesUi`, `IParalivesSaveStorage` |
| Native and unsafe boundary scaffolds | `IParalivesNativeApi`, `ParalivesNativeBoundary`, `IParalivesUnsafeApi`, `ParalivesUnsafeBoundary` |
| Facades | character, player, people, settings, time, occupations, skills, queues, wants, needs, status, relationships, personality, memories, goals, social, Together, world, UI/window helpers |
| Registries and factories | interaction content/factory/registry, localization registry, notification registry, attendance policy registry |
| Additional model and facade additions | action lifecycle, game lifecycle, content, interaction builders/models, requirements, occupation registry/schedule/task models, save lifecycle/storage, UI extension/panel models, patch diagnostics |
| Snapshots and result DTOs | people/activity, content, interaction, queue entry/injection result, occupation summary/schedule/upgrade, skill, need, status, relationship, memory, goal, want, time |
| Event DTOs and change enums | want, skill, status, need, relationship, memory, goal, action completion, interaction selection, Together choices/card use |
| Utilities | `ParalivesGuid`, `ParalivesReflection` |

The full current list can be regenerated with:

```cmd
tools\verify-paralivesapi-surface.cmd -ListCurrent
```

## Stable-ish Today

These surfaces are closest to stable mod-author API, subject to gameplay testing:

| Surface | Why it is stable-ish |
|---------|----------------------|
| `ParalivesRuntimeInfo.Current` | Single runtime aggregate registered by bootstrap. |
| `ParalivesApiVersion` and capability metadata | Current runtime exposes game/API/adapter version and capability strings. |
| `ParalivesAPI.Stable` interfaces | Initial contract scaffolding exists, but current facades are not forced to implement these interfaces yet. |
| Snapshot DTOs | Mostly primitive/ID/string data rather than live native objects. |
| GUID-based read helpers | Avoid direct manager traversal in ordinary mod code. |
| `ParalivesPeopleFacade` snapshots | Provides selected/current household and activity summaries. |
| `ParalivesTimeFacade` with `ParalivesTimeState` | Uses a small API-owned state DTO. |
| Queue digest and GUID cancel/inject helpers | Wrap native queue operations and report failure instead of exposing only manager calls. |
| Localization string helpers | `Has` and `Translate` avoid direct `TranslationManager` use for common reads. |
| Change events | Publish manager changes through API-owned event DTOs, though some event DTOs still carry raw payloads. |
| `ParalivesGuid` | Deterministic GUID helper independent of live manager state. |

Stable-ish does not mean final. It means the shape can plausibly remain after the Stable/Native/Unsafe split.

## Raw Native Internals Currently Exposed

Several current public signatures expose live `Paralives.dll` types. These are useful for early experimentation but should be treated as Native or Unsafe until wrapped.

The current scanner reports 162 public raw game type exposures outside `ParalivesAPI.Native` and `ParalivesAPI.Unsafe`.

| Area | Examples of exposed native types |
|------|----------------------------------|
| Characters and players | `AssetCharacter`, `Player`, `LifeStage` |
| Interactions and actions | `ActionUnit`, `InteractionUnit`, `InteractionGroup`, `InteractionGroupItem`, `InteractionUsabilityRule`, `ContextRequirement` |
| Occupations and schedules | `Occupation`, `OccupationUnlockable`, `ScheduleDaysOfWeek`, `OccupiedHours`, occupation save-data objects |
| Settings and content | `Skill`, `Want`, `Need`, `StatusEffect`, `RelationshipLabel`, `PersonalityTrait`, `SettingBase` |
| Localization and notifications | `TranslationItem`, `Notification`, `NotificationData` |
| Memory and status payloads | `MemoryLogType`, `MemoryData`, `AddStatusEffectData` |
| Social and Together | `SocialGroup`, `TogetherCard`, `TogetherCardCategory` |
| World | `AssetLot`, `ItemObjectRoot` |
| Reflection and patches | `ParalivesReflection`, Harmony patch targets, private member access |

Current raw exposure is expected during the refactor. It should be documented as current debt, not silently normalized as stable API.

## Proposed Capability Names

Use capability names without `v2` naming. Current capability strings use `.v1` for additive contracts; the names below avoid a `v2` label unless a future contract is intentionally breaking. Names below are proposed unless the corresponding type already exists.

| Capability | Purpose |
|------------|---------|
| `ParalivesRuntime` | Runtime readiness, game identity, facade access, diagnostics. |
| `ParalivesCharacters` | Stable character identity, household membership, availability, life-stage snapshots. |
| `ParalivesPeople` | Current household, selected people, activity snapshots. |
| `ParalivesHouseholds` | Household membership, money/inventory summary, current household state. |
| `ParalivesInteractions` | Stable interaction definitions, registration, root/group ownership. |
| `ParalivesActions` | Action definitions and action-completion events. |
| `ParalivesQueues` | Queue snapshots, injection, cancellation, digesting. |
| `ParalivesOccupations` | Occupation reads/mutations, tasks, performance, upgrade helpers. |
| `ParalivesSchools` | School enrollment, mandatory attendance policy, grade helpers. |
| `ParalivesAttendance` | Attendance override policies and diagnostics. |
| `ParalivesSkills` | Skill snapshots, level/experience mutation, skill events. |
| `ParalivesWants` | Active/offered wants, completion, reroll/status helpers. |
| `ParalivesGoals` | Goal/request snapshots, objective progress, reward and turn-in helpers. |
| `ParalivesNeeds` | Need snapshots and safe need mutation. |
| `ParalivesStatus` | Status-effect snapshots and add/remove helpers. |
| `ParalivesRelationships` | Relationship snapshots, label checks, label unlock/level helpers. |
| `ParalivesPersonality` | Trait snapshots and trait queries. |
| `ParalivesMemories` | Memory snapshots and safe write/cancel helpers. |
| `ParalivesSocial` | Social group snapshots and membership checks. |
| `ParalivesTogether` | Together card categories, card choices, card-use events. |
| `ParalivesLocalization` | Translation registration and lookup. |
| `ParalivesNotifications` | Notification registration and display. |
| `ParalivesSettings` | Stable generated-setting lookup snapshots. |
| `ParalivesWorld` | World summary, lot/item snapshots, safe item operations. |
| `ParalivesLots` | Lot state, dirty marking, ownership, item lists. |
| `ParalivesItems` | Item lookup, item state, inventory/content helpers. |
| `ParalivesSaves` | Current save identity, save status, save request helpers. |
| `ParalivesSaveEvents` | Load/save lifecycle events. |
| `ParalivesDirtyState` | Explicit dirty marking for save-backed assets. |
| `ParalivesUi` | Stable UI/window actions and UI event surfaces. |
| `ParalivesWindows` | Focused window-opening helpers. |

## Occupation Public-Surface Rules

Occupation APIs are generic. Do not add Homeschool-specific public facade, DTO, method, property, event, namespace, or capability names. Homeschool should be represented as one possible mod that uses school occupation data, attendance policies, tasks, panel providers, and snapshots.

Stable occupation APIs should expose:

- character GUIDs and occupation GUIDs;
- primitive values, strings, and API-owned enums/DTOs;
- immutable snapshots such as occupation content, enrollment, schedule, task, attendance, unlockable, and restore snapshots;
- structured request/result objects for enrollment, swap/restore, task, performance, and unlockable operations;
- generic names such as `Occupation`, `Schedule`, `Attendance`, `Task`, `Unlockable`, `PanelProvider`, and `Snapshot`.

Stable occupation APIs should not expose:

- `global::AssetCharacter` or native occupation save-data types;
- `Setting.Occupation`, `Setting.Occupations`, `Setting.OccupationUnlockable`, or generated setting/save-data objects;
- native schedule enums such as `ScheduleDaysOfWeek` or `OccupiedHours` unless deliberately kept in Native;
- native `UI*` window/item types;
- mod-specific names such as `Homeschool`.

Native occupation APIs may expose raw character, setting, schedule, and save-data types when the boundary is deliberate and named as Native. Unsafe occupation APIs cover patch targets, reflection, private UI methods, and manager internals.

Current occupation-related raw exposure is expected in `ParalivesAPI.Core` while the refactor is in progress. The intended direction is to keep GUID/snapshot overloads as Stable-compatible and move or duplicate raw overloads under Native/Unsafe.

Current and proposed boundary namespaces:

| Namespace | Meaning |
|-----------|---------|
| `ParalivesAPI.Stable` | Initial stable contract namespace. Interfaces currently exist as scaffolding. |
| `ParalivesAPI.Core` | Existing concrete facades and models. This namespace is still mixed: some members are stable-ish and some expose raw native types. |
| `ParalivesAPI.Native` | Current marker namespace for intentional typed access to live native game objects. |
| `ParalivesAPI.Unsafe` | Current marker namespace for reflection, patch-target, private-field, or decompiled-internal access. |

## Verification Notes

`tools/Verify-ParalivesApiSurface.ps1` is intentionally non-invasive. By default it lists counts and warns about raw native signatures. Use `-FailOnRawGameTypes` when a future phase is ready to enforce the Stable/Native/Unsafe namespace rule.

The verifier also reports:

- public API names containing `Homeschool` under `ParalivesAPI.Core`, `ParalivesAPI.Stable`, `ParalivesAPI.Native`, or `ParalivesAPI.Unsafe`;
- Stable interface members that expose raw `global::AssetCharacter`, `Setting.*`, native `UI*`, or other raw game types.

A future strict public-surface baseline can live at:

```text
documentation/ParalivesAPI_PublicSurface_Baseline.tsv
```

Do not create that baseline until the owning agents have landed the intended public contract shape.
