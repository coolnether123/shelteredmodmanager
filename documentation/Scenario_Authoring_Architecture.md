# Scenario Authoring Architecture

The advanced in-game scenario authoring workspace is an optional 2.0 component implemented by `ShelteredScenarioEditor.dll`. Its implementation follows a single-owner rule: every live concept has one authority, and presentation code consumes snapshots from that authority instead of rediscovering or reconstructing the same concept.

## Assembly boundary

```text
ShelteredScenarioEditor.dll -> ShelteredAPI.dll -> ModAPI.dll
```

- `ShelteredScenarioEditor.dll` owns interactive drafts, authoring sessions and commands, live target editing, editor projections, editor diagnostics, and authoring presentation.
- `ShelteredAPI.dll` owns the public XML/runtime facades used by mods, the single Sheltered-specific registration/catalog facade (`ShelteredScenarios`), scenario definitions and validation contracts, the installed-scenario catalog and browser, launch/runtime binding, apply behavior, and scenario save routing.
- `ModAPI.dll` owns neutral plugin lifecycle, registration contracts, runtime bootstrap contracts, settings, and persistence ports.
- Mods use `ModAPI.dll` and `ShelteredAPI.dll`; they do not reference `ShelteredScenarioEditor.dll`.

The editor consumes supported ShelteredAPI contracts. ShelteredAPI must compile and run without the editor assembly, and the extraction must not be implemented with reverse references, friend-assembly access, reflection into internals, compatibility namespaces, or old toggle aliases.

`ShelteredScenarioRegistration` was removed in the unreleased 2.0 line. Do not recreate it or add another registration wrapper: mods and the editor register/list through `ShelteredScenarios`; `ShelteredScenarioAuthoring` remains the separate file/XML authoring facade and `ShelteredScenarioRuntime` remains the active-runtime facade.

## Ownership lanes

| Concern | Canonical owner | Consumers |
|---|---|---|
| Live target identity and classification | `ScenarioAuthoringSelectionService` target adapters | Pointer selection, hierarchy, backdrop catalog, integration tooling |
| Selection validation and mutation | `ScenarioAuthoringSelectionService` | Selection commands and world input |
| Backdrop discovery | `ScenarioBackdropTargetCatalogService` | Authoring presentation builder |
| Backdrop row projection | `ScenarioAssetAuthoringContentBuilder` | Hierarchy, palette, and build-tools windows through one shared presentation context |
| Inspector item construction | `ScenarioInspectorItemFactory` through the authoring `Item` facade | Authoring content builders |
| Settings decoding | `ScenarioAuthoringSettingsSnapshot` typed properties | Content and renderer code |
| Definition DTO/XML serialization, catalog, and validation | ShelteredAPI scenario facades and runtime implementations | Mods and the optional editor through supported contracts |
| World readiness and shelter-scene classification | ShelteredAPI `ScenarioWorldReady` through `ShelteredScenarioRuntime` | Runtime launch/preview and pre-session editor bootstrap |
| Live sprite component/path resolution | ShelteredAPI `ScenarioSpriteRuntimeResolver` through read-only `ScenarioRuntimeSpriteTarget` | Runtime swaps and the editor's stateless adapter |
| Runtime appearance application and configured-color resolution | ShelteredAPI `ScenarioCharacterAppearanceService` through `ShelteredScenarioRuntime` | Runtime family apply and editor portrait projection |
| Grid math, metadata defaults/versioning, map icons/terrain IDs, future-survivor actor-ref fallback | ShelteredAPI policies behind `ShelteredScenarioRuntime` or `ShelteredScenarioAuthoring` | Runtime implementations, mods, and the optional editor through one compiled owner |
| Rendering | `ScenarioAuthoringShellImguiRenderModule` | `ScenarioAuthoringPresentationService` |
| Editor checklist persistence | `ScenarioAuthoringSidecarStore` | Editor session, draft snapshots, package README projection |
| Preview lifetime | Editor-owned `ScenarioPreviewSessionHost` over `IScenarioPreviewSession` | Playtest, scene assets, test console, runtime snapshots |
| Command policy and execution | `ScenarioAuthoringCommandService` | Shell actions, shortcuts, automation, global search, and editor integrations |
| Command ownership lookup | `ScenarioCommandDispatcher` | `ScenarioAuthoringCommandService` only |
| Authoring launch/close lifecycle | `ScenarioAuthoringSessionLifecycleService` | Bootstrap, reload, runtime guards, and shell lifecycle projection |

