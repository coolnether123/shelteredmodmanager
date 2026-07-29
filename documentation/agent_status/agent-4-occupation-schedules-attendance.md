# Agent 4 Occupation Schedules And Attendance Status

Last updated: 2026-05-30

Branch: `generalize-smm-manager`

`AGENTS.md`: not present in the repository root when checked.

## Scope

Generic occupation schedule read/registration helpers and occupation-scoped attendance/travel decision policies for `ParalivesAPI`.

Registry, enrollment, task, unlockable, UI panel, runtime bootstrap, and documentation ownership stayed with the other agents except for minimal project-file compile entries needed by this worktree.

## What Changed

- Preserved the existing `ParalivesAttendancePolicy` delegate and `Register(ParalivesAttendancePolicy)` API.
- Added `ParalivesOccupationAttendanceDecision` with `UseGameDefault`, `AttendNormally`, `SuppressTravel`, `SkipToday`, and `WorkRemotely`.
- Added `ParalivesOccupationAttendanceDecisionPolicy` and coexistence support in `ParalivesAttendancePolicyRegistry`.
- Kept the `OccupationsManager.ShouldBeWorkingNow` patch tiny; it still delegates to the registry and only assigns the returned bool override.
- Expanded `ParalivesOccupationScheduleContext` with generic occupation context: occupation GUID, kind/type, schedule GUID, active state, selected days/start/duration, travel duration, schedule-window status, character GUID, day/time, and the original game decision.
- Added `ParalivesOccupationScheduleFacade` and exposed it as `ParalivesOccupationFacade.Schedules`.
- Added schedule type read helpers for all schedules, a schedule by GUID, an occupation's schedule, and a character's assigned occupation schedule.
- Added schedule registration scaffolding through `ParalivesOccupationScheduleDefinition` and `Occupations.Schedules.RegisterSchedule(...)` / `EnsureRegistered()`, appending to `Setting.Occupations.AllScheduleTypes` when settings are ready.
- Added stable contracts:
  - `IParalivesOccupationAttendancePolicies`
  - `IParalivesOccupationSchedules`
- Updated `ParalivesAPI.csproj` because the old-style project requires explicit compile entries. Also added the existing panel-provider stable interface compile entry because `IParalivesOccupations.cs` already referenced it and the solution could not build without it.

## Files Touched

- `ParalivesAPI/Core/ParalivesAttendancePolicyRegistry.cs`
- `ParalivesAPI/Core/ParalivesOccupationFacade.cs`
- `ParalivesAPI/Core/ParalivesOccupationScheduleFacade.cs`
- `ParalivesAPI/Stable/IParalivesOccupationAttendancePolicies.cs`
- `ParalivesAPI/Stable/IParalivesOccupationSchedules.cs`
- `ParalivesAPI/ParalivesAPI.csproj`
- `documentation/agent_status/agent-4-occupation-schedules-attendance.md`

## Bool Policy Compatibility

Existing `bool?` policies continue to work. A legacy policy returning `null` still means no decision, `true` maps to `AttendNormally`, and `false` maps to `SuppressTravel`. Policies still resolve in registration order, and the first non-default decision wins.

## New Decision Model

`UseGameDefault` leaves the native result unchanged and allows later policies to decide.

`AttendNormally` maps to `ShouldBeWorkingNow = true`.

`SuppressTravel` maps to `ShouldBeWorkingNow = false`.

`SkipToday` maps to `false` and also calls `SuppressAttendanceToday(...)`, which adds today's skip marker and clears current/to-go occupation state when needed.

`WorkRemotely` maps to `false` because the current native seam only accepts a bool. It prevents physical travel, but it does not create a full remote-work completion pipeline; a future patch or manager seam would be needed for remote end-of-day effects that differ from skip/vacation behavior.

## Schedule Helpers Added

- `ParalivesOccupationScheduleDefinition`
- `ParalivesOccupationScheduleDaysOption`
- `ParalivesOccupationScheduleHoursOption`
- `ParalivesOccupationScheduleTypeSnapshot`
- `ParalivesAssignedOccupationScheduleSnapshot`
- `ParalivesOccupationScheduleFacade.RegisterSchedule(...)`
- `ParalivesOccupationScheduleFacade.TryRegisterSchedule(...)`
- `ParalivesOccupationScheduleFacade.EnsureRegistered()` / `ApplyWhenReady()`
- `ParalivesOccupationScheduleFacade.TryReadSchedule(...)`
- `ParalivesOccupationScheduleFacade.ReadSchedules()`
- `ParalivesOccupationScheduleFacade.TryReadScheduleForOccupation(...)`
- `ParalivesOccupationScheduleFacade.TryReadAssignedSchedule(...)`

