# Paralives Save Lifecycle And Dirty State Research

> **Build/reference metadata**
> Research note created/reviewed: 2026-05-29.
> Game build represented: local Paralives managed assemblies from A:\SteamLibrary\steamapps\common\Paralives, DLL timestamps 2026-05-29 UTC.
> Assembly fingerprint: Assembly-CSharp.dll SHA256 885D46DF..., Paralives.dll SHA256 BEE83983..., Plugins.dll SHA256 311E9ED9.... Full hashes are in Decompiled/decompile-state.json.
> Metadata added: 2026-05-30.

Date reviewed: 2026-05-29

Scope: `Decompiled/Paralives.dll` save/load code and the current `ParalivesAPI` facade layer. This is reverse-engineered from the local decompiled build and should be treated as planning guidance, not a stable public game contract.

## Documentation Gap

The existing Paralives docs mention save-dirty behavior as a footgun and list a save facade as follow-up work, but there is no dedicated explanation of how Paralives decides what to write during a save. That matters for mods because some state is saved unconditionally, some state depends on per-asset dirty flags, and autosave is time-driven rather than dirty-driven.

## Source Map

| Area | Primary files |
| --- | --- |
| Save entry points | `SaveGameEvent`, `MessageSaveGame`, `AdvanceTime`, `Player.CanSave()` |
| Save request pipeline | `GameSavingManager`, `GameSavingRequest`, `GameSavingPhase` |
| Load request pipeline | `SavedGameManager`, `GameLoadingManager`, `GameLoadingRequest`, `GameLoadingPhase` |
| Base asset persistence | `AssetData`, `AssetJSONBase<T>`, `AssetPackage`, `AssetManager` |
| Save package data | `AssetSavedGame`, `AssetSavedGameData`, `AssetSavedGameStats` |
| Common child assets | `AssetHousehold`, `AssetCharacter`, `AssetLot`, town terrain/roads/perimeters |
| Current API wrappers | `ParalivesTimeFacade`, gameplay facades that manually set `AssetCharacter.IsSaveDirty` |

## Mental Model

Paralives saves are asset packages. The active saved game is an `AssetSavedGame` package, identified by `SavedGameManager.Instance.CurrentSavedGameGUID`.

There are several persistence buckets:

| Bucket | Storage | Save behavior |
| --- | --- | --- |
| Global saved-game data | `AssetSavedGame.Data`, serialized to the saved-game package import file | Written during every save request in `SaveGeneralData`. |
| Character data | `AssetCharacter.Data`, serialized to each character import file | All loaded, non-dummy, visual-loadable characters are forced dirty and saved every save request. |
| Character visuals | `AssetCharacter.Visual.Data` | Saved only when `character.Visual.IsSaveDirty` is true, because `AssetCharacter.TriggerSave()` checks that flag. |
| Household data | `AssetHousehold.Data` | Saved only if the household asset is dirty during the household asset pass. |
| Lot data | `AssetLot.Data` | Live lot data is fetched only for dirty lots, then dirty lot assets are saved in the town asset pass. |
| Town terrain/roads/perimeters | `AssetTown.Terrain`, `AssetTown.Roads`, `AssetTown.LotPerimeters` | Each child asset has its own dirty flag and fetch step. |
| Stats | `AssetSavedGameStats` under the save and household packages | Marked dirty by `AdvanceTime`; saved during asset passes. |

`AssetData.TriggerSave()` clears `IsSaveDirty`. Derived asset classes then write their JSON/import file or child data.

## Save Entry Points

Manual saves arrive through `SaveGameEvent.UpdateMessage(MessageSaveGame)`. Unless the message is forced, it calls `Player.CanSave()` and shows `Error_CannotSaveRightNow_<status>` when the save is blocked.

Autosave is driven from `AdvanceTime.Update()`:

- It runs only when `GeneralOptions.EnableAutoSave` is true.
- It requires no pending save or load request.
- It increments `SavedGameManager.Instance.AutoSaveTimer` by unscaled time.
- When the timer reaches `NewGame.AutoSaveInterval`, it calls `Player.CanSave()`.
- If saving is allowed, it creates an autosave request. If not, it subtracts 60 seconds and tries again later.

Important implication: dirty flags do not schedule autosaves. Autosave is interval-based. Marking an asset dirty only decides whether that asset is written once a save request happens.

## Save Pipeline

`GameSavingManager.CreateRequest(...)` immediately creates and starts a `GameSavingRequest`. The request then advances through `GameSavingPhase` values across frames.

