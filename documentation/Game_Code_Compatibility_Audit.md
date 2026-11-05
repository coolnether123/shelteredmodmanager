# Game code compatibility audit

Source: supplied decompiled Epic game, Assembly-CSharp MVID
`D1A2944B-FD7A-4EA8-98C2-1C26197F6A3B`. Paths below are relative to
`ShelteredCode`. This records source evidence, not a claim of runtime validation.

The framework README defines the intended plugin, event, content, scenario and
save services. There is no root `About/About.json` in this checkout. Manager,
Cortex, Decompiler, Doorstop, other mods and game installations were excluded.
The previously completed persistence scenarios were not repeated in game.

## Confirmed defects and changes

| Area | Game code traced | Change |
| --- | --- | --- |
| Session subscriptions | `Core/GameTime.cs:Awake` clears static day delegates. `Managers/ExplorationManager.cs:StartManager` initializes a scene manager; `PartyHasReturned` invokes its instance event before disbanding the party. | Reattach day callbacks on each Awake and party callbacks when the manager changes. Avoid duplicate subscriptions and detach the previous manager. |
| Accepted save/load requests | `Managers/SaveManager.cs:StartSave`, `StartLoad`, `Update_SaveSceneContents`, `Update_LoadData`; `Core/FrontEndController.cs:Awake` and `UI/CustomisationPanel.cs` request global preference loads. | Reset slot bookkeeping only after an accepted slot request. Global preferences and rejected requests cannot disturb the active operation or suppress a later new-game event. The manifest save hook also ignores rejected requests. |
| Preference saves | `Managers/SaveManager.cs:SaveGlobalData` calls `StartSave(GlobalData)`, which serializes only `SaveGlobal`. Callers include settings close and front-end initialization. Actual shelter saves use `SaveToCurrentSlot`. | Remove the extra patch that serialized scene saveables and overwrote the active custom save during a preference save, before vanilla's busy guard. Preserve normal save-proxy routing. |
| Load verification | `Managers/SaveManager.cs:StartLoad` leaves `m_data` unchanged. `Update_LoadData` retrieves platform bytes and only then replaces `m_data` and starts the scene transition. `Platform/PlatformSave_PC.cs:PlatformGetLoadedData` retains its buffer until the next load. | Verify the pending platform payload rather than the previous `m_data`. Retain the custom payload for vanilla's subsequent read, and clear it at the next load request. |
| Panel lifecycle | `Managers/UIPanelManager.cs:PushPanel`, `PushPanel_Delayed`, both `PopPanel` overloads, `PopPanel_Delayed`, `ClosePanel`; `UI/CustomisationPanel.cs:OnResume`. | Publish open after deferred initialization, OnShow and Refresh. Publish close through the shared actual close path. Canceled queued pushes publish neither event. Cover the customization resume override that does not call base. |
| Scenario cancellation | `UI/GameModeSelectionPanel.cs:OnSurvivalModeChosen`, `UI/SlotSelectionPanel.cs:OnCancel`, `UI/CustomisationPanel.cs:OnCancel`. | Honor input, busy and page guards before clearing pending scenario state or queued saves. Preserve state when the slot archive handles cancel without running vanilla. |
| Random conversation | `Character/FamilyMember.cs:RandomIdle`, `SayRandomComment`, `GetRandomIllnessSpeech`, `GetRandomBantzSpeech`. RandomIdle owns the random draw and conversation/cutscene checks. The illness fallback sets both participants' flags even if bantz returns empty, while RandomIdle schedules no response for empty speech. | Remove the extra RandomIdle interception. Handle authored chatter at SayRandomComment, after vanilla eligibility checks. Restore prior participant state when suppressed empty fallback speech would leave them stuck. |
| Home coordinates | `Expeditions/ExpeditionMap.cs:CreateMap`, `WorldPosToGridRef`, both near-shelter placement methods, `FindMapRegionForQuest`, `GetPossibleFactionZoneLocations`; `Expeditions/ExplorationParty.cs:SaveLoad`, `UpdateRegionEntry`; `Managers/FactionMan.cs:GrowFactionZone`. The coordinate converter is also used for arbitrary locations and faction candidates. | Remove the global near-zero coordinate remap. Rewrite only the traced literal-origin home reads. Leave the second faction conversion, which handles a candidate location, intact. |
| Map clearance and abandoned shelters | `Expeditions/ExpeditionMap.cs:FindClearSpace`, its building-placement callers, `PlaceShelters`, `BuildMap`. FindClearSpace measures the home exclusion from the grid center. PlaceShelters generates separate searchable abandoned shelters. | Use the registered home for the exclusion center. Run vanilla abandoned-shelter generation and then stamp the registered home, instead of replacing abandoned shelters with home-neighbor cells. |
| Item visibility | `UI/ItemButtonBase.cs:SetLocked`, `SetHidden`; overrides in `UI/ItemButtonBase_Transfer.cs`; `UI/ItemGrid.cs:UpdateItems` initializes, populates, hides and locks the same buttons. | Compute custom icon visibility from both current flags when either changes. Clearing one flag cannot reveal an icon still concealed by the other. |
| Nested character RNG targets | `Character/CharacterMeshOptions.cs:CharacterMeshType.GetRandomTexture(CharacterMesh.TextureType)`, `CharacterPreset.Randomize_DontCallThisOutsideOfCharacterMeshOptions()`, `QuestCharacterPreset.Randomize_DontCallThisOutsideOfCharacterMeshOptions()`, `GetRandomStasisMutantCharacterPreset()`; callers `Character/CharacterMesh.cs:RandomizeTextures`, `Managers/NpcVisitManager.cs:SpawnNpc`, `Managers/RelocationManager.cs:SpawnNewFamilyMembers`, `Family/FamilySpawner.cs:SetUpStasisMutant`. | Target the two actual nested declaring types instead of the outer type. Keep their draws in the characters domain. The quest override delegates to the patched base method. |
| Failed transition teardown | `UI/LoadingScreen.cs:ShowLoadingScreen(string)`, `HideLoadingScreen(bool)`, `OnClearComplete`, `ClearNextLevel`; `Core/LoadingLevel.cs:Awake`, `Update`; `Scene/ScreenFade.cs:ClearFade(float,bool)`, `Update`; `Core/FrontEndController.cs:Awake`. | Release managed/direct transition ownership and clear the persistent black fade. Cancel the next-level request and show flag before the immediate fade callback. Establish ownership before ShowLoadingScreen can throw and invoke recovery. |
| Encounter replacement | `Encounters/EncounterCharacter.cs:Setup(PartyMember)`, `SetupFromNPCVisitor(NpcVisitor)`, `SetName`, `SetStats(BaseStats)`, `Health`, `maxHealth`; `Character/BaseStats.cs:Initialize`, `CopyOther`; `Character/BaseStat.cs:SetInitialLevel`. | Write the actual name and maximum-health fields, copy five stats through SetStats(BaseStats), and preserve baseline health semantics without recording replacement health as combat damage. Snapshot live family/visitor stats instead of supplying zeros. Clamp stat table indices. |
| Slot-load state | `UI/SaveSlotButton.cs:OnClick`; `UI/SlotSelectionPanel.cs:OnSlotChosen`, `OnSaveLoaded`, `TriggerHatchAnim`, `OnTweenFinished`; `Managers/SaveManager.cs:SetSlotToLoad`. | Apply vanilla input/deletion guards before every page route. Custom and mirrored loads now set the chosen slot, disable input, and retain the load result as vanilla does. A second click cannot replace pending load context. |
| Persistent saveable registration | `Managers/SaveManager.cs:Awake`, `ClearAll`, `SetCurrentSlot`, `SetSlotToLoad`, `LoadFromCurrentSlot`, `RegisterSaveable`, `Update_Transitioning`, `Update_LoadSceneContents`; `Core/GameTime.cs:Awake`. | Re-register actors, characters, persistent collections and the data proxy in each shelter scene. ClearAll removes them while their static registration flags survive. Register once at scene startup, not every frame, because registration during scene loading can requeue a processed saveable. |
| Character save reconstruction | The same `GameTime.Awake` and `SaveManager.Update_Transitioning` / `Update_LoadSceneContents` ordering above invokes session reset before restoring ISaveable data. | Clear synthetic characters between shelters so a matching persistence key cannot retain another slot's state. Use the session-start callback once instead of clearing again on new-game notification. Preserve zero health and all five stat levels in the internal save DTO, with optional added fields for older saves. |
| Effect delivery and timing | `Character/BaseCharacter.cs:Update`, `Damage(int,DamageType,string,bool,bool)`, `Heal(int)`, `SetMaxHealth(int)`; `Character/FamilyMember.cs:OnFatalDamageTaken`, `CanDieNow`; `Core/GameTime.cs:Update`. | Synchronize live health after effect ticks and immediately after application/removal. Use native damage/healing so unconsciousness, pending death, tutorial and death callbacks remain intact. Read back the resulting health, and prevent implicit resurrection. Tick active synthetic effects/attributes too. Stop automatic effects during pause, loading, saving and cutscenes. |
| Paging and delete failure | `UI/SlotSelectionPanel.cs:OnShow`, `OnSlotChosen`, `OnDeleteMessageBox`, `PromptDeleteCurrentSlot`, `OnSaveLoaded`, `TriggerHatchAnim`; `UI/SaveSlotButton.cs:OnClick`. The vanilla delete callback uses the visible row plus one without a custom-page mapping. | Stop custom-delete exceptions from falling through to a different vanilla save. Report the preserved save. Keep page/sort controls fixed while input is disabled or a load/delete is active. Snapshot loads retain chosen-row and loading-result state too. |
| Actor reconstruction timing | `Core/GameTime.cs:Update`, `SaveLoad`; `Managers/FamilyManager.cs:IsReadyForLoad`, `SaveLoad`; `Character/FamilyMember.cs:IsReadyForLoad`; `Character/NpcVisitor.cs:IsReadyForLoad`; `Managers/SaveManager.cs:Update_LoadSceneContents`. A Harmony postfix still executes after an early vanilla return, and the clock can restore before family/visitor objects finish loading. | Defer automatic actor snapshots, simulation and adapters while loading, saving, paused or in a cutscene. Explicit Tick calls retain their existing behavior. |
| Journal session duplicate cache | `Managers/JournalManager.cs:Awake`, `StartManager`, `InsertJournalEntry(string,string,bool)`, `GetEntryText`, `SaveLoad`; scenario schedule dispatch reaches ScheduledJournalRuntimeService.Handle. The scene journal owns its entry list while the mod's minute-key set was static. | Reset minute-key reservations when the journal instance changes. Same-session retries remain deduplicated; a new shelter can write the same opening entry at the same game minute. |
| Registered mod data on New Game | `UI/SlotSelectionPanel.cs:OnSlotChosen`, `ShowCustomizationScreen`; `UI/CustomisationPanel.cs:OnMessageBoxClosed`; `UI/MainMenuPanel.cs:OnMessageBoxClosed`, `Update`; `Managers/SaveManager.cs:SetCurrentSlot`, `ClearAll`, `SetSlotToLoad`; `Core/GameTime.cs:Awake`. New Game selects an empty slot and loads a shelter without StartLoad. Returning to the menu saves, resumes and changes scenes without clearing registered mod objects. | Restore each registered object's captured defaults in place before New Game callbacks. Clear the prepared-load key, callback state and shutdown buffer. Preserve callback initialization and existing save files. One call at the start of PluginManager.OnNewGame is required to precede its mod callback fanout. |
| Scheduled visitor group size | `Managers/NpcVisitManager.cs:UpdateManager`, `UpdatePendingSpawns`, both `SpawnNpcs` overloads, `CreateNpcVisitor`, `SpawnInfo.SaveLoadSpawn`; `Family/FamilySpawner.cs:CharacterAttributes.Randomize`. The pending path derives quantity from npcAttributes.Count and still creates a leader for an empty list. | Supply a randomized attribute record for each unauthored visitor, with a nonempty mesh ID for pending-spawn save/load. Keep authored overrides. Requested groups no longer collapse to one visitor. |

