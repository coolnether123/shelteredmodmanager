# Agent 4 Interactions And Actions Status

Date: 2026-05-29

Branch: `generalize-smm-manager`

`AGENTS.md`: not present in the repository root when checked.

## What Changed

- Added stable DTO models for interaction packs, action definitions, group definitions, interaction definitions, group child definitions, and common context/usability rules.
- Added fluent builders for the common content path: timed/instant/conversation actions, interactions, groups, root-group children, and common Homeschool-style gating rules.
- Added factory conversions from the stable DTOs to native `Setting.ActionUnit`, `Setting.InteractionUnit`, `Setting.InteractionGroup`, `InteractionGroupItem`, `InteractionUsabilityRule`, and `ContextRequirement`.
- Added registry overloads so mods can register a `ParalivesInteractionPack` or individual stable definitions without directly constructing raw Setting objects.
- Preserved all existing raw `ParalivesInteractionRegistry` registration methods and raw `ParalivesInteractionFactory` helpers.
- Added `ParalivesActionLifecycleFacade` with a `Completed` event that wraps the existing completion dispatcher without adding new patch points.
- Added stable aliases on `ParalivesActionCompletedEvent`: `InteractionGuid`, `IsCancelled`, `IsCanceled`, and `WasSuccessful`.

## Files Touched

- `ParalivesAPI/Core/ParalivesInteractionModels.cs`
- `ParalivesAPI/Core/ParalivesInteractionBuilders.cs`
- `ParalivesAPI/Core/ParalivesActionLifecycleFacade.cs`
- `ParalivesAPI/Core/ParalivesInteractionFactory.cs`
- `ParalivesAPI/Core/ParalivesInteractionRegistry.cs`
- `ParalivesAPI/Core/ParalivesActionCompletedEvent.cs`
- `ParalivesAPI/Core/ParalivesRuntimeInfo.cs`
- `ParalivesAPI/ParalivesAPI.csproj`
- `documentation/agent_status/agent-4-interactions-actions.md`

`ParalivesAPI.csproj` was touched only because it is still an explicit old-style compile list and the new files would not compile otherwise. This is a coordination point with Agent 1's scaffolding work.

## Stable Models / Builders Added

- `ParalivesInteractionPack`
- `ParalivesActionDefinition`
- `ParalivesInteractionGroupDefinition`
- `ParalivesInteractionDefinition`
- `ParalivesInteractionGroupChildDefinition`
- `ParalivesInteractionUsabilityRuleDefinition`
- `ParalivesContextRequirementDefinition`
- `ParalivesInteractionPackBuilder`
- `ParalivesActionDefinitionBuilder`
- `ParalivesInteractionDefinitionBuilder`
- `ParalivesInteractionGroupDefinitionBuilder`
- `ParalivesInteractionBuilders`

The stable path maps internally to raw Setting objects through `ParalivesInteractionFactory.CreateContent(...)` and registry overloads. Advanced raw Setting-based registration remains available.

## Action Lifecycle Capabilities

- Existing `ParalivesRuntimeInfo.Current.ActionCompletions.ActionCompleted` remains available.
- New `ParalivesRuntimeInfo.Current.ActionLifecycle.Completed` exposes the same completion stream through a lifecycle facade that can later add Started/Cancelled events.
- Existing `UpdateCharacterActionsOnActionEndPatch` remains the only action lifecycle patch host and already has `PatchPolicy`.

## Assumptions Made

- Stable action definitions cover common single-action cases: instant, fixed-duration, and conversation-style actions. Advanced action types still use the preserved raw API.
- Common stable requirements cover same household, actor/target character requirements, mandatory school life stage, and switched actor/target evaluation. More requirement shapes can be added later without breaking the current DTOs.
- No new capability string was strictly required because Agent 1 already added `paralives.interactions.content.v1` and `paralives.actions.completion.v1`.
- `ParalivesRuntimeInfo` ownership allowed the tiny additive `ActionLifecycle` property because Agent 1's note documents additive runtime properties as the current integration pattern.

## Risks

- `ParalivesAPI.csproj` is a shared coordination file while multiple agents add new source files.
- Stable requirement DTOs intentionally do not model every native `ContextRequirement` field yet; mods needing exotic requirements should use the raw advanced path for now.

## Tests / Build Run

Build command:

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
cmd /c tools\verify-modapi-boundary.cmd
ModAPI boundary verifier failed. New or increased violations: 1
NEW source-symbol ModAPI/Core/IGameHelper.cs Localization 2
```

```text
cmd /c tools\scan-stale-version-references.cmd -FailOnChange
Stale version scan complete. Findings: 30. Change candidates: 19.
```

The stale scan output listed existing Manager version metadata, prior agent-status echoes of the same metadata, and generated/documentation artifacts. This task did not modify those stale-version files.

Additional check:

```text
git diff --check -- ParalivesAPI/Core/ParalivesActionCompletedEvent.cs ParalivesAPI/Core/ParalivesActionLifecycleFacade.cs ParalivesAPI/Core/ParalivesInteractionBuilders.cs ParalivesAPI/Core/ParalivesInteractionFactory.cs ParalivesAPI/Core/ParalivesInteractionModels.cs ParalivesAPI/Core/ParalivesInteractionRegistry.cs ParalivesAPI/Core/ParalivesRuntimeInfo.cs ParalivesAPI/ParalivesAPI.csproj
```

Result: no whitespace errors. Git emitted line-ending normalization warnings for existing files.

## Follow-Up Needed

- Boundary owner needs to resolve the existing `ModAPI/Core/IGameHelper.cs` Localization verifier finding.
- Release/version owner needs to resolve stale Manager version metadata so `tools\scan-stale-version-references.cmd -FailOnChange` can pass.
- Agent 1 may optionally add stable-interface members for `RegisterPack(...)` and `ActionLifecycle.Completed`; current mods can use `ParalivesRuntimeInfo.Current.Interactions` and `ParalivesRuntimeInfo.Current.ActionLifecycle` directly.
