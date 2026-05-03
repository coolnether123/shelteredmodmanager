# Patch Governance

This document defines how Harmony patches are governed inside SMM during the ModAPI 1.3 Beta.3 line.

## Goals

- keep patch ownership explicit
- keep invasive behavior routed through owned subsystems
- make patch activation auditable
- keep the 1.3 public API routed through explicit ModAPI and ShelteredAPI ownership boundaries

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

During ModAPI 1.3 Beta.3:

- keep existing public ModAPI patch-related behavior working
- prefer internal refactors over public API moves
- use registries/coordinators behind compatibility facades
