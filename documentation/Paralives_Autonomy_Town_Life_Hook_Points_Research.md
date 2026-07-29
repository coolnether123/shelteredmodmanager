# Paralives Autonomy And Town Life Hook Points Research

> **Build/reference metadata**
> Research note created/reviewed: 2026-05-28.
> Game build represented: local Paralives managed assemblies from A:\SteamLibrary\steamapps\common\Paralives, DLL timestamps 2026-05-29 UTC.
> Assembly fingerprint: Assembly-CSharp.dll SHA256 885D46DF..., Paralives.dll SHA256 BEE83983..., Plugins.dll SHA256 311E9ED9.... Full hashes are in Decompiled/decompile-state.json.
> Metadata added: 2026-05-30.

Date reviewed: 2026-05-29

Scope: `Decompiled/Paralives.dll` autonomy, town autonomy, town walking, townie population, and the current `ParalivesAPI` integration surface.

## Gap This Fills

Existing docs identify autonomy and town life as a good modding target, but they do not map the live update loops, manager methods, saved fields, setting records, or safe hook points. This doc is a code-level map for agents adding diagnostics, APIs, or mods around autonomous behavior.

There is no dedicated `ParalivesAPI` autonomy facade right now. Mods can observe queue/action state through `ParalivesAPI.Queues`, `ActionCompletions`, and `People`, and can register or inject ordinary interactions, but autonomy rule registration and town-autonomy events still require raw `Setting.Autonomy` access or Harmony patches.

## Runtime Update Order

`SystemManager` registers several relevant systems in `State.Game`:

| Order area | System | Role |
| --- | --- | --- |
| Interaction execution | `UpdateCharacterInteractions`, `UpdateCharacterActions`, `UpdateCharacterLocomotion`, `UpdateCharacterMoveToItemLocator` | Runs queued interactions and action steps before autonomy picks new work. |
| Live interaction autonomy | `UpdateCharacterAutonomy` | Evaluates forced and idle autonomy and injects interactions. |
| Social/progression systems | `UpdateSocialGroups`, `UpdateCharacterRelationships`, `UpdateTogetherEnergy`, `UpdateGroupRelationships`, wants/occupations/needs/memories | Processes downstream gameplay state affected by interactions. |
| Population | `UpdateSpawnTownies` | Refills town NPC population and first-time patron spawns. |
| Town autonomy | `UpdateTownAutonomy`, `UpdateCharacterTownAutonomyWalk` | Chooses NPC town destinations, then moves unloaded/loaded NPCs along waypoint paths. |

The important split is that `UpdateCharacterAutonomy` controls interaction queues for characters that are loaded or actively relevant, while `UpdateTownAutonomy` controls where non-household NPCs should be in town over longer time spans.

## Settings And Save Data

Autonomy is mostly data-driven through `Setting.Autonomy`:

| Setting | Used by | Notes |
| --- | --- | --- |
| `EnableAutonomy`, `EnableForcedAutonomy`, `EnableIdleAutonomy` | `UpdateCharacterAutonomy` | Master switches for live interaction autonomy. |
| `AutonomyRules` | `UpdateCharacterAutonomy` | `ForcedAutonomyRule[]`; context-triggered interactions such as emergencies or required behaviors. |
| `ScoreBasedIdleAutonomyRules` | `AutonomyManager.GetAllPossibleScoredBasedAutonomousInteractions` | Weighted idle interactions with score modifiers, target-character selection, skin picking, and item-finder validation. |
| `AutonomyTags` / `WorkAutonomyTags` | `AutonomyManager.GetCurrentCharacterAutonomyTags` | Tags come from current occupation, town rule idle tags, scheduled work tags, and forced town occupation. They can shorten idle cooldowns or block forced autonomy. |
| `ActionChainingAutonomies` | `UpdateCharacterActions.OnActionEnd` | Adds follow-up actions after a completed action without creating a new interaction. |
| `EnableTownAutonomy`, `TownAutonomyRules`, `TownAutonomyWaitTime`, `TownAutonomyBaseRequirement` | `UpdateTownAutonomy` | Chooses NPC town destinations and long-duration activities. |
| `TownAutonomyTakeWalkRule`, `TownAutonomyGoToFakeHomeRule`, `TownAutonomyWalkInteraction`, walk speeds | `UpdateTownAutonomy`, `UpdateCharacterTownAutonomyWalk` | Special rules and movement behavior for out-of-lot town movement. |

