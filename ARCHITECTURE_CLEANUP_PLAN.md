# SMM 2.0 Architecture Cleanup Plan and Regression Checklist

Date: 2026-08-02
Status: standalone scenario-editor ownership extraction and automated acceptance complete
Source audit: `ARCHITECTURE_REDUNDANCY_AUDIT.md`

## Outcome

Consolidate competing implementations into one ownership lane per responsibility, remove superseded internal code before the unreleased 2.0 boundary becomes public, retain intentional facade APIs, and document where each kind of behavior belongs.

This is not a line-count-only cleanup. A deletion is acceptable only after its replacement owner and behavior have been verified. A facade stays when it is a supported external entry point, dependency boundary, or compatibility surface.

## Compatibility rules

- [x] Preserve intentional public ModAPI and ShelteredAPI facades unless the 2.0 migration documentation explicitly replaces them.
- [x] Do not classify a public SDK API as dead solely because this repository has no caller.
- [x] Check Harmony reflection, Unity lifecycle callbacks, WinForms events, serialization names, and command/string identifiers before removal.
- [x] Update API signature baselines and migration documentation only for deliberate pre-2.0 changes.
- [x] Do not weaken tests to accommodate a cleanup.
- [x] Make one component own mutable state; adapters may translate but must not duplicate storage.
- [x] Make one component own platform/process lifecycle; workloads may vary but cleanup and rollback must not.
- [x] Make one component own target identity and selection mutation; views only filter/project it.
- [x] Prefer an existing primitive or policy over a new one-call helper.

## Editing map

### Lane A — Standalone scenario editor and authoring architecture

Primary files/directories:

- `ShelteredScenarioEditor/Application/Authoring/**`
- `ShelteredScenarioEditor/Application/Commands/**`
- `ShelteredScenarioEditor/Presentation/**`
- `ShelteredScenarioEditor/Infrastructure/**`
- `ShelteredScenarioEditor/Composition/ScenarioAuthoringModule.cs`
- `ShelteredScenarioEditor/Composition/ScenarioPresentationModule.cs`
- `ShelteredAPI/Scenarios/{Public,Definitions,Registration,Lifecycle,Application/Runtime,Application/Selection}`

Planned consolidation:

- [x] Establish one canonical target factory/resolver and one selection mutation operation.
- [x] Convert backdrop discovery into a filtered/cached projection of canonical targets.
- [x] Share one backdrop view-model projection across palette, build-tools, and hierarchy views.
- [x] Add one transform-path utility and migrate the duplicate builders to it.
- [x] Reduce inspector-item construction to one intentional convenience facade over the factory.
- [x] Remove pure service adapters by binding interface contracts to concrete implementations.
- [x] Replace repeated string-key reads with typed authoring settings.
- [x] Remove the ineffective renderer mode and both unreachable fallback renderers after registration/reachability proof.
- [x] Remove dead authoring state and hollow per-frame services.
- [x] Move interactive draft, command, projection, patch, diagnostic, and presentation ownership to `ShelteredScenarioEditor.dll`.
- [x] Enforce the dependency direction `ShelteredScenarioEditor -> ShelteredAPI -> ModAPI`; ShelteredAPI has no editor reference.
- [x] Keep every mod-developer scenario contract in ShelteredAPI and expose only deliberate public runtime ports needed by the editor.
- [x] Remove pre-extraction namespaces, toggle aliases, friend-assembly shortcuts, reflection shims, and forwarding compatibility types rather than retaining legacy extraction paths.
- [x] Keep one ShelteredAPI installed-scenario catalog/browser/launch lane; editor draft actions compose outside that runtime browser.
- [x] Make `ShelteredScenarios` the single Sheltered-specific registration/catalog facade; remove `ShelteredScenarioRegistration` without a forwarding alias.
- [x] Keep author-test checklist metadata out of `ScenarioDefinition`; persist it transactionally as adjacent `scenario.editor.xml`, pair it with snapshots/copies, and exclude it from published packages.
- [x] Fold tutorial/setup progress out of the direct-written `authoring_state.xml` path and into the same transactional `ScenarioEditorState` sidecar; remove the parallel metadata authority.
- [x] Replace fine-grained preview lifecycle calls with one editor-owned `IScenarioPreviewSession : IDisposable` lane over ShelteredAPI.
- [x] Verify the editor DLL is optional: physically absent, present/disabled, and present/enabled.

### Lane B — Save runtime, routing, backup, and scenario-save ownership

Primary files/directories:

- `ShelteredAPI/Saves/Runtime/**`
- `ShelteredAPI/Saves/Paging/**`
- `ShelteredAPI/Saves/SaveBackups/**`
- `ShelteredAPI/Scenarios/Application/Selection/ScenarioSaveLibrary.cs`
- save-related event and scenario-book callers

