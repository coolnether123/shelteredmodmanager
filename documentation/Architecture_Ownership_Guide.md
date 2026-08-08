# SMM 2.0 Architecture Ownership Guide

This guide defines where behavior belongs inside SMM 2.0. It is the maintainer-facing answer to a recurring failure mode: adding a second helper, state store, renderer, or lifecycle beside an existing owner instead of extending the established path.

The governing rule is simple: each mutable responsibility has one owner. Public facades remain small, stable entry points; adapters translate between environments; views project state; none of those layers may quietly become a second owner.

## System boundaries

```text
ShelteredScenarioEditor.dll (optional)
  interactive draft authoring, editor presentation, and editor tooling
        |
        | public scenario/runtime contracts only
        v
ShelteredAPI.dll
  Sheltered runtime integration and supported mod-author facades
        |
        | neutral lifecycle and registration contracts
        v
ModAPI.dll
  neutral plugin host, lifecycle, and contracts

Manager.exe                         Shared
  desktop configuration,            serializer-neutral DTOs and pure policy
  discovery, load order, deployment used by more than one assembly

External mods reference ModAPI.dll and, when needed, ShelteredAPI.dll.
They never need ShelteredScenarioEditor.dll.
```

### Manager

Manager owns desktop-only policy and presentation: discovering installed mods, editing load order and options, validating compatibility, configuring game installations, and deploying runtime artifacts. It must not duplicate runtime interpretation rules when those rules can be expressed as serializer-neutral policy in `Shared`.

### Shared

`Shared` is deliberately small. Code belongs here only when at least two assemblies need the same contract or pure policy and the code has no Unity, WinForms, filesystem-location, serializer, or process dependency. A type does not belong in `Shared` merely because it is pure C#.

### ModAPI

`ModAPI.dll` owns game-neutral plugin lifecycle, discovery, registries, logging, settings, persistence ports, input contracts, neutral actor contracts, Harmony infrastructure, and runtime bootstrap ports. It does not own Sheltered managers, NGUI, `ScenarioDef`, Sheltered save routing, or other game vocabulary.

### ShelteredAPI

`ShelteredAPI.dll` owns integration with Sheltered: game adapters, Harmony targets, content, characters, events, input implementations, runtime UI, scenario registration/playback, XML definition contracts, and save routing. Public mod code enters through documented facades; implementation services remain internal even when their source is visible. ShelteredAPI does not reference the optional editor.

### ShelteredScenarioEditor

`ShelteredScenarioEditor.dll` owns the interactive scenario workspace: drafts, authoring commands and sessions, live target selection, editor-only projections, editor patches, diagnostics, and authoring UI. It depends on the public surfaces of `ShelteredAPI.dll` and `ModAPI.dll`. It must not move a capability that mod developers need out of ShelteredAPI or expose an editor type through a ShelteredAPI signature.

The dependency rule is one-way: `ShelteredScenarioEditor -> ShelteredAPI -> ModAPI`. There are no reverse references, `InternalsVisibleTo` shortcuts, reflected access to ShelteredAPI internals, compatibility namespace shims, or legacy editor-toggle aliases.

## Facades, adapters, policies, and owners

These terms are intentionally different:

| Kind | Purpose | May own mutable state? | Compatibility expectation |
| --- | --- | ---: | --- |
| Public facade | Small supported entry point for mod authors | Only when explicitly documented as the owner | Preserve or document a 2.0 migration |
| Application owner | Enforces one responsibility's invariants | Yes | Internal unless deliberately public |
| Adapter | Translates paths, serialization, platform, or game APIs | No duplicated domain state | Replaceable behind the owner |
| Projection/view model | Converts an owner snapshot into display data | No | Rebuildable and side-effect free |
| Policy | Pure normalization, classification, or mapping | No runtime resource ownership | Shared only when genuinely reused |
| Decorator/coordinator | Adds retry, revision, logging, or transaction behavior | Only its own coordination state | Keep only when it adds behavior |

A one-line facade can be valuable when it is a documented compatibility boundary. A private one-call helper that only renames another operation is usually accidental architecture. Exceptions should isolate a resource lifetime, transactional phase, reusable policy, native interop boundary, or independently tested invariant.