The main saved fields are on `AssetCharacterData`:

- Live autonomy: `IdleAutonomyCooldown`, `TimeLastTriedAutonomy`, `AutonomyRulesTimestamps`.
- Town autonomy: `LastUpdatedTownAutonomy`, `CurrentTownAutonomyDuration`, `EndOfCurrentTownAutonomyAction`, `CurrentTownAutonomyTargetLot`, `CurrentTownAutonomyRule`, `TownAutonomyDestinationState`, `CurrentTownAutonomyWalkPath`, `TownAutonomyCooldowns`, `TownAutonomyForcedOccupation`, `TownAutonomyWalkStuckTries`, `CurrentTownAutonomyTargetsAnImpostor`.

If a mod mutates these fields directly, mark the affected `AssetCharacter.IsSaveDirty` and update the whole state tuple, not just one field.

## Live Interaction Autonomy Flow

`UpdateCharacterAutonomy.Update()` returns immediately while time is paused, autonomy is disabled, or the player is in the intro. It increments a round-robin character index every frame. The method loops over all characters, but non-every-frame forced rules and idle autonomy only run for the current round-robin character.

Eligibility gates:

- Dead/taken-away characters, dummy characters, and `DoNotLoadVisual` characters are skipped.
- Characters without loaded visuals are skipped.
- Non-household NPCs with a non-idle town destination are skipped unless they are in a social group with at least one current-household character.
- Selected and non-selected household characters respect `Gameplay.EnableAutonomyForSelectedCharacters` and `Gameplay.EnableAutonomyForOtherCharacters`.

Forced autonomy runs first:

1. Refresh `AutonomyRulesTimestamps` so saved cooldown records match current `AutonomyRules`.
2. For each `ForcedAutonomyRule`, check the global switch, player autonomy settings, every-frame policy, interaction character requirements, current autonomy tags, current lot, social group, and context requirements.
3. If a rule matches, it may cancel current interactions and remove the character from a social group.
4. If the rule has an interaction, `InteractionManager.InjectInteraction` queues it with `isForcedAutonomous: true`; otherwise running interactions are marked `ToBeCanceled`.
5. Notifications are shown through `AutonomyManager.ShowNotificationIfAutonomyRuleAllowsIt`.

Idle autonomy runs second:

1. Household characters can idle-autonomize if idle autonomy is enabled and the selected/non-selected setting allows it.
2. Non-household characters can idle-autonomize when `TownAutonomyDestinationState == Idle`.
3. Characters with `CurrentOccupationIndex != -1` can also pass the idle-autonomy gate.
4. `IdleAutonomyCooldown` is incremented and compared against `Autonomy.IdleCooldownTime`, with optional lower cooldowns from active `AutonomyTag`s.
5. `AutonomyManager.GetWeightedRandomizedAutonomyRuleInteraction` selects a rule, then `InteractionManager.InjectInteraction` queues it with `isIdleAutonomous: true`.

One implementation wart: when a forced rule tries to inject an interaction already in the queue, `UpdateCharacterAutonomy` uses `return`, not `continue`, so one duplicate can exit the entire update method for that frame.

## Idle Rule Scoring

`AutonomyManager.GetWeightedRandomizedAutonomyRuleInteraction(character)` checks mandatory idle rules first, then ordinary idle rules:

- Mandatory pass: `ScoreBasedIdleAutonomyRule.AlwaysChooseThisRuleIfRequirementsAreMet` or `AlwaysChooseThisRuleIfCharacterHasTags`.
- Ordinary pass: all other positive-scoring idle rules.

`GetAllPossibleScoredBasedAutonomousInteractions` filters each candidate by:

1. Registered `InteractionUnit` exists.
2. Interaction character requirement is met.
3. Rule `ContextRequirements` evaluate true for the character and lot.
4. If the rule targets another character, a target is found through `GetPossibleCharactersForInteractionToInject`.
5. Interaction usability rules pass for the selected target.
6. Skin selection succeeds for skinned interactions.
7. The first action's item-finder params can be created.
8. Score modifiers produce a positive accumulated score.

The final picker sorts by score, examines the configured number of top rules, validates item-finder slots, then randomly picks among the best valid rules. This is a useful diagnostics hook because it explains why autonomy did not pick a rule before queue mutation happens.

