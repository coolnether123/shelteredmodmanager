# Architecture Redundancy Audit

Date: 2026-08-01
Repository: `A:\Dev\GitHub\Coolnether123\Sheltered\shelteredmodmanager`

## Purpose

This is a read-only review for accidental additive architecture: code that introduces a second owner, parallel implementation, unused facade, duplicated policy, or one-call wrapper where an established path should have been extended. No production or test code was changed as part of this audit.

The review covered ShelteredAPI scenarios, saves, UI, Harmony/hooks, Manager, ModAPI, Shared, Decompiler, and the performance/stability tooling. Generated/decompiled game sources were not treated as maintained repository code. Static-reference results were checked against Harmony discovery, Unity callbacks, WinForms events, reflection, and public SDK compatibility before being classified.

## Summary

| Severity | Count | Main risk |
|---|---:|---|
| High | 6 | Competing ownership, unreachable major implementations, repeated full lifecycle or scene work |
| Medium | 12 | Duplicate policy and construction paths likely to drift |
| Low | 14 | Dead helpers, pass-through layers, and small repeated primitives |
| **Total** | **32** | |

The clearest recurrence of the recent scenario-save mistake is **parallel ownership**. The repository often adds a coordinator, facade, renderer, catalog, or runner without retiring or extending the existing owner. That leaves both paths compiled and makes behavioral drift likely.

## High-severity findings

### H1. Backdrop authoring creates a parallel target-discovery and selection pipeline

Evidence:

- New catalog and scene scan: `ShelteredAPI/Scenarios/Application/Selection/ScenarioBackdropTargetCatalogService.cs:15`, `:26`, `:74`.
- Existing canonical target adapter/constructor: `ShelteredAPI/Scenarios/Application/Authoring/ScenarioAuthoringSelectionService.cs:1017`, with construction at `:1070` and background classification at `:1203`.
- New backdrop action route: `ShelteredAPI/Scenarios/Application/Commands/ScenarioAuthoringCommandHandlers.cs:2670`, resolved separately at `:2687`.
- Existing hierarchy action for the same `Kind:instanceId` identity: the same file at `:2677` and `:2753`.
- Backdrop selection clears selection-stack state through `ApplySelectedTarget` at `:2810-2820`; hierarchy selection assigns at `:2769-2773` and leaves that state intact.

Why this is accidental: discovery, identity construction, scope validation, and state mutation now have two owners with observably different behavior.

Recommendation: expose one target factory/resolver from the selection subsystem, filter its canonical targets for backdrop presentation, and route backdrop and hierarchy clicks through one generic selection operation.

### H2. Backdrop rows trigger duplicate full-scene scans through three presentation paths

Evidence:

- Asset backdrop list: `ShelteredAPI/Scenarios/Presentation/Authoring/Shell/ScenarioAssetAuthoringContentBuilder.cs:83`, with row projection around `:158-193`.
- Hierarchy backdrop list: `ShelteredAPI/Scenarios/Presentation/Authoring/Shell/ScenarioHierarchyAuthoringContentBuilder.cs:42`.
- Every `GetTargets()` call runs `FindObjectsOfType<SpriteRenderer>()`: `ShelteredAPI/Scenarios/Application/Selection/ScenarioBackdropTargetCatalogService.cs:30`.
- Palette/build-tools can independently call `BuildBackdropSections`: `ScenarioAuthoringPresentationBuilder.cs:1652` and `:1840`; hierarchy is registered at `:1615`.

Why this is accidental: one presentation update can discover and project the same live scene targets up to three times.

Recommendation: create one backdrop snapshot per scene revision or presentation frame and share one row/view-model projection between windows.

### H3. Two complete authoring renderers are effectively unreachable

Evidence:

- Three renderers are registered at `ShelteredAPI/Scenarios/Composition/ScenarioPresentationModule.cs:28` and supplied in priority order at `:61-67`.
- Resolution always takes the first available renderer: `ShelteredAPI/Scenarios/Presentation/Authoring/Shell/ScenarioAuthoringPresentationService.cs:126`.
- Shell IMGUI has priority 200 and eagerly creates runtime state before returning true from `CanRender()`: `ScenarioAuthoringShellImguiRenderModule.cs:159`, `:230-246`.
- Legacy IMGUI is priority 100: `ScenarioAuthoringImguiRenderModule.cs:45`.
- NGUI is priority 50: `ScenarioAuthoringNguiRenderModule.cs:51`.
- `shell.renderer_mode` is registered at `ScenarioAuthoringSettingsService.cs:30`, but only NGUI checks it—after Shell IMGUI has already won.

Impact: roughly 3,002 lines of lower-priority renderer code cannot be selected through the setting intended to select renderer mode.

Recommendation: make renderer mode participate in selection before priority resolution, or retire obsolete implementations and the ineffective setting after fallback requirements are confirmed.

### H4. Save runtime state has two ownership surfaces

Evidence:

- `ShelteredAPI/Saves/Runtime/PlatformSaveProxy.cs:10` owns the pending-load and pending-save locks/dictionaries plus `ActiveCustomSave`.
- `ShelteredAPI/Saves/Runtime/SaveRuntimeState.cs:27` forwards `ActiveCustomSave`, while `:164-360` manipulates the proxy-owned collections.
- Callers bypass the coordinator: `ShelteredAPI/Events/GameEvents.cs:379` reads the proxy directly; `ScenarioBookBrowserDataSource.cs:1066` reads its dictionaries directly; other save code uses `SaveRuntimeState`.

Why this is accidental: the new coordinator did not become the owner, so its invariants remain optional.

Recommendation: move state ownership into `SaveRuntimeState`; leave only platform adapter operations on `PlatformSaveProxy`.

### H5. More than 1,060 compiled ShelteredAPI lines are unreachable or superseded

No maintained-code consumers were found for:

- `ShelteredAPI/Saves/IdGenerator.cs:5`—backup code creates GUIDs directly.
- `ShelteredAPI/Saves/PreviewAuto.cs:11`—`EnsureHooked` is never called.
- `ShelteredAPI/Saves/SaveManagerInspector.cs:5`—marked temporary and never called.
- `ShelteredAPI/UI/Compatibility/UIFactory.cs:13`—entire factory/options/wrapper stack unused.
- `ShelteredAPI/UI/Internal/UIPatchCoordinator.cs:5`—seven forwarding methods, no consumers.
- `ShelteredAPI/UI/FieldManual/Frame/FieldManualFrame.cs:19`—superseded by `ShelteredBookFrame`, selected at `FieldManualWindowChrome.cs:69`.
- `ShelteredAPI/UI/FieldManual/Layout/PaperScrollList.cs:17`—complete unused scrolling implementation.
- `ShelteredAPI/UI/FieldManual/Diagnostics/FieldManualLayoutDiagnostics.cs:8`—unused 230-line diagnostics implementation.
- `ShelteredAPI/UI/FieldManual/Primitives/HoverReveal.cs:10`.
- `ShelteredAPI/UI/Internal/Runtime/Widgets/RuntimeQuantityStepper.cs:5`.
- `ShelteredAPI/UI/Compatibility/ContextUIExtensions.cs:10`—no internal or friend-assembly consumer.
- `ShelteredAPI/Hooks/UIHooks.cs:11` and `WorldHooks.cs:14`—internal API classes with no repository consumers.

Recommendation: confirm compatibility obligations, then remove unreachable implementations instead of maintaining them beside their replacements.

### H6. The stability runner duplicates the benchmark platform lifecycle

Evidence:

- `tools/stability/Invoke-ShelteredAgentStress.ps1:185-241` independently handles snapshots, mod resolution, Doorstop/load-order/options mutation, hash checks, process launch, sampling, harness connection, leases, readiness, and blockers.
- The existing sequence is already in `tools/performance/ShelteredBenchmark.Runner.psm1:399-457`.
- Stress cleanup at `Invoke-ShelteredAgentStress.ps1:386-427` duplicates benchmark cleanup at `ShelteredBenchmark.Runner.psm1:545-580`.
- The stress script imports the runner at line 24 but uses only low-level pieces to construct a second session orchestrator.
- One-call `Stop-StabilityOwnedProcess` at line 106 parallels `Stop-BenchmarkGameProcess` at runner line 318.