## Canonical ownership lanes

### Scenario authoring

```text
Unity scene discovery
        v
canonical target identity/resolution
        v
ScenarioAuthoringSelectionService
  selected target + selection mutation invariants
        v
cached/filterable target snapshots
        v
shared presentation projection
        v
Shell authoring windows
        v
commands call the same selection/mutation lane
```

This interactive lane is owned by `ShelteredScenarioEditor.dll`. `ScenarioAuthoringModule`, `ScenarioPresentationModule`, and the authoring composition root are editor modules under `ShelteredScenarioEditor/`; they are not ShelteredAPI runtime modules. XML DTOs, serialization/validation facades, registration, and playback remain ShelteredAPI responsibilities. `ShelteredScenarios` is the single Sheltered-specific registration/catalog facade; the pre-2.0 `ShelteredScenarioRegistration` facade was removed rather than forwarded.

Rules:

- Target identity and selection mutation belong to `ScenarioAuthoringSelectionService`.
- Backdrop, hierarchy, sprite, and weather views may filter canonical targets; they do not construct competing identities or mutate selection independently.
- Scene scans are cached at the discovery boundary and shared by all visible windows in a presentation update.
- Transform paths use one null contract and one builder.
- Inspector construction has one concise authoring facade over the concrete factory.
- The editor consumes ShelteredAPI definition/validation contracts through deliberate public ports; it does not duplicate the runtime catalog or reach into internal implementations.
- Only registered, reachable renderers are maintained. A renderer setting must genuinely affect selection or be removed with the obsolete implementation.

#### Editor command lane

The generic inspector model remains the reusable presentation language for labels, hints, icons, previews, fields, and automation identity. An automation ID identifies a control for the agent harness and diagnostics; it is not an executable command protocol. Interactive controls carry a typed `ScenarioAuthoringCommand`, and every caller executes it through `ScenarioAuthoringCommandService`.

```text
feature projection -> action + automation ID + typed command
                                          |
                                          v
                         ScenarioAuthoringCommandService
                    policy gates + optional safety snapshot
                                          |
                                          v
                          ScenarioCommandDispatcher
                         cached sole-owner type lookup
                                          |
                                          v
                              feature command handler
                                          |
                                          v
                        canonical session/state mutation
```

Rules:

- Feature handlers receive typed payloads. They do not discover ownership by scanning string prefixes or decoding UI strings.
- Automation IDs remain stable where harness targeting requires them. They are metadata only: there is no legacy action parser, string-prefix dispatch, raw-string backend fallback, or compatibility execution lane.
- A feature owns its command types, handler, mutation policy, and projection factory. The generic renderer owns layout only.
- `ScenarioAuthoringCommandService` centrally enforces reload eligibility, world readiness, and pre-mutation safety snapshots before dispatch. The same policy applies to clicks, shortcuts, automation, integrations, and indirect navigation.
- A composite global-search result contains typed route steps. Each step re-enters `ScenarioAuthoringCommandService`; composite navigation cannot bypass policy or handler ownership.
- `ScenarioCommandDispatcher` caches the sole handler for each concrete command type. Duplicate ownership throws instead of depending on handler registration order.
- Cross-feature synchronization, history snapshots, revision changes, and presentation invalidation occur once at the editor transaction boundary.
- Application and presentation services use constructor dependencies. `ScenarioCompositionRoot.Resolve<T>()` is restricted to Harmony patches, Unity lifecycle/runtime guards, and native callbacks that cannot be constructed by the editor container. Boundary facades immediately delegate and do not own a second state or command lane.

#### Editor state owners

Mutable state is divided by lifetime, with no mirrored writable copies:

| State | Sole owner | Lifetime |
| --- | --- | --- |
| Working definition, dirty/revision data, scenario path, and persisted `ScenarioEditorState` | `ScenarioEditorSessionStore` | Active draft |
| Pending/active launch identity, lifecycle phase, transition revision, and close/reload teardown | `ScenarioAuthoringSessionLifecycleService` | Queued world load through editor close |
| Transient shell, selection, window, and interaction state | Authoring backend transaction state | Active UI session |