## Transpiler checks

The new home-origin matcher requires exactly one coordinate-conversion call,
or two in GrowFactionZone, with the first immediately preceded by `Vector2.zero`
or two zero floats and `Vector2(float,float)`. It rewrites only the conversion
call, with the same inputs and output. Missing, extra and nonzero shapes emit a
warning and retain the original method. FindClearSpace requires exactly one
`GridRef(int,int)` constructor and retains instruction labels and exception
metadata when replacing it with an equally shaped static call.

Existing home-expedition transpilers redirect the exact
`GameModeManager.shelterMapWorldPosition` getter in CalculateRouteDistance,
SetRoute, OpenGroundEncounterCheck and UpdateMapSymbols. They warn when absent.
LoadingLevel.Update diagnostics wrap the three calls visible in its source:
ContinueTransition, LoadSceneAsync(string) and ClearNextLevel. FluentTranspiler
records unmatched required calls. RNG redirection uses typed MethodInfo matches
and validates replacement signatures; its installation precheck reports missing
methods and absent redirectable RNG calls. A method containing only generic
Shuffle calls is excluded by that precheck even though the transpiler supports
Shuffle; this is reported as a skip, not silent success. The continuation
corrected two nested declaring types. It did not establish the exact instruction
sequence for every RNG target.