Planned consolidation:

- [x] Move pending load/save and active custom-save ownership into `SaveRuntimeState`.
- [x] Keep `PlatformSaveProxy` as the platform adapter, not a second state owner.
- [x] Route all standard/scenario registry selection through `SaveStorageRouter`.
- [x] Make `VanillaSaveRouting` the only vanilla slot/type mapping authority.
- [x] Centralize backup path sanitization.
- [x] Remove unused internal save implementations after reflection/serialization review.

### Lane C — Runtime UI and settings UI

Primary files/directories:

- `ShelteredAPI/UI/**`
- save dialog files under `ShelteredAPI/Saves/Paging/**`
- `ShelteredAPI/Harmony/SettingsKeybindsButtonPatches.cs`

Planned consolidation:

- [x] Move save dialogs onto `NguiModalPrimitives`.
- [x] Use `UIFontCache` and `UIUtil` texture ownership in snapshot/verification UI.
- [x] Reuse `PanelPageState` in mod settings.
- [x] Centralize cloned `UIEventListener` delegate clearing.
- [x] Remove forwarding wrappers left after settings extraction.
- [x] Choose `ShelteredUI` as the keybind entry point while preserving the public facade.
- [x] Remove unreachable/superseded internal UI implementations after compatibility review.

### Lane D — Benchmark and stability tooling

Primary files/directories:

- `tools/performance/ShelteredBenchmark.Runner.psm1`
- `tools/performance/ShelteredBenchmark.Harness.psm1`
- `tools/stability/Invoke-ShelteredAgentStress.ps1`
- associated contract scripts and READMEs

Planned consolidation:

- [x] Extract one platform-session lifecycle: lock, snapshot, configure, launch, ready, lease, stop, restore, unlock.
- [x] Make benchmark and stability code supply workloads to that lifecycle.
- [x] Use one owned-process stop path and one rollback/error path.
- [x] Remove the unused exported UI polling function.
- [x] Consolidate verification baseline/path helpers without obscuring verifier policy.

### Lane E — Manager, ModAPI, Shared, and Decompiler

Primary files/directories:

- `Manager/Core/Models/ManagerBooleanOption.cs`
- `Manager/Core/Services/ManagerBooleanOptionsService.cs`
- `ModAPI/Core/ManagerBooleanOptions.cs`
- `Shared/**`
- Manager theme/settings files
- `Decompiler/**`

Planned consolidation:

- [x] Put the boolean-option DTO and pure normalization/merge policy in `Shared`.
- [x] Keep environment-specific serialization and paths in Manager and ModAPI adapters.
- [x] Keep the active MainForm/view theming owner and remove the dormant parallel implementation.
- [x] Remove proven dead private helpers and the uncompiled editing snippet.
- [x] Remove the unused Decompiler facade and speculative semantic helpers after confirming no CLI/plugin contract.

### Lane F — Documentation and API surface

Primary documents:

- `documentation/ModAPI_Architecture_guide.md`
- `documentation/ModAPI_Sheltered_Boundary_Refactor.md`
- `documentation/ShelteredAPI_Guide.md`
- `documentation/ShelteredAPI_Runtime_UI_Stores_Guide.md`
- `documentation/Custom_Scenarios_Guide.md`
- `documentation/For_Modders_2.0_API_Migration.md`
- `tools/performance/README.md`
- `tools/stability/README.md`
- `documentation/README.md`

Required documentation:

- [x] One ownership diagram covering Manager, ModAPI, ShelteredAPI, and Shared.
- [x] One scenario authoring flow: discovery → canonical target → selection → presentation → mutation.
- [x] One save flow: UI intent → runtime state → storage router → platform adapter → backup/event reporting.
- [x] One benchmark/stability lifecycle with workload extension points and rollback guarantees.
- [x] Supported facade list and internal implementation boundaries.
- [x] Pre-2.0 removals and replacements in the migration guide.
- [x] Contributor rule: extend the canonical lane before creating a parallel helper/service.

## Baseline checklist — before implementation

### Repository and API baselines

- [x] Record dirty-worktree paths and preserve unrelated work.
- [x] Capture `git diff --stat`, API signature baselines, and project graph. Deployment hashes remain part of the live baseline/final gate.
- [x] Run `git diff --check`.
- [x] Build `ShelteredModManager.sln` in Release.
- [x] Confirm `ShelteredAPI/ShelteredAPI.sln` contains only `ShelteredAPI.csproj`, already exercised by the root solution build.
- [x] Run ModAPI boundary, ShelteredAPI public-surface, and runtime compatibility verification.