## Interaction Injection Details

Autonomy ultimately queues work through `InteractionManager.InjectInteraction(AssetCharacter, InteractionToInject, AssetCharacter targetOtherCharacter = null, bool isIdleAutonomous = false, bool isForcedAutonomous = false, ulong skinGUID = 0, ulong lotGUID = 0)`.

`InteractionToInject` controls:

- `InjectedInteraction`
- `Priority`
- `TargetPosition`
- `TargetOtherCharacter`
- `InjectToFirstCharacterOnly`
- `InjectItemSlotAsTargetItem`
- `InjectOtherCharacterAsTargetCharacter`
- `OtherCharacterRequirements`

`InjectInteraction` resolves the target position, finds usable item slots for the first action, creates `NewInteractionValues`, marks `IsIdleAutonomous` / `IsForcedAutonomous`, optionally queues a drop-carried-character interaction first, and applies the selected priority:

- `CancelAllActions`
- `CancelCurrentAction`
- `AtTheEndOfInteractionQueue`
- `AfterCurrentAction`
- `AfterCurrentActionOrCancelCurrentActionIfCancellable`

Do not call `InjectInteraction` with `TargetOtherCharacter != None` and a null `targetOtherCharacter` expecting it to auto-pick a target. The decompiled method calls `GetPossibleCharactersForInteractionToInject` in that branch but discards the result. The idle-autonomy picker supplies the target explicitly, and `ParalivesAPI.Queues.TryInjectInteraction` lets callers pass `TargetCharacterGuid`.

## Action Chaining

Action chaining is a separate autonomy path. `UpdateCharacterActions.OnActionEnd` calls:

`AutonomyManager.FindTriggeringActionChainingAutonomy(actionUnit.GUID, characterAsset.GUID, actionsToIgnore)`

If it finds a matching `ActionChainingAutonomy`, it inserts `ActionToInject` immediately after the completed action in the same interaction. It can also preserve posture, skip social clusters, or retarget the interaction item to none or the last created item.

This is the clean hook for "after washing hands, dry hands" or "after creating an item, use that new item" behavior. It is not a queue-level autonomous interaction and should not be treated as a new social/idle decision.

## Town Autonomy Flow

`UpdateTownAutonomy.Update()` runs only when time is not paused, town autonomy is enabled, characters exist, and the player is not in the intro. It updates one character per frame using `_characterIndexToUpdate`.

`UpdateOneCharacter` flow:

1. Skip invalid characters and the current household.
2. Reset time-travel state if `LastUpdatedTownAutonomy` is in the future.
3. Respect `TownAutonomyWaitTime`.
4. Keep the current rule active while the destination lot is loaded and open when `CurrentTownAutonomyDurationIsWhileLotLoadedAndOpen` is true.
5. Reset town autonomy if a duration requirement no longer evaluates true.
6. Refresh `AssetLot.HasLotZone` by checking `ZoneManager`.
7. Score `MustRunIfNotAlreadyRunning` rules across lots first. These can override current behavior and cancel current interactions.
8. If not overridden, skip characters already walking, still inside `EndOfCurrentTownAutonomyAction`, or in a social group.
9. Check `TownAutonomyBaseRequirement`.
10. Score normal non-reference rules against all lots with zones and any required calendar tickets.
11. Score special fake-home and walk rules with no lot when in a town save.
12. Shuffle, sort by score, pick from `TownAutonomyPickFromTopChoices`, claim a calendar ticket if required, then call `AutonomyManager.InjectTownAutonomyToCharacter`.

Town rule scoring happens in `AutonomyManager.GetScoreForOneRuleAndLot`:

- Applies rule cooldowns.
- Checks rule character requirement.
- Checks target lot type.
- Starts with `BaseScore`.
- Applies referenced scored rules first, then local `ScoredRules`.
- `AutomaticallyFailIfFalse` blocks the rule.
- `AutomaticallyPickedIfTrue` forces score `1000`.

Suspicious cooldown detail: the decompiled cooldown check returns `-1` when `townAutonomyCooldown.Cooldown < ParaTime.TotalMinutes`. Since cooldown entries are stored as `current time + rule.Cooldown`, that appears to block expired cooldowns rather than active cooldowns. Verify in game before patching, but this is a strong candidate bug for a town-autonomy diagnostics/fix pass.

