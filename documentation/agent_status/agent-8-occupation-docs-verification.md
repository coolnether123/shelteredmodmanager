# Agent 8 Occupation Docs Verification Status

Last updated: 2026-05-30

Branch: `generalize-smm-manager`

`AGENTS.md`: not present in the repository root when checked.

## Scope

Documentation, diagrams, status tracking, and verification guardrails for the generic occupation API refactor. This pass did not modify `ParalivesAPI` source files or project files.

## What Changed

- Added a dedicated generic occupation API guide.
- Updated Paralives seam, public-surface, and refactor status docs to make the occupation-first direction explicit.
- Documented school as one specialization of the occupation system, not the root abstraction.
- Documented current occupation registry, enrollment/swap/restore, schedules, attendance, tasks, unlockables, panel providers, and snapshot responsibilities based on current source and agent status notes.
- Updated the docs index to link the occupation API guide.
- Enhanced the public-surface verifier to report Homeschool-specific public API names and Stable interface raw-type exposure.

## Files Touched

- `documentation/README.md`
- `documentation/Paralives_API_Seams.md`
- `documentation/Paralives_API_Public_Surface.md`
- `documentation/Paralives_API_Refactor_Status.md`
- `documentation/Paralives_Occupation_API.md`
- `documentation/agent_status/agent-8-occupation-docs-verification.md`
- `tools/Verify-ParalivesApiSurface.ps1`

## Documentation Added Or Updated

- Added `documentation/Paralives_Occupation_API.md`.
- Updated seam docs with an occupation system diagram and current occupation seam table.
- Updated public-surface docs with Stable/Native/Unsafe occupation rules.
- Updated refactor status with the second-pass occupation agents and late-arriving occupation registry, enrollment, schedules/attendance, and tasks status notes.

## Verification Rules Added

- Reports public API signatures or enum members containing `Homeschool` under `ParalivesAPI.Core`, `ParalivesAPI.Stable`, `ParalivesAPI.Native`, and `ParalivesAPI.Unsafe`.
- Reports raw `global::AssetCharacter`, `Setting.*`, native `UI*`, and other configured raw game types in Stable interface members.
- Keeps the verifier non-failing by default. Existing `-FailOnRawGameTypes` still fails when raw game-type findings are present.

## Public Surface Findings

Current verifier output:

```text
ParalivesAPI public-surface scan completed.
Public type declarations: 175
Public raw game type exposures outside allowed namespaces: 162
Public Homeschool-specific API names: 0
Stable interface raw game type exposures: 0
```

Raw exposure remains expected current debt in `ParalivesAPI.Core`; no source code was changed to fix it in this pass.

## Assumptions

- The current occupation API is generic and should stay generic across jobs, schools, custom careers, clubs, apprenticeships, gigs, remote work, and similar systems.
- Homeschool is only an example consumer of generic school/attendance/task APIs.
- Current agent status notes are authoritative where they describe landed code; when source and notes changed during this pass, docs were adjusted to current observed state.
- Build failures in `ParalivesAPI` are owned by the source agents, not this docs/verification pass.

## Risks

- The worktree is actively changing while multiple agents run. Public-surface counts and build output are accurate for the final verification run in this pass, not a stable baseline.
- Current `ParalivesAPI.Core` occupation DTOs and contexts still expose raw native types.
- The stale-version scanner reports existing agent-status echoes, so copying failure output into status notes increases future scanner noise.

## Tests And Verification

```text
cmd /c tools\verify-paralivesapi-surface.cmd
ParalivesAPI public-surface scan completed.
Public type declarations: 175
Public raw game type exposures outside allowed namespaces: 162
Public Homeschool-specific API names: 0
Stable interface raw game type exposures: 0
Raw game type exposure samples:
RAW	ParalivesAPI/Core/ParalivesAttendancePolicyRegistry.cs	25	ParalivesAPI.Core	AssetCharacter	public global::AssetCharacter Character { get; internal set; }
RAW	ParalivesAPI/Core/ParalivesAttendancePolicyRegistry.cs	31	ParalivesAPI.Core	AssetCharacterOccupationData	public global::AssetCharacterOccupationData OccupationData { get; internal set; }
RAW	ParalivesAPI/Core/ParalivesAttendancePolicyRegistry.cs	33	ParalivesAPI.Core	Occupation	public Occupation Occupation { get; internal set; }
RAW	ParalivesAPI/Core/ParalivesAttendancePolicyRegistry.cs	39	ParalivesAPI.Core	SchoolJobTypes	public SchoolJobTypes OccupationType { get; internal set; }
RAW	ParalivesAPI/Core/ParalivesAttendancePolicyRegistry.cs	47	ParalivesAPI.Core	ScheduleDaysOfWeek	public ScheduleDaysOfWeek ChosenScheduledDays { get; internal set; }
RAW	... 137 more
Use -FailOnRawGameTypes to make these findings fail the command after Native/Unsafe seams are ready.
```

