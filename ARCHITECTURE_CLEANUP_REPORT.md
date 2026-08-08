# SMM 2.0 Scenario Editor Architecture Rewrite Report

Date: 2026-08-02
Branch: `Nether/editor-single-lane-architecture`
Scope: `ShelteredScenarioEditor.dll` ownership, execution, lifecycle, composition, and its public boundary with `ShelteredAPI.dll`

## Outcome

The Scenario Editor now has one typed execution avenue and explicit owners for command policy, handler dispatch, session lifecycle, draft state, presentation state, and runtime preview lifetime. The rewrite removes the old string-command architecture instead of retaining it behind adapters or compatibility wrappers.

This report describes the rewritten architecture. Final build, full contract, performance, and live Steam/Epic acceptance must be recorded only after they are run against the final binaries; pre-rewrite evidence is baseline material, not proof that the rewritten paths pass.

## Assembly boundary

```text
mods
  |
  v
ShelteredAPI.dll --------------------> ModAPI.dll
  ^
  | supported public/runtime contracts
  |
ShelteredScenarioEditor.dll (optional)
```

- `ShelteredAPI.dll` is the public Sheltered modding and runtime boundary. It owns scenario definition/XML contracts, registration and installed-scenario catalog behavior, validation, launch/runtime binding, apply behavior, save routing, and the documented `ShelteredScenarios`, `ShelteredScenarioAuthoring`, and `ShelteredScenarioRuntime` facades.
- `ShelteredScenarioEditor.dll` owns interactive drafts, authoring commands, lifecycle sessions, live target editing, editor-only projections, diagnostics, preview orchestration, and the authoring UI.
- Mods reference `ModAPI.dll` and `ShelteredAPI.dll`; no capability a mod author needs is exposed only by the editor assembly.
- The dependency direction is `ShelteredScenarioEditor -> ShelteredAPI -> ModAPI`. ShelteredAPI has no editor reference, editor type in a public signature, friend-assembly access, reflected internal access, compatibility namespace, or legacy editor-toggle alias.
- `ShelteredScenarioRegistration` remains removed in the unreleased 2.0 API. `ShelteredScenarios` is the single Sheltered-specific registration/catalog facade; file authoring and active runtime behavior remain distinct supported facades.

## One typed command lane

All executable editor interactions converge on the same path:

```text
click / shortcut / automation / integration / global search
                            |
                            v
                 ScenarioAuthoringCommand
             AutomationId + typed payload + policy
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

`AutomationId` is stable metadata used to identify controls in the agentic harness and diagnostics. It is not an executable protocol. The editor no longer contains a legacy string action parser, prefix-routed command interface, raw-string backend fallback, or a second compatibility dispatch method.

Each command carries a shared `ScenarioAuthoringCommandPolicy`. `ScenarioAuthoringCommandService` is the only policy boundary and applies:

- whether the command is allowed while a world reload is pending;
- whether the command requires a ready world; and
- whether a safety snapshot is attempted before the mutation.

The dispatcher does not repeat those policies. It searches registered `IScenarioCommandHandler` instances once for each concrete command type, caches the sole owner, and throws if more than one handler claims the type. Adding a handler invalidates the ownership cache.

Global search is not an exception. A `ScenarioGlobalSearchRouteCommand` contains typed route steps, such as opening a workspace and then selecting an entity. Every step recursively re-enters `ScenarioAuthoringCommandService`, so composite navigation receives the same policy gates, snapshot behavior, handler ownership, status projection, and failure handling as direct interaction. Nested route commands are rejected.

## Lifecycle source of truth

`ScenarioAuthoringSessionLifecycleService` replaces the former parallel session-store behavior and solely owns:

- the pending and active `ScenarioAuthoringSession` identities;
- the `Inactive`, `Queued`, `WorldLoading`, `Active`, `ReloadPending`, and `Closing` phase;
- a monotonically increasing lifecycle revision;
- activation, reload preparation, cancellation, close-to-menu, orphan cleanup, and shutdown teardown; and
- launch redirects, active scenario-session cleanup, inventory projection cleanup, and fixed-seed release at the appropriate transition.

A launch identity includes the draft ID, base mode, launch save type, startup save ID, and startup slot. A queued session is reused only when that complete identity matches. The lifecycle revision guards delayed save/close confirmations, preventing a callback from closing a different draft after the user changes sessions.

Bootstrap and runtime guards query this owner. The authoring shell receives transition notifications and projects them into UI state; it does not keep a second writable lifecycle authority.

## Composition and dependency ownership

`ScenarioCompositionRoot`, `ScenarioAuthoringModule`, and `ScenarioPresentationModule` form the only editor construction path. The modules register repositories, runtime adapters, lifecycle and command services, the dispatcher, every feature handler, presentation builders, and the shell renderer. Application and presentation services receive those dependencies through constructors.

Static composition-root resolution is limited to externally constructed entry points:

- Harmony patch callbacks;
- Unity lifecycle/update hooks and runtime guards; and
- native IMGUI or game callbacks whose instance cannot be supplied by the editor graph.

These boundary facades resolve a composed service and immediately delegate. They are not mutable-state owners and cannot offer an alternate string or typed command lane. Application-to-application and presentation-to-application calls use injected dependencies.

## Other canonical owners retained

- `ScenarioEditorSessionStore` owns the current working definition, dirty/revision data, scenario path, and persisted per-draft `ScenarioEditorState`.
- `ScenarioAuthoringSelectionService` owns target identity, selection scope, and selection mutation.
- `ScenarioBackdropTargetCatalogService` is a cached projection over canonical targets, not another discovery authority.
- `ScenarioAuthoringSidecarStore` owns transactional `scenario.editor.xml` persistence; runtime `scenario.xml` remains a ShelteredAPI definition.
- `ScenarioPreviewSessionHost` owns at most one editor preview over disposable `IScenarioPreviewSession`; disposal is the sole runtime preview cleanup path.
- `ScenarioAuthoringShellImguiRenderModule` is the one renderer. Presentation builds view models and does not mutate domain state directly.

## Removed legacy paths

The rewrite intentionally does not preserve compatibility shims for the unreleased 2.0 internals. Removed concepts include:

- raw string `ExecuteAction` and `ExecuteActionWithResult` backend APIs;
- string `TryHandleAction` ownership and prefix scanning;
- `ScenarioAuthoringActionParser` execution parsing;
- `ScenarioCommandRegistration` and the parallel typed-handler interface name;
- `LegacyActionId`, legacy global-search route steps, and typed-to-string fallbacks;
- handler construction hidden inside the command service;
- the old authoring session store and parallel lifecycle/close paths; and
- application/presentation convenience service-location where constructor injection is possible.

Stable automation IDs remain because the harness needs stable targets. Their continued presence is not legacy execution support.

## Verification matrix for this rewrite

The final acceptance evidence must be collected after the final code change in each affected lane. Valid evidence for an unchanged lane should not be repeated merely to produce another run.

| Gate | Required result | Current report status |
|---|---|---|
| Typed-lane source audit | No legacy parser/string fallback symbols; enabled executable actions carry typed commands; no duplicate command owner | Required after final integration |
| Focused architecture contracts | Workspace routing, composition, lifecycle, authoring shell projection, story/metadata, assets, pixel editing, backdrop, and assembly boundary | Focused checks passed during migration; rerun only those invalidated by later dependency integration |
| Editor project build | Visual Studio MSBuild Release, zero errors | Required against final source |
| Solution/API builds | Full solution plus standalone ModAPI and ShelteredAPI builds | Required against final source |
| Broad contracts | Repository `Test-*.ps1`, public-surface, runtime compatibility, serializer and persistence behavior | Required after architecture-aligned contract updates |
| Optional editor boundary | Editor DLL absent; present/disabled; present/enabled | Required live and contract verification |
| Functional editor E2E | Every workspace, draft lifecycle, rapid clicks, global search, shortcuts, sidecars/snapshots, preview lifecycle, save/load/restart, clean exit | Required against final binary |
| Concurrent storefront smoke | Steam and Epic launched concurrently from identical artifacts | Required against final binary |
| Long all-mod stability | All supported mods, long simulation, UI/build/edit pressure, health/log monitoring, no new failure class | Required against final binary |
| Performance | Startup, scenario transition, smooth-FPS/loop-rate, working set, sustained memory growth, rapid command pressure | Required against pre-rewrite baseline |
| Restoration | Install/save hashes restored, no owned process or mutable-install residue | Required for every live transaction |
| Final hygiene | `git diff --check`, dead-reference scan, facade-boundary audit | Required after final integration |

The source-shape tests must change where they assert a removed class, method, string route, or old project location. Their behavioral invariant must remain: commands are reachable, policy is applied, ownership is unique, lifecycle cleanup is complete, optional DLL separation holds, and saves/runtime behavior is unchanged. A behavioral failure is fixed in production rather than weakened in the test.

## Live acceptance criteria

Steam and Epic testing uses identical editor/API artifact hashes and starts both platform sessions before either is awaited. The final matrix covers editor absent, present/disabled, present/enabled, and present/enabled with all supported mods. It includes rapid interaction, long sessions, save/load/restart pressure, process and log health, memory growth, and transactional restoration.

Release acceptance requires three successful measured iterations per storefront, no new failure class, and no owned process or mutable-install residue. Relative to the recorded pre-rewrite baseline, startup time, working set, and smooth-FPS rate may regress by at most 10%; scenario-book transition time may regress by at most 25%.

## Historical evidence boundary

The benchmark and stability artifacts recorded on 2026-08-01 and early 2026-08-02 remain useful pre-rewrite baselines for visual behavior, optional-DLL extraction, scenario/save separation, performance, and restoration. They were produced before this typed-command/lifecycle/dependency-injection rewrite and therefore must not be cited as final acceptance of the changed execution paths.

In particular, the expected UI baseline remains:

- the Custom Scenarios book is opaque and readable;
- the sort dropdown occludes the content behind it;
- labels do not overlap;
- the world outside the book remains visible rather than black;
- the vanilla window contains only stock Surrounded and Stasis saves; and
- unlimited vanilla archives remain separate in Custom Scenarios.

This architecture rewrite did not authorize a visual redesign. Final UI checks should compare the affected flows with that baseline and avoid rerunning unrelated views after unchanged evidence is valid.

## Extension rules

- Add an editor operation as a typed command and one feature handler. Do not make an automation ID executable.
- Put reload/world/snapshot requirements on the command policy. Do not reproduce those gates in renderers, shortcuts, search, or handlers.
- Composite operations must invoke their child commands through `ScenarioAuthoringCommandService`.
- Register dependencies and handlers in the composition modules; use constructor injection inside the graph.
- Add a static facade only for a real Harmony, Unity, or native entry boundary, and keep it as a stateless delegate.
- Keep supported mod-facing operations in ShelteredAPI. Do not expose editor implementation types through the public API.
- Extend an existing owner before adding a helper. A single-use helper is justified only when it isolates policy, a transaction, a resource lifetime, a native boundary, or a test seam; otherwise inline it.

The detailed ownership rules are in `documentation/Architecture_Ownership_Guide.md`; the scenario-specific flow is in `documentation/Scenario_Authoring_Architecture.md`.
