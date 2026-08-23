# Architecture ownership

Use this reference to decide where new behavior belongs. Each mutable responsibility has one owner. Facades expose that owner, adapters translate environments, and views project snapshots.

## System boundaries

```text
ShelteredScenarioEditor.dll
        |
        v
ShelteredAPI.dll
        |
        v
ModAPI.dll

Manager.exe <-> Shared contracts and policies
```

| Project | Owns |
| --- | --- |
| `Manager` | Desktop discovery, load order, options, installation, updates, Nexus workflows, content tools, and presentation |
| `Shared` | Contracts and policies used by more than one assembly |
| `ModAPI` | Game-neutral plugin lifecycle, registries, logging, settings, persistence ports, input contracts, actors, Harmony, and runtime bootstrap ports |
| `ShelteredAPI` | Sheltered adapters, patch targets, content, characters, events, input runtime, UI, scenarios, maps, queues, and saves |
| `ShelteredScenarioEditor` | Optional drafts, authoring commands and sessions, preview ownership, editor diagnostics, and authoring UI |

`Shared` may own reusable file and serialization policies used by more than one assembly, including the content-pack serializer. Callers still supply environment-specific roots. Shared code must not own a Unity object, a WinForms control, a process, or an installation location.

The compile-time dependency direction is `ShelteredScenarioEditor -> ShelteredAPI -> ModAPI`. There are no reverse assembly references. `ShelteredAPI` currently declares `InternalsVisibleTo("ModAPI")`; treat that friend declaration as existing debt, not permission for a new cross-boundary call.

## Architecture roles

| Role | Job | State rule |
| --- | --- | --- |
| Public facade | Give mod authors one supported entry point | Own state only when its contract says so |
| Application owner | Enforce one subsystem's invariants | Own the subsystem's mutable state |
| Adapter | Translate paths, serialization, platform, or game APIs | Do not copy domain state |
| Projection or view model | Convert an owner snapshot for display | Rebuildable and side-effect free |
| Policy | Normalize, classify, or map values | No runtime resource ownership |
| Coordinator | Add retries, revisions, transactions, or cleanup | Own only coordination state |

A private helper with one caller should isolate an invariant, transaction, resource lifetime, platform boundary, or test seam. Otherwise, keep the code with its caller.

## Canonical owners

| Concern | Owner |
| --- | --- |
| Plugin discovery and activation | `ModAPI.Core.PluginManager` and `PluginRunner` |
| Per-mod persistence | `ModAPI.Persistence.SaveSystemImpl` behind `IPluginContext.SaveSystem` |
| Sheltered slot routing and manifests | `ShelteredAPI.Saves` |
| Deterministic random streams | `ModAPI.Core.ModRandom` |
| Background calculations and main-thread results | `ModAPI.Core.ModThreads` |
| Manager boolean-option schema and merge policy | `Shared/ManagerOptions` |
| Desktop manager option storage and UI | `Manager` |
| Runtime option access | `ModAPI.Core.ManagerBooleanOptions` |
| Neutral actors | `ModAPI.Actors` |
| Sheltered actor and character projection | `ShelteredActors` and `ShelteredCharacters` |
| Content registration and injection | `ShelteredContent` and its internal content runtime |
| Sheltered events and scheduler | `ShelteredEvents` |
| Installed scenario registration and catalog | `ShelteredScenarios` |
| Scenario XML and validation | `ShelteredScenarioAuthoring` |
| Active scenario behavior | `ShelteredScenarioRuntime` |
| Interactive scenario authoring | `ShelteredScenarioEditor` owners in [Scenario authoring architecture](Scenario_Authoring_Architecture.md) |
| Runtime UI panels | `ShelteredRuntimeUI` |
| Item stores and reservations | `ShelteredStores` and storage owners |
| Cooking stations | `ShelteredCooking` |
| Expedition map state and policy | `ShelteredMap` |
| Expedition markers | `ShelteredMapMarkers` |
| Player queue snapshots and restore | `ShelteredQueues` |
| Patch discovery, policy, and reports | `ModAPI.Harmony.PatchRegistry` |

## Add behavior

Before you add a service, cache, state field, renderer, or fallback:

1. Find the owner of the invariant or resource.
2. Search that owner for an existing policy or extension point.
3. Extend the owner or add a translating adapter or read-only projection.
4. If two implementations are required, define how runtime selection works and test both paths.
5. Remove the superseded internal path when compatibility permits.
6. Add tests for the invariant, cleanup, serialization, registration, or public boundary.
7. Update the task guide when a public contract changes.

Do not infer that a public member is unused from repository references alone. Check external mods, Harmony discovery, Unity callbacks, WinForms wiring, serialized names, reflection, and string identifiers.

## Review checklist

- Does one owner hold each mutable state?
- Does each adapter translate instead of copying policy or state?
- Do all UI entry points call the same command and state owners?
- Can the runtime select every retained implementation or fallback?
- Does each public facade have a documented use?
- Is each shared type used by more than one assembly?
- Do success, cancellation, and failure converge on the same cleanup path?
- Can ShelteredAPI build and run without the editor DLL?
- Does disabling the editor prevent editor services, patches, and UI from being created?
- Does `ShelteredScenarios` remain the only Sheltered-specific registration facade?
- Are public API and assembly-boundary changes deliberate and tested?

## Verify changes

Run checks that match the changed owner. The core architecture checks are:

```cmd
tools\verify-modapi-boundary.cmd
tools\verify-shelteredapi-public-surface.cmd
tools\verify-runtimecompat-rect.cmd
tools\test-shelteredapi-contracts.cmd
tools\test-scenario-authoring-toggle-contracts.cmd
```

Build the affected project and the full solution when a shared contract changes. Run serializer round trips for persisted data, UI checks for presentation changes, and the appropriate workload from [`tools/performance`](../tools/performance) or [`tools/stability`](../tools/stability) for performance-sensitive runtime work.
