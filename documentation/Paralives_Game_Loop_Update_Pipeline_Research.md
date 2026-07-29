# Paralives Game Loop and Update Pipeline Research

> **Build/reference metadata**
> Research note created/reviewed: 2026-05-28.
> Game build represented: local Paralives managed assemblies from A:\SteamLibrary\steamapps\common\Paralives, DLL timestamps 2026-05-29 UTC.
> Assembly fingerprint: Assembly-CSharp.dll SHA256 885D46DF..., Paralives.dll SHA256 BEE83983..., Plugins.dll SHA256 311E9ED9.... Full hashes are in Decompiled/decompile-state.json.
> Metadata added: 2026-05-30.

Date reviewed: 2026-05-29

Scope: local decompiled `Paralives.dll` sources, especially the native frame scheduler, message/event systems, request managers, and gameplay update systems that are useful hook points for mods and future `ParalivesAPI` facades.

## Gap Filled

Existing docs cover gameplay domains such as interactions, wants, goals, needs, relationships, and mod opportunities, but they do not explain the frame pipeline that makes those managers run. This note documents how the game loop is wired, where manager mutations happen, and where hook points are safest.

## Core Shape

Paralives has two important update mechanisms:

| Mechanism | Main type | What it drives | Hook value |
| --- | --- | --- | --- |
| Frame scheduler | `SystemManager : MonoBehaviour` | Registered `ParaSystemBase` systems for gameplay, UI, build tools, and message events. | Best map for "what runs before what" in normal play. |
| Request managers | `RequestManagerBase<T> : MonoBehaviour` | Multi-frame jobs such as loading and saving. | Best hook for load/save lifecycle and long-running phased work. |

`SystemManager.Start()` registers the game systems in a fixed list order. In this decompile there are 381 registrations before the helper methods: 144 player systems and 237 non-player systems. About 91 of those non-player systems are message/event systems. The same ordered list is used for both `Update()` and `LateUpdate()`.

Source anchors:

- `Decompiled/Paralives.dll/SystemManager.cs`: `Start()` registers systems, `Update()` and `LateUpdate()` both call `RunSystems(...)`.
- `Decompiled/Paralives.dll/ParaSystemBase.cs`: empty virtual hooks for `Update`, `UpdateForPlayer`, `UpdateForMessage`, `LateUpdate`, `LateUpdateForPlayer`, and `LateUpdateForMessage`.
- `Decompiled/Paralives.dll/ParaMessageSystem.cs`: generic message queue implementation.
- `Decompiled/Paralives.dll/RequestManagerBase.cs`: pending/running/completed request loop.

## SystemManager Execution Model

Each Unity `Update()` calls `RunSystems(false)`. Each Unity `LateUpdate()` calls `RunSystems(true)`.

For every registered `ParaSystemBase`, `SystemManager` checks `GameLoadingManager.State` against the system's eligible state mask:

```csharp
(system.ElligibleStates & currentState) == currentState
```

The state enum is:

| State | Meaning |
| --- | --- |
| `State.MainMenu` | Main menu. |
| `State.Loading` | Load pipeline is active. |
| `State.Game` | Live game/build/character gameplay is available. |
| `State.All` | Bitmask catch-all, used for UI, messages, benchmarks, and always-on systems. |

Dispatch branches:

- Player systems run once per `PlayerManager.Instance.Players` entry via `UpdateForPlayer(player)` or `LateUpdateForPlayer(player)`.
- Message systems run only if they have pending messages and call `UpdateForMessage()` or `LateUpdateForMessage()`.
- Regular systems call `Update()` or `LateUpdate()`.

`RunSystems` catches and logs exceptions around each system, so one failing system does not stop later systems in the frame. This is good for game resilience but can hide mod breakage if a patch logs every frame.

## Important Registration Order

The order matters because later systems see mutations made by earlier systems in the same frame. The most useful gameplay slice is:

