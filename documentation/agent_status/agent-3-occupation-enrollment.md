# Agent 3 Occupation Enrollment Status

Last updated: 2026-05-29

Branch: `generalize-smm-manager`

`AGENTS.md`: not present in the repository root when checked.

## Scope

Generic occupation enrollment, unenrollment, swap, restore, and read snapshot operations for `ParalivesAPI`.

Other agents own occupation registration, schedules, attendance, tasks, unlockables, UI providers, save storage, runtime contracts, and documentation. This pass only added the enrollment facade and the minimal compile/property wiring it needs.

## What Changed

- Added `ParalivesOccupationEnrollmentFacade`.
- Added `IParalivesOccupationEnrollment`.
- Added `ParalivesOccupationFacade.Enrollment`.
- Added the tiny occupation-kind mapper needed by concurrent snapshot code in `ParalivesOccupationFacade`.
- Added enrollment DTOs into the shared occupation model file:
  - `ParalivesOccupationEnrollmentOptions`
  - `ParalivesOccupationEnrollmentResult`
  - `ParalivesOccupationSnapshot`
  - `ParalivesOccupationRestoreToken`
  - `ParalivesOccupationSkillLevelSnapshot`
- Reused the existing `ParalivesAssignedOccupationScheduleSnapshot` and `ParalivesOccupationUnlockableSnapshot` shapes from concurrent schedule/unlockable work instead of adding competing DTOs.
- Updated `ParalivesAPI.csproj` for the new enrollment facade and stable interface because the project uses an explicit compile list.

## Files Touched

- `ParalivesAPI/Core/ParalivesOccupationEnrollmentFacade.cs`
- `ParalivesAPI/Core/ParalivesOccupationModels.cs`
- `ParalivesAPI/Core/ParalivesOccupationFacade.cs`
- `ParalivesAPI/Stable/IParalivesOccupationEnrollment.cs`
- `ParalivesAPI/ParalivesAPI.csproj`
- `documentation/agent_status/agent-3-occupation-enrollment.md`

`ParalivesOccupationModels.cs`, `ParalivesOccupationFacade.cs`, and `ParalivesAPI.csproj` contain concurrent additions from other occupation agents. This agent's intended changes in those files are limited to enrollment DTOs, the `Enrollment` facade property, and the enrollment compile entries.

## Enrollment API Signatures Added

```csharp
public sealed class ParalivesOccupationEnrollmentFacade : IParalivesOccupationEnrollment
{
    public bool TryGetActive(ulong characterGuid, ulong occupationGuid, out ParalivesOccupationSnapshot snapshot);
    public ParalivesOccupationSnapshot ReadSnapshot(ulong characterGuid, int occupationIndex);
    public bool TryReadSnapshot(ulong characterGuid, int occupationIndex, out ParalivesOccupationSnapshot snapshot);
    public ParalivesOccupationSnapshot[] ReadActiveSnapshots(ulong characterGuid);
    public bool TryGetActiveByKind(ulong characterGuid, ParalivesOccupationKind occupationKind, out ParalivesOccupationSnapshot snapshot);
    public ParalivesOccupationEnrollmentResult TryEnroll(ulong characterGuid, ulong occupationGuid);
    public ParalivesOccupationEnrollmentResult TryEnroll(ulong characterGuid, ulong occupationGuid, int startingRank);
    public ParalivesOccupationEnrollmentResult TryEnroll(ulong characterGuid, ulong occupationGuid, ScheduleDaysOfWeek selectedDays, OccupiedHours selectedHours, int startingRank);
    public ParalivesOccupationEnrollmentResult TryEnroll(ulong characterGuid, ulong occupationGuid, ParalivesOccupationEnrollmentOptions options);
    public ParalivesOccupationEnrollmentResult TryUnenroll(ulong characterGuid, int occupationIndex);
    public ParalivesOccupationEnrollmentResult TryUnenroll(ulong characterGuid, int occupationIndex, bool wasFired);
    public ParalivesOccupationEnrollmentResult TrySwap(ulong characterGuid, ulong fromOccupationGuid, ulong toOccupationGuid, out ParalivesOccupationRestoreToken restoreToken);
    public ParalivesOccupationEnrollmentResult TrySwap(ulong characterGuid, ulong fromOccupationGuid, ulong toOccupationGuid, ParalivesOccupationEnrollmentOptions toOccupationOptions, out ParalivesOccupationRestoreToken restoreToken);
    public ParalivesOccupationEnrollmentResult TryRestore(ulong characterGuid, ParalivesOccupationRestoreToken restoreToken);
}
```

## Restore Token Shape

`ParalivesOccupationRestoreToken` stores character GUID, previous occupation GUID/index/kind, active state, capture timestamp, replacement occupation GUID/index, and a `ParalivesOccupationSnapshot`.

