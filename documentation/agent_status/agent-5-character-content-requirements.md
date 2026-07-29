# Agent 5 Character Content Requirements Status

Last updated: 2026-05-29

## Scope

Safe character snapshots, requirement evaluation, and read-only content lookup facades for `ParalivesAPI`.

`AGENTS.md`: not present when checked from the repository root.

## What Changed

- Added safe character snapshot access on `ParalivesCharacterFacade`:
  - `ReadSnapshot(...)`
  - `TryReadSnapshot(...)`
  - `ReadCurrentHouseholdSnapshots()`
- Added GUID-based life-stage helpers:
  - `IsCurrentLifeStage(...)`
  - `IsCurrentLifeStageAny(...)`
- Kept existing raw character access and behavior:
  - `TryGet(...)`
  - `GetOrNull(...)`
  - `GetAll()`
  - `GetCurrentHouseholdCharacters()`
- Added `ParalivesRequirementFacade` with read-only helpers:
  - `CharacterHasRequirement(...)`
  - `AnyCharacterHasRequirement(...)`
  - `CanDoInteraction(...)`
  - `TryCanDoInteraction(...)`
- Added `ParalivesContentFacade` with read-only snapshot lookup for:
  - actions
  - interactions
  - interaction groups
  - skills
  - occupations
- Added `ParalivesSettingsFacade.Content` and `ParalivesSettingsFacade.TryGetCharacterRequirement(...)`.
- Added top-level `ParalivesRuntimeInfo.Content`, `ParalivesRuntimeInfo.Requirements`, and matching `ParalivesGameFacade` passthroughs. This follows Agent 1's documented additive facade passthrough pattern.
- Updated the explicit `ParalivesAPI.csproj` compile list for the new Agent 5 source files. This project does not wildcard-include new `.cs` files, so this was required to compile the added facade/model files.

## Files Touched

- `ParalivesAPI/Core/ParalivesCharacterFacade.cs`
- `ParalivesAPI/Core/ParalivesCharacterModels.cs`
- `ParalivesAPI/Core/ParalivesContentFacade.cs`
- `ParalivesAPI/Core/ParalivesContentModels.cs`
- `ParalivesAPI/Core/ParalivesRequirementFacade.cs`
- `ParalivesAPI/Core/ParalivesSettingsFacade.cs`
- `ParalivesAPI/Core/ParalivesRuntimeInfo.cs`
- `ParalivesAPI/Core/ParalivesGameFacade.cs`
- `ParalivesAPI/ParalivesAPI.csproj`
- `documentation/agent_status/agent-5-character-content-requirements.md`

## Snapshots And Models Added

- `ParalivesCharacterSnapshot`
- `ParalivesActionContentSnapshot`
- `ParalivesInteractionContentSnapshot`
- `ParalivesInteractionGroupContentSnapshot`
- `ParalivesInteractionGroupChildSnapshot`
- `ParalivesSkillContentSnapshot`
- `ParalivesOccupationContentSnapshot`
- `ParalivesInteractionRequirementRequest`

## Raw / Native Access Preserved

- `ParalivesCharacterFacade.TryGet(...)` and related raw `AssetCharacter` helpers were not removed.
- `ParalivesSettingsFacade.TryGet<T>()` and existing raw setting-specific lookups were preserved.
- Requirement evaluation uses native `CharacterManager.CharacterHasCharacterRequirement(...)` and `InteractionManager.CanCharacterDoInteraction(...)` behind guarded API methods.

## Brittle Checks

- Existing display-name life-stage helpers were kept for source compatibility.
- `IsLifeStageNamedAny(...)` and `IsTeenOrOlder(...)` are now XML-documented as compatibility helpers.
- New GUID-based life-stage helpers and requirement helpers provide safer alternatives where mods know the game GUIDs.

## Assumptions

- Requirement GUID `0` means no requirement, matching native `CharacterManager` behavior.
- Character requirement refresh mutates the native cached `CharacterRequirementsMet` list, but is treated as a read-side cache refresh rather than gameplay mutation.
- Content snapshots should expose stable primitive values and counts, not raw setting objects.
- Capability strings are not required for this pass. If a capability is wanted later, request Agent 1 to add strings such as `paralives.characters.snapshots.v1`, `paralives.content.lookup.v1`, and `paralives.requirements.read.v1`.