The supplied C# does not preserve every original IL instruction. Synthetic
instruction contracts check matcher acceptance, rejection and metadata handling;
they do not establish that Steam/GOG/Epic binaries have the tested instruction
sequence or that runtime inlining leaves every target reachable.

## Coverage and remaining work

An attribute inventory found 175 distinct type/member/patch-file entries across
49 type names, including the framework's own ModManagerPanel. Every other
inventoried type/member name has a declaration in the supplied game source.
This inventory does not prove overloads, callers, private-member lifetimes or
the manually installed RNG manifest.

Detailed source traces were completed for the defects above. Additional sampled
checks covered inventory initialization and AddExistingItem, enum-based storage,
recycling and trading panels, private player-queue fields and JobQueue/Job save
reconstruction, faction event fields, party return ordering, and future-survivor
adoption before deferred destruction of NpcVisitor. No additional defect was
established by those samples.

### Continuation coverage

The continuation checked declarations for all 178 named RNG targets across 59
manifest rows. These are type/member pairs, not a count of exact overloads or
patched methods. Detailed declaring-type and caller traces covered the three
CharacterMeshOptions declaring types above. Additional overload samples covered
the four overloads of each DiceRoll die, EncounterCharacter.SetAppearance
wrappers, and the two SpawnNpcs overloads.
Wrapper overloads that delegate the draw and contain no direct RNG call are
correctly skipped. The installer uses DeclaredOnly and therefore could not
reach the two nested character methods through the original outer-type entry.
The typed Range/value/InitState call substitutions and generic Shuffle bridge
were also inspected against `Utility/ExtensionMethods.cs:Shuffle<T>`.
WeatherManager.StartManager() and ChooseNewRandomWeather() have direct Range
draws. LoadingManager starts the manager; WeatherManager.UpdateManager() is
the later caller of ChooseNewRandomWeather. Both initialization and the later
timer path retain native rain restrictions and ActivateWeather side effects.