The snapshot includes rank/level, start/end timestamps, assigned schedule, performance, pending upgrade data, randomized upgrade GUIDs, extras, matching expertises, starting useful-skill levels, strike timestamps, vacation days, skipped days, day/update flags, and runtime occupation indexes.

Restore tokens are returned as DTOs only. This pass does not persist them.

## Mutation Safety Rules

- Enrollment validates character, occupation definition, and schedule definition before calling native enrollment.
- `TryEnroll` prevents duplicate active occupation GUIDs unless `AllowDuplicateActiveOccupation` is set.
- Native enrollment/unenrollment are wrapped in structured `ParalivesOccupationEnrollmentResult` objects instead of surfacing normal failure exceptions.
- `TryUnenroll` uses native unenrollment, then clears runtime occupation indexes that still point at the inactive entry.
- `TrySwap` snapshots the source occupation before mutation, unenrolls it, enrolls the target, and attempts to restore the source if target enrollment fails.
- `TryRestore` prefers reusing the original occupation entry when it still exists, appends only when the entry is missing, and avoids inserting in the middle so other occupation indexes are not shifted.
- Restore updates matching expertises from the token but does not remove unrelated character expertises.
- All successful mutations mark the character save data dirty.

## Assumptions

- Current native occupation kinds still map through `SchoolJobTypes.Job` and `SchoolJobTypes.School`; expanded API kinds such as remote work are definition-level categories until native support exists.
- Occupation definitions must be registered before enrollment/restore can safely reactivate an occupation GUID.
- A restore token from `TrySwap` is the safest restore path because it records the replacement occupation to unenroll before reactivating the previous occupation.
- Mandatory school re-enrollment remains vanilla-owned and can still affect school occupations after mods mutate state.

## Risks

- `TryRestore` restores save data directly rather than replaying native enrollment side effects such as notifications, memories, and relationship labels.
- If a replacement occupation definition is missing, native replacement unenrollment may fail and restore will return a structured failure.
- Snapshot DTOs currently reuse Core schedule types that still expose native schedule enums. Stable interface signatures remain raw-game-type clean according to the public-surface verifier.
- The shared occupation model/facade/project files are active coordination points with concurrent agents.

## Tests And Verification

Build:

```text
& "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" ShelteredModManager.sln /t:Build /p:Configuration=Debug /p:Platform="Any CPU" /v:minimal
```

Result: passed.

```text
ParalivesAPI -> A:\Dev\Worktrees\shelteredmodmanager-generalize-smm\Dist\SMM\bin\ParalivesAPI.dll
```

Verification:

```text
cmd /c tools\verify-runtimecompat-rect.cmd
RuntimeCompat Rect verifier passed.
```

```text
cmd /c tools\verify-paralivesapi-surface.cmd
ParalivesAPI public-surface scan completed.
Public type declarations: 175
Public raw game type exposures outside allowed namespaces: 162
Public Homeschool-specific API names: 0
Stable interface raw game type exposures: 0
```

```text
cmd /c tools\verify-modapi-boundary.cmd
ModAPI boundary verifier failed. New or increased violations: 1
NEW	source-symbol	ModAPI/Core/IGameHelper.cs	Localization	2
```

```text
cmd /c tools\scan-stale-version-references.cmd -FailOnChange
Stale version scan complete. Findings: 49. Change candidates: 38.
```

The stale-version output includes existing release-facing Manager version metadata, prior agent-status echoes, `full-diff.patch`, and a generated HTML artifact. The exact release-facing stale lines from this run included the existing Manager `1.3.0-beta.3` and `1.3.0.3` metadata in `Manager/Core/AppVersionInfo.cs`, `Manager/ManagerGUI.csproj`, and `Manager/Properties/AssemblyInfo.cs`.

Additional check:

```text
git diff --check -- ParalivesAPI\Core\ParalivesOccupationEnrollmentFacade.cs ParalivesAPI\Core\ParalivesOccupationModels.cs ParalivesAPI\Core\ParalivesOccupationFacade.cs ParalivesAPI\Stable\IParalivesOccupationEnrollment.cs ParalivesAPI\ParalivesAPI.csproj documentation\agent_status\agent-3-occupation-enrollment.md
```

Result: no whitespace errors. Git reported only line-ending normalization warnings for existing files.

## Follow-Up Needed

- Runtime/API contract owner can decide whether to expose enrollment directly on a stable runtime aggregate; current access is through `ParalivesRuntimeInfo.Current.Occupations.Enrollment`.
- Registry owner should ensure occupation definitions are applied before enrollment attempts for custom occupations.
- Schedule owner can replace remaining Core native schedule enum exposure with API-owned DTOs in a later stable-boundary pass.
- Boundary/version owners should resolve the existing `ModAPI/Core/IGameHelper.cs` boundary verifier finding and stale Manager version metadata.