Registration is not wired into `ParalivesRuntimeHost.Update()` in this pass because runtime host ownership was outside this assignment. Mods or a later integration pass can call `Occupations.Schedules.EnsureRegistered()` after registering schedules.

## Raw Game Seams Inspected

- `Decompiled/Paralives.dll/OccupationsManager.cs`
- `Decompiled/Paralives.dll/Setting/Occupations.cs`
- `Decompiled/Paralives.dll/Setting/OccupationScheduleType.cs`
- `Decompiled/Paralives.dll/Setting/OccupiedHours.cs`
- `Decompiled/Paralives.dll/Setting/ScheduleDaysOfWeek.cs`
- `Decompiled/Paralives.dll/Setting/Occupation.cs`
- `Decompiled/Paralives.dll/AssetCharacterOccupationData.cs`
- `Decompiled/Paralives.dll/EnumAndGuid.cs`
- `Decompiled/Paralives.dll/Settings.cs`
- `Decompiled/Paralives.dll/UpdateCharacterOccupations.cs`

Key seam facts: `ShouldBeWorkingNow(...)` returns only a bool; `CheckIfInSchedule(...)` returns day/hour status; selected schedules live on `AssetCharacterOccupationData`; schedule definitions live in `Occupations.AllScheduleTypes`; skipped days are stored as offsets in `NextSkippedDays`.

## Assumptions

- Appending non-duplicate `OccupationScheduleType` entries to `Occupations.AllScheduleTypes` is the safest available schedule registration scaffold in the current decompiled build.
- Schedule registrations should not overwrite an existing schedule GUID, including vanilla content.
- The current attendance patch should remain on `ShouldBeWorkingNow(...)`; richer behavior is represented in API decisions but must be reduced to bool until another native seam is owned.
- `SkipToday` is the only richer decision that should mutate vanilla occupation data from the bool seam.
- `ParalivesRuntimeInfo.cs` remained untouched as requested; schedules are reachable through `ParalivesRuntimeInfo.Current.Occupations.Schedules`.

## Risks

- `WorkRemotely` cannot produce distinct remote work rewards, task completion, or end-of-day processing through the current bool-only patch.
- Schedule registrations are in-memory additions to compiled settings. A settings recompile may require `EnsureRegistered()` to run again.
- `ParalivesAPI.csproj` remains a shared coordination file across agents.
- `ParalivesOccupationScheduleContext` still carries pre-existing raw native character and occupation objects; the stable interfaces added here do not expose raw game types.

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
Public raw game type exposures outside allowed namespaces: 161
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
Manager/Core/AppVersionInfo.cs:5	change: release-facing stale version reference	public const string Current = "1.3.0-beta.3";
Manager/ManagerGUI.csproj:25	change: release-facing stale version reference	<ApplicationVersion>1.3.0.3</ApplicationVersion>
Manager/Properties/AssemblyInfo.cs:29	change: release-facing stale version reference	[assembly: AssemblyVersion("1.3.0.0")]
Manager/Properties/AssemblyInfo.cs:30	change: release-facing stale version reference	[assembly: AssemblyFileVersion("1.3.0.3")]
Manager/Properties/AssemblyInfo.cs:31	change: release-facing stale version reference	[assembly: AssemblyInformationalVersion("1.3.0-beta.3")]
Stale version scan complete. Findings: 43. Change candidates: 32.
```

The stale-version scan also reported prior agent-status echoes, intentional migration-document references, `full-diff.patch`, `tools/Scan-StaleVersionReferences.ps1`, and a very large generated `shelteredapi-architecture.html` line. The release-facing stale Manager metadata above is the actionable failure and is outside this assignment.

Additional check:

```text
git diff --check -- ParalivesAPI\Core\ParalivesAttendancePolicyRegistry.cs ParalivesAPI\Core\ParalivesOccupationFacade.cs ParalivesAPI\Core\ParalivesOccupationScheduleFacade.cs ParalivesAPI\Stable\IParalivesOccupationAttendancePolicies.cs ParalivesAPI\Stable\IParalivesOccupationSchedules.cs ParalivesAPI\ParalivesAPI.csproj
```

Result: no whitespace errors. Git printed line-ending normalization warnings for existing files.

## Follow-Up Needed

- Runtime/API owner can decide whether `Occupations.Schedules.EnsureRegistered()` should be called automatically by `ParalivesRuntimeHost`.
- A future native seam is needed if `WorkRemotely` should complete a remote workday differently from suppressing travel.
- Boundary owner should resolve the existing `ModAPI/Core/IGameHelper.cs` verifier finding.
- Release/version owner should resolve the existing Manager stale version metadata so the stale-version scan can pass.
