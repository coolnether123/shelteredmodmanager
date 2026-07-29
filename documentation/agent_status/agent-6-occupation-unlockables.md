# Agent 6 Occupation Unlockables Status

Last updated: 2026-05-30

Branch: `generalize-smm-manager`

`AGENTS.md`: not present in the repository root when checked.

## Scope

Generic occupation unlockable facade for expertises, extras, and pending upgrades in `ParalivesAPI`.

Registration, enrollment, schedules, attendance, tasks, UI panel providers, and runtime contract aggregation remain owned by the other agents. This pass only added unlockable read/mutation helpers and the minimal facade/project-file wiring required for those helpers to compile once the concurrent occupation contract conflicts are resolved.

## What Changed

- Added `ParalivesOccupationUnlockableFacade`.
- Exposed it as `ParalivesOccupationFacade.Unlockables`.
- Added `IParalivesOccupationUnlockables`.
- Added structured read and mutation result types:
  - `ParalivesOccupationUnlockableReadResult`
  - `ParalivesOccupationUnlockableMutationResult`
- Extended the shared `ParalivesOccupationUnlockableSnapshot` with unlockable metadata needed by generic read APIs, including character/occupation context, display token, type flags, attachment/acquired/pending flags, starting/max levels, and slot indexes.
- Added minimal explicit compile entries for the new facade and stable interface because `ParalivesAPI.csproj` uses an explicit source list.

## Files Touched

- `ParalivesAPI/Core/ParalivesOccupationUnlockableFacade.cs`
- `ParalivesAPI/Core/ParalivesOccupationModels.cs`
- `ParalivesAPI/Core/ParalivesOccupationFacade.cs`
- `ParalivesAPI/Stable/IParalivesOccupationUnlockables.cs`
- `ParalivesAPI/ParalivesAPI.csproj`
- `documentation/agent_status/agent-6-occupation-unlockables.md`

`ParalivesOccupationModels.cs`, `ParalivesOccupationFacade.cs`, and `ParalivesAPI.csproj` were already active coordination files with concurrent occupation registry/enrollment/schedule/task/UI edits.

## Unlockable API Signatures Added

```csharp
ParalivesOccupationUnlockableReadResult ReadUnlockables(ulong characterGuid, int occupationIndex);
ParalivesOccupationUnlockableReadResult ReadExpertises(ulong characterGuid, int occupationIndex);
ParalivesOccupationUnlockableReadResult ReadExtras(ulong characterGuid, int occupationIndex);
ParalivesOccupationUnlockableReadResult ReadPendingUpgrades(ulong characterGuid, int occupationIndex);

ParalivesOccupationUnlockableMutationResult SetExpertiseLevel(ulong characterGuid, int occupationIndex, ulong unlockableGuid, int level);
ParalivesOccupationUnlockableMutationResult GrantExtra(ulong characterGuid, int occupationIndex, ulong unlockableGuid);
ParalivesOccupationUnlockableMutationResult RemoveExpertise(ulong characterGuid, int occupationIndex, ulong unlockableGuid);
ParalivesOccupationUnlockableMutationResult ClearPendingUpgrades(ulong characterGuid, int occupationIndex);
ParalivesOccupationUnlockableMutationResult CompletePendingUpgrade(ulong characterGuid, int occupationIndex, ulong unlockableGuid);
```

## Raw Game Seams Inspected

