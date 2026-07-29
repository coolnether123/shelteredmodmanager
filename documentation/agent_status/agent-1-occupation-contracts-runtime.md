# Agent 1 Occupation Contracts Runtime Status

Last updated: 2026-05-30

Branch: `generalize-smm-manager`

`AGENTS.md`: not present in the repository root when checked.

## Scope

Generic occupation API contracts, capability strings, and runtime composition wiring for `ParalivesAPI`.

Registry, enrollment, schedule/attendance, task, unlockable, and UI panel-provider implementation classes were treated as other-agent-owned. This pass only added the top-level stable aggregate wiring and the small adapter layer needed to compose those sub-services.

## What Changed

- Expanded `IParalivesOccupations` as the top-level stable occupation API.
- Wired `ParalivesOccupationFacade` to implement `IParalivesOccupations` through composed sub-services:
  - registry
  - enrollment
  - schedules
  - tasks
  - unlockables
  - attendance policies
  - panel providers
  - snapshot/read helpers
- Added `ParalivesOccupationServices.cs` with small adapter classes for contract-only composition:
  - `ParalivesOccupationRegistryContract`
  - `ParalivesOccupationTaskContract`
  - `ParalivesOccupationPanelProviderService`
  - `ParalivesOccupationContractMapper`
- Added additive runtime/game facade pass-throughs for occupation sub-services.
- Added generic occupation capability strings with `.v1` names only.
- Resolved the shared model duplicate `ParalivesOccupationScheduleSnapshot` conflict by reusing the existing assigned-schedule snapshot shape instead of keeping a second type with the same namespace/name.
- Kept concrete sub-service behavior in the owning agents' files.

## Files Touched

- `ParalivesAPI/Core/ParalivesCapability.cs`
- `ParalivesAPI/Core/ParalivesCapabilityRegistry.cs`
- `ParalivesAPI/Core/ParalivesGameFacade.cs`
- `ParalivesAPI/Core/ParalivesOccupationFacade.cs`
- `ParalivesAPI/Core/ParalivesOccupationModels.cs`
- `ParalivesAPI/Core/ParalivesOccupationServices.cs`
- `ParalivesAPI/Core/ParalivesRuntimeInfo.cs`
- `ParalivesAPI/ParalivesAPI.csproj`
- `ParalivesAPI/Stable/IParalivesOccupations.cs`
- `ParalivesAPI/Stable/IParalivesOccupationAttendancePolicies.cs`
- `ParalivesAPI/Stable/IParalivesOccupationEnrollment.cs`
- `ParalivesAPI/Stable/IParalivesOccupationPanelProviders.cs`
- `ParalivesAPI/Stable/IParalivesOccupationRegistry.cs`
- `ParalivesAPI/Stable/IParalivesOccupationSchedules.cs`
- `ParalivesAPI/Stable/IParalivesOccupationTasks.cs`
- `ParalivesAPI/Stable/IParalivesOccupationUnlockables.cs`
- `documentation/agent_status/agent-1-occupation-contracts-runtime.md`

Several of these files also contain concurrent edits from the occupation registry, enrollment, schedules/attendance, tasks, unlockables, and UI agents.

## Interfaces / Contracts Added

- `IParalivesOccupations` now exposes `Registry`, `Enrollment`, `Schedules`, `Tasks`, `Unlockables`, `AttendancePolicies`, `PanelProviders`, `ReadSnapshot(...)`, `TryReadSnapshot(...)`, and `ReadActiveSnapshots(...)`.
- Existing concrete sub-service facades are composed under `ParalivesOccupationFacade` without moving their implementation logic into the top-level facade.
- `ParalivesOccupationKind` now includes generic future-facing values for `RemoteWork`, `Club`, `Apprenticeship`, `Gig`, and `Custom`.
- `ParalivesOccupationUnlockableDefinition`, `ParalivesOccupationUnlockableKind`, and `ParalivesOccupationOperationResult` are present as generic DTOs for shared occupation API contracts.

## Capability Strings Added

- `paralives.occupations.v1`
- `paralives.occupations.registry.v1`
- `paralives.occupations.enrollment.v1`
- `paralives.occupations.schedules.v1`
- `paralives.occupations.tasks.v1`
- `paralives.occupations.unlockables.v1`
- `paralives.occupations.attendancePolicies.v1`
- `paralives.occupations.panelProviders.v1`