## Risks

- `CanDoInteraction(...)` delegates to native interaction usability checks and can fail if dependent managers or settings are not ready.
- Character snapshots include the current cached `CharacterRequirementsMet` array without forcing a refresh.
- The full solution currently includes other agents' in-progress UI/occupation and interaction/action files, so build status depends on those parallel changes.

## Tests And Verification

Build command run:

```text
& "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" ShelteredModManager.sln /t:Build /p:Configuration=Debug /p:Platform="Any CPU" /v:minimal
```

Current result: failed outside Agent 5 scope in `ParalivesOccupationPanelProvider.cs`:

```text
A:\Dev\Worktrees\shelteredmodmanager-generalize-smm\ParalivesAPI\Core\ParalivesOccupationPanelProvider.cs(100,17): error CS0103: The name 'LayoutRebuilder' does not exist in the current context [A:\Dev\Worktrees\shelteredmodmanager-generalize-smm\ParalivesAPI\ParalivesAPI.csproj]
A:\Dev\Worktrees\shelteredmodmanager-generalize-smm\ParalivesAPI\Core\ParalivesOccupationPanelProvider.cs(187,27): error CS0012: The type 'TextMeshProUGUI' is defined in an assembly that is not referenced. You must add a reference to assembly 'Unity.TextMeshPro, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null'. [A:\Dev\Worktrees\shelteredmodmanager-generalize-smm\ParalivesAPI\ParalivesAPI.csproj]
A:\Dev\Worktrees\shelteredmodmanager-generalize-smm\ParalivesAPI\Core\ParalivesOccupationPanelProvider.cs(189,24): error CS0012: The type 'TextMeshProUGUI' is defined in an assembly that is not referenced. You must add a reference to assembly 'Unity.TextMeshPro, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null'. [A:\Dev\Worktrees\shelteredmodmanager-generalize-smm\ParalivesAPI\ParalivesAPI.csproj]
A:\Dev\Worktrees\shelteredmodmanager-generalize-smm\ParalivesAPI\Core\ParalivesOccupationPanelProvider.cs(232,17): error CS0012: The type 'float4' is defined in an assembly that is not referenced. You must add a reference to assembly 'Unity.Mathematics, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null'. [A:\Dev\Worktrees\shelteredmodmanager-generalize-smm\ParalivesAPI\ParalivesAPI.csproj]
A:\Dev\Worktrees\shelteredmodmanager-generalize-smm\ParalivesAPI\Core\ParalivesOccupationPanelProvider.cs(250,24): error CS0012: The type 'float4' is defined in an assembly that is not referenced. You must add a reference to assembly 'Unity.Mathematics, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null'. [A:\Dev\Worktrees\shelteredmodmanager-generalize-smm\ParalivesAPI\ParalivesAPI.csproj]
```

Verification commands run:

```text
tools\verify-runtimecompat-rect.cmd
RuntimeCompat Rect verifier passed.
```

```text
tools\verify-modapi-boundary.cmd
ModAPI boundary verifier failed. New or increased violations: 1
NEW	source-symbol	ModAPI/Core/IGameHelper.cs	Localization	2
```

```text
tools\scan-stale-version-references.cmd -FailOnChange
Stale version scan complete. Findings: 22. Change candidates: 11.
```

The stale-version scan failed on pre-existing release-version metadata and previously recorded Agent 1 status output; the raw command output also matched a very large generated HTML artifact, so the full output is not repeated here.

## Follow-Up Needed

- UI/occupation owner should resolve `ParalivesOccupationPanelProvider.cs` dependencies on `LayoutRebuilder`, `Unity.TextMeshPro`, and `Unity.Mathematics`.
- Boundary/stale-version verifier failures remain outside this task's allowed scope.
- Agent 1 can add explicit capability strings for snapshot/content/requirement surfaces if a future coordination pass wants capability-gated discovery.