High-level order:

1. `Init`: capture current town, UI state, lot list, save settings, and budgets.
2. `SaveLotImpostor`: iterate town lots and skip lots whose `IsSaveDirty` is false.
3. `SaveLotFetchItems`: for a dirty lot, collect live item state into manager data.
4. `SaveLotFetchData`: for a dirty lot, fetch lot data unless the lot is an impostor that should stay unloaded.
5. `SaveTownTerrain`: if terrain is dirty, copy computed terrain arrays back into saved terrain chunks.
6. `SaveTownLotPerimeters`: if lot perimeters are dirty, rebuild segment data.
7. `SaveTownRoads`: if roads are dirty, rebuild segment data.
8. `SaveCharacters`: force-save all loaded non-dummy characters that are not `DoNotLoadVisual`.
9. `SaveGeneralData`: write current time, latest instance ID, camera, playtime, special lot, save metadata, and `AssetSavedGame.Data`.
10. `SaveThumbnail`: generate and save the save thumbnail.
11. `SaveDirtyAssetsInGame`: save dirty assets directly under the active saved-game package.
12. `SaveDirtyAssetsInHousehold`: save dirty assets directly under the current household package.
13. `SaveDirtyAssetsInTown`: save dirty assets directly under the current town package.
14. Optional default-town copy, unload-unused-assets wait, and completion UI.

The asset passes call `AssetManager.GetAssetsInPackage(...)` and then `TriggerSave()` only for assets whose `IsSaveDirty` is true.

## Load Pipeline

`SavedGameManager.LoadGame(...)` unloads the current save, then registers `MessageLoadGame`. `GameLoadingManager` owns the asynchronous load request.

Important load phases:

- `LoadSave` sets `CurrentSavedGameGUID`, triggers the save package load process, restores `InstanceIDManager.LatestInstanceID`, and copies saved time into `ParaTime`.
- `LoadTown` restores camera data, loads current town data, special lots, terrain, roads, lot perimeters, and the lot list.
- Lot phases load structure, items, impostors, terrain, and navmesh.
- `LoadCharacters` waits for character visual work, clears social group runtime character lists, cancels cancellable queued interactions, processes cancellation/final outcomes for running actions, and unstucks invalid positions.
- `ShowGame` hides loading UI, starts tutorial handling when needed, switches `GameLoadingManager.State` to `State.Game`, and invokes the load callback.

Mods that touch managers such as `SavedGameManager`, `HouseholdManager`, `CharacterManager`, `LotManager`, or `Settings` should wait until the session is loaded and the game state is `State.Game`.

## Dirty-State Rules For Mods

Use the narrowest dirty flag that matches the data being changed.

| Mutation | Mark dirty |
| --- | --- |
| `AssetSavedGame.Data` global lists or fields | `SavedGameManager.Instance.CurrentSavedGame.IsSaveDirty = true` for convention and diagnostics, although `SaveGeneralData` writes it on every save request. |
| `ParaTime` runtime time state | Update `ParaTime`; saved-game `Data.TotalMinutes` is overwritten from `ParaTime` during save. |
| `AssetCharacter.Data` gameplay state | `character.IsSaveDirty = true`; current saves force-save loaded characters anyway, but wrappers should still mark it. |
| Character visual/genome/mesh/texture state | `character.Visual.IsSaveDirty = true`; often also mark `character.IsSaveDirty = true`. |
| Household members, money, inventory, collectibles, owned lots | `household.IsSaveDirty = true`. |
| Lot items, bills, mailbox letters on a lot, placement, item state | `lot.IsSaveDirty = true`; mark `lot.IsImpostorDirty = true` if the impostor preview must be regenerated. |
| Town package metadata | `TownManager.Instance.CurrentTown.IsSaveDirty = true`. |
| Terrain data | `TownManager.Instance.CurrentTown.Terrain.IsSaveDirty = true`. |
| Roads or lot perimeters | `CurrentTown.Roads.IsSaveDirty = true` or `CurrentTown.LotPerimeters.IsSaveDirty = true`. |
| New assets created through `AssetManager` | Ensure their data exists, write metadata, and mark the new asset dirty if runtime data changed after creation. |

Character state is forgiving because `GameSavingManager` force-saves most loaded characters. Lot, household, town, terrain, road, perimeter, and stats assets are not forgiving: if their dirty flag is false, the normal dirty-asset passes skip them.

## Current ParalivesAPI Coverage