- `Decompiled/Paralives.dll/OccupationsManager.cs`
- `Decompiled/Paralives.dll/AssetCharacterOccupationData.cs`
- `Decompiled/Paralives.dll/AssetCharacterOccupationUnlockableData.cs`
- `Decompiled/Paralives.dll/AssetCharacterData.cs`
- `Decompiled/Paralives.dll/Setting/Occupations.cs`
- `Decompiled/Paralives.dll/Setting/Occupation.cs`
- `Decompiled/Paralives.dll/Setting/OccupationUnlockable.cs`
- `Decompiled/Paralives.dll/Setting/OccupationUnlockableTypes.cs`
- `Decompiled/Paralives.dll/Setting/PossibleUnlockable.cs`
- `Decompiled/Paralives.dll/Setting/UsefulExpertise.cs`
- `Decompiled/Paralives.dll/UIOccupations.cs`
- `Decompiled/Paralives.dll/UIOccupationUnlockableItem.cs`
- `Decompiled/Paralives.dll/AddExtraToOccupationProcessor.cs`
- `Decompiled/Paralives.dll/AddRandomOccupationExtraProcessor.cs`
- `Decompiled/Paralives.dll/IncreaseExpertiseLevelProcessor.cs`
- `Decompiled/Paralives.dll/HasOccupationUnlockableEvaluator.cs`

The requested `Decompiled/Paralives.dll/Setting/Expertise.cs` and `Decompiled/Paralives.dll/Setting/Extra.cs` files are not present in this decompiled tree. The current game model represents both through `Setting.OccupationUnlockable` plus `OccupationUnlockableTypes`.

## Registration Support Status

Unlockable definition registration was not added.

The decompiled settings show `Occupations.AllUnlockables` and generated setter methods that can resize or edit that array, and occupation definitions have `Occupation.Unlockables` attachment entries. However, this pass did not find a proven safe runtime refresh path for appending new unlockable definitions with localization, requirements, outcome effects, and occupation attachment without colliding with the registry agent's occupation-definition ownership. The facade exposes read/manipulation helpers only.

## Unsupported Cases

- No API for registering new `OccupationUnlockable` definitions.
- No API for attaching unlockables to occupation definitions.
- `ReadPendingUpgrades(...)` does not generate native random upgrade options because it is a read method. `CompletePendingUpgrade(...)` may ask the native manager to generate options as part of mutation validation.
- `GrantExtra(...)` only grants native `Extra` unlockables, not `Instant` unlockables.
- Extra removal is not exposed in this pass; only requested expertise removal and pending-upgrade clearing are wrapped.

## Assumptions

- Expertises live in `AssetCharacter.Data.OccupationExpertises` and are scoped to an occupation by filtering through `Occupation.Unlockables`.
- Extras and instant upgrade records live in `AssetCharacterOccupationData.Extras`.
- Native pending upgrade option GUIDs live in `AssetCharacterOccupationData.PendingRandomizedUpgrades`; `PendingUpgradeCount` is the spendable point count.
- `CompletePendingUpgrade(..., unlockableGuid: 0)` represents the native occupation level upgrade option.
- Normal mod-facing failure cases should return result objects with `Succeeded = false` and a message instead of requiring callers to catch exceptions.

## Risks

- `CompletePendingUpgrade(...)` delegates to native `OccupationsManager.CompleteUpgrade(...)`, which can play audio, write memories, process outcomes, mutate pending counts, and mark the character dirty.
- The occupation facade and model files are being edited concurrently; this pass avoided registry/enrollment/schedule/task behavior changes, but the shared files need a coordination pass.
- After the current duplicate-method compile error is resolved, `ParalivesOccupationServices.cs` should be checked for an explicit project compile entry because `ParalivesOccupationFacade` references `ParalivesOccupationPanelProviderService`.

## Tests And Verification

Build command run:

```text
& "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" ShelteredModManager.sln /t:Build /p:Configuration=Debug /p:Platform="Any CPU" /v:minimal
```

Current result: failed in concurrent occupation snapshot wiring:

```text
A:\Dev\Worktrees\shelteredmodmanager-generalize-smm\ParalivesAPI\Core\ParalivesOccupationFacade.cs(832,44): error CS0111: Type 'ParalivesOccupationFacade' already defines a member called 'ReadSnapshot' with the same parameter types [A:\Dev\Worktrees\shelteredmodmanager-generalize-smm\ParalivesAPI\ParalivesAPI.csproj]
A:\Dev\Worktrees\shelteredmodmanager-generalize-smm\ParalivesAPI\Core\ParalivesOccupationFacade.cs(840,21): error CS0111: Type 'ParalivesOccupationFacade' already defines a member called 'TryReadSnapshot' with the same parameter types [A:\Dev\Worktrees\shelteredmodmanager-generalize-smm\ParalivesAPI\ParalivesAPI.csproj]
A:\Dev\Worktrees\shelteredmodmanager-generalize-smm\ParalivesAPI\Core\ParalivesOccupationFacade.cs(852,46): error CS0111: Type 'ParalivesOccupationFacade' already defines a member called 'ReadActiveSnapshots' with the same parameter types [A:\Dev\Worktrees\shelteredmodmanager-generalize-smm\ParalivesAPI\ParalivesAPI.csproj]
```

Earlier in the same work session, before those duplicate methods appeared, the build failed on concurrent occupation contract wiring:

```text
A:\Dev\Worktrees\shelteredmodmanager-generalize-smm\ParalivesAPI\Core\ParalivesOccupationFacade.cs(74,53): error CS0535: 'ParalivesOccupationFacade' does not implement interface member 'IParalivesOccupations.ReadSnapshot(ulong, int)' [A:\Dev\Worktrees\shelteredmodmanager-generalize-smm\ParalivesAPI\ParalivesAPI.csproj]
A:\Dev\Worktrees\shelteredmodmanager-generalize-smm\ParalivesAPI\Core\ParalivesOccupationFacade.cs(74,53): error CS0535: 'ParalivesOccupationFacade' does not implement interface member 'IParalivesOccupations.TryReadSnapshot(ulong, int, out ParalivesOccupationSnapshot)' [A:\Dev\Worktrees\shelteredmodmanager-generalize-smm\ParalivesAPI\ParalivesAPI.csproj]
A:\Dev\Worktrees\shelteredmodmanager-generalize-smm\ParalivesAPI\Core\ParalivesOccupationFacade.cs(74,53): error CS0535: 'ParalivesOccupationFacade' does not implement interface member 'IParalivesOccupations.ReadActiveSnapshots(ulong)' [A:\Dev\Worktrees\shelteredmodmanager-generalize-smm\ParalivesAPI\ParalivesAPI.csproj]
A:\Dev\Worktrees\shelteredmodmanager-generalize-smm\ParalivesAPI\Core\ParalivesOccupationFacade.cs(79,26): error CS0246: The type or namespace name 'ParalivesOccupationPanelProviderService' could not be found (are you missing a using directive or an assembly reference?) [A:\Dev\Worktrees\shelteredmodmanager-generalize-smm\ParalivesAPI\ParalivesAPI.csproj]
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

The stale-version scan also reported prior agent-status echoes, intentional migration references, `full-diff.patch`, `tools/Scan-StaleVersionReferences.ps1`, and a very large generated `shelteredapi-architecture.html` line.

Additional check:

```text
git diff --check -- ParalivesAPI\Core\ParalivesOccupationUnlockableFacade.cs ParalivesAPI\Core\ParalivesOccupationModels.cs ParalivesAPI\Core\ParalivesOccupationFacade.cs ParalivesAPI\Stable\IParalivesOccupationUnlockables.cs ParalivesAPI\ParalivesAPI.csproj
```

Result: no whitespace errors. Git reported line-ending normalization warnings for `ParalivesOccupationFacade.cs` and `ParalivesAPI.csproj`.

## Follow-Up Needed

- Enrollment/contract owners need to collapse the duplicate `ReadSnapshot`, `TryReadSnapshot`, and `ReadActiveSnapshots` implementations in `ParalivesOccupationFacade.cs`.
- Project-file owner should verify whether `ParalivesOccupationServices.cs` needs an explicit compile entry after the duplicate method error is fixed.
- Registry owner should decide when unlockable definition registration and occupation attachment can safely be added.
- Runtime/API owner can decide whether to advertise a capability string such as `paralives.occupations.unlockables.v1`.
- Boundary/version owners should resolve the existing `ModAPI/Core/IGameHelper.cs` boundary finding and stale Manager version metadata.