## Town Autonomy Injection And Walking

`AutonomyManager.InjectTownAutonomyToCharacter` is the main town trip start hook. It:

- Runs `ActionTiming.End` outcomes from the previous town rule when the rule changes.
- Runs `ActionTiming.Start` outcomes from the new rule.
- Resets forced occupation and current duration end time.
- Sets `CurrentTownAutonomyRule`, `CurrentTownAutonomyTargetLot`, duration flags, duration requirement, and walk path.
- Adds or updates `TownAutonomyCooldowns`.
- Sets `TownAutonomyDestinationState`:
  - `OnAWalkAroundTown` for `TownAutonomyTakeWalkRule`.
  - `HeadingOutOfTown` for fake-home rules.
  - `HeadingToALot` for lot destinations.
- Builds a path through `TownWaypointManager`.
- Switches to a rule outfit or specific outfit when configured.

The `onlyDoInjection` parameter is passed by lot-fill teleport code, but the decompiled method does not branch on it. Treat it as currently non-functional unless runtime testing proves otherwise.

`UpdateCharacterTownAutonomyWalk` advances NPCs along `CurrentTownAutonomyWalkPath`:

- Skips when paused, town autonomy disabled, no loaded game, navmesh not idle, or navmesh dirty.
- Processes all non-household NPCs with a current town rule and non-empty walk path.
- Skips social-group participants.
- If the visual unloads, stores interpolated `Data.Position`.
- If the visual loads, injects pathfinding using `TownAutonomyWalkInteraction`.
- If pathfinding is missing or stale, re-injects it.
- If an NPC is stuck more than two tries, warps to the nearest waypoint and resets town autonomy.
- On arrival, calls `AutonomyManager.SetTownAutonomyWalkDone`.

`SetTownAutonomyWalkDone` sets `EndOfCurrentTownAutonomyAction` from the work schedule or random rule duration, switches `HeadingOutOfTown` to `OutOfTown`, and otherwise returns the character to `Idle`.

## Townie Population

`UpdateSpawnTownies` is adjacent to autonomy but not the same system:

- `CheckToRefillTown` counts non-household, non-dead, non-dummy, loadable characters. If below `Townies.TargetTownPopulation`, it creates a new household and character from a premade.
- Patreon household members are created together by matching `PatreonKey`.
- `CheckToSpawnFirstTimePatreons` uses `CurrentSavedGame.Data.TimesToSpawnPatrons`, town entry waypoints, and patron premades.

This is the hook for population caps, household composition, spawn timing, or premade filtering. It should be coordinated with town autonomy because newly created NPCs need valid positions and eventually town rules.

## Lot Presence Teleporting

`AutonomyManager.CheckToFillLotByTeleportingNPCs` uses `LotType` settings to ensure minimum worker/visitor presence. It finds suitable unloaded NPCs, scores them with the target town rule, calls `InjectTownAutonomyToCharacter`, marks them as being in an impostor lot, and sets `WaitingToLoadInLot`.

Suspicious branch: `CheckHowManyNPCsAndTeleport` skips existing NPCs when `TownAutonomyDestinationState == Idle`, but `SetTownAutonomyWalkDone` sets destination state back to `Idle` on arrival. The log text in that branch also says "not idle" while checking for `Idle`. This may be a decompiler artifact, naming confusion, or a real overfill/counting bug; verify before fixing.

## Hook Point Candidates