Risk: platform ownership, rollback, and cleanup rules can drift between performance and stability campaigns.

Recommendation: extract reusable platform-session start/ready/stop/restore operations from the benchmark runner. Keep benchmark and stress workloads separate while sharing lifecycle ownership.

## Medium-severity findings

### M1. Transform-path construction is independently implemented ten times

Locations:

- `ScenarioBackdropTargetCatalogService.cs:129`
- `ScenarioAuthoringSelectionService.cs:504`, `:1349`, `:1414`
- `ScenarioAuthoringCommandHandlers.cs:2873`
- `ScenarioSpriteFamilyMatcher.cs:353`
- `ScenarioSpriteRuntimeResolver.cs:335`
- `ScenarioWeatherEffectSpriteCatalogService.cs:297`
- `ScenarioAuthoringUiDebugService.cs:245`
- `ScenarioHierarchyAuthoringContentBuilder.cs:427` (`BuildHierarchyPath`)

The parent-walk/reverse/join policy is duplicated, and null handling has already drifted between `null` and `string.Empty`.

Recommendation: one `ScenarioTransformPath.Build(Transform)` utility with one explicit null contract.

### M2. Inspector item construction passes through up to three facade layers

Evidence:

- Actual construction: `ShelteredAPI/Scenarios/Presentation/Inspector/ScenarioInspectorItemFactory.cs:7`.
- First forwarding layer: `ScenarioAuthoringPresentationUtilities.cs:10` (`Action`, `Text`, `Property`, `ActionItem`, `Safe`).
- Second forwarding layer: `ScenarioAuthoringInspectorItemFacade.cs:5` forwards the same API to the utilities layer.

Neither facade adds validation or transformation, and their overloads expose different subsets of the real API.

Recommendation: retain at most one authoring convenience facade and have it call the factory directly.

### M3. Three scenario service adapters only forward interface members

Evidence:

- Serializer, catalog, and validator adapters: `ShelteredAPI/Scenarios/Shared/ScenarioServiceAdapters.cs:8`, `:48`, `:73`.
- DI creates concrete services and then wrappers only to expose interfaces: `ServiceCollectionExtensions.cs:36`, `:44`, `:300`.

Recommendation: implement the application interfaces directly on `ScenarioDefinitionSerializer`, `ScenarioCatalog`, and `ScenarioValidatorImpl`, then bind those instances to the interfaces.

### M4. Advanced-details state is repeatedly decoded from a string setting

Evidence:

- Definition: `ScenarioAuthoringSettingsService.cs:36`.
- Duplicate helpers: `ScenarioAssetAuthoringContentBuilder.cs:711`, `:933`; `ScenarioAssetInventoryContentBuilder.cs:178`; `ScenarioAuthoringPresentationBuilder.cs:3924`; `ScenarioHierarchyAuthoringContentBuilder.cs:420`; `ScenarioWorkflowAuthoringContentBuilder.cs:549`.
- Additional direct reads exist in overview, publish, test-console, and renderer builders.

Recommendation: expose typed `ShowAdvancedDetails` state on the settings snapshot/service.

### M5. Five save dialogs duplicate the existing modal primitive layer

Independent box/label/button/collider/font/depth construction exists in:

- `ShelteredAPI/Saves/Paging/CondensePromptDialog.cs:193`
- `CustomSavesWelcomeDialog.cs:140`
- `SaveDetailsWindow.cs:693`
- `SnapshotLoadWarningDialog.cs:209`
- `VanillaMirrorConflictDialog.cs:190`

The existing primitive set is `ShelteredAPI/UI/NguiModalPrimitives.cs:9`, currently used by `LoadingTransitionRecoveryDialog`.

Recommendation: route the five save dialogs through those primitives or one shared dialog shell, leaving dialog-specific composition local.

### M6. Snapshot UI duplicates shared font and texture caches

Evidence:

- `SaveSnapshotSlotControls.cs:271` owns a private font cache; line 284 creates a private white texture.
- `SaveVerification.cs:407` repeats the font cache; `:420-438` creates a new 2×2 texture per verification icon.
- Existing shared facilities: `ShelteredAPI/UI/Compatibility/UIFontCache.cs:23` and `UIUtil.cs:16`.

