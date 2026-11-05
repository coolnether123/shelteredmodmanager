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
Shuffle; this is reported as a skip, not silent success. This pass did not
expand that target manifest.

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

The exhaustive audit is unfinished. Remaining work includes the full RNG target
manifest and callers; all actor/effect, reflection and direct-call dependencies;
the rest of slot paging and scenario orchestration; and full loading-recovery
and input caller coverage. Declaration inventory or a passing build must not be
reported as completion of those traces.

## Focused regression checks

- `tools/Test-GameEventSessionContracts.ps1`: callback replacement and accepted,
  rejected and global operation boundaries.
- `tools/Test-GamePatchContracts.ps1`: home IL matches and drift warnings,
  scenario input/cancel guards, conversation restoration and deferred UI events.
- `tools/Test-SaveVerificationTimingContracts.ps1`: pending payload selection,
  busy/missing/invalid data, repeated custom reads and custom-to-vanilla loads.

## Required live checks

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
