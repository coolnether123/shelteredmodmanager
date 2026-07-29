# Agent 2 Patch Governance Status

Last updated: 2026-05-29

Branch: `generalize-smm-manager`

`AGENTS.md`: not present in the repository root when checked.

## Scope

Patch governance integration for ParalivesAPI. Other Paralives API contract, UI, facade, and lifecycle work was left to the owning agents.

## What Changed

- Routed `ParalivesHarmonyPatcher.EnsurePatched()` through `PatchRegistry.ApplyAssembly(...)` for the BootCritical ParalivesAPI patch group.
- Registered the ParalivesAPI assembly with `DeferredPatchCoordinator` so future non-BootCritical governed patch timings can use the existing ModAPI deferred path.
- Replaced the old `_patched` boolean with explicit patch state: `NotStarted`, `Applying`, `Applied`, `AppliedWithFailures`, and `Failed`.
- Added a small game-neutral `PatchRegistryOptions.IsPatchTypeAlreadyApplied` hook so an assembly can use the registry without double-applying patch hosts that were already applied by an earlier bootstrap pass.
- Preserved the existing duplicate guard behavior by wiring the Paralives patcher predicate to Harmony's current patched-method list.
- Added public Paralives diagnostics accessors:
  - `ParalivesPatchDiagnostics.GetLatestReport()`
  - `ParalivesPatchDiagnostics.GetLatestSummary()`
  - `ParalivesPatchSummary` counts discovered, newly applied, already-applied, skipped, missing-policy, conflicts, required failures, optional failures, and final state.

## Files Touched

- `ModAPI/Harmony/PatchRegistry.cs`
- `ParalivesAPI/Core/ParalivesHarmonyPatcher.cs`
- `documentation/agent_status/agent-2-patch-governance.md`

No `ParalivesAPI.csproj` changes were made. `ParalivesPatchDiagnostics` and `ParalivesPatchSummary` currently live in `ParalivesHarmonyPatcher.cs` to avoid project-file churn while Agent 1 owns project-file scaffolding.

## Patch Application Path

Before:

- `ParalivesHarmonyPatcher.EnsurePatched()` marked `_patched = true` before applying patches.
- It scanned `typeof(ParalivesHarmonyPatcher).Assembly.GetTypes()`.
- It filtered Harmony patch classes manually.
- It skipped any patch type already visible in Harmony patch info.
- It called `HarmonyUtil.PatchType(...)` directly for each remaining patch type.

After:

- `EnsurePatched()` enters `Applying` state before work starts.
- It creates manager-aware `PatchRegistryOptions`, including optional patch/domain/debug/dangerous/struct-return settings.
- It registers the assembly with `DeferredPatchCoordinator`.
- It applies the BootCritical timing bucket with `PatchRegistry.ApplyAssembly(...)`.
- It records the registry `PatchReportDto` snapshot and Paralives summary counts.
- Required patch application failures produce `Failed`; optional patch application failures produce `AppliedWithFailures`; already-applied hosts count as effective success.

## Diagnostics Exposed

- `ParalivesPatchDiagnostics.GetLatestReport()` returns a cloned `PatchReportDto` from the latest Paralives patch attempt.
- `ParalivesPatchDiagnostics.GetLatestSummary()` returns a cloned `ParalivesPatchSummary`.
- Optional patch failures are counted separately from required failures so optional `PatchPolicy(IsOptional = true)` hosts do not surface as critical patcher failures.
- Already-applied hosts are counted separately so the runtime bootstrap path can remain idempotent when ModAPI has already scanned the ParalivesAPI runtime assembly.

## Assumptions

- `PatchPolicyAttribute.StartupTiming` defaults to `BootCritical`; current Paralives patch hosts do not declare alternate startup timing.
- `HarmonyBootstrap` may already apply runtime assembly BootCritical patches before `ParalivesRuntimeBootstrap.Initialize()` calls `ParalivesHarmonyPatcher.EnsurePatched()`.
- The already-applied predicate should be game-neutral and opt-in through `PatchRegistryOptions`, not hard-coded into the registry for every consumer.
- `documentation/agent_status` did not exist at initial checkout, but Agent 1's note appeared while this task was running and was read before this note was written.