| Order | System | State | Role |
| ---: | --- | --- | --- |
| 2 | `AdvanceTime` | `Game` | Computes `ParaTime.DeltaTime`, `DeltaMinute`, pause state, time scale, autosave timer, and play stats. |
| 40 | `UpdateCharacterInteractions` | `Game` | Promotes pending interactions, validates running interactions, handles cancellation and deletion. |
| 41 | `UpdateCharacterActions` | `Game` | Runs current action state machines, starts/updates/ends actions, processes action outcomes. |
| 42 | `UpdateCharacterLocomotion` | `Game` | Applies pathfinding movement for running locomotion actions. |
| 54 | `UpdateCharacterAutonomy` | `Game` | Evaluates forced and idle autonomy, injects interactions through `InteractionManager`. |
| 67 | `UpdateCharacterWants` | `Game` | Evaluates want/goal objective progress and calls `WantsManager` / `GoalsManager`. |
| 78 | `UpdateGameUI` | `All`, player system | Shows/hides HUD panels, thought bubbles, time UI, build UI, character UI, notifications. |
| 212-215 | time message events | `All` | Toggle pause and set speed messages mutate `ParaTime`. |
| 221-223 | save/load/unload events | `All` | Commands that create save/load/unload requests. |
| 272 | `UpdateCharacterNeeds` | `Game`, late work | Does need decay in `LateUpdate()`, not `Update()`. |
| 377 | `GenerateRequests` | `Game` | Generates daily NPC requests into `GoalsManager.CurrentRequests`. |
| 378 | `UpdateLoopRequests` | `Game` | Displays request thought bubbles for NPCs and request boards. |

Implication: if a mod needs to observe the final interaction queue for the frame, a postfix on `UpdateCharacterActions.Update()` is usually more useful than a prefix on `UpdateCharacterInteractions.Update()`. If it needs to affect vanilla validation, patch before the relevant native system or patch the manager method that the system calls.

## Time and Pause

`AdvanceTime.Update()` is the time root. It exits if no saved game is loaded, reads `Time.unscaledDeltaTime`, updates UI-driven pause state, applies configured speed multipliers, writes `ParaTime.DeltaTime`, `ParaTime.DeltaMinute`, `TotalMinutes`, and Unity `Time.timeScale`, then advances stats and autosave.

Many gameplay systems exit on `ParaTime.IsPaused`, including:

- `UpdateCharacterActions`
- `UpdateCharacterAutonomy`
- `UpdateCharacterOccupations`
- `UpdateCharacterNeeds` in late update

Not everything pauses. UI systems and many `State.All` message systems continue. A mod should not assume "paused" means no state changes can happen. Commands, UI visibility, save/load requests, and some dirty-refresh systems can still run.

Hook points:

- Observe time after vanilla computation: postfix `AdvanceTime.Update()`.
- Override speed/pause behavior: prefer message or `ParaTime.SetTimeSpeed(...)` when possible; patch `AdvanceTime.Update()` only for cross-cutting time behavior.
- Need a real-time runner unaffected by pause: use a mod-owned `MonoBehaviour.Update()` with `Time.unscaledTime`, like `ParalivesRuntimeHost`.

## Interaction and Action Pipeline

Interaction queue state lives on `AssetCharacter.Data.CurrentInteractionsInQueue`.

The frame flow is:

1. `UpdateCharacterInteractions.Update()` scans all characters.
2. Pending interactions can become `ToBeStarted` if instant, first in line, or multitasking-compatible.
3. Invalid interactions are marked `ToBeCanceled`.
4. Deleted interactions are removed, carried items can be dropped/destroyed, child interactions can be deleted, and social cluster membership can be cleaned.
5. `UpdateCharacterActions.Update()` processes `ToBeStarted`, `Running`, `Cancelling`, and delete transitions.
6. `UpdateCharacterActions` expands action containers such as sequences, skinned steps, switch-on-context actions, locomotion setup, posture transitions, social group activity, and action outcomes.
7. `UpdateCharacterLocomotion.Update()` applies motion for the running locomotion current action.

Manager touch points:

- `InteractionManager.Instance.CanCharacterDoInteraction(...)`
- `InteractionManager.Instance.InjectInteraction(...)`
- `InteractionManager.Instance.CancelInteraction(...)`
- `ActionManager.Instance.GetActionReferencedByInteraction(...)`
- `CharacterManager.Instance.ProcessOutcomes(...)`
- `SocialGroupManager` for groups and clusters
- `PathfindingManager` / `TownWaypointManager` for locomotion paths

Current `ParalivesAPI` already patches `UpdateCharacterActions.OnActionEnd` to publish action completion. It does not currently provide a general "before/after system tick" hook.

Safer hooks:

- To inject work into a queue: use `InteractionManager.InjectInteraction` via the API facade where available.
- To observe user selection: use the existing `UIInteractionsListItem.ClickedInteraction` patch path.
- To observe action completion: use the existing `UpdateCharacterActions.OnActionEnd` patch path.
- To change whether a queued interaction can start: patch `InteractionManager.CanCharacterDoInteraction(...)` or a narrow evaluator instead of rewriting `UpdateCharacterInteractions`.
- To alter locomotion: patch path creation or `PathfindingManager` methods before patching the large `UpdateCharacterLocomotion` loop.

## Autonomy Pipeline

`UpdateCharacterAutonomy.Update()` runs after the core action/animation systems. It skips when paused, when autonomy is disabled, or during intro.

It evaluates:

- forced autonomy rules from `Settings.Get<Autonomy>().AutonomyRules`;
- autonomy tags from `AutonomyManager.Instance.GetCurrentCharacterAutonomyTags(...)`;
- selected vs non-selected character settings;
- social group side effects;
- idle autonomy cooldown using `ParaTime.DeltaMinute`;
- weighted random idle choices via `AutonomyManager.Instance.GetWeightedRandomizedAutonomyRuleInteraction(...)`;
- final injection through `InteractionManager.Instance.InjectInteraction(...)`.

Hook points:

- Autonomy filtering: `AutonomyManager.GetWeightedRandomizedAutonomyRuleInteraction(...)`.
- Blocking a forced rule: prefix/postfix on requirement evaluation or `InteractionManager.InjectInteraction(...)` with `isForcedAutonomous`.
- Observing autonomous actions: inspect `AssetCharacterDataInteraction.IsFromIdleAutonomy`, `IsFromForcedAutonomy`, or patch injection.
- Avoid broad transpilers in `UpdateCharacterAutonomy.Update()` unless there is no manager-level method to patch. It is a high-frequency loop over every character.

## Needs, Wants, Goals, and Requests

Progression is split across early update and late update.

`UpdateCharacterWants.Update()` runs in regular update. It:

- clears unavailable wants when life stage has no wants;
- removes completed/failed wants after their clear timestamp;
- writes want memories;
- evaluates fulfillment and failure requirements with `ContextEvaluationManager`;
- completes/fails wants through `WantsManager`;
- evaluates goal objective want progress and calls `GoalsManager.CompleteWantInGoal(...)`.

`UpdateCharacterGoals.Update()` is later in the registration list. It checks completion, shows notifications, writes goal-completed memories, and marks characters dirty.

`UpdateCharacterNeeds.LateUpdate()` does need decay after normal update. It:

- exits when paused or in intro;
- checks `Needs.EnableNeedsFeature`;
- loops active needs;
- calls `NeedManager.GetNeedData(...)`, `ForceReliefNeed(...)`, `ChangeNeedByValue(...)`, and `ReliefNeed(...)`;
- uses memory logs of type `MemoryLogType.IsRelievingNeed`.

Footgun: `NeedManager.GetNeedData(...)` can create missing save data. Existing API docs already call this out. Do not use it for pure reads in diagnostics or UI.

Daily request flow:

- `GenerateRequests.Update()` clears and repopulates `GoalsManager.Instance.CurrentRequests` once per day, seeded by day and save GUID.
- `UpdateLoopRequests.Update()` reads `CurrentRequests`, caches request boards, and adds available/completed thought bubbles through `UIThoughtBubbles`.

Hook points:

- Wants: manager methods such as `WantsManager.CompleteWant`, `FailWant`, `AddRandomOccupationWant`, and current `ParalivesAPI` wants hooks.
- Goals/requests: `GoalsManager.AddGoalToCharacter`, `CompleteWantInGoal`, `CancelRequestOrGoal`, `TurnInRequest`, plus `GenerateRequests.Update()` if the target is daily request selection.
- Needs: manager methods already patched in `ParalivesAPI`; avoid patching `UpdateCharacterNeeds.LateUpdate()` unless the change is about the decay loop itself.
- Request UI: `UpdateLoopRequests.Update()` is a reasonable postfix observation point for final per-frame thought bubble additions, but manager hooks are better for changing request state.

## Message and Event Systems

Native command-style actions use `MessageBase` subclasses and `ParaMessageSystem<T>` handlers.

`SystemManager.Instance.RegisterMessage(message)`:

1. Finds the first registered system where `IsMessageSystemOfType(message.GetType())` is true.
2. Adds the message to that system's `_pendingMessages`.
3. Logs an error if no registered message system accepts the message.

`ParaMessageSystem<T>.UpdateForMessage()` drains pending messages and calls `UpdateMessage(T message)`. Because the system list is ordered, a message can run in the same frame only if it is registered before that message system's turn in the current `SystemManager.Update()` pass. Messages registered after their handler already ran wait until the next frame.

Examples:

- `TogglePauseEvent.UpdateMessage(MessageTogglePause)` toggles `ParaTime.IsPausedByPlayer`.
- `SetPlayerLiveModeEvent.UpdateMessage(MessageSetPlayerLiveMode)` changes the player state and refreshes lot perimeter visibility.
- `DirtyZonesEvent.UpdateMessage(MessageDirtyZones)` invalidates build-mode refresh flags and marks town lot perimeter/road assets dirty.

Hook points:

- Prefer `SystemManager.Instance.RegisterMessage(new MessageX(...))` when a native command exists. It preserves vanilla ordering, undo side effects, dirty flags, and UI refresh behavior better than direct manager field mutation.
- Patch `SomeEvent.UpdateMessage(...)` when a mod needs to observe or alter a command.
- Patch `SystemManager.RegisterMessage(...)` only for diagnostics or broad event-bus behavior; it is a global hot path for many UI/build commands.

## Loading and Saving Are Separate Request Loops

`GameLoadingManager` and `GameSavingManager` are `RequestManagerBase<T>` subclasses, not `ParaSystemBase` systems. They run through their own Unity `Update()` and `LateUpdate()` methods.

`RequestManagerBase<T>` behavior:

- `CreateRequest()` puts a request in `_pendingRequests`.
- `Update()` moves pending requests to running slots and calls `UpdateRequest(...)`.
- `LateUpdate()` checks `IsCompleted(...)`, moves completed requests, and invokes completion callbacks.
- `MaxActiveRequestsCount` controls parallel request count; default is 1.

`GameLoadingManager.UpdateRequest(...)` changes `GameLoadingManager.State`:

- `ShowLoadingScreen` sets `State.Loading`.
- `ShowGame` restores cameras/UI, optionally starts tutorial or storyboards, then sets `State.Game` and calls `request.OnGameLoaded`.
- Some lot-loading paths also set `State.Game` after hiding the loading UI and destroying impostors.

Hook points:

- "Session is playable": postfix `GameLoadingManager.UpdateRequest(...)` and detect transition to `State.Game`, or attach to `GameLoadingRequest.OnGameLoaded` if creating the request.
- "Before vanilla systems run in game state": a custom persistent `MonoBehaviour.Update()` can watch for the first frame where `GameLoadingManager.State == State.Game`, but it will not have deterministic position inside `SystemManager` unless you patch a native system.
- Save lifecycle: patch `GameSavingManager.CreateRequest(...)`, `UpdateRequest(...)` phase transitions, or `RequestManagerBase<T>.LateUpdate()` only if broad request completion is required.

## Existing ParalivesAPI Hook Coverage

The current `ParalivesAPI` bootstrap applies Harmony patches and starts a persistent runtime host:

- `ParalivesRuntimeBootstrap.Initialize()` registers runtime info, calls `ParalivesHarmonyPatcher.EnsurePatched()`, starts `ParalivesRuntimeHost`, and starts the SMM mod screen bridge.
- `ParalivesHarmonyPatcher` scans patch classes in the assembly and applies Harmony patches once.
- `ParalivesRuntimeHost.Runner.Update()` polls once per unscaled second and applies localization, interaction, and notification registrations when native settings/managers are ready.

Current patch files cover manager-level events for goals, memory, needs, occupations, relationships, skills, status effects, together cards, wants, mod enable state, UI mod screen items, interaction list clicks, and action completion.

Not currently covered as first-class API hooks:

- `SystemManager` frame start/end.
- Per-system before/after tick callbacks.
- `GameLoadingManager` state transitions.
- `GameSavingManager` phase transitions.
- Generic native message bus observation.

Those are the natural next API surfaces if agents need lifecycle-safe hooks instead of direct Harmony patches.

## Practical Hook Matrix

| Goal | Prefer | Avoid unless necessary |
| --- | --- | --- |
| Add or cancel gameplay interactions | `ParalivesAPI.Queues` or `InteractionManager.InjectInteraction` / `CancelInteraction` wrappers | Directly editing `CurrentInteractionsInQueue` from arbitrary frames |
| Observe action completion | Existing `ActionCompletions` / `UpdateCharacterActions.OnActionEnd` patch | Scanning all queues every frame |
| Change autonomy choices | `AutonomyManager` or `InteractionManager.InjectInteraction` hook | Transpiling the full autonomy loop |
| Change need decay | `NeedManager` hooks or narrow postfix on `UpdateCharacterNeeds.LateUpdate()` | Calling `GetNeedData` for reads |
| Generate or alter daily requests | `GoalsManager` methods, or narrow patch on `GenerateRequests.Update()` | Mutating `CurrentRequests` without respecting daily/cooldown flags |
| Run after a save loads | `GameLoadingManager` transition to `State.Game` | Assuming managers are ready during plugin `Initialize` |
| Execute vanilla commands | `SystemManager.RegisterMessage(new MessageX())` | Direct field mutation that bypasses event side effects |
| Draw/update custom UI each frame | Mod-owned `MonoBehaviour`, existing UI facade, or `UpdateGameUI.UpdateForPlayer` postfix | Patching many individual UI panels |
| Profile expensive hooks | `SystemManager.SystemExecutionTimeInTicks` and `SystemBenchmark` behavior | Long-running work inside high-frequency per-character loops |

## Modding Rules of Thumb

- Patch the narrowest manager method that owns the behavior. Use frame-system patches only when the behavior is truly about ordering or repeated tick work.
- Prefix when vanilla needs altered inputs or should be skipped. Postfix when observing final state or publishing events.
- Respect `GameLoadingManager.State`; most manager singletons exist early, but save/household/town data may not be ready.
- Treat `ParaTime.IsPaused` as a gameplay pause, not a global execution stop.
- Prefer native messages for build/UI/game commands because event handlers set dirty flags, update undo state, and refresh UI.
- Keep per-frame hooks cheap. `UpdateCharacterAutonomy`, `UpdateCharacterInteractions`, and `UpdateCharacterActions` loop over characters and can run every frame.
- Be careful with save dirtiness. Some native manager mutations mark assets dirty and some do not; API wrappers should mark affected characters/assets dirty where practical.

## Follow-Up Work

Useful next docs or API work:

- Generate a machine-readable registration map from `SystemManager.Start()` for agents to diff between game versions.
- Add lifecycle events in `ParalivesAPI`: `LoadingStarted`, `GameReady`, `BeforeNativeSystemsUpdate`, `AfterNativeSystemsUpdate`, `BeforeNativeSystemsLateUpdate`, `AfterNativeSystemsLateUpdate`, `BeforeSave`, and `AfterSave`.
- Add a safe native-message facade so mods can enqueue known `MessageBase` commands without direct decompiled type references.
- Add diagnostics that report active `GameLoadingManager.State`, current player `GameStates`, request manager counts, and top `SystemExecutionTimeInTicks` entries.
