# Paralives Decompiled Systems

> **Build/reference metadata**
> Research note created/reviewed: 2026-05-27.
> Game build represented: local Paralives managed assemblies from A:\SteamLibrary\steamapps\common\Paralives, DLL timestamps 2026-05-29 UTC.
> Assembly fingerprint: Assembly-CSharp.dll SHA256 885D46DF..., Paralives.dll SHA256 BEE83983..., Plugins.dll SHA256 311E9ED9.... Full hashes are in Decompiled/decompile-state.json.
> Metadata added: 2026-05-30.

Date reviewed: 2026-05-27

Scope: `Decompiled/Paralives.dll`, the Homeschool-style modding pattern, and the gameplay helpers added to `ParalivesAPI`.

## API Direction

Gameplay mods should not need to reach directly into decompiled singletons for common work. `ParalivesAPI` now exposes facade APIs for the systems Homeschool-style mods need most:

| Area | Facade | Native systems | Notes |
| --- | --- | --- | --- |
| People and selected characters | `ParalivesAPI.People`, `Characters`, `Players` | `CharacterManager`, `PlayerManager`, `HouseholdManager` | Stable lookup and snapshots by GUID. |
| Interactions and queues | `Interactions`, `Queues`, `InteractionSelections`, `ActionCompletions` | `InteractionManager`, `UIInteractionsListItem`, `UpdateCharacterActions` | Register content, inject interactions, read queues, cancel queued interactions, observe selections/completions. |
| Occupations | `Occupations`, `AttendancePolicies` | `OccupationsManager`, `UpdateCharacterOccupations` | Read schedules, assignments, summaries, upgrade points, and override attendance decisions. |
| Skills | `Skills` | `SkillManager`, `Setting.Skills` | Read/write levels and observe level/experience changes. |
| Wants and goals | `Wants`, `Goals` | `WantsManager`, `GoalsManager`, `UpdateCharacterWants` | Read active/offered wants, mutate goal lifecycle, and subscribe to native changes. |
| Needs and status | `Needs`, `Status`, `Statuses` | `NeedManager`, `StatusEffectManager` | Read safely without creating missing need data, mutate values/effects, and observe native changes. |
| Relationships and personality | `Relationships`, `Personality` | `RelationshipManager`, `PersonalityManager` | Read labels/traits and wrap common relationship mutations. |
| Memories | `Memories`, `Memory` | `MemoryManager`, `BrainLogicManager` | Read/write/cancel memory log entries and observe memory/brain-logic events. |
| Social groups and Together | `Social`, `Together` | `SocialGroupManager`, `TogetherManager` | Read active groups, add Together cards/categories, contribute card choices, observe card use. |
| UI, notifications, localization | `Windows`, `Notifications`, `Localizations` | `UI`, `NotificationManager`, `TranslationManager`, `Setting.Translations` | Guarded registration and display/translation helpers. |
| World | `World` | `AssetManager`, lots/items managers | Read lots/items without exposing broad raw manager mutation. |

## Character State

The central person record is `AssetCharacter`. Most gameplay state is under `AssetCharacter.Data`:

| State | Save data | Manager |
| --- | --- | --- |
| Needs | `NeedSaveData` | `NeedManager` |
| Status effects | `StatusEffectSaveData` | `StatusEffectManager` |
| Skills | `SkillsSaveData` | `SkillManager` |
| Occupations | `OccupationsSaveData` | `OccupationsManager` |
| Wants | `Wants`, `OfferedWants` | `WantsManager` |
| Goals | `GoalsSaveData`, `TrackedGoal` | `GoalsManager` |
| Memories | `MemoryLogSaveData` | `MemoryManager` |
| Relationships | `Relationships` | `RelationshipManager` |
| Personality | personality trait arrays/data | `PersonalityManager` |

Important safety rule: some native read methods are not pure. `NeedManager.GetNeedData` creates missing save data, so `ParalivesAPI.Needs.ReadNeeds(..., includeConfiguredNeeds: true)` builds unsaved snapshots instead of mutating the character just to answer a read.

## Interactions

The native interaction stack separates definitions from runtime queue entries:

| Type | Role |
| --- | --- |
| `Setting.InteractionUnit` | Menu/queue interaction definition. |
| `Setting.ActionUnit` | Action step executed by an interaction. |
| `AssetCharacterDataInteraction` | Runtime queued interaction instance. |
| `CurrentAction` | Runtime action state inside a queued interaction. |
| `InteractionToInject` | Native injection request consumed by `InteractionManager.InjectInteraction`. |

`ParalivesAPI.Queues` wraps the useful safe operations:

- `ReadQueue(...)` returns stable queue snapshots.
- `BuildQueueDigest(...)` gives mods a cheap way to detect queue changes.
- `TryInjectInteraction(...)` validates the actor, target, and interaction before calling the native injector.
- `TryCancelInteraction(...)` delegates to `InteractionManager.CancelInteraction` and reports whether the queue entry was marked for cancellation.
- `TryCancelSocialGroupInteractions(...)` delegates social-group cancellation for group interactions.