## Risks

- `PatchRegistry.ApplyAssembly(...)` reports failed Harmony patch attempts as skipped hosts because `HarmonyUtil.PatchKnownType(...)` catches exceptions and returns no patched methods. The Paralives summary classifies failures by observing `OnResult` messages from the same patch attempt.
- Patch hosts with multiple method-level Harmony annotations still use the existing patch-type-level already-applied guard. This preserves previous idempotency behavior but does not attempt partial per-method repair inside one patch host.
- `ParalivesAPI.csproj` is being edited by other agents and is a coordination point. This agent intentionally avoided it.

## Tests And Verification

Build command run:

```text
& "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" ShelteredModManager.sln /t:Build /p:Configuration=Debug /p:Platform="Any CPU" /v:minimal
```

Result: failed in concurrent Paralives UI scaffolding outside this agent's touched files.

```text
A:\Dev\Worktrees\shelteredmodmanager-generalize-smm\ParalivesAPI\Core\ParalivesOccupationPanelProvider.cs(193,70): error CS0246: The type or namespace name 'ParalivesUiText' could not be found (are you missing a using directive or an assembly reference?) [A:\Dev\Worktrees\shelteredmodmanager-generalize-smm\ParalivesAPI\ParalivesAPI.csproj]
A:\Dev\Worktrees\shelteredmodmanager-generalize-smm\ParalivesAPI\Core\ParalivesOccupationPanelProvider.cs(201,79): error CS0246: The type or namespace name 'ParalivesUiText' could not be found (are you missing a using directive or an assembly reference?) [A:\Dev\Worktrees\shelteredmodmanager-generalize-smm\ParalivesAPI\ParalivesAPI.csproj]
A:\Dev\Worktrees\shelteredmodmanager-generalize-smm\ParalivesAPI\Core\ParalivesOccupationPanelProvider.cs(212,43): error CS0246: The type or namespace name 'ParalivesUiText' could not be found (are you missing a using directive or an assembly reference?) [A:\Dev\Worktrees\shelteredmodmanager-generalize-smm\ParalivesAPI\ParalivesAPI.csproj]
```

Verification commands run:

```text
cmd /c tools\verify-modapi-boundary.cmd
ModAPI boundary verifier failed. New or increased violations: 1
NEW	source-symbol	ModAPI/Core/IGameHelper.cs	Localization	2
```

```text
cmd /c tools\verify-runtimecompat-rect.cmd
RuntimeCompat Rect verifier passed.
```

```text
cmd /c tools\scan-stale-version-references.cmd -FailOnChange
Stale version scan failed. Output listed existing release-facing Manager version metadata plus another agent status note echoing those same findings. Final summary from this run: Findings: 22. Change candidates: 11.
```

Additional check:

```text
git diff --check -- ModAPI/Harmony/PatchRegistry.cs ParalivesAPI/Core/ParalivesHarmonyPatcher.cs
```

Result: no whitespace errors. Git reported only line-ending normalization warnings for the two touched source files.

## Follow-Up Needed

- UI/facade owners need to resolve `ParalivesOccupationPanelProvider.cs` references to the missing `ParalivesUiText` type before the full solution build can pass again.
- Boundary owner needs to resolve the existing `ModAPI/Core/IGameHelper.cs` Localization verifier finding.
- Release/version owner needs to resolve the stale Manager version metadata so `tools\scan-stale-version-references.cmd -FailOnChange` can pass.
- Documentation/API reference owner should decide whether to list `ParalivesPatchDiagnostics`, `ParalivesPatchSummary`, and the new `PatchRegistryOptions.IsPatchTypeAlreadyApplied` option in generated public surface docs.
