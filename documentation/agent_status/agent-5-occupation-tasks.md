# Agent 5 Occupation Tasks Status

Last updated: 2026-05-29

Branch: `generalize-smm-manager`

`AGENTS.md`: not present in the repository root when checked.

## Scope

Generic occupation-task facade backed by native Paralives want/task state. Occupation registration, enrollment, swap/restore, schedules, attendance, unlockables, UI panel providers, and documentation-wide verification remain owned by other agents.

## What Changed

- Added `ParalivesOccupationTaskFacade` with read, assign, and complete operations over active wants whose `OccupationGUID` is set.
- Added task DTO/result types:
  - `ParalivesOccupationTaskDefinition`
  - `ParalivesOccupationTaskEntry`
  - `ParalivesOccupationTaskAssignmentResult`
  - `ParalivesOccupationTaskCompletionResult`
- Added `IParalivesOccupationTasks` as a small stable contract scaffold over the new task facade operations.
- Exposed the facade through `ParalivesOccupationFacade.Tasks`.
- Added explicit `ParalivesAPI.csproj` compile entries for the new task facade and stable interface because this project does not wildcard-include source files.
- Preserved existing `ParalivesWantFacade` behavior and did not add a separate persistent task store.

## Files Touched

- `ParalivesAPI/Core/ParalivesOccupationTaskFacade.cs`
- `ParalivesAPI/Core/ParalivesOccupationFacade.cs`
  - Added only the lazy `Tasks` property and backing field. Concurrent registry/schedule edits already exist in this file.
- `ParalivesAPI/Stable/IParalivesOccupationTasks.cs`
- `ParalivesAPI/ParalivesAPI.csproj`
  - Added only the compile entries for `ParalivesOccupationTaskFacade.cs` and `IParalivesOccupationTasks.cs`. The project file remains a shared coordination point.
- `documentation/agent_status/agent-5-occupation-tasks.md`

## Task API Signatures Added

```csharp
ParalivesOccupationTaskEntry[] ReadActiveTasks(ulong characterGuid);
ParalivesOccupationTaskEntry[] ReadActiveTasks(ulong characterGuid, ulong occupationGuid);

ParalivesOccupationTaskAssignmentResult AssignTask(
    ulong characterGuid,
    ulong occupationGuid,
    ulong taskGuid);

ParalivesOccupationTaskAssignmentResult AssignTask(
    ulong characterGuid,
    ulong occupationGuid,
    ulong taskGuid,
    ulong skillGuid);

ParalivesOccupationTaskAssignmentResult AssignTask(
    ulong characterGuid,
    ParalivesOccupationTaskDefinition definition);

ParalivesOccupationTaskCompletionResult CompleteTask(
    ulong characterGuid,
    ulong occupationGuid,
    ulong taskGuid);

ParalivesOccupationTaskCompletionResult CompleteMatchingTask(
    ulong characterGuid,
    ulong occupationGuid);

ParalivesOccupationTaskCompletionResult CompleteMatchingTask(
    ulong characterGuid,
    ulong occupationGuid,
    ulong skillGuid);

ParalivesOccupationTaskCompletionResult CompleteMatchingTask(
    ulong characterGuid,
    ulong occupationGuid,
    ulong skillGuid,
    ulong characterTargetGuid);

ParalivesOccupationTaskCompletionResult CompleteMatchingTask(
    ulong characterGuid,
    ulong occupationGuid,
    Predicate<ParalivesOccupationTaskEntry> predicate);

ParalivesOccupationTaskCompletionResult CompleteAllMatchingTasks(
    ulong characterGuid,
    ulong occupationGuid,
    Predicate<ParalivesOccupationTaskEntry> predicate);
```

## WantFacade Methods Consumed