Recommendation: use the shared caches; avoid per-icon Unity texture allocation.

### M7. Backup path sanitization is copied verbatim

Identical invalid-character replacement, explicit `\ / : |` replacement, and 96-character truncation exist at:

- `ShelteredAPI/Saves/SaveBackups/SaveBackupService.cs:612`
- `ShelteredAPI/Saves/SaveBackups/SaveBackupRepository.cs:2355`

Recommendation: one backup path policy used for both branch markers and timeline directories.

### M8. Vanilla slot routing is redefined four times

The authority is `ShelteredAPI/Saves/Runtime/VanillaSaveRouting.cs:14`, but mappings are repeated in:

- `ShelteredAPI/Saves/Paging/SlotPagingScope.cs:103`
- `ShelteredAPI/Saves/SaveBackups/SaveBackupService.cs:134`
- `SaveBackupService.cs:225`
- `ShelteredAPI/Saves/Runtime/SaveDeleteRouter.cs:124`

Recommendation: add reverse lookup/recognition to `VanillaSaveRouting` and reuse `VanillaSaveRoute` values.

### M9. ScenarioSaveLibrary duplicates the existing storage router

- Duplicate registry branch: `ShelteredAPI/Scenarios/Application/Selection/ScenarioSaveLibrary.cs:230`.
- Existing authority: `ShelteredAPI/Saves/Runtime/SaveStorageRouter.cs:29`.

Recommendation: convert through `ScenarioSelectionIds.ToStorageScenarioId`, then use the existing router.

### M10. ModSettingsPanel retained forwarding wrappers after extraction

Pure forwarding methods:

- `ShouldUseWideKeybindLayout`: `ShelteredAPI/UI/Compatibility/ModSettingsPanel.cs:524`
- `UpdateCurrentPresetState`: `:614`
- `BuildDisplayEntries`: `:755`
- `IsSectionHeaderEntry`: `:941`
- `ApplySettingValue`: `:946`

They forward to `ModSettingsKeybindLayout`, `ModSettingsPresetController`, or `ModSettingsKeybindRuntime` without adding behavior.

Recommendation: call the extracted owners directly.

### M11. Manager boolean-option persistence is implemented twice

Evidence:

- Manager schema: `Manager/Core/Models/ManagerBooleanOption.cs:5-34`.
- ModAPI repeats the persisted schema: `ModAPI/Core/ManagerBooleanOptions.cs:340-376`.
- Manager merge/normalization: `ManagerBooleanOptionsService.cs:120`, `:135-169`, `:252-277`.
- ModAPI merge/normalization: `ModAPI/Core/ManagerBooleanOptions.cs:151-247`.

The serializers must differ for runtime compatibility, but the DTO contract and pure normalization/merge policy do not.

Recommendation: move contract and pure policy into `Shared`; retain thin Manager and ModAPI path/serialization adapters.

### M12. A complete unused theme implementation exists beside the active route

- Dormant implementation: `Manager/ThemeManager.cs:6-89`; no repository references.
- Active route: `Manager/MainForm.cs:2026-2100` plus per-view `ApplyTheme` methods.

Recommendation: choose one theming path. Fold useful palette/recursive logic into the active path or remove the dormant implementation.

## Low-severity findings

### L1. Build-action classification is duplicated exactly

- `IsBuildPlacementStartAction`: `ScenarioAuthoringCommandHandlers.cs:674`.
- `IsPlaceableBuildAction`: `ScenarioAssetAuthoringContentBuilder.cs:558`.

Both have one caller and the same four conditions.

Recommendation: put the policy beside `ScenarioAuthoringActionIds` and reuse it.

### L2. Pure pass-through scenario helpers add names but no behavior

Confirmed examples:

- `ApplyCompatibilityState`: `ScenarioStageCoordinator.cs:129`
- `DecodeActionToken`: `ScenarioSceneSpritePlacementAuthoringService.cs:913`
- `IsWeatherEffectArtTarget`: `ScenarioSpriteCatalogService.cs:480`
- `MatchesObjectPlacement`: `ScenarioBuildDeletionAuthoringService.cs:593`
- `BuildEventGraphItems`: `ScenarioAuthoringPresentationBuilder.cs:2540`

