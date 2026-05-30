# Patch Governance

This document defines how Harmony patches are governed inside SMM during the ModAPI 2.0 line.

## Goals

- keep patch ownership explicit
- keep invasive behavior routed through owned subsystems
- make patch activation auditable
- keep the 2.0 public API routed through explicit ModAPI and ShelteredAPI ownership boundaries

## Domains

Patches are classified into domains:

- `Bootstrap`
- `SaveFlow`
- `UI`
- `Input`
- `Content`
- `Diagnostics`
- `Events`
- `Interactions`
- `Characters`
- `World`
- `Scenarios`

## Required metadata

Patch hosts should declare:

- domain
- startup timing
- owning feature
- target behavior
- failure mode
- rollback strategy

This is done with `PatchPolicyAttribute`.

Startup timing values:

- `BootCritical`: needed before first menu/game callbacks, applied during `HarmonyBootstrap.EnsurePatched`
- `MenuCritical`: applied at first main menu show before menu interactions
- `SaveFlowCritical`: applied before slot selection, load, save, or save/quit flow proceeds
- `GameplayDeferred`: applied before session/gameplay bootstrap
- `EditorDeferred`: applied when custom scenario authoring is enabled/opened
- `DebugDeferred`: applied only when diagnostics/debug patching is enabled

## Registration rules

- Runtime patch activation must go through `PatchRegistry`.
- Manual patch modules must also register through `PatchRegistry`.
- Patch classes should stay thin and delegate behavior to coordinators/services.

## Diagnostic reports

`PatchRegistry` retains up to 64 stable `PatchReportDto` snapshots for the current process. These snapshots are intended for support bundles and post-startup diagnostics:

- `PatchRegistry.GetReportHistory()` returns retained snapshots in capture order.
- `PatchRegistry.GetLatestReport()` returns the most recent snapshot, or `null` before a registry scan runs.
- `PatchApplyReport.DiagnosticSnapshot` exposes the stable snapshot for the current application attempt.

Each report identifies its assembly, source name, optional deferred trigger, and stable host summaries for discovered, applied, skipped, and missing-policy patch hosts. A `PatchHostReportDto` includes owning feature, domain, startup timing, metadata state, and string-form target method signatures where target discovery ran.

Target resolution remains timing-aware. Deferred patch hosts receive resolved target method signatures when their timing group is scanned, so consumers should aggregate report history rather than relying only on the boot report.

## Conflict diagnostics

When two registry-resolved patch hosts target the same method, the registry adds a `PatchConflictReportDto`. Conflict diagnostics are reporting only: they do not block, reorder, or opt a patch out of application.

- `Warning`: missing policy metadata, an unknown domain, mixed domains, or required hosts owned by different features.
- `Informational`: hosts share the same declared feature/domain, or only optional hosts from different features share the target.

Warnings use `MMLog.WarnOnce`; informational messages are also emitted once per target and severity to avoid repeated output as deferred groups are scanned.

Manual patch modules are retained in reports, but a module that applies Harmony operations entirely inside its callback can expose only its declared `TargetBehavior` unless it also has registry-resolvable Harmony target metadata.

## Safety controls

The registry honors these controls:

- `EnableDebugPatches`
- `EnableOptionalPatches`
- `AllowDangerousPatches`
- `AllowStructReturns`
- `DisabledPatchDomains`

`DisabledPatchDomains` is a comma-separated list, for example:

`Diagnostics,UI`

## Compatibility policy

During ModAPI 2.0:

- keep existing public ModAPI patch-related behavior working
- prefer internal refactors over public API moves
- use registries/coordinators behind compatibility facades
