# Agent 2 Occupation Registry Status

Last updated: 2026-05-29

Branch: `generalize-smm-manager`

`AGENTS.md`: not present in the repository root when checked.

## Scope

Generic occupation definition registration for `ParalivesAPI`, separate from enrollment, schedules, attendance, tasks, unlockables, UI panel providers, and persistence.

## What Changed

- Added `ParalivesOccupationRegistry` for generic occupation definition registration.
- Added `ParalivesOccupationDefinition`, `ParalivesOccupationRegistrationResult`, and `ParalivesOccupationRegistrationStatus` to the current occupation model file.
- Added `IParalivesOccupationRegistry` stable contract scaffold.
- Exposed the registry through `ParalivesOccupationFacade.Registry`.
- Added facade pass-through methods for `RegisterOccupation(...)` and `ApplyWhenReady()`.
- Registration validates non-zero GUIDs and required safe fields, stores definitions by GUID, converts definitions to `Setting.Occupation` internally, and appends to `Settings.Get<Occupations>().AllOccupations` only when the GUID is not already present.
- Registry application also updates the native `Jobs` / `Schools` dictionaries when it appends a new occupation.
- Exceptional registration/application failures use `MMLog.WarnOnce`.

## Files Touched

- `ParalivesAPI/Core/ParalivesOccupationRegistry.cs`
- `ParalivesAPI/Core/ParalivesOccupationModels.cs`
- `ParalivesAPI/Core/ParalivesOccupationFacade.cs`
- `ParalivesAPI/Stable/IParalivesOccupationRegistry.cs`
- `ParalivesAPI/ParalivesAPI.csproj`
- `documentation/agent_status/agent-2-occupation-registry.md`

`ParalivesAPI.csproj` is an old-style explicit compile list. It was touched for the new registry/model/interface compile entries. The file also contains concurrent compile-list edits from other agents.

## Registry API Signatures Added

```csharp
public sealed class ParalivesOccupationRegistry
{
    public int RegisteredOccupationCount { get; }
    public ParalivesOccupationRegistrationResult RegisterOccupation(ParalivesOccupationDefinition definition);
    public ParalivesOccupationRegistrationResult ApplyWhenReady();
}

public sealed class ParalivesOccupationFacade
{
    public ParalivesOccupationRegistry Registry { get; }
    public ParalivesOccupationRegistrationResult RegisterOccupation(ParalivesOccupationDefinition definition);
    public ParalivesOccupationRegistrationResult ApplyWhenReady();
}
```

## Setting.Occupation Fields Mapped

- `GUID`
- `DisplayName`
- `Type`
- `Company`
- `ProgressionLevel`
- `Schedule`
- `Domains`
- `AppropriateLifestages`
- `AutonomyTags`
- `OverridesCompanyRabbithole`
- `IsRabbithole`
- `TravelDuration`
- `MaxNumberOfExtraSlots`
- `RarityWeight`
- `OutfitType`
- `WorkOutfit`
- `ForcedToAppearEveryday`
- `Unlockables` is initialized to an empty array.
- `UsefulSkills` is initialized to an empty array.
- `GenerateTaskType` is set to `Never`.

## Unsupported Fields Intentionally Skipped

- Deep unlockable registration and `PossibleUnlockable` authoring.
- Useful skill/application-point authoring.
- Task generation and `SpecificDays` task behavior.
- Occupation offer registration.
- Schedule type registration beyond the occupation's simple `ScheduleGuid` reference.
- Company, domain, progression level, life-stage, autonomy tag, outfit, and work outfit registration.
- Debug action delegates `EnrollSelectedCharacterToOccupation` and `SetWorkOutfitToSelectedCharacter`.
- Localization item registration for `OccupationName_...`; the definition only maps the occupation display-name token.

## Assumptions

- `DisplayName` is the native occupation display-name token used by the existing `OccupationName_` localization convention.
- `ScheduleGuid` is required because native enrollment assumes the occupation's schedule resolves to a schedule type.
- Job definitions require a non-zero `ProgressionLevelGuid` because native job extra-slot and offer paths assume one exists.
- Referenced schedule/company/domain/progression/life-stage/autonomy/outfit GUIDs may be registered by other agents or mods; this registry stores references and does not own those definitions.
- Duplicate native occupation GUIDs are treated as idempotent success and are not mutated.
- Runtime host auto-application was not wired in this pass because runtime wiring is owned by another agent. `RegisterOccupation(...)` attempts immediate application, and `ApplyWhenReady()` is available for later retries.

## Risks

- If a mod registers before settings are ready and no runtime owner calls `ApplyWhenReady()` later, the stored definition remains pending.
- Existing native occupations with the same GUID are not updated, by design, to avoid mutating vanilla or another mod's entry.
- The shared worktree has concurrent occupation schedule/task/enrollment/unlockable/UI contract edits. Full build status depends on those files being completed and wired.
- `ParalivesOccupationModels.cs` was created/edited concurrently; this pass merged registry models into the current occupation model file rather than overwriting the other agent's enrollment/snapshot models.