The current `ParalivesAPI` facades often mark character data dirty after mutations. Examples include goal, occupation, memory, status, want, and character helpers. There is not yet a dedicated save facade.

Notable gaps:

- No public `ParalivesSaves` facade for current save identity, current town/household IDs, loaded state, or save scheduling.
- No shared `MarkDirty(...)` helper that maps game objects to the right asset dirty flag.
- No Paralives save lifecycle events equivalent to the Sheltered save docs.
- No wrapper around `MessageSaveGame` or `GameSavingManager.CreateRequest(...)`.
- No diagnostics that explain whether a save request will include a given asset.

## Native Footguns

- `CurrentSavedGame.IsSaveDirty` is not used to decide whether `AssetSavedGame.Data` is written during a save request. The data is written in `SaveGeneralData` regardless. Do not mistake this flag for autosave scheduling.
- Mutating `AssetSavedGame.Data.TotalMinutes` directly is usually wrong while a game is loaded; `SaveGeneralData` overwrites it from `ParaTime.TotalMinutes`.
- `CalendarEventManager.AddCalendarEventOnLot(...)` and `ClaimTicketForEventAtLot(...)` mutate `CurrentSavedGame.Data.CalendarEvents` but do not mark the save dirty in this build.
- `GoalsManager` writes request cooldowns into `CurrentSavedGame.Data.TakenRequests` without marking the save dirty, but save requests still write general data.
- `CollectSpawnedItemProcessor` and `RemoveSpawnedItemProcessor` add to `SpawnersCollectedToday` without marking the save dirty, relying on the general data save pass.
- `MailboxManager.ResetMailboxes()` clears `CurrentSavedGame.Data.Newspapers` but does not mark `CurrentSavedGame.IsSaveDirty` after that clear.
- Character visuals are separate from character data. Marking `character.IsSaveDirty` alone is not enough for visual data if `Visual.IsSaveDirty` remains false.
- Lot data is fetched from live objects only for dirty lots. If a mod changes live lot objects but does not dirty the lot, the save may preserve old lot data.
- `Player.CanSave()` blocks saves during loading, pending save, intro, item placement/rotation/resize/scale, and segment/platform placement. Mods should respect the same constraints.

## Recommended Facade Shape

A future `ParalivesSaves` facade should stay small and explicit:

```csharp
public sealed class ParalivesSaveFacade
{
    public bool HasLoadedSave { get; }
    public bool IsGameLoaded { get; }
    public ulong CurrentSaveGuid { get; }
    public ulong CurrentTownGuid { get; }
    public ulong CurrentHouseholdGuid { get; }

    public ParalivesSaveStatus GetSaveStatus();
    public bool TryRequestSave(bool force = false);
    public bool TryRequestSaveAndQuit();
    public bool TryRequestSaveAndReturnToMainMenu();

    public bool MarkCurrentSaveDirty();
    public bool MarkCharacterDirty(ulong characterGuid, bool includeVisual = false);
    public bool MarkHouseholdDirty(ulong householdGuid);
    public bool MarkLotDirty(ulong lotGuid, bool includeImpostor = false);
    public bool MarkCurrentTownDirty();
    public bool MarkTerrainDirty();
    public bool MarkRoadsDirty();
    public bool MarkLotPerimetersDirty();
}
```

Useful events:

- `BeforeSaveRequested`
- `SaveStarted`
- `SavePhaseChanged`
- `AfterSaveCompleted`
- `SaveFailed`
- `AfterLoadCompleted`
- `BeforeUnload`

These events should be backed by Harmony patches around `SaveGameEvent`, `GameSavingManager.UpdateRequest`, `GameLoadingManager.UpdateRequest`, and `SavedGameManager.UnloadCurrentGame`.

## Testing Checklist

For any mod or facade that mutates save-backed state:

1. Start a loaded save and mutate exactly one data bucket.
2. Mark the expected dirty flag.
3. Trigger a manual save through the normal save request path.
4. Reload the save and verify the data persisted.
5. Repeat with autosave if the mod expects autosave to capture it.
6. Test blocked save states, especially intro, pending load/save, and item placement.
7. For visual or lot changes, verify both the JSON/import data and the rendered state after reload.

## Practical Rule

If the data lives on a character, mark the character. If it lives on a household, lot, town child asset, or character visual, mark that exact asset. If it lives in `AssetSavedGame.Data`, mark the current save for clarity, but remember that a save request is still required and autosave is not dirty-triggered.
