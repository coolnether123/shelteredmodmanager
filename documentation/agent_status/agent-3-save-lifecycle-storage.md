# Agent 3 Save Lifecycle And Storage Status

Last updated: 2026-05-29

Branch: `generalize-smm-manager`

`AGENTS.md`: not present in the repository root when checked.

## Scope

Paralives save lifecycle facade and save-scoped mod storage. Interaction, action, character, occupation, UI, API-contract, and documentation ownership were left to their assigned agents except for the minimal bootstrap/project-file integration needed for this work to load.

## What Changed

- Added `ParalivesGameLifecycleFacade` with safe save/session properties:
  - `IsGameLoaded`
  - `CurrentSaveGuid`
  - `CurrentSaveKey`
  - `CurrentTownGuid`
  - `CurrentHouseholdGuid`
- Added typed lifecycle events:
  - `SaveLoading`
  - `SaveLoaded`
  - `SaveSaving`
  - `SaveSaved`
  - `SaveUnloading`
- Implemented `IGameLifecycleSource` on the lifecycle facade so ModAPI neutral persistence receives:
  - `BeforeLoadSceneContents` from `SaveLoading`
  - `AfterLoad` from `SaveLoaded`
  - `BeforeSave` from `SaveSaving`
  - `SessionStarted` from `SaveLoaded`
  - `NewGame` from `SaveLoaded` when the native load request is from a new game
- Added `ParalivesSaveStorageFacade` and `IParalivesSaveStorage` for save-scoped JSON state.
- Implemented `ISaveRuntimeAdapter` on `ParalivesSaveStorageFacade` so existing `ctx.SaveSystem` gets a Paralives save context.
- Registered lifecycle/storage services from `ParalivesRuntimeBootstrap`.
- Added a governed Harmony patch host for save lifecycle seams.
- Updated `ParalivesAPI.csproj` only because this old-style project requires explicit `<Compile>` entries for new source files.

## Files Touched

- `ParalivesAPI/Core/ParalivesGameLifecycleFacade.cs`
- `ParalivesAPI/Core/ParalivesSaveLifecycleEvents.cs`
- `ParalivesAPI/Core/ParalivesSaveStorageFacade.cs`
- `ParalivesAPI/Core/ParalivesRuntimeBootstrap.cs`
- `ParalivesAPI/Patches/ParalivesSaveLifecycleHooksPatch.cs`
- `ParalivesAPI/ParalivesAPI.csproj`
- `documentation/agent_status/agent-3-save-lifecycle-storage.md`

`ParalivesAPI.csproj` is concurrently edited by other agents. This agent added only the compile entries for the three save lifecycle/storage files and the save lifecycle patch host.

## Lifecycle Seams Used

- `GameLoadingManager.CreateRequest(ulong, Action, bool, bool)` prefix: publishes `SaveLoading` and sets the pending save key before load-context consumers run.
- `GameLoadingManager.UpdateRequest(GameLoadingRequest)` prefix/postfix: detects transition from `ShowGame` to `IsDone` and publishes `SaveLoaded`.
- `GameSavingManager.CreateRequest(bool, bool, bool, bool)` prefix: publishes `SaveSaving` before the save request starts writing native save data.
- `GameSavingManager.UpdateRequest(GameSavingRequest)` prefix/postfix: detects transition into `Completed` and publishes `SaveSaved`.
- `SavedGameManager.UnloadCurrentGame()` prefix: publishes `SaveUnloading` while the current save identity is still available.

The patch host uses `PatchDomain.SaveFlow` and `StartupTiming = BootCritical` because Agent 2's current governance path applies only boot-critical runtime patches during Paralives bootstrap; no deferred save-flow trigger exists yet.

## Save Storage Path

Direct `ParalivesSaveStorageFacade` writes:

```text
<GameRoot>/SMM/SaveState/paralives/<save-key>/<mod-id>/<name>.json
```

The neutral `ctx.SaveSystem` path uses the same save root through `ISaveRuntimeAdapter`, so its existing nested layout becomes:

```text
<GameRoot>/SMM/SaveState/paralives/<save-key>/mods/<ModId>/data.json
```

The storage facade sanitizes mod IDs and file names as path segments, writes through a temp file, and keeps a `.bak` backup when replacing existing JSON.

## Raw Game Seams Inspected

- `Decompiled/Paralives.dll/SavedGameManager.cs`
- `Decompiled/Paralives.dll/GameLoadingManager.cs`
- `Decompiled/Paralives.dll/GameSavingManager.cs`
- `Decompiled/Paralives.dll/AssetSavedGameData.cs`
- `Decompiled/Paralives.dll/GameLoadingRequest.cs`
- `Decompiled/Paralives.dll/GameLoadingPhase.cs`
- `Decompiled/Paralives.dll/GameSavingRequest.cs`
- `Decompiled/Paralives.dll/GameSavingPhase.cs`
- `Decompiled/Paralives.dll/RequestManagerBase.cs`
- `Decompiled/Paralives.dll/SaveGameEvent.cs`
- `Decompiled/Paralives.dll/LoadGameEvent.cs`
- `Decompiled/Paralives.dll/UnloadGameEvent.cs`
- `Decompiled/Paralives.dll/MessageLoadGame.cs`
- `Decompiled/Paralives.dll/MessageSaveGame.cs`

## Assumptions