The interactive owners and `ScenarioAuthoringModule`/`ScenarioPresentationModule` live under `ShelteredScenarioEditor/`. Definition/runtime ownership remains in ShelteredAPI. Runtime scenario composition lives under `ShelteredAPI/Scenarios/Composition/` and must not register editor services.

## Target and backdrop flow

1. Unity input or a live catalog supplies a `GameObject`.
2. `ScenarioAuthoringSelectionService` creates the target through its ordered adapter registry. The adapter owns the durable `Kind:instanceId` identity, transform path, classification, and capabilities.
3. `ScenarioBackdropTargetCatalogService` filters canonical targets to backgrounds. It performs at most one sprite-renderer scan per Unity frame and loaded scene and returns defensive copies.
4. `ScenarioAuthoringPresentationBuilder` requests one backdrop snapshot and asks `ScenarioAssetAuthoringContentBuilder` for one section projection.
5. The hierarchy, palette, and build-tools windows reuse that projection through their shared `ScenarioAuthoringWindowContentContext`.
6. A row action resolves and applies its target through `ScenarioAuthoringSelectionService`; commands do not construct targets or assign selection collections directly.

Direct list selection replaces the current target and clears a stale pointer-selection stack. Pointer selection may preserve and cycle its hit-test stack. Both paths use the same scope validation and selection mutation code.

## Typed command flow

Every executable editor interaction uses one typed lane:

```text
presentation / shortcut / automation / global search
                         |
                         v
              ScenarioAuthoringCommand
              + stable AutomationId
              + command policy
                         |
                         v
          ScenarioAuthoringCommandService
        reload gate / world gate / safety snapshot
                         |
                         v
              ScenarioCommandDispatcher
              cached sole handler by command Type
                         |
                         v
                 feature handler
                         |
                         v
             canonical state mutation
```

`AutomationId` is control identity for the agentic harness, diagnostics, and stable targeting. It is metadata, not a command language. `ScenarioAutomationIdCodec` may encode values into an ID, but no execution path parses an ID or dispatches string prefixes. There is no legacy string action parser, raw-string backend fallback, or compatibility dispatch lane.

`ScenarioAuthoringCommandService` is the transaction and policy boundary for all callers. It applies the command's `ScenarioAuthoringCommandPolicy` exactly once: commands may require a ready world, opt into the narrowly allowed reload set, and request a pre-mutation safety snapshot. The dispatcher is deliberately policy-free. It locates one `IScenarioCommandHandler` for the concrete command type, caches that ownership lookup, and throws when two handlers claim the same type instead of resolving ownership by registration order.

A global-search result is a `ScenarioGlobalSearchRouteCommand` containing typed route steps. Each step re-enters `ScenarioAuthoringCommandService`; opening a workspace and selecting its entity therefore receives the same reload, world-readiness, snapshot, dispatch, status, and failure behavior as a direct click. Route steps cannot contain another route command.

## Lifecycle owner

`ScenarioAuthoringSessionLifecycleService` is the sole owner of the pending and active launch identities and of the authoring phase:

```text
Inactive -> Queued -> WorldLoading -> Active
   ^                                  |
   |                                  v
   +------------- Closing <---- ReloadPending
```

Its monotonically increasing revision identifies every transition and status publication. The revision also guards asynchronous close confirmation: a callback cannot save or close a different draft after the active launch identity changes. A launch identity includes the draft, base mode, launch save type, startup save ID, and slot, so a changed identity replaces stale queued work instead of reusing it. Bootstrap, reload, cancellation, activation, close-to-menu, orphan cleanup, and shutdown all pass through this owner. Shell lifecycle fields are projections of transition notifications, not a second writable session store.

## Composition and native boundaries

`ScenarioCompositionRoot` and `ScenarioAuthoringModule` are the only construction lane for editor services and command handlers. Application and presentation services receive dependencies through constructors. Registering a new feature means registering its concrete command handler in this composition graph; it does not mean adding another singleton, service locator, or dispatcher.