- `ReadActiveWants(characterGuid)` to read active occupation tasks from native want state.
- `TryGetWantDisplayName(taskGuid, out displayName)` to verify the task's backing want exists before assignment or exact completion.
- `CreateOrRefreshActiveWant(...)` to assign or refresh task wants with occupation GUID, skill GUID, character target, brain logic, skin, catalogue, and `DoesNotCount` data.
- `TryCompleteWant(characterGuid, wantIndex)` to complete matched task wants through native want completion behavior.

`CompleteMatchingWants(...)` was inspected but not used directly because the new result type needs task entries, matched counts, and single-vs-bulk completion control.

## Unsupported Task Cases

- No separate task persistence is introduced; native `AssetCharacterWantData` remains the backing store.
- Native random occupation task generation is not wrapped here because `WantsManager.AddRandomOccupationWant(...)` is school-specific in the current decompiled build and performs randomness/UI/notification side effects.
- The facade does not evaluate native want fulfillment requirements or infer a task from an action GUID. Action lifecycle callers can pass a predicate to `CompleteMatchingTask(...)` when they know how their action maps to a task entry.
- The facade does not animate UI task rows. UI behavior remains with the existing UI facade and Agent 7's panel-provider work.
- Expired task removal remains native `WantsManager.RemovePastOccupationWants(...)` behavior; this facade does not run cleanup or compute expiration windows.

## Assumptions

- A public task GUID is the native backing want GUID, but the task facade names it `TaskGuid` so mods do not need to use the want facade directly.
- Assignment requires a loaded character, nonzero occupation GUID, nonzero task GUID, and a registered backing want.
- Assignment does not require the character to have an active native occupation record for the occupation GUID. This keeps custom careers/gigs possible while `ParalivesOccupationTaskEntry.HasActiveOccupation` and `OccupationIndex` report whether an active native occupation was found.
- Default assignment refresh matching active tasks by `TaskGuid + OccupationGuid`, matching existing occupation want behavior. `ParalivesOccupationTaskDefinition.MatchSkillGuid` can opt into skill-specific duplication.

## Risks

- `ParalivesOccupationFacade.Tasks` lazily depends on `ParalivesRuntimeInfo.Current.Wants`; it should be used after the runtime aggregate has finished construction.
- Task completion is conservative: singular methods complete the first matching active task, while `CompleteAllMatchingTasks(...)` is explicit for bulk completion.
- The project file and `ParalivesOccupationFacade.cs` contain concurrent changes from other agents, so future merges should review those files carefully.
- The current full solution does not build because of concurrent schedule/contract work outside this task.

## Tests And Verification

Full build command:

```text
& "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" ShelteredModManager.sln /t:Build /p:Configuration=Debug /p:Platform="Any CPU" /v:minimal
```

Result: failed in concurrent occupation schedule/contract files outside this task:

```text
A:\Dev\Worktrees\shelteredmodmanager-generalize-smm\ParalivesAPI\Core\ParalivesOccupationScheduleFacade.cs(38,25): error CS0101: The namespace 'ParalivesAPI.Core' already contains a definition for 'ParalivesOccupationScheduleSnapshot' [A:\Dev\Worktrees\shelteredmodmanager-generalize-smm\ParalivesAPI\ParalivesAPI.csproj]
A:\Dev\Worktrees\shelteredmodmanager-generalize-smm\ParalivesAPI\Stable\IParalivesOccupations.cs(9,9): error CS0246: The type or namespace name 'IParalivesOccupationEnrollment' could not be found (are you missing a using directive or an assembly reference?) [A:\Dev\Worktrees\shelteredmodmanager-generalize-smm\ParalivesAPI\ParalivesAPI.csproj]
A:\Dev\Worktrees\shelteredmodmanager-generalize-smm\ParalivesAPI\Stable\IParalivesOccupations.cs(15,9): error CS0246: The type or namespace name 'IParalivesOccupationUnlockables' could not be found (are you missing a using directive or an assembly reference?) [A:\Dev\Worktrees\shelteredmodmanager-generalize-smm\ParalivesAPI\ParalivesAPI.csproj]
A:\Dev\Worktrees\shelteredmodmanager-generalize-smm\ParalivesAPI\Stable\IParalivesOccupations.cs(19,9): error CS0246: The type or namespace name 'IParalivesOccupationPanelProviders' could not be found (are you missing a using directive or an assembly reference?) [A:\Dev\Worktrees\shelteredmodmanager-generalize-smm\ParalivesAPI\ParalivesAPI.csproj]
```