### Contract baseline

- [x] Run every root `tools/Test-*.ps1` contract script (19 suites passed).
- [x] Run `tools/performance/Test-ShelteredBenchmarkContracts.ps1` (33 assertions passed, including sampler initialization and real `Start-Job` lock-envelope transport).
- [x] Run `tools/stability/Test-ShelteredAgentStressContracts.ps1` (all assertions passed).
- [x] Record flaky tests separately. The native reference-frame gate remains intermittently flaky on both storefronts; the harness semantic readiness and stability routes are reported separately. Existing build warnings were recorded and tests were not weakened.

### Steam and Epic live baseline

- [x] Use the pre-cleanup dual-storefront deployment verified by the harness hash gates on 2026-08-01.
- [x] Launch both platforms concurrently through the agent harness (`2026-08-01_014320_dual-all-supported-long-soak-20260801`).
- [x] Record startup, menu-ready, process, memory, FPS, log, API-registration, and load-order health (`2026-08-01_015617_stability-canonical-3x-20260801`).
- [ ] Capture/confirm every named UI window in the final build; the baseline artifacts cover menu, scenario selection, scenario book, and stress screenshots but are not a complete visual inventory.
- [x] Preserve pre-cleanup logs and platform results in the dated benchmark artifacts. The ten-minute dual all-supported-mod soak had 510 observations, zero alive/responding failures, and no reported campaign failures.

The restoration baseline is `2026-08-01_023756_dual-restore-verification-proving-20260801`; all eight Steam/Epic mutable-state verification rows passed.

## Change-specific regression checklist

### Scenario target and authoring behavior

- [ ] Open authoring in Standard, Surrounded, and Stasis bases.
- [ ] Discover the same backdrop targets from palette, build-tools, and hierarchy views.
- [ ] Confirm only one discovery snapshot is produced per scene/revision or frame.
- [ ] Select a backdrop from each entry point and compare selected, hovered, multi-selection, and stack state.
- [ ] Cycle overlapping selection-stack targets before and after backdrop selection.
- [ ] Swap a backdrop sprite, preview it, save it, restore it, and reopen the draft.
- [ ] Import a same-size PNG and verify ownership/persistence.
- [ ] Open the pixel editor, paint, undo, redo, save, cancel, and switch targets while the picker is open.
- [ ] Confirm weather/effect target selection still uses the same canonical lane.
- [ ] Exercise the sole shell renderer and confirm no superseded fallback renderer remains selectable.
- [ ] Validate advanced-details visibility in every authoring window.

### Scenario library and saves

- [x] Vanilla Surrounded and Stasis windows show only their stock save.
- [x] Unlimited Surrounded and Stasis archives remain separate.
- [ ] ShelteredAPI's runtime Custom Scenarios browser contains installed scenarios and save archives only, with no draft/add/edit/duplicate/import/recovery actions.
- [ ] The enabled standalone editor supplies draft creation/editing without adding a second installed-scenario catalog or launch lane.
- [ ] Custom scenario navigation, search/sort pressure, authoring/playtest, save, and disposable-draft cleanup work after extraction.
- [ ] Long labels, populated save lists, and delete/result columns do not overlap.
- [ ] Start and load a custom scenario without replacing the stock vanilla scenario slot.

### Editor metadata and preview lifecycle

- [ ] Edit checklist items, close/reopen the draft, and confirm `scenario.editor.xml` round-trips without changing `scenario.xml`.
- [ ] Corrupt the live sidecar and confirm the last-good `.bak` recovery path is reported and loaded.
- [ ] Duplicate a draft and confirm its sidecar is copied with independent subsequent edits.
- [ ] Create autosave and named-version XML/sidecar pairs; verify incomplete pairs stay hidden and cannot restore.
- [ ] Export with and without README; confirm no `*.editor.xml` is packaged and the enabled README reflects the current session checklist.
- [ ] Open a preview, refresh world/assets, restart, capture snapshots, and close it; confirm each replacement/close/failure/shutdown disposes the one current session.
- [ ] Confirm disposal clears the process-local preview definition, fixed seed, quest carrier, and preview runtime binding without reporting a scenario outcome.
- [ ] Confirm no `EndPreview`, editor static wrapper lane, or second Sheltered-specific registration facade remains.

### Save runtime and backups

- [ ] Standard save/load/delete across physical and expanded slots.
- [ ] Surrounded and Stasis stock save/load without expanded paging.
- [ ] Custom and unlimited scenario save/load/delete.
- [ ] Pending save/load state is visible through one owner and clears on success/failure/cancel.
- [ ] Save event ordering remains unchanged.
- [ ] Backup create/list/inspect/restore/delete and lineage navigation.
- [ ] Snapshot browse, sort, warning, verification, and return-page behavior.
- [ ] Vanilla mirror conflict and condense dialogs.
- [ ] Simulated interrupted restore and manual recovery leave installs/saves byte-identical.