Each is single-use and forwards an existing operation or predicate without creating a meaningful policy seam.

Recommendation: call the existing operation directly unless the helper gets independent policy/tests.

### L3. Dead or hollow scenario authoring artifacts remain

- Unused `ScenarioAuthoringSetupChecklistItem`: `ScenarioAuthoringSetupState.cs:60`.
- Unused `_settingsScrollPosition`: `ScenarioAuthoringShellImguiRenderModule.cs:89`.
- Unused `ReopenShortcut`: `ScenarioAuthoringMenuService.cs:12`.
- Empty `ScenarioAuthoringMenuService.Update`: line 23, called every authoring update at `ScenarioAuthoringBootstrapService.cs:493`.

Recommendation: remove dead members and the per-update hollow call unless concrete menu behavior is restored.

### L4. UI lifecycle is a pass-through service with three identical methods

`ShelteredAPI/UI/Internal/Runtime/RuntimeUiLifecycleService.cs:6` defines opened, closed, and resumed methods that ignore the panel and all call `RuntimeUiRegistry.RequestRebindAll()`. Its sole caller is `UIPanelLifecycleRuntimeService.cs:8`.

Recommendation: call the registry directly at lifecycle sites or give the service event-specific behavior.

### L5. Single-use keybind facade is already bypassed

`ShelteredAPI/UI/ShelteredKeybindsUI.cs:6` only forwards `Show()` to V2. One caller uses it at `SettingsKeybindsButtonPatches.cs:102`, while `ShelteredUI.cs:122` already calls V2 directly.

Recommendation: remove the wrapper or make it the single stable entry point used everywhere.

### L6. Cloned-event cleanup is implemented three times

The same ten `UIEventListener` delegates are cleared at:

- `ShelteredAPI/UI/Compatibility/UIUtil.cs:168`
- `ShelteredAPI/UI/Internal/UIExtensionService.cs:316`
- `ShelteredAPI/UI/Internal/ModManager/ModManagerPanelScaffolding.cs:327`

Recommendation: centralize only delegate clearing; retain surrounding policy locally.

### L7. Mod settings duplicates shared page-state behavior

- Manual `_currentPageIndex`, clamping, movement, and visibility: `ModSettingsPanel.cs:64`, `:384-397`, `:988-1003`.
- Existing shared state: `ShelteredAPI/UI/FieldManual/Layout/PanelPageState.cs:8`, already used by `KeybindsPanelV2`.

Recommendation: reuse `PanelPageState`; keep section-aware page construction local.

### L8. Context-menu Harmony patches cannot receive registrations

`ContextMenuHelper.cs:35` has an internal registration method with no caller, while patches at lines 46 and 65 execute against an always-empty `_addons` list.

Recommendation: expose/wire registration intentionally or remove/defer the patch host.

### L9. Exported harness polling function has no callers

`tools/performance/ShelteredBenchmark.Harness.psm1:279-297` defines `Wait-ShelteredUiPath`; it is exported at line 364 but has no PowerShell AST invocation.

Recommendation: remove it or make transition functions use it as the readiness primitive.

### L10. Verification scripts duplicate baseline/path infrastructure

- `ConvertTo-RepoRelativePath`: `tools/Verify-ModApiBoundary.ps1:118`, `Verify-ShelteredApiPublicSurface.ps1:25`, `Scan-StaleVersionReferences.ps1:29`.
- Near-identical five-column baseline readers: boundary script `:257-298`, surface script `:84-116`.
- Repeated key/TSV helpers: boundary `:146-149`, `:252-255`; surface `:53-61`.

Recommendation: a small verification-support module with configurable row and message policy.

### L11. Decompiler has an unused facade and speculative semantic APIs

- `Decompiler/DecompilerService.cs:3-13` only forwards to `DecompilerEngine`; no consumers.
- Entry point constructs the engine directly: `Decompiler/Program.cs:87`.
- Unused semantic helpers: `Decompiler/SemanticExtensions.cs:31`, `:42`, `:53`; only `GetILRanges` is used at `DecompilerEngine.cs:217`.

