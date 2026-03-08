# Patch Governance

This document defines how Harmony patches are governed inside SMM during the ModAPI 1.3 alpha line.

## Goals

- keep patch ownership explicit
- keep invasive behavior routed through owned subsystems
- make patch activation auditable
- preserve compatibility for 1.2.2 mods that still reference ModAPI only

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
- owning feature
- target behavior
- failure mode
- rollback strategy

This is done with `PatchPolicyAttribute`.

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

During ModAPI 1.3 alpha:

- keep existing public ModAPI patch-related behavior working
- prefer internal refactors over public API moves
- use registries/coordinators behind compatibility facades