Focused compile probe for this task's new files:

```text
& "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\Roslyn\csc.exe" /nologo /target:library /out:%TEMP%\ParalivesOccupationTaskFacadeCheck.dll /reference:"C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.7.2\Facades\netstandard.dll" /reference:"A:\Dev\Worktrees\shelteredmodmanager-generalize-smm\Dist\SMM\bin\ParalivesAPI.dll" /reference:"A:\Dev\Worktrees\shelteredmodmanager-generalize-smm\Dist\SMM\ModAPI.dll" /reference:"A:\SteamLibrary\steamapps\common\Paralives\Paralives_Data\Managed\Paralives.dll" /reference:"A:\SteamLibrary\steamapps\common\Paralives\Paralives_Data\Managed\UnityEngine.dll" /reference:"A:\SteamLibrary\steamapps\common\Paralives\Paralives_Data\Managed\UnityEngine.CoreModule.dll" ParalivesAPI\Core\ParalivesOccupationTaskFacade.cs ParalivesAPI\Stable\IParalivesOccupationTasks.cs
```

Result: passed.

```text
cmd /c tools\verify-runtimecompat-rect.cmd
RuntimeCompat Rect verifier passed.
```

```text
cmd /c tools\verify-paralivesapi-surface.cmd
ParalivesAPI public-surface scan completed.
Public type declarations: 169
Public raw game type exposures outside allowed namespaces: 149
Public Homeschool-specific API names: 0
Stable interface raw game type exposures: 0
Use -FailOnRawGameTypes to make these findings fail the command after Native/Unsafe seams are ready.
```

```text
cmd /c tools\verify-modapi-boundary.cmd
ModAPI boundary verifier failed. New or increased violations: 1
NEW	source-symbol	ModAPI/Core/IGameHelper.cs	Localization	2
```

```text
cmd /c tools\scan-stale-version-references.cmd -FailOnChange
Stale version scan complete. Findings: 37. Change candidates: 26.
```

The stale-version command failed on pre-existing Manager version metadata, existing agent-status echoes, a generated architecture artifact, and documentation migration text. The raw terminal output also included a very large generated `shelteredapi-architecture.html:54` line, so it is not copied in full here to avoid expanding recursive scanner noise in this status note.

Additional check:

```text
git diff --check -- ParalivesAPI\Core\ParalivesOccupationTaskFacade.cs ParalivesAPI\Core\ParalivesOccupationFacade.cs ParalivesAPI\Stable\IParalivesOccupationTasks.cs ParalivesAPI\ParalivesAPI.csproj
```

Result: no whitespace errors. Git reported only line-ending normalization warnings for `ParalivesOccupationFacade.cs` and `ParalivesAPI.csproj`.

## Follow-Up Needed

- Schedule/contract owners need to resolve the duplicate `ParalivesOccupationScheduleSnapshot` and missing `IParalivesOccupationEnrollment`, `IParalivesOccupationUnlockables`, and `IParalivesOccupationPanelProviders` references before the full solution can build.
- Agent 1/API contracts can decide whether to add a capability string such as `paralives.occupations.tasks.v1`.
- UI owner can call `ParalivesRuntimeInfo.Current.Occupations.Tasks.ReadActiveTasks(...)` for panels, but task assignment/completion logic should remain in this facade.
- Boundary/version owners still need to resolve the known `ModAPI/Core/IGameHelper.cs` boundary finding and stale Manager version metadata.