Static resolution is reserved for entry points whose lifetime is created outside the graph: Harmony patch callbacks, Unity lifecycle/update hooks and runtime guards, and native IMGUI/game callbacks. Those thin boundary facades resolve a composed service and immediately delegate. They do not own mutable domain state or provide an alternate command path. Calls between application or presentation services remain constructor-injected.

## Renderer boundary

The shell IMGUI renderer is the only scenario-authoring renderer. `ScenarioAuthoringPresentationService` builds renderer-neutral view models and sends them directly to `ScenarioAuthoringShellImguiRenderModule`. New renderer work must first establish a real selection requirement and parity tests; it must not be added as a lower-priority implementation that cannot be selected.

## Editor metadata boundary

The runtime scenario file is always `scenario.xml`. The editor's author-test checklist is editor workflow metadata and is stored beside it as `scenario.editor.xml`; it is not a `ScenarioDefinition` property and never enters the public XML schema.

- Sidecar saves stage a same-directory temporary file, parse it back, then replace/move it atomically; an existing sidecar retains a `.bak` recovery copy.
- Draft duplication copies the adjacent sidecar.
- Autosaves and named snapshots commit the scenario XML and matching `*.editor.xml` as one discoverable pair. Restore refuses an incomplete pair.
- Package planning is allowlist-based and excludes `*.editor.xml`. An optional `README.txt` receives a summary projected from the current session checklist, not the editor persistence file.

Other local editor preferences must remain editor-owned and must not be described as scenario runtime metadata. If a new piece of workflow state needs snapshot/package behavior, extend the sidecar owner and its pair transaction rather than adding a field to the runtime definition or a parallel file-copy path.

`ScenarioEditorState` is the single per-draft metadata aggregate for setup flow, completed tours, and the author test checklist. `ScenarioAuthoringSidecarStore` persists that aggregate transactionally beside the scenario XML as `*.editor.xml`; snapshot and duplicate operations preserve the scenario/sidecar pair, while published packages omit the editor sidecar.

## Preview boundary

`ScenarioPreviewSessionHost` is the editor owner for the one current preview. It opens the coarse ShelteredAPI boundary with `ShelteredScenarioRuntime.BeginPreview(...)`, then delegates refresh, world restart, snapshot capture, animation preview, live-object capture, and station authoring to `IScenarioPreviewSession`.

The session is disposable. Replacement, playtest exit, editor close, initialization failure, and shutdown all converge on `Dispose`; ShelteredAPI then restores tracked animation previews and releases the process-local preview definition, fixed seed, quest carrier, and preview runtime binding. There is no `EndPreview` static lane and editor-only stateful operations must not add fine-grained static runtime wrappers alongside the session.

`ShelteredScenarioRuntime` still exposes deliberate mod-author runtime hooks whose value is independent of the editor: transform identity, read-only runtime sprite-target resolution, runtime sprite keys/assets, direct sprite application, configured character appearance/color resolution, runtime identity queries, deterministic loot planning (including explicit-seed offline verification), triggers, and scoring. World-readiness and shelter-scene queries plus scenario-world launch/complete/return remain coarse static pre-session handoffs because a preview session cannot start until the shelter world exists. Edited-animation playback, object-state capture, and station authoring mutations are session-only.

The editor does not compile a second world-readiness or sprite-resolution policy. Its sprite adapter first submits the durable authored transform path to ShelteredAPI and uses a transient Unity object only as a fallback. ShelteredAPI alone owns path traversal, external-root caching, component preference, and the read-only `ScenarioRuntimeSpriteTarget` result. Character preview/edit/randomization remains editor-owned; runtime application, defaults, parsing, and portrait color resolution remain ShelteredAPI-owned.

Scenario policy is compiled once in ShelteredAPI even when it is stateless. The editor consumes grid operations through `ShelteredScenarioRuntime` and metadata defaults/versioning, map icon/terrain identifiers, and future-survivor actor-reference fallback through `ShelteredScenarioAuthoring`. `Shared` remains reserved for genuinely cross-boundary implementation such as the internal pixel editor used by Manager and the optional editor; it is not a second compilation lane for ShelteredAPI policy. Stateful Unity resolution and caches likewise stay in one assembly owner.