## Tests And Verification

Isolated compile probe for the registry and current occupation models:

```text
& 'C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\Roslyn\csc.exe' /nologo /target:library /out:$env:TEMP\ParalivesOccupationRegistryCheck.dll /reference:'C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.7.2\Facades\netstandard.dll' /reference:'A:\Dev\Worktrees\shelteredmodmanager-generalize-smm\Dist\SMM\ModAPI.dll' /reference:'A:\SteamLibrary\steamapps\common\Paralives\Paralives_Data\Managed\Paralives.dll' /reference:'A:\SteamLibrary\steamapps\common\Paralives\Paralives_Data\Managed\UnityEngine.dll' /reference:'A:\SteamLibrary\steamapps\common\Paralives\Paralives_Data\Managed\UnityEngine.CoreModule.dll' 'ParalivesAPI\Core\ParalivesGuid.cs' 'ParalivesAPI\Core\ParalivesOccupationModels.cs' 'ParalivesAPI\Core\ParalivesOccupationRegistry.cs'
```

Result: passed.

Full build command run:

```text
& "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" ShelteredModManager.sln /t:Build /p:Configuration=Debug /p:Platform="Any CPU" /v:minimal
```

Latest result: failed in concurrent occupation contract wiring outside this registry scope:

```text
A:\Dev\Worktrees\shelteredmodmanager-generalize-smm\ParalivesAPI\Stable\IParalivesOccupations.cs(9,9): error CS0246: The type or namespace name 'IParalivesOccupationEnrollment' could not be found (are you missing a using directive or an assembly reference?) [A:\Dev\Worktrees\shelteredmodmanager-generalize-smm\ParalivesAPI\ParalivesAPI.csproj]
A:\Dev\Worktrees\shelteredmodmanager-generalize-smm\ParalivesAPI\Stable\IParalivesOccupations.cs(15,9): error CS0246: The type or namespace name 'IParalivesOccupationUnlockables' could not be found (are you missing a using directive or an assembly reference?) [A:\Dev\Worktrees\shelteredmodmanager-generalize-smm\ParalivesAPI\ParalivesAPI.csproj]
A:\Dev\Worktrees\shelteredmodmanager-generalize-smm\ParalivesAPI\Stable\IParalivesOccupations.cs(19,9): error CS0246: The type or namespace name 'IParalivesOccupationPanelProviders' could not be found (are you missing a using directive or an assembly reference?) [A:\Dev\Worktrees\shelteredmodmanager-generalize-smm\ParalivesAPI\ParalivesAPI.csproj]
```

Verification:

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

The stale-version scan output includes existing Manager `1.3.0-beta.3` metadata, prior agent-status echoes of the same known failure, and a generated HTML artifact. The exact release-facing stale lines include:

```text
Manager/Core/AppVersionInfo.cs:5	change: release-facing stale version reference	public const string Current = "1.3.0-beta.3";
Manager/ManagerGUI.csproj:25	change: release-facing stale version reference	<ApplicationVersion>1.3.0.3</ApplicationVersion>
Manager/Properties/AssemblyInfo.cs:29	change: release-facing stale version reference	[assembly: AssemblyVersion("1.3.0.0")]
Manager/Properties/AssemblyInfo.cs:30	change: release-facing stale version reference	[assembly: AssemblyFileVersion("1.3.0.3")]
Manager/Properties/AssemblyInfo.cs:31	change: release-facing stale version reference	[assembly: AssemblyInformationalVersion("1.3.0-beta.3")]
```

Additional check:

```text
git diff --check -- ParalivesAPI\Core\ParalivesOccupationModels.cs ParalivesAPI\Core\ParalivesOccupationRegistry.cs ParalivesAPI\Core\ParalivesOccupationFacade.cs ParalivesAPI\Stable\IParalivesOccupationRegistry.cs ParalivesAPI\ParalivesAPI.csproj
```

Result: no whitespace errors; Git reported only line-ending normalization warnings for existing files.

## Follow-Up Needed

- Runtime wiring owner should decide whether the runtime host should call `ParalivesRuntimeInfo.Current.Occupations.Registry.ApplyWhenReady()` periodically, matching interaction/localization registration.
- Enrollment, unlockable, and UI panel-provider owners need to complete or wire their stable occupation interfaces so `IParalivesOccupations.cs` compiles.
- Schedule/task owners should confirm their concurrent `ParalivesOccupationScheduleFacade` and `ParalivesOccupationTaskFacade` project-file entries are intentional.
- API contract owner may add a capability string such as `paralives.occupations.registry.v1`.
- Boundary/version owners should resolve the existing `ModAPI/Core/IGameHelper.cs` boundary verifier finding and stale Manager version metadata.