`ScenarioAuthoringSessionLifecycleService` owns the `Inactive`, `Queued`, `WorldLoading`, `Active`, `ReloadPending`, and `Closing` phases. Its launch identity includes draft, base mode, launch save type, startup save ID, and slot. Its monotonically increasing revision guards delayed confirmation callbacks from acting on a draft that has since changed. Shell lifecycle fields are projections of transition notifications, not a second session store.

`CurrentState` may return defensive snapshots, but snapshots are never independent authorities. Handlers mutate the transaction snapshot they receive; nested calls must not reach back through a global backend singleton and mutate a newer canonical state that the outer transaction could overwrite.

#### Composition root

`ScenarioCompositionRoot`, `ScenarioAuthoringModule`, and `ScenarioPresentationModule` form the editor's only object-construction lane. The authoring module registers the dispatcher, command-policy service, lifecycle owner, feature handlers, repositories, runtime adapters, and application services. The presentation module composes projections and the one shell renderer from those services.

Services inside the graph use constructor injection. Static facades are allowed only where control enters from Harmony, Unity, or a native callback and the external framework cannot accept a constructed instance. Those entry adapters may resolve one composed service and delegate; they must not be called as convenience service locators from application or presentation code.

#### Editor presentation modes

One shell renderer supports two deliberate modes: the central document workspace and the live world-canvas overlay used for placement and direct scene interaction. A single surface-mode policy chooses between them. Both consume the same backend snapshot, typed command lane, design tokens, input-capture service, popup/modal primitives, and layout authority. Neither mode is a fallback renderer or a parallel editor implementation.

### Scenario runtime and browser

```text
mod registration / installed scenario.xml
                  v
        ShelteredAPI scenario catalog
                  v
       one runtime scenario-browser lane
          /                    \
 installed scenario cards   save archives
          \                    /
                  v
      scenario launch/runtime binding
```

Rules:

- ShelteredAPI owns one installed-scenario catalog, browser, launch coordinator, runtime binding, and apply lane.
- The runtime browser contains installed scenarios and their save archives. It does not own drafts, `Add New Scenario`, editing, duplication, import, recovery, or editor package-management actions.
- The editor may extend the user experience while enabled, but it consumes ShelteredAPI's public scenario contracts instead of creating a second runtime catalog or launch implementation.
- The vanilla scenario window contains only the stock Surrounded and Stasis choices and each mode's stock save.
- The Custom Scenarios window keeps unlimited Surrounded and Stasis archives separate from the stock vanilla slots, and keeps modded-scenario saves scoped by their stable custom scenario ID.

### Editor metadata and snapshots

Editor workflow state does not extend the public `ScenarioDefinition` schema. For a draft named `scenario.xml`, the editor stores its author test checklist in the adjacent `scenario.editor.xml` sidecar. Writes stage and validate a same-directory temporary file before replace/move, retain a `.bak` recovery copy when replacing existing state, and treat a missing sidecar as empty editor state for an ordinary draft.

Autosaves and named snapshots are committed as an XML-plus-sidecar pair. Snapshot discovery and restore require both files, draft-folder duplication copies both, and interrupted pair transactions are cleaned without presenting a half-snapshot. Exported packages contain the runtime `scenario.xml`, referenced assets, manifest, and optional README; they never contain `*.editor.xml`. When a README is requested, its checklist summary is projected from the current editor session rather than copied from the sidecar.

Tutorial/setup progress, completed tours, and the author test checklist now share the per-draft `ScenarioEditorState` aggregate. `ScenarioAuthoringSidecarStore` is the only persistence owner and writes the matching `*.editor.xml` sidecar transactionally; snapshots and duplicated draft folders preserve that same paired state.

### Runtime preview lifecycle

```text
ScenarioPreviewSessionHost (editor owner)
        |
        | ShelteredScenarioRuntime.BeginPreview(...)
        v
IScenarioPreviewSession : IDisposable (ShelteredAPI runtime boundary)
  Refresh / RestartWorld / CaptureSnapshot / TryExecute
        |
        | Dispose
        v
preview definition + fixed seed + quest carrier + runtime binding released
```