### Runtime and settings UI

- [ ] All migrated dialogs preserve focus, input blocking, depth, callbacks, and close behavior.
- [ ] Mod settings paging, section headers, presets, keybind capture, and value persistence.
- [ ] Settings/keybind entry points open the same supported facade.
- [ ] Runtime UI store rebinds on open, close, resume, scene transition, and mod reload.
- [ ] Repeated open/close does not leak textures, fonts, widgets, event delegates, or GameObjects.

### Manager and ModAPI

- [ ] Boolean option creation, metadata merge, normalization, read/write, and round-trip compatibility in both processes.
- [ ] Existing option files remain readable without migration.
- [ ] Manager theme changes every supported view through one owner.
- [ ] Mod discovery, dependencies, load order, compatibility checks, and plugin loading.
- [ ] Public API surface and migration baselines reflect only deliberate 2.0 changes.
- [ ] Decompiler CLI output and the actively used IL-range semantic path remain unchanged.

### Benchmark and stability lifecycle

- [x] Benchmark contract suite verifies the shared lifecycle.
- [x] Stability contract suite verifies the same lifecycle with concurrent sessions.
- [x] Normal benchmark success restores both installs.
- [x] Failure before launch, after launch, after lease, and during workload restores both installs.
- [x] Ctrl+C/cancellation and manual recovery preserve restoration boundaries.
- [x] Steam and Epic can run simultaneously without lease, port, process, or snapshot collision.
- [x] Performance metrics and screenshots retain their current schemas.

### Standalone editor build/deploy matrix

- [x] Build ModAPI alone.
- [x] Build ShelteredAPI alone without compiling or resolving ShelteredScenarioEditor.
- [x] Build ShelteredScenarioEditor against the public ModAPI/ShelteredAPI surfaces.
- [x] Build the full Debug and Release solution.
- [x] Package `ShelteredScenarioEditor.dll` as optional, not as a required runtime file.
- [x] Verify Steam and Epic deploy identical ModAPI, ShelteredAPI, and (when present) editor hashes.
- [x] Steam absent: physically remove only `ShelteredScenarioEditor.dll` through the transactional runner; start, browse/launch/resume installed scenarios, exercise saves, and scan for editor type-resolution failures.
- [x] Epic absent: run the same physical-absence checks from identical ModAPI/ShelteredAPI hashes.
- [x] Concurrent absent: run Steam and Epic together and verify independent process ownership, logs, restoration, stock saves, unlimited Surrounded/Stasis archives, and modded save lanes.
- [x] Steam disabled: deploy the editor DLL with `ShelteredScenarioEditor.Enabled=false`; confirm no editor graph, patch, Unity object, window, draft service, or preview session is created.
- [x] Epic disabled: run the same disabled checks from identical managed artifact hashes.
- [x] Concurrent disabled: pressure menu, installed browser, launch/resume, and all save lanes on both storefronts while confirming behavior matches physical absence.
- [x] Steam enabled: exercise every editor workspace/module, rapid clicking, draft create/edit/duplicate/import/export, sidecar/snapshot pairs, preview lifecycle, save/load/restart, and clean shutdown.
- [x] Epic enabled: run the same enabled workload from identical managed artifact hashes.
- [x] Concurrent enabled/all mods: load every supported mod on both storefronts, apply repeated UI/input pressure, run long games and soak/restart cycles, monitor logs/health/memory/FPS, and restore installs/saves byte-identically.
- [x] Compare editor-off and editor-on startup, working set, scenario-browser transition, smooth FPS, long-run growth, logs, and restoration against the recorded baseline gates.

## Historical baseline acceptance plus extraction closeout

- [x] Full Release solution build succeeds; existing repository warnings remain and no touched cleanup member introduced a warning.
- [x] Every contract and verification script passes.
- [x] `git diff --check` passes.
- [x] No public facade removal is undocumented.
- [x] No internal implementation remains registered beside a superseding owner without an explicit fallback reason.
- [x] No new private helper is referenced only once unless the final report explains why it is an intentional policy/resource boundary.
- [x] Steam and Epic deploy the same managed artifact hashes.
- [x] Both platforms pass concurrent startup/menu/scenario/save/settings/authoring smoke tests.
- [x] Both platforms complete a longer stability workload with rapid UI actions and all intended mods enabled.
- [x] Final logs are compared with baseline; no new failure class or material memory regression was observed.
- [x] Architecture and migration documentation match the final extracted code and verified lifecycle.