Actor/character traces covered the five BaseStats levels, encounter replacement,
live family/visitor snapshots, GameTime registration/ticking, and ISaveable
registration and reconstruction after slot changes. Effect tracing followed
BasicCharacterEffects application, removal, tick and serialization through
LiveCharacterProxy refresh/synchronization, plus the previously omitted
synthetic tick path. Health changes now reach BaseCharacter's actual damage and
heal operations. FamilyMember's fatal-damage override can retain one health and
queue unconsciousness; its CanDieNow also waits for an empty job queue. The
bridge reads back that native result. The PartyHelper private
fields for family statistics and expedition resources exist with the expected
types; their update/save sites were located in `Character/FamilyMember.cs` and
`Expeditions/ExplorationParty.cs`. The actor registry's automatic update now
waits for reconstruction to finish. FamilyManager waits for ObjectManager,
spawns members and restores their IDs; each FamilyMember then waits for
InteractionManager, and visitors wait for ObjectManager or BreachMan according
to their type. Actor import can precede those objects because its saveable is
immediately ready. Its live reconciliation must not run in the intervening
frames. This is not full actor/effect coverage.

Scheduled weather reflection resolves one private ActivateWeather(WeatherState)
method. Scheduled journal reflection resolves one private
InsertJournalEntry(string,string,bool) method; its native path updates the
entry count, retention and onJournalUpdated callback. The scenario schedule
coordinator reaches both through ScenarioEffectDispatcher.Dispatch. Journal
save/load owns the entry list, and a scene creates a fresh manager. The static
duplicate cache now follows that manager.