The editor owns at most one current preview session and closes it when switching drafts, leaving playtest, or shutting down the editor. ShelteredAPI owns the game-facing preview resources. There is no separate `EndPreview` facade: `Dispose` is the single cleanup path, including failed or partial sessions. Refresh, restart, snapshots, edited-animation playback, object capture, and station authoring are strongly typed session operations; animation targets are restored during disposal. Pure deterministic loot planning stays on the runtime facade so code authors and offline verification do not need a live Unity preview composition.

Stateless operations that are independently useful to mods remain on the documented `ShelteredScenarioRuntime` facade. World-readiness and shelter-scene queries plus the scenario-world launch/complete/return trio are coarse pre-session exceptions: they establish the ready shelter world that `BeginPreview` requires, so moving them behind an already-active session would create a circular precondition. This is a lifecycle boundary, not a second preview owner.

Canonical definition indexing, story-flow validation, play readiness, world readiness, live sprite resolution/cache, runtime appearance application/color resolution, map projection descriptors, and station reflection policy live only in ShelteredAPI. The editor consumes value-only results, facade queries, or preview-session operations; it does not carry copies of those policies. `ScenarioRuntimeSpriteTarget` is the read-only cross-assembly projection; the resolver and its cache stay internal to ShelteredAPI.

Live shelter-grid mapping belongs to ShelteredAPI because it reads `ShelterRoomGrid`; editor code uses the coarse `ShelteredScenarioRuntime` grid operations and does not compile another owner. Metadata defaults/version increments, map icon/terrain constants, and future-survivor actor-reference fallback are also compiled once in ShelteredAPI and exposed through `ShelteredScenarioAuthoring`, so stateless policy cannot drift between assemblies. Pixel-editing implementation is linked only into Manager and the optional editor and remains internal in both assemblies. The scenario-editor boolean option metadata is one shared descriptor compiled by Manager and the editor.

### Optional editor lifecycle

| Deployment/configuration | Required behavior |
| --- | --- |
| Editor DLL absent | ModAPI and ShelteredAPI initialize normally; installed scenario browsing, launch, runtime bindings, and saves continue to work. No editor type is resolved. |
| Editor DLL present, `ShelteredScenarioEditor.Enabled=false` | The optional bootstrap may register its option and report disabled state, but does not create the editor composition graph, patches, Unity objects, windows, draft services, or authoring sessions. |
| Editor DLL present, `ShelteredScenarioEditor.Enabled=true` | On the next restart, the editor bootstrap initializes the editor-owned authoring graph and UI. Runtime scenario execution still goes through ShelteredAPI. |

`ShelteredScenarioEditor.Enabled` is the only supported editor enablement ID. It defaults to `false` and requires restart. Pre-extraction toggle IDs and namespaces are removed rather than redirected.

### Save runtime

```text
UI/game intent
      v
SaveRuntimeState
  pending load/save + active custom save
      v
SaveStorageRouter ----> VanillaSaveRouting
  registry owner         slot/type identity policy
      v
PlatformSaveProxy
  platform I/O adapter only
      v
save files, backups, events, verification
```

Rules:

- `SaveRuntimeState` is the only owner of pending load/save records and the active custom save.
- `PlatformSaveProxy` adapts the game's platform-save operations; callers must not use it as a parallel state store.
- `SaveStorageRouter` chooses the standard, expanded-vanilla, or scenario registry.
- `VanillaSaveRouting` owns the mapping between vanilla scenario identity, slot, transport type, and proxy classification.
- Backup filename/path sanitization is one policy used by both repository and service layers.
- Save dialogs use shared modal primitives while keeping dialog-specific state and callbacks local.

### Runtime UI

Runtime UI registries own registered panels and rebind requests. `UIFontCache`, shared textures, paging state, modal primitives, and listener-clearing operations are reusable resources or policies; individual panels should consume them instead of creating parallel caches.

`ShelteredUI` is the supported public facade. Compatibility types that have no public obligation, registration path, reflective consumer, or runtime use should not remain compiled beside the active implementation.

### Manager options

```text
Shared contract + pure merge/normalization policy
          /                         \
Manager adapter                 ModAPI adapter
WinForms ordering/path          Unity-safe cache/path
JavaScriptSerializer            ManualJson
          \                         /
             manager_options.json
```