## Enablement lifecycle

The canonical manager option is `ShelteredScenarioEditor.Enabled`. It defaults to `false` and requires restart.

- With the DLL absent, no editor bootstrap or editor type is loaded. ShelteredAPI still registers, browses, launches, and resumes installed scenarios.
- With the DLL present and the option disabled, the editor composition root is not initialized and editor patches, Unity objects, windows, draft repositories, and authoring sessions remain absent.
- With the DLL present and the option enabled, the editor initializes its own composition graph after runtime bootstrap. Scenario playtest/runtime work crosses into ShelteredAPI through public contracts.

There is no fallback option ID, pre-extraction namespace shim, or compatibility forwarding assembly.

## Build, deploy, and test matrix

The architecture rewrite is accepted only after the following matrix is run against its final binaries. Steam and Epic game cases run concurrently from the same built artifact hashes. A passing result from before the rewrite is baseline evidence, not acceptance evidence for the rewrite.

| Layer | Required checks |
|---|---|
| Static architecture | No string parser/fallback symbols; every enabled executable action carries a typed command; every concrete command has exactly one handler; global-search route steps re-enter the policy service; only native entry boundaries resolve from the composition root |
| Build and contracts | Standalone editor and API Release builds; full solution build; editor composition, workspace routing, lifecycle, story, asset, pixel, backdrop, assembly-boundary, public-surface, and runtime compatibility contracts |
| DLL absent | ModAPI/ShelteredAPI standalone behavior; menu; vanilla scenarios; installed custom browser; launch/resume; stock, unlimited, and modded-save separation; no editor type-resolution failures |
| DLL present, disabled | No editor composition, patches, Unity objects, windows, drafts, or preview sessions; installed-scenario behavior matches the absent row |
| DLL present, enabled | Editor bootstrap; every workspace/module; rapid repeated clicks; draft create/edit/duplicate/import/export; sidecar/snapshot pairing; preview open/refresh/restart/dispose; save/load/restart; clean shutdown |
| All supported mods | Long concurrent Steam/Epic soak; editor and scenario-book pressure; spawn/build/edit operations; log and process health; memory growth; save/load/restart; byte-identical install/save restoration |
| Performance | Startup, transition latency, smooth-FPS/loop-rate, working set, sustained growth, and dispatcher/interaction pressure compared with the recorded baseline |

Release acceptance requires three successful measured iterations per storefront, identical deployed hashes, no new failure class, and no owned process or mutable-install residue. Relative to the recorded pre-rewrite baseline, startup time, working set, and smooth-FPS rate may regress by at most 10%; scenario-book transition time may regress by at most 25%. Tests whose covered production path did not change should not be repeated after valid evidence exists; changed command, lifecycle, composition, and integration paths must be rerun after their last code change.

## Extension rules

- Add new target kinds by extending or registering a selection adapter, not by constructing `ScenarioAuthoringTarget` in a window or command handler.
- Add new backdrop fields to the shared backdrop projection, not separately to hierarchy and palette windows.
- Add typed settings accessors when a setting is interpreted by more than one consumer.
- Implement application service interfaces on the concrete implementation when an adapter would only forward calls.
- Keep public mod entry points in the documented `ShelteredScenarios`, `ShelteredScenarioAuthoring`, and `ShelteredScenarioRuntime` facades in ShelteredAPI. The editor's internal owners described here are not supported mod APIs.
- Do not add draft or editor commands to ShelteredAPI's runtime scenario browser. Installed scenario browsing and launching has one runtime lane; the editor composes authoring behavior on its side of the boundary.
- Add executable UI behavior as a typed `ScenarioAuthoringCommand` and one owning `IScenarioCommandHandler`; do not make an automation ID executable.
- Put reload/world/snapshot requirements on the command policy and execute through `ScenarioAuthoringCommandService`, including composite or indirect routes.
- Add dependencies to the composition modules and constructor signatures. Static resolution is permitted only at an externally constructed Harmony, Unity, or native callback boundary.