World-event traces covered NpcVisitManager.UpdateManager -> UpdatePendingSpawns
-> SpawnNpcs(NpcType,List<CharacterAttributes>,out List<NpcVisitor>) ->
SpawnNpcs(NpcType,List<CharacterAttributes>,int,out List<NpcVisitor>) ->
CreateNpcVisitor, plus SpawnInfo.SaveLoadSpawn. The private pending list, nested
SpawnInfo type and its public fields match the reflection flags. An empty
attribute list still spawns a leader but supplies quantity zero, so unauthored
visits previously ignored count. Each requested visitor now has an independent,
randomized, saveable attribute record.

The same manager calls UpdateSurvivial or UpdateStasis after processing authored
pending/ongoing spawns. Suppression skips the random visitors but preserves the
separate binman update when enabled. BreachMan.CanStartBreach() has one protected
overload with the party-return, call-in, cutscene and departing-family guards.
StartBreach() selects difficulty and enters WaitingToStart; the authored raid
then changes difficulty before EnterStage_MainPhase consumes it. Finished-stage
raid suppression leaves active breach stages running. Obj_Radio's two broadcast
methods set visitor chances/frequency and m_broadcasting; StopBroadcasting
restores defaults. Authored radio dispatch bypasses suppression, while ordinary
suppressed broadcasts retain their UI state. This does not cover all scheduled
effect handlers or their save/load ordering.

Slot traces covered selection input, deletion guards, pending custom/mirrored
and snapshot load state, native load completion and hatch animation. Delete
traces covered prompt/click intent capture, page/row/save-id validation, archive
snapshot deletion and failure handling. Paging traces covered arrow and button
entry points, ChangePage, ToggleSnapshotSort and AutoLoadFlow.TryMoveToPage;
the automated caller checks input readiness before moving pages. Scenario traces covered
direct-launch ownership before ShowLoadingScreen, managed completion conditions,
failure recovery ownership, and persistent fade teardown. Recovery reflection
names were checked against SaveManager, LoadingLevel, LoadingScreen, MainMenu
and UIPanelManager. The recovery SetState lookup has one matching private
SaveState overload. Its queued-panel lists and input flags exist.

Input traces covered the typed PlatformInput_PC overloads and PlatformInput
wrappers, the UICamera Update -> ProcessTouches -> ProcessMouse and
Update -> ProcessOthers paths, mHover state and drag/press cleanup, and the
ExpeditionMainPanelNew.MapScreen field used by touchpad routing. A source-wide
member search found no caller for UICamera.ProcessFakeTouches or the static
PlatformInput_PC.GetAnyInput; their declaration alone is not evidence of a live
patch path. No input change was made.

The loading caller inventory includes FrontEndController, LoadingManager,
SaveManager, CutsceneManager, PlatformEvents, customization, slot selection,
main-menu and game-over panels. Their ShowLoadingScreen/HideLoadingScreen
caller bodies were inspected, including save completion before menu exit,
game-over continuation guards, sign-out pause cleanup, customization's pending
family/pet setup, and CutsceneManager's two hide paths. LoadingManager.Awake
only hides the standard shelter's new-game transition; SaveManager and cutscene
paths complete other loads. Detailed teardown traces cover LoadingLevel,
LoadingScreen, ScreenFade, the save transition/load-completion state machine,
and recovery panel restoration. BaseCharacter.Update/FixedUpdate and
PauseManager.Pause consume isShowing to prevent simulation/input during loads.