Recommendation: retain the direct engine path and currently consumed semantic operations.

### L12. Stale manager setting mirrors the active model

`Manager/ManagerSettings.cs:3-7` contains only `SkipHarmonyDependencyCheck` and is unused. The active property is `Manager/Core/Models/AppSettings.cs:110`.

Recommendation: keep `AppSettings` as the sole owner.

### L13. Private helpers exist without any connection to active code

- `PluginManager.SafeAssemblyLocation`: `ModAPI/Core/PluginManager.cs:334-337`; duplicates active `SafeAssemblyPath` at `:1343-1347`.
- `PluginManager.ProbeModRootFromAssembly`: `:1349-1363`.
- `MMLog.TryParseLogCategory`: `ModAPI/Core/MMLog.cs:215-225`.
- `ModManagerTab.ConvertUnixDate`: `Manager/Views/ModManagerTab.cs:951-964`.
- Empty, unwired `MainForm._gameSetupTab_Load`: `Manager/MainForm.cs:2266-2269`.

Recommendation: remove dead helpers; add behavior to an active path only when there is a real caller.

### L14. A committed editing snippet duplicates live code

`Manager/Controls/SearchBox.cs_snippet_to_insert:1-13` is not compiled. Its `Dispose` behavior already exists at `Manager/Controls/SearchBox.cs:234`.

Recommendation: remove the editing artifact.

## Investigated candidates that were not findings

- Harmony `Prefix`, `Postfix`, `Prepare`, and patch hosts can be reflection-discovered; missing textual callers are not evidence of dead code.
- Unity `Awake`, `Update`, `OnDestroy`, `OnDisable`, and `OnHover` callbacks do not require textual callers.
- WinForms event handlers were excluded unless they were both empty and proven unwired.
- Public ModAPI helpers can be external SDK surfaces; no in-repository caller alone is insufficient.
- `ResetQuitStateForHost` is invoked reflectively from `ShelteredAPI/Harmony/MainMenuPatches.cs:141`.
- `.cmd` files beside PowerShell scripts are launch shims, not second implementations.
- Manager and ModAPI `ModAboutReader` classes intentionally use different models, serializers, validation, and dependency boundaries.
- Manager load-order resolution and ModAPI runtime load-plan application are different responsibilities.
- Benchmark native capture/readiness helpers isolate platform interop and async ownership; their single call sites are legitimate.
- `ScenarioDefinitionCatalogRefreshCoordinator` adds revision detection and retry behavior; it is a meaningful decorator.
- `ScenarioAuthoringInputCaptureService.TransitionActive` is a deliberate frame snapshot, not competing state.
- Current-diff `ReconcilePickerTarget` has one caller but enforces a real action-entry invariant across partial class files; it was not classified as removable without deciding the invariant's proper owner.
- `SaveBackupRepository` one-call helpers divide transaction planning, validation, journaling, durable mutation, rollback, and recovery and should not be collapsed merely to reduce line count.
- `PanelPageState`, `PaperPagePaginator`, `PaperPagedList`, `BookPageNavigatorWidget`, and `FieldManualPageTurnController` have distinct state/render/navigation/animation responsibilities.
- `LoadingTransitionFallbackDialog` and `LoadingTransitionRecoveryDialog` are intentionally different IMGUI/NGUI recovery paths.
- `ShelteredSaves`, `ShelteredRuntimeUI`, `UITakeover`, and `ISaveApi` are intentional public facade/contract boundaries.
- Default-argument overloads such as `DrawCandidateCard`, `ConfigureButton`, and `Close` reduce repeated argument lists across multiple useful call sites.

## Recommended order of future work

No fixes were made. If remediation is requested, the safest order is:

1. Establish single ownership for the stability lifecycle, save runtime state, and scenario target selection.
2. Decide which authoring renderer is supported before deleting or rewiring renderer code.
3. Remove or quarantine proven unreachable implementations after compatibility review.
4. Consolidate repeated routing, path, modal, cache, and settings policies.
5. Remove low-risk dead helpers and pass-through layers last.