Hook coverage:

- `UIInteractionsListItem.ClickedInteraction` raises `InteractionSelections`.
- `UpdateCharacterActions.OnActionEnd` raises `ActionCompletions`.

## Progression

Progression systems are mostly data-driven through `Setting.*` assets, but not all generated APIs are equally complete.

| System | Setting data | Runtime manager | API status |
| --- | --- | --- | --- |
| Occupations | `Setting.Occupations` | `OccupationsManager` | facade helpers for schedule, assignment, summaries, upgrade points, attendance. |
| Skills | `Setting.Skills` | `SkillManager` | facade helpers compensate for thin generated API. |
| Wants | `Setting.Wants` | `WantsManager` | active/offered read and lifecycle events. |
| Goals | `Setting.Goals` | `GoalsManager` | goal snapshots, current requests, add/track/reward/objective/cancel/turn-in helpers. |
| Memories | `Setting.BrainLogic`, `Setting.MemoryLogType` | `MemoryManager`, `BrainLogicManager` | memory snapshots, write/cancel helpers, lifecycle events. |

Native progression events now surfaced by `ParalivesAPI`:

- `Skills.SkillChanged`
- `Wants.WantChanged`
- `Goals.GoalChanged`
- `Memories.MemoryChanged`
- `Needs.NeedChanged`
- `Status.StatusEffectChanged`
- `Relationships.RelationshipChanged`
- `Together.ChoicesBuilding`
- `Together.CardUsed`

## Social Groups And Together

`SocialGroupManager` stores active group state in saved-game social group data. The groups are gameplay state, not durable mod-owned records, and can be cleared or rebuilt during loading.

Useful native methods:

- `SocialGroupManager.GetSocialGroupByGUID`
- `SocialGroupManager.GetCurrentSocialGroupOfCharacter`
- `SocialGroupManager.GetSocialGroupsOfCharacter`
- `TogetherManager.PickCharacterCards`
- `TogetherManager.ProcessOutcomes`
- `TogetherManager.WasCardUsedRecently`

`ParalivesAPI.Social` returns group snapshots and character lists by group. `ParalivesAPI.Together` registers card categories/cards, lets mods add choices during `PickCharacterCards`, and publishes card use after native outcomes are processed.

## Goals And Memory Lifecycle

`GoalsManager` is the goal/request manager. There is no separate `GoalManager`.

Hooked goal methods:

- `AddGoalToCharacter`
- `SetTrackedGoal`
- `ClaimGoalReward`
- `CompleteWantInGoal`
- `CancelRequestOrGoal`
- `TurnInRequest`

Hooked memory methods:

- `WriteMemory`
- `SetMemoryAsCancelled`
- `ExecuteBrainLogic`
- `ExecuteBrainLogicAction`

The memory write hook snapshots the matching memory before and after the native call so refreshes and new entries can both be observed without publishing false events when the native method exits early.

## Localization And Notifications

`Setting.Translations.RebuildDictionnary()` must run after adding translation items. The localization registry batches registrations and rebuilds only when a registration changes the native array.

New safe read helpers:

- `Localizations.Has(key)`
- `Localizations.Translate(key, params string[] parameters)`
- `Localizations.Translate(guid, params string[] parameters)`
- `Localizations.GetItemOrNull(key)`

`Notifications.Show(...)` validates that settings are ready and that the notification is registered before dispatching through `NotificationManager`.

## Native Footguns

- `NeedManager.GetNeedData` mutates missing needs. Use the API snapshots for reads.
- Many native managers assume settings and save data are loaded. Facades return false or empty arrays when systems are not ready.
- Relationship mutations are not uniformly save-dirty in native code. API wrappers mark affected characters dirty where practical.
- `SocialGroupManager` data is active gameplay state and can be reset during load.
- `InteractionManager.CancelInteraction` may cancel group members when the interaction belongs to a social group.
- `TranslationManager.Get(...)` has overloads that require `Unity.Mathematics`; the API localization helper formats simple string parameters directly to avoid an extra reference.
- Some native notification calls silently do nothing when content is not registered or characters are not valid household targets.
- Settings generated APIs are uneven. Use `ParalivesAPI.Settings.TryGet*` helpers before direct `Settings.Get<T>()`.

## Follow-Up API Work

Good next facades to add after gameplay testing:

- Household and life stage facade for birth, death, school-age checks, current household membership, and selected household state.
- Save facade for current save identity, save-dirty marking, load/save lifecycle callbacks, and mod-owned save diagnostics.
- Richer world facade for lots, item lookups, inventory, item states, and safe item spawning.
- Occupation UI helpers for vacation, schedules, task status, and unlockable display.
- Autonomy facade for safe autonomous interaction injection and debug tracing.