Registered mod persistence now has a complete New Game versus load trace.
SlotSelectionPanel selects empty slots with SetCurrentSlot, which only clears
vanilla save queues and changes the slot enum. CustomisationPanel queues family
and pet spawning, then requests a shelter scene directly. Existing saves instead
call SetSlotToLoad -> StartLoad. MainMenuPanel waits for saving before resuming
and loading MenuScene; it does not reset registered mod objects. GameTime.Awake
initializes the scene clock and registers it, and the lifecycle postfix raises
New Game outside the load pipeline. PluginManager subscribes before plugins
register their data, so subscribing a later reset would erase their New Game
initialization. The reset therefore runs at the start of PluginManager.OnNewGame,
with its implementation in SaveSystemImpl. This is the only edit to the
coordinated dispatch files.

### Remaining work

The exhaustive audit remains unfinished. Do not repeat the detailed traces
above or the original pass's traced paths when continuing.

- Complete exact overload, draw-site and caller traces for the RNG manifest
  outside the character, DiceRoll and wrapper cases listed above. Declaration
  presence does not establish reachability or inlining behavior.
- Complete actor/effect dependencies beyond the paths above, including live
  stat/appearance mutation propagation and reconstruction order relative to
  vanilla family/visitor saveables. The live bridge now transfers health and
  names from effect callbacks; public stat and appearance setters still have
  no corresponding native write in SyncToGameObject. Direct Data edits outside
  callbacks also need an explicit mutation/refresh contract.
- Complete the remaining reflection/direct-call inventory in ShelteredAPI and
  ModAPI, especially scenario definition building, projection and runtime
  gateways. Check each lookup's type, overload, lifetime and actual caller.
- Complete archive layout, auxiliary controls and navigation paths beyond the
  coordinator/page actions above, and the rest of scenario orchestration beyond
  selection/ownership recovery.
- Complete input action, keybinding, capture and touchpad callers, and recovery
  callbacks beyond the source traces above. The unused input members above
  cannot substitute for tracing live consumers.
- Verify exact binary IL and runtime patch reachability for every transpiler.
  C# source and synthetic instruction fixtures cannot prove binary instruction
  sequences or runtime inlining.

## Focused regression checks

- `tools/Test-GameEventSessionContracts.ps1`: callback replacement and accepted,
  rejected and global operation boundaries.
- `tools/Test-GamePatchContracts.ps1`: home IL matches and drift warnings,
  scenario input/cancel guards, conversation restoration and deferred UI events.
- `tools/Test-SaveVerificationTimingContracts.ps1`: pending payload selection,
  busy/missing/invalid data, repeated custom reads and custom-to-vanilla loads.
- `tools/Test-GameAuditContinuationContracts.ps1`: nested RNG target resolution,
  failure ownership/fade cleanup, encounter field/stat transfer, slot input
  state, scene saveable registration, and synthetic character reconstruction.
  It also executes the live effect/health bridge, synthetic updates, pause/load
  guards, custom-delete failure path, page/sort guards, actor update gates and
  journal reservation lifetime. Visitor attribute checks cover unauthored
  records, independent group members and authored overrides.
- `tools/Test-ModPersistenceContracts.ps1`: default restoration across New Game,
  callback initialization order, in-place arrays, separate mod registries,
  shutdown-cache invalidation, first-save isolation, previous-save preservation
  and clearing the prepared-load key for a repeated context.

## Continuation verification and live checks

The final Release solution build passed with exit 0, 0 error lines and 3,689
warning lines. The command was MSBuild ShelteredModManager.sln /restore
/p:Configuration=Release /m:1 /v:minimal. All nine check scripts passed with
exit 0 on the final source candidate:

| Check | Result |
| --- | --- |
| Test-ShelteredApiContracts | Passed; no aggregate count reported |
| Verify-ShelteredApiPublicSurface | 333/333 baseline entries |
| Verify-ModApiBoundary | 0 findings; baseline 0 |
| Test-SaveBackupSafety | Retention, pin, deduplication, GC, codec and safety passed; no aggregate count reported |
| Test-SaveRestoreCrashSafety | Crash-safety and fail-closed checks passed; no aggregate count reported |
| Test-ShelteredBenchmarkContracts | 38/38 |
| Test-ModPersistenceContracts | 19/19 plus New Game dispatch order 1/1 |
| Test-GamePatchContracts | 21/21 |
| Test-GameAuditContinuationContracts | 67/67 |