The pre-existing singular `paralives.occupations.attendancePolicy.v1` string was preserved for compatibility.

## Runtime Properties Added

`ParalivesRuntimeInfo` and `ParalivesGameFacade` now expose:

- `OccupationRegistry`
- `OccupationEnrollment`
- `OccupationSchedules`
- `OccupationTasks`
- `OccupationUnlockables`
- `OccupationPanelProviders`

`ParalivesRuntimeInfo` also calls `Occupations.AttachRuntimeServices(AttendancePolicies, Windows)` after constructing the attendance registry and UI facade.

## Assumptions

- Concrete sub-service facades from the other agents are the source of truth for behavior.
- The stable top-level occupation API should compose those services rather than duplicate their implementation.
- Expanded occupation kinds are contract-level categories for future support; the current native registration path still maps only `Job` and `School`.
- Stable interface signatures remain raw-game-type clean according to `tools\verify-paralivesapi-surface.cmd`, even though `ParalivesAPI.Core` still has known raw native exposures.

## Risks

- `ParalivesOccupationModels.cs`, `ParalivesOccupationFacade.cs`, and `ParalivesAPI.csproj` are active coordination files with concurrent edits.
- Schedule DTOs still include native schedule enum exposure in Core model types; this is documented by the schedule/enrollment agents as current Core debt, not Stable interface signature debt.
- Runtime host retry wiring for pending occupation and schedule registrations remains a follow-up for runtime/bootstrap ownership.

## Tests And Verification

Build:

```text
& "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" ShelteredModManager.sln /t:Build /p:Configuration=Debug /p:Platform="Any CPU" /v:minimal
```

Result: passed.

```text
ParalivesAPI -> A:\Dev\Worktrees\shelteredmodmanager-generalize-smm\Dist\SMM\bin\ParalivesAPI.dll
```

MSBuild also printed the existing non-fatal copy message:

```text
The file cannot be copied onto itself.
0 file(s) copied.
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
Stale version scan complete. Findings: 49. Change candidates: 38.
```

The stale-version run also reported prior agent-status echoes, intentional migration references, `full-diff.patch`, `tools/Scan-StaleVersionReferences.ps1`, and a very large generated `shelteredapi-architecture.html` line. The release-facing stale Manager metadata above is the actionable failure and is outside this assignment.

Additional check:

```text
git diff --check -- ParalivesAPI\Core\ParalivesOccupationModels.cs ParalivesAPI\Core\ParalivesOccupationServices.cs ParalivesAPI\Core\ParalivesOccupationFacade.cs ParalivesAPI\Core\ParalivesRuntimeInfo.cs ParalivesAPI\Core\ParalivesGameFacade.cs ParalivesAPI\Core\ParalivesCapability.cs ParalivesAPI\Core\ParalivesCapabilityRegistry.cs ParalivesAPI\ParalivesAPI.csproj ParalivesAPI\Stable\IParalivesOccupations.cs ParalivesAPI\Stable\IParalivesOccupationRegistry.cs ParalivesAPI\Stable\IParalivesOccupationEnrollment.cs ParalivesAPI\Stable\IParalivesOccupationSchedules.cs ParalivesAPI\Stable\IParalivesOccupationTasks.cs ParalivesAPI\Stable\IParalivesOccupationUnlockables.cs ParalivesAPI\Stable\IParalivesOccupationAttendancePolicies.cs ParalivesAPI\Stable\IParalivesOccupationPanelProviders.cs
```

Result: no whitespace errors. Git reported only line-ending normalization warnings for existing files.

## Follow-Up Needed

- Runtime/bootstrap owner should decide whether pending occupation registrations and schedule registrations should be retried from `ParalivesRuntimeHost`.
- Registry owner should decide how expanded occupation kinds beyond current native `Job` and `School` should map once the game/runtime supports them.
- Boundary owner should resolve the existing `ModAPI/Core/IGameHelper.cs` verifier finding.
- Release/version owner should resolve the existing Manager stale version metadata so the stale-version scan can pass.