The persisted schema, ID normalization, metadata refresh, value preservation, and mutation rules are shared. Manager and ModAPI retain separate path, locking, caching, ordering, logging, and serializer adapters. Unknown mod-owned options and values survive a metadata refresh.

### Benchmark and stability tooling

```text
per-platform session owner
  lock -> snapshot -> configure -> launch -> ready -> lease
       -> sample/workload -> release -> stop -> restore -> unlock
                    ^
                    |
      benchmark workload or stress workload
```

The parent runner owns the real install mutexes. `ShelteredBenchmark.Core.psm1` creates and validates
the serializable authorization envelope used by parallel workers, and the parent does not release those
mutexes until every worker is drained and restoration is complete. The benchmark and stress entrypoints
supply workloads; they do not implement a second lifecycle.

The platform session owns process identity, start time, install mutations, sampler, harness lease, readiness evidence, and rollback state. Benchmark and stress runners supply different workloads but do not launch, terminate, lease, or restore through parallel implementations.

Steam and Epic sessions remain independent so both may start before either is awaited. Cleanup is idempotent, validates PID plus start time, releases leases before stopping processes, drains samplers, refuses restoration while an owned process is alive, and restores every install even when a peer platform fails.

## How to add behavior

Before adding a service, helper, cache, state field, or renderer:

1. Identify the current owner of the invariant or resource.
2. Search for existing policies and primitives in that lane.
3. Extend the owner or add an adapter/projection around it.
4. If a second implementation is required, document how selection occurs and how both paths are tested.
5. Remove the superseded internal implementation in the same change when compatibility permits.
6. Add contract coverage for ownership, rollback, serialization, reflection registration, or public surface as appropriate.
7. Update this guide and the user-facing API/migration documents when the boundary changes.

Do not infer that a public SDK member is unused from repository references alone. Check external compatibility documentation, Harmony discovery, Unity callbacks, WinForms event wiring, serialization names, reflection, and string command identifiers before removing anything.

## Review checklist

- Is mutable state stored in exactly one owner?
- Does every adapter translate rather than duplicate policy or state?
- Do all UI entry points project the same snapshot and invoke the same command lane?
- Is each renderer or fallback actually selectable?
- Does every facade have a supported compatibility purpose?
- Is every shared type consumed by more than one assembly?
- Does every private one-call helper isolate a real policy, invariant, transaction phase, resource lifetime, native boundary, or test seam?
- Do cancellation and partial failure use the same cleanup path as success?
- Are public-surface and assembly-boundary baselines unchanged or deliberately documented?
- Can ShelteredAPI build and run with `ShelteredScenarioEditor.dll` physically absent?
- Does the disabled editor avoid constructing every editor-owned service, patch, and UI object?
- Does the runtime scenario browser remain free of draft/editor actions?
- Does `ShelteredScenarios` remain the only Sheltered-specific registration facade?
- Are editor-only checklist changes persisted transactionally beside, not inside, `scenario.xml`, and excluded from packages?
- Is every preview session disposed on replacement, editor close, failure, and shutdown?
- Do Steam and Epic run from identical managed artifacts and restore their own state independently?

## Verification gates

Every architectural change must pass, in proportion to its scope:

- Release and Debug solution builds, plus standalone ModAPI, ShelteredAPI, and ShelteredScenarioEditor project builds;
- ModAPI boundary, ShelteredAPI public-surface, and runtime compatibility verifiers;
- editor-to-runtime dependency and optional-DLL absence/disabled/enabled contract checks;
- repository contract suites without weakening their behavioral assertions;
- serializer round trips and persisted-option/save compatibility fixtures;
- Manager visual and persistence checks;
- Steam and Epic concurrent harness smoke tests with the editor absent, present/disabled, and present/enabled;
- benchmark comparison and longer all-supported-mod stability runs for editor-off and editor-on profiles;
- install and mutable-state hash restoration;
- `git diff --check` and a final dead-reference/registration scan.

The detailed execution checklist for this cleanup is in [`../ARCHITECTURE_CLEANUP_PLAN.md`](../ARCHITECTURE_CLEANUP_PLAN.md).