Full output is retained locally in Log/build-release-continuation.log,
Log/full-gates-continuation.log and the per-script continuation logs. Execution
used the required build queue for this session; the requested direct-shell
execution route was not met. These source and fixture checks do not establish
runtime patch reachability, binary IL compatibility or successful live gameplay.

Live checks needed on Steam and Epic for the continuation fixes:

1. Repeat a fixed-seed scenario and compare randomized textures, presets and
   stasis mutants across launches.
2. Force a failed direct scenario transition. Confirm the menu is visible and
   accepts input, the fade clears, and a subsequent launch succeeds.
3. Replace an encounter character. Confirm names, baseline health and five
   stats match the replacement without generating combat-damage side effects.
4. Load custom, mirrored and archive rows. Try rapid repeated clicks, paging
   and sorting during loading. Confirm one load and the correct hatch row.
   Force a custom-delete failure and verify both the custom save and vanilla
   slots 1 through 3 remain intact.
5. Save mod collections, data proxies, actors and characters after returning
   to the menu, switching slots and relocating. Reload and verify their data.
6. Load two shelters with the same synthetic persistence key. Confirm isolation,
   zero health and five stat levels, and check an older character save.
7. Apply, tick and remove live effects. Confirm native damage, unconsciousness,
   pending death and healing behavior. Check synthetic effect expiry and that
   automatic effects and actor adapters wait during pause, saves, loading and
   cutscenes, then resume after reconstruction.
8. Start the same scenario twice through the menu. Confirm its opening journal
   entry appears in both shelters and same-session retries remain deduplicated.
9. Modify registered data in shelter A, return to the menu and start shelter B.
   Confirm New Game callbacks see defaults and can initialize them. Save and
   reload B, then reload A; each must retain its own data.
10. Schedule three unauthored visitors. Confirm three spawn, including after
    saving and loading during the delay. Check authored visitor overrides too.

The previously reported live success of all 17 release scenarios and all 17
plugins applies to the original pass. It does not validate these new fixes.

## Original pass verification and live checks

The Release solution build passed with exit 0, 0 error lines and 2,346 warning
lines. All six required check scripts passed with exit 0: API contracts, public
surface (333/333), ModAPI boundary (0 findings), save backup safety, restore crash
safety and benchmark contracts (38/38). Additional suites passed with exit 0:
persistence (11/11), event sessions (10/10), game patches (21/21) and save
verification timing (7/7). Full output is retained locally under `Log/`.

An earlier restore crash-safety attempt timed out waiting for a fault-boundary
child process. Its focused retry and the final suite both passed. A failed
intermediate build exposed protected-field and instance-property accesses;
those were corrected before the final build and test results above.

1. Start a game after preferences load. Confirm one new-game event. Return to
   menu, load another shelter, advance a day and return a party. Confirm one
   day/party callback per event in both sessions.
2. Reject save/load requests while busy. Confirm the active operation keeps its
   context. Change settings while a custom save is active and in the menu.
   Confirm only preferences change and the shelter save remains intact.
3. Load saves with different embedded mod manifests consecutively. Confirm each
   validation dialog describes the requested save. Cancel one, load another,
   then switch between custom and vanilla saves.
4. Queue and cancel a panel push in one frame, open/close normal and top panels,
   and resume customization. Confirm callbacks reflect completed transitions.
5. Cancel while customization ignores input, on a later page, while slot data
   is loading, and inside the backup archive. Confirm queued scenario state and
   saves survive until the actual flow exits.
6. Exercise authored chatter and separately suppressed illness/bantz. Confirm
   native timing and cutscene guards hold and neither participant remains stuck.
7. Generate an off-center home. Check arbitrary center-region lookups, starting
   buildings, home clearance, quest/faction placement and legacy party loads.
   Confirm abandoned shelters still exist and remain searchable. Save, reload
   and return an expedition to the registered home.
8. Toggle hidden and locked flags in both orders for custom item buttons.
   Confirm the icon appears only after both flags are cleared.

Run these on Steam/GOG and Epic. No game launch or deployment was performed by
this source audit.