| Goal | Best hook | Why |
| --- | --- | --- |
| Observe why idle autonomy did not choose an interaction | Postfix `AutonomyManager.GetAllPossibleScoredBasedAutonomousInteractions` | Has scored candidates and rejection categories before final item-slot validation. |
| Override or bias an idle choice | Postfix `AutonomyManager.GetWeightedRandomizedAutonomyRuleInteraction` | Single return point for mandatory/ordinary idle choice. Keep cooldown and item-finder behavior intact. |
| Add new idle/forced actions | Register `ActionUnit`/`InteractionUnit` through `ParalivesAPI.Interactions`, then append `ScoreBasedIdleAutonomyRule` or `ForcedAutonomyRule` to `Settings.Get<Autonomy>()` | Current API registers interactions but not autonomy rules. Use stable GUIDs and idempotent append/upsert logic. |
| Observe all queue injections | Prefix/postfix `InteractionManager.InjectInteraction` or use `ParalivesAPI.Queues` for explicit mod injections | Captures both autonomy and non-autonomy injections; guard against noisy recursion. |
| Observe completed autonomous actions | `ParalivesAPI.ActionCompletions.ActionCompleted` | Existing API event includes `IsFromAutonomy`. |
| Add action follow-ups | Append `ActionChainingAutonomy` or patch `FindTriggeringActionChainingAutonomy` | Keeps behavior inside the same interaction instead of queueing a separate autonomous interaction. |
| Observe town destination decisions | Prefix/postfix `AutonomyManager.InjectTownAutonomyToCharacter` | Central point after scoring and before/after state mutation. |
| Observe NPC arrival | Postfix `AutonomyManager.SetTownAutonomyWalkDone` | Central town-walk completion point. |
| Change town scoring | Prefix/postfix `AutonomyManager.GetScoreForOneRuleAndLot` | Lowest-risk scoring hook; avoid patching the full `UpdateTownAutonomy.UpdateOneCharacter` loop unless needed. |
| Add town rule content | Append `TownAutonomyRule` to `Settings.Get<Autonomy>().TownAutonomyRules` | Content-first approach; requires lot types, character requirements, calendar tickets, and outcomes to be valid. |
| Tune population | Patch `UpdateSpawnTownies.CheckToRefillTown` and `CheckToSpawnFirstTimePatreons` | Population is separate from town routing. |
| Fill lots with custom rules | Patch `AutonomyManager.CheckToFillLotByTeleportingNPCs` or lot-type settings | Good for venue worker/visitor mods; verify idle-state branch first. |

## Safety Notes For Mods

- Prefer content-driven rules before broad update-loop patches.
- Use `InteractionManager.InjectInteraction` or `ParalivesAPI.Queues.TryInjectInteraction`; do not manually append `AssetCharacterDataInteraction` unless reproducing native initialization.
- Do not inject every frame. Respect `IdleAutonomyCooldown`, `TimeLastTriedAutonomy`, rule cooldowns, and current queue state.
- Forced autonomy can leave social groups and cancel interactions. Patches here need household/NPC/social-group tests.
- Town autonomy is long-duration state. If a mod changes a destination, update rule, target lot, duration, destination state, walk path, and cooldown coherently.
- Calendar-event town rules consume tickets through `CalendarEventManager.ClaimTicketForEventAtLot`.
- Pathfinding hooks must handle unloaded NPCs, impostor lots, dirty navmesh, and `Vector3.zero` safeguards.
- Native reset/injection helpers do not consistently mark save dirty. Mark changed characters dirty in mod code when mutating saved autonomy fields.

## Recommended API Work

The next useful `ParalivesAPI` facade would be `ParalivesAutonomyFacade` with:

- Read-only snapshots of live autonomy state and town autonomy state per character.
- `AutonomyDecisionBuilding` or diagnostics event around idle rule scoring.
- `TownAutonomySelected`, `TownAutonomyStarted`, `TownAutonomyArrived`, and `TownAutonomyReset` events.
- Idempotent registration for `ForcedAutonomyRule`, `ScoreBasedIdleAutonomyRule`, `ActionChainingAutonomy`, `AutonomyTag`, and `TownAutonomyRule`.
- Safe helpers for common actions:
  - set or clear a town destination through native state helpers,
  - evaluate a rule score,
  - read active autonomy tags,
  - validate an interaction target and item-finder availability before injection.
- Compatibility diagnostics for suspicious native behavior:
  - forced duplicate injection returning from the whole update,
  - discarded target-character auto-selection in direct `InjectInteraction`,
  - town cooldown comparison,
  - lot-fill idle-state branch,
  - unused `onlyDoInjection` parameter.

## Good First Mod/Tool

Build an "Autonomy Debug Overlay" first:

1. Show selected character queue entries with `IsFromAutonomy`, current action, target item, target character, and lot.
2. Show active autonomy tags and idle cooldown.
3. Log the last idle-rule scoring result and rejection reasons.
4. Show NPC town rule, target lot, destination state, duration end time, and walk path length.
5. Add explicit buttons to cancel current autonomous interaction or reset town autonomy for one NPC.

This gives immediate visibility into whether autonomy is broken, merely under-contented, or blocked by requirements/item-finder/pathfinding state.