- `SavedGameManager.CurrentSavedGameGUID` is the stable save identity for the current decompiled build.
- A decimal GUID string is sufficient as the initial save key.
- `SavedGameManager.UnloadCurrentGame()` is the narrowest reliable unload seam because it runs before `CurrentSavedGameGUID` is cleared.
- `GameSavingManager.CreateRequest(...)` represents an accepted save request; blocked manual saves return earlier in `SaveGameEvent.UpdateMessage(...)`.
- Storage should not write gameplay state into `mods/<Mod>/Config` by default.
- Direct `ParalivesRuntimeInfo` / `ParalivesGameFacade` properties were not added in this pass to avoid colliding with Agent 1's API-contract ownership; services are exposed through static `Current` singletons and `ModAPIRegistry`.

## Risks

- If a native load request fails before `SaveLoaded`, the pending storage save key may remain set until another load/current-save transition updates it.
- `SaveSaved` is based on the request phase entering `Completed`, not on a lower-level filesystem completion callback.
- `SaveSaving` runs at request creation, before later phases such as thumbnail and dirty asset passes.
- `ParalivesSaveStorageFacade` uses reflection for `UnityEngine.JsonUtility` so no new Unity JSON module reference is required; if Unity changes that type location, typed `TrySaveJson` / `TryLoadJson` will report an error while raw JSON read/write remains usable.
- Full solution build is currently blocked by another agent's occupation-panel UI seam, so a complete integrated compile could not be verified.

## Tests And Verification

Isolated compile probe for this agent's new source files:

```text
& 'C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\Roslyn\csc.exe' /nologo /target:library /out:$env:TEMP\ParalivesSaveLifecycleCheck.dll /reference:'C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.7.2\Facades\netstandard.dll' /reference:'A:\Dev\Worktrees\shelteredmodmanager-generalize-smm\Dist\SMM\ModAPI.dll' /reference:'A:\Dev\Worktrees\shelteredmodmanager-generalize-smm\mods\0Harmony\Assemblies\0Harmony.dll' /reference:'A:\SteamLibrary\steamapps\common\Paralives\Paralives_Data\Managed\Paralives.dll' /reference:'A:\SteamLibrary\steamapps\common\Paralives\Paralives_Data\Managed\UnityEngine.dll' /reference:'A:\SteamLibrary\steamapps\common\Paralives\Paralives_Data\Managed\UnityEngine.CoreModule.dll' 'ParalivesAPI\Core\ParalivesSaveLifecycleEvents.cs' 'ParalivesAPI\Core\ParalivesGameLifecycleFacade.cs' 'ParalivesAPI\Core\ParalivesSaveStorageFacade.cs' 'ParalivesAPI\Patches\ParalivesSaveLifecycleHooksPatch.cs'
```

Result: passed.

Full build command run:

```text
& "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" ShelteredModManager.sln /t:Build /p:Configuration=Debug /p:Platform="Any CPU" /v:minimal
```

Latest result: failed in concurrent occupation-panel UI work outside this agent's scope.

```text
A:\Dev\Worktrees\shelteredmodmanager-generalize-smm\ParalivesAPI\Core\ParalivesOccupationPanelProvider.cs(237,24): error CS0012: The type 'float4' is defined in an assembly that is not referenced. You must add a reference to assembly 'Unity.Mathematics, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null'. [A:\Dev\Worktrees\shelteredmodmanager-generalize-smm\ParalivesAPI\ParalivesAPI.csproj]
```

Verification:

```text
tools\verify-modapi-boundary.cmd
ModAPI boundary verifier failed. New or increased violations: 1
NEW	source-symbol	ModAPI/Core/IGameHelper.cs	Localization	2
```

```text
tools\verify-runtimecompat-rect.cmd
RuntimeCompat Rect verifier passed.
```

```text
tools\scan-stale-version-references.cmd -FailOnChange
Stale version scan complete. Findings: 22. Change candidates: 11.
```

The stale-version output listed existing Manager `1.3.0-beta.3` metadata and agent-status echoes of that known failure. These files are outside this assignment.

Additional check:

```text
git diff --check -- ParalivesAPI\Core\ParalivesGameLifecycleFacade.cs ParalivesAPI\Core\ParalivesSaveLifecycleEvents.cs ParalivesAPI\Core\ParalivesSaveStorageFacade.cs ParalivesAPI\Patches\ParalivesSaveLifecycleHooksPatch.cs ParalivesAPI\Core\ParalivesRuntimeBootstrap.cs ParalivesAPI\ParalivesAPI.csproj
```

Result: no whitespace errors. Git reported only line-ending normalization warnings for `ParalivesRuntimeBootstrap.cs` and `ParalivesAPI.csproj`.

## Follow-Up Needed

- Agent 1/API contracts: consider adding capability strings such as `paralives.saves.lifecycle.v1` and `paralives.saves.storage.v1`, and optionally add additive `RuntimeInfo` / `GameFacade` properties once the public surface pattern is settled.
- Agent 1/API contracts: reconcile the existing `ParalivesAPI.Stable.IParalivesSaveStorage` scaffold with this concrete `IParalivesSaveStorage` / `ParalivesSaveStorageFacade` implementation.
- Agent 2/patch governance: if a deferred `SaveFlowCritical` trigger is added, this patch host can move from `BootCritical` to `SaveFlowCritical`.
- UI/facade owner: resolve the current `ParalivesOccupationPanelProvider.cs` Unity reference build blocker.
- Boundary/version owners: resolve the existing `ModAPI/Core/IGameHelper.cs` boundary verifier finding and stale Manager 1.3 version metadata.