```text
cmd /c tools\verify-runtimecompat-rect.cmd
RuntimeCompat Rect verifier passed.
```

```text
cmd /c tools\verify-modapi-boundary.cmd
ModAPI boundary verifier failed. New or increased violations: 1
NEW	source-symbol	ModAPI/Core/IGameHelper.cs	Localization	2
```

```text
& "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" ShelteredModManager.sln /t:Build /p:Configuration=Debug /p:Platform="Any CPU" /v:minimal
MSBuild version 17.14.40+3e7442088 for .NET Framework
ModAPI -> A:\Dev\Worktrees\shelteredmodmanager-generalize-smm\Dist\SMM\ModAPI.dll
Doorstop -> A:\Dev\Worktrees\shelteredmodmanager-generalize-smm\Doorstop\bin\Debug\Doorstop.dll
ModAPI.Networking -> A:\Dev\Worktrees\shelteredmodmanager-generalize-smm\Dist\SMM\bin\ModAPI.Networking.dll
ManagerGUI -> A:\Dev\Worktrees\shelteredmodmanager-generalize-smm\Dist\SMM\Manager.exe
The file cannot be copied onto itself.
Decompiler -> A:\Dev\Worktrees\shelteredmodmanager-generalize-smm\Decompiler\bin\Debug\net8.0\Decompiler.dll
A:\Dev\Worktrees\shelteredmodmanager-generalize-smm\ParalivesAPI\Core\ParalivesOccupationFacade.cs(875,23): error CS0103: The name 'ParalivesOccupationContractMapper' does not exist in the current context [A:\Dev\Worktrees\shelteredmodmanager-generalize-smm\ParalivesAPI\ParalivesAPI.csproj]
```

```text
cmd /c tools\scan-stale-version-references.cmd -FailOnChange
Result: failed. Final summary from this run:
Stale version scan complete. Findings: 43. Change candidates: 32.
```

The stale-version output included existing release-facing Manager metadata:

```text
Manager/Core/AppVersionInfo.cs:5	change: release-facing stale version reference	public const string Current = "1.3.0-beta.3";
Manager/ManagerGUI.csproj:25	change: release-facing stale version reference	<ApplicationVersion>1.3.0.3</ApplicationVersion>
Manager/Properties/AssemblyInfo.cs:29	change: release-facing stale version reference	[assembly: AssemblyVersion("1.3.0.0")]
Manager/Properties/AssemblyInfo.cs:30	change: release-facing stale version reference	[assembly: AssemblyFileVersion("1.3.0.3")]
Manager/Properties/AssemblyInfo.cs:31	change: release-facing stale version reference	[assembly: AssemblyInformationalVersion("1.3.0-beta.3")]
```

The same stale-version run also reported prior agent-status echoes, intentional migration-document references, `full-diff.patch`, `tools/Scan-StaleVersionReferences.ps1`, and a very large generated `shelteredapi-architecture.html:54` artifact line.

Additional check:

```text
git diff --check -- documentation\README.md documentation\Paralives_API_Seams.md documentation\Paralives_API_Public_Surface.md documentation\Paralives_API_Refactor_Status.md documentation\Paralives_Occupation_API.md tools\Verify-ParalivesApiSurface.ps1 tools\verify-paralivesapi-surface.cmd
Result: no whitespace errors. Git reported a line-ending normalization warning for documentation/README.md.
```

## Follow-Up Needed

- Occupation/API-contract owner should resolve the missing `ParalivesOccupationContractMapper` reference so the full solution can build.
- Runtime owners should decide whether occupation registry and schedule registrations should auto-apply from the runtime host.
- Future Stable-boundary work should move or wrap remaining raw Core occupation types under Native/Unsafe or API-owned DTOs.
- Boundary/version owners should resolve the existing `ModAPI/Core/IGameHelper.cs` boundary finding and stale Manager version metadata.
