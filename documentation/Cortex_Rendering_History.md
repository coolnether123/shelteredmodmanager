# Cortex Rendering History

This document records the render-related Cortex history from the git log.
It is a dated recall of commits, file changes, and architecture state as it appeared in the repository history.

Source commands used:

- `git log --grep=render --grep=renderer --grep=presentation --grep=host --grep=bridge --all`
- `git show --name-only --date=short` for the commits listed below

## Methodology

Phase boundaries are based on clusters of commits that moved ownership or dependency direction for rendering, presentation, shell execution, host selection, or bridge-based presentation. The boundary usually ends where tests, docs, packaging, or host wiring made the new ownership shape visible in the repository.

Commits were included when they directly changed render/presentation ownership, host/runtime UI selection, shell execution, renderer-neutral models, desktop bridge presentation, or tests/docs/packaging that proved those changes. Non-render commits were folded in only when their file changes affected the render path or the runtime/shell/host boundary.

Commits were excluded when they were general feature work, unrelated plugin behavior, ordinary editor workflow changes without render-boundary impact, or checkpoint refs. Each phase includes a confidence note to distinguish direct file evidence from reconstruction based on commit messages and nearby topology changes.

## Phase 1 - Renderer Boundary Foundation

Date range: 2026-03-26 through 2026-04-02

Confidence: High for file ownership and dependency shifts; medium for exact motivation because some motivation is reconstructed from commit messages and docs.

### Why This Phase Existed

Goal: create a renderer boundary around Cortex shell/editor presentation.

Problem being solved: editor, popup, hover, panel, and inspector behavior was still mixed with IMGUI-oriented surface code.

Boundary being moved: rendering contracts and frame/runtime UI behavior moved out of shell/editor call sites toward `Cortex.Rendering`, `Cortex.Rendering.RuntimeUi`, and host-owned runtime UI composition.

### Code Snapshots

| Kind | Commit | Notes |
| --- | --- | --- |
| Compare against | `132f363` | Parent of phase start `6772ae5`. |
| Start | `6772ae5` | First commit in this phase's selected render/presentation chain. |
| End | `f7c39e3` | Build/topology hardening after runtime UI extraction. |
| Recommended inspection snapshot | `a1fcd8a` | Rendering proof coverage and renderer/host documentation landed here. |

### Commits

- `6772ae5` 2026-03-26 - Centralize semantic token presentation and caret readiness
- `f100739` 2026-03-26 - Centralize editor context and diagnostics contracts
- `9136a53` 2026-03-26 - Add shared symbol occurrence highlighting
- `b01fe96` 2026-03-27 - Implement shared Cortex editor hover system
- `c71bd6f` 2026-03-28 - Refine panel rendering and method inspector relationships
- `a23042c` 2026-03-28 - Separate inspector presentation from rendering and harden stale request handling
- `1bd39e4` 2026-03-26 - Refactor Cortex rendering foundation
- `cd850eb` 2026-04-02 - Refactor Cortex into a host-isolated portable core
- `bf28085` 2026-04-02 - refactor(cortex-rendering): extract runtime UI/frame contracts into portable rendering layers
- `7a7a434` 2026-04-02 - Refactor Cortex shell rendering around host-owned runtime UI
- `f23c149` 2026-04-02 - refactor(cortex-shell): adopt portable shell planners and host-supplied runtime UI
- `a1fcd8a` 2026-04-02 - Add rendering proof coverage and boundary documentation
- `f7c39e3` 2026-04-02 - build(cortex): harden Manager bundling, solution topology, and runtime-ui enforcement

### Mini Diff Summary

| Change type | Summary |
| --- | --- |
| Added assemblies | `Cortex.Rendering.RuntimeUi`, expanded `Cortex.Rendering`, expanded `Cortex.Renderers.Imgui`, expanded `Cortex.Presentation` |
| Removed ownership from | shell/editor surfaces that previously carried popup, tooltip, panel, hover, and inspector rendering behavior |
| New dependencies | shell/editor code depended on `Cortex.Rendering`, `Cortex.Rendering.RuntimeUi`, and `Cortex.Presentation`; host code supplied frame/runtime UI composition |
| Deleted transitional paths | duplicated popup/tooltip helpers and stale runtime frame-context ownership were removed or reduced during the split |

### Before Architecture Snapshot

- Shell/editor surfaces owned most render-time behavior directly.
- IMGUI concepts were present in editor, popup, hover, panel, and inspector paths.
- Host/backend selection was not yet consistently expressed as a host-owned runtime UI composition path.
- Presentation shaping and drawing responsibilities were still in the same call paths.

```text
Cortex shell/editor surfaces
  -> IMGUI-oriented surface code
  -> editor/popup/tooltip/panel behavior
  -> partial renderer abstractions

Cortex.Host.Unity / Cortex.Host.Sheltered
  -> mixed host services and runtime UI setup
```

### After Architecture Snapshot

- `Cortex.Rendering` owned low-level render models and frame/input contracts.
- `Cortex.Rendering.RuntimeUi` owned portable runtime UI contracts and reusable popup/panel/tooltip/shell planners.
- `Cortex.Renderers.Imgui` remained the concrete IMGUI drawing backend.
- Host projects owned runtime UI/frame composition.
- Presentation services/models started separating prepared output from concrete rendering.

```text
Cortex shell/editor services
  -> Cortex.Presentation
  -> Cortex.Rendering
  -> Cortex.Rendering.RuntimeUi

Cortex.Host.Unity / Cortex.Host.Sheltered
  -> host frame/runtime UI composition

Cortex.Renderers.Imgui
  -> concrete IMGUI execution
```

### Ownership Changes

| Area | Before owner | After owner | Evidence commits | Evidence files |
| --- | --- | --- | --- | --- |
| Runtime UI contracts | Shell/editor-local and transitional render paths | `Cortex.Rendering.RuntimeUi` | `bf28085`, `7a7a434` | `Cortex.Rendering.RuntimeUi/Runtime/WorkbenchRuntimeUiContracts.cs`, `Cortex.Rendering.RuntimeUi/Runtime/NullWorkbenchRuntimeUi.cs` |
| Frame/input contracts | Host/shell-specific frame context | `Cortex.Rendering` | `bf28085` | `Cortex.Rendering/Frame/WorkbenchFrameContracts.cs`, `Cortex.Host.Unity/Runtime/UnityWorkbenchFrameContext.cs` |
| Popup/panel/tooltip planning | IMGUI renderer and shared module helpers | `Cortex.Rendering.RuntimeUi` planners/controllers | `7a7a434`, `f23c149` | `Cortex.Rendering.RuntimeUi/PopupMenus/PopupMenuLayoutPlanner.cs`, `Cortex.Rendering.RuntimeUi/Panels/PanelLayoutPlanner.cs`, `Cortex.Rendering.RuntimeUi/Tooltips/HoverTooltipLayoutPlanner.cs` |
| Panel/popup/tooltip drawing | Surface-local IMGUI calls | `Cortex.Renderers.Imgui` concrete renderers | `1bd39e4`, `c71bd6f` | `Cortex.Renderers.Imgui/ImguiPanelRenderer.cs`, `Cortex.Renderers.Imgui/ImguiPopupMenuRenderer.cs`, `Cortex.Renderers.Imgui/ImguiHoverTooltipRenderer.cs` |
| Method inspector presentation | Surface/service mixed path | `Cortex.Presentation` models and services plus editor adapter | `a23042c` | `Cortex.Presentation/Models/MethodInspectorPresentationModels.cs`, `Cortex/Services/EditorMethodInspectorPresentationService.cs`, `Cortex/Modules/Editor/EditorMethodInspectorPanelDocumentAdapter.cs` |
| Runtime UI backend selection | Generic shell/runtime paths | Host-owned runtime UI composition | `7a7a434`, `bf28085` | `Cortex.Host.Sheltered/Runtime/ShelteredUnityHostComposition.cs`, `Cortex.Renderers.Imgui/ImguiWorkbenchRuntimeUiFactory.cs` |

### Code Moved Or Added

- `Cortex.Rendering` added render surface models and frame/input contracts.
- `Cortex.Rendering.RuntimeUi` added runtime UI contracts, null runtime UI, pointer input adaptation, popup/menu/tooltip/panel planners, and shell split/overlay planners.
- `Cortex.Renderers.Imgui` added the IMGUI render pipeline, overlay renderer factory, panel renderer/theme, popup renderer, hover tooltip renderer, workbench runtime UI factory, and related theme/resource helpers.
- Editor files such as `CodeViewSurface.cs`, `EditableCodeViewSurface.cs`, `EditorMethodInspectorSurface.cs`, and `EditorModule.cs` moved toward renderer abstractions and shared presentation services.
- `Cortex.Presentation` added method inspector presentation models and host/presenter abstractions.
- `Cortex.Host.Unity` and `Cortex.Host.Sheltered` gained host-owned frame/runtime UI composition points.
- Tests were added or expanded under `Cortex.Tests/Rendering` and `Cortex.Tests/Architecture`.

### Dependency Changes

```text
Before:
Cortex shell/editor -> IMGUI-shaped render logic
Cortex shell/editor -> host/runtime frame assumptions

After:
Cortex shell/editor -> Cortex.Presentation
Cortex shell/editor -> Cortex.Rendering.RuntimeUi
Cortex.Host.Unity -> host frame context/runtime UI composition
Cortex.Renderers.Imgui -> concrete drawing and measurement
```

### Tests, Docs, Packaging Proof

- Rendering tests were added under `Cortex.Tests/Rendering`, including runtime UI interaction/layout and recording backend coverage.
- Architecture tests were added or expanded under `Cortex.Tests/Architecture`.
- Documentation updates included `documentation/Cortex_Runtime_Rendering_Proposal.md`, `documentation/Cortex_Renderer_Host_Extensibility_Guide.md`, `documentation/Cortex_UI_Surface_Guide.md`, `documentation/Cortex_Portability_Report.md`, and `documentation/Cortex_Runtime_Shell_Separation_Guardrails.md`.
- `f7c39e3` hardened Manager bundling, solution topology, and runtime UI enforcement.

### Known Debt At Phase End

- Unity IMGUI was still the only implemented renderer.
- Alternate renderer support existed as contracts and recording tests, not as a production renderer.
- Some shell/editor call sites still executed Unity IMGUI code directly.
- Shell module execution had not yet been extracted into a dedicated shell assembly.
- Some editor and shell behavior still lived near concrete IMGUI modules after planner extraction.

### Risk And Regression Notes

- `a23042c` added stale request handling for method inspector relationship loading.
- `0f3afcb` immediately before this phase fixed a decompiled editor fold-toggle crash.
- `f7c39e3` addressed bundling/topology/runtime UI enforcement after the split.

### Observed Changes

- Semantic token mapping, caret readiness, hover behavior, symbol highlighting, panel plumbing, and method-inspector presentation moved from surface-local code into shared services and models.
- Render contracts and frame context moved into `Cortex.Rendering`.
- Popup, panel, tooltip, and shell interaction planners moved into `Cortex.Rendering.RuntimeUi`.
- `Cortex.Renderers.Imgui` remained the concrete IMGUI drawing path.
- Host projects took ownership of backend selection and frame adaptation.

### Interpretation

Phase 1 created the first renderer boundary and introduced host-owned runtime UI composition for later shell extraction and alternate renderer work.

## Phase 2 - Shell Extraction And Headless Presentation

Date range: 2026-04-03

Confidence: High. File movement into `Cortex.Shell.Unity.Imgui` and guardrail tests make the ownership shift explicit.

### Why This Phase Existed

Goal: move concrete Unity IMGUI shell execution out of generic `Cortex`.

Problem being solved: generic Cortex still owned shell controller, chrome, module execution, and concrete IMGUI surface code.

Boundary being moved: shell/runtime/host separation moved from convention into a dedicated `Cortex.Shell.Unity.Imgui` assembly and headless presentation services.

### Code Snapshots

| Kind | Commit | Notes |
| --- | --- | --- |
| Compare against | `0f3afcb` | Parent of phase start `e9de45d`. |
| Start | `e9de45d` | Dedicated Unity IMGUI shell assembly introduced. |
| End | `ad5cf8d` | IMGUI backend composition moved into the shell assembly. |
| Recommended inspection snapshot | `ad5cf8d` | Shows shell-owned IMGUI composition after extraction. |

### Commits

- `e9de45d` 2026-04-03 - build(cortex-shell): introduce the Unity IMGUI shell assembly
- `fb11fcc` 2026-04-03 - refactor(cortex-presentation): make shell-owned metadata drive snapshot assembly
- `12c4885` 2026-04-03 - refactor(cortex-onboarding): keep onboarding coordination headless
- `0314e09` 2026-04-03 - refactor(cortex-editor): move editor display policy into presentation services
- `6cb8421` 2026-04-03 - test(cortex-shell): add runtime and shell separation guardrails
- `dfb2ff7` 2026-04-03 - refactor(cortex): add desktop-first topology lanes and guardrails
- `9458723` 2026-04-03 - Refactor Cortex headless presentation and IMGUI shell ownership
- `be7c09f` 2026-04-03 - Move remaining IMGUI execution into shell modules
- `ad5cf8d` 2026-04-03 - Move IMGUI backend composition into shell

### Mini Diff Summary

| Change type | Summary |
| --- | --- |
| Added assemblies | `Cortex.Shell.Unity.Imgui`, `Cortex.Contracts` |
| Removed ownership from | generic `Cortex` shell controller, shell chrome, module execution, and IMGUI runtime UI composition |
| New dependencies | Unity/Sheltered host projects referenced the explicit shell assembly; generic `Cortex` retained headless services and presentation boundaries |
| Deleted transitional paths | generic shell presenter types and remaining IMGUI module execution paths were removed or moved into the shell assembly |

### Before Architecture Snapshot

- `Cortex` still contained concrete shell controller files, shell chrome, overlay execution, module executors, editor surfaces, and layout helpers.
- `UnityWorkbenchRuntime` still participated in shell-facing snapshot construction.
- IMGUI execution and module orchestration were still part of the generic project surface.

```text
Cortex
  -> shell controller/chrome
  -> IMGUI module execution
  -> editor surface rendering
  -> presentation shaping

Cortex.Host.Unity
  -> generic Cortex shell/runtime
```

### After Architecture Snapshot

- `Cortex.Shell.Unity.Imgui` owned Unity IMGUI shell execution.
- `Cortex.Presentation` owned snapshot projection services/models.
- Generic `Cortex` retained headless services and runtime coordination.
- `Cortex.Contracts` appeared as a desktop-shareable lane.
- Host projects depended on the explicit shell assembly for the legacy Unity IMGUI path.

```text
Cortex
  -> headless services/runtime coordination
  -> Cortex.Presentation

Cortex.Shell.Unity.Imgui
  -> shell controller/chrome
  -> IMGUI module execution
  -> shell-local editor/render services

Cortex.Host.Unity / Cortex.Host.Sheltered
  -> Cortex.Shell.Unity.Imgui
```

### Ownership Changes

| Area | Before owner | After owner | Evidence commits | Evidence files |
| --- | --- | --- | --- | --- |
| Unity IMGUI shell controller | `Cortex` | `Cortex.Shell.Unity.Imgui` | `e9de45d` | `Cortex.Shell.Unity.Imgui/CortexShell.cs`, `Cortex.Shell.Unity.Imgui/CortexShell.Runtime.cs` |
| Shell chrome and overlay execution | `Cortex` | `Cortex.Shell.Unity.Imgui` | `e9de45d`, `be7c09f` | `Cortex.Shell.Unity.Imgui/Chrome/CortexWindowChromeController.cs`, `Cortex.Shell.Unity.Imgui/Shell/ShellOverlayCoordinator.cs` |
| IMGUI module executors | `Cortex/Modules/*` | `Cortex.Shell.Unity.Imgui/Modules/*` | `be7c09f` | `Cortex.Shell.Unity.Imgui/Modules/Editor/EditorModule.cs`, `Cortex.Shell.Unity.Imgui/Modules/Settings/SettingsModule.cs` |
| Workbench snapshot projection | `UnityWorkbenchRuntime` | `Cortex.Presentation/WorkbenchPresenter` plus shell metadata | `fb11fcc` | `Cortex.Presentation/Services/WorkbenchPresenter.cs`, `Cortex.Presentation/Models/PresentationModels.cs`, `Cortex.Host.Unity/Runtime/UnityWorkbenchRuntime.cs` |
| Editor display policy | `EditorModule` | `EditorPresentationService` | `0314e09`, `9458723` | `Cortex/Services/Editor/Presentation/EditorPresentationService.cs`, `Cortex/Services/Editor/Presentation/EditorPresentationModels.cs` |
| Desktop-shareable contracts | None / mixed lanes | `Cortex.Contracts` | `dfb2ff7` | `Cortex.Contracts/Cortex.Contracts.csproj`, `Cortex.Contracts/AssemblyMarker.cs` |
| IMGUI runtime UI composition | Sheltered host composition | `Cortex.Shell.Unity.Imgui/Composition` | `ad5cf8d` | `Cortex.Shell.Unity.Imgui/Composition/ImguiWorkbenchRuntimeUiComposition.cs`, `Cortex.Host.Sheltered/Runtime/ShelteredUnityHostComposition.cs` |

### Code Moved Or Added

- `Cortex.Shell.Unity.Imgui` was introduced as a separate Unity IMGUI shell assembly.
- `CortexShell.*`, `Chrome/CortexWindowChromeController.cs`, shell layout/overlay coordinators, onboarding overlay presentation, IMGUI module executors, editor surfaces, and shell-local services moved under `Cortex.Shell.Unity.Imgui`.
- `Cortex.Host.Unity/Cortex.Host.Unity.csproj` and `Cortex.Host.Sheltered/Cortex.Host.Sheltered.csproj` were updated to reference the shell assembly.
- `Cortex.Presentation/Services/WorkbenchPresenter.cs`, `Cortex.Presentation/Models/PresentationModels.cs`, and `Cortex.Presentation/Abstractions/Interfaces.cs` took over shell-facing snapshot projection from `UnityWorkbenchRuntime`.
- `Cortex/Services/Editor/Presentation/EditorPresentationService.cs`, `Cortex/Services/Projects/ProjectWorkspaceInteractionService.cs`, `Cortex/Services/Reference/ReferenceBrowserSessionService.cs`, and `Cortex/Services/Search/SearchWorkbenchPresentationService.cs` were added or updated for headless presentation/workflow decisions.
- `Cortex.Contracts` was added as a desktop-shareable `.NET 8` lane.
- `Cortex.sln` and `Directory.Build.props` were reorganized around desktop/shared, Unity IMGUI, and tool lanes.
- Architecture tests were added or expanded in `Cortex.Tests/Architecture/RuntimeShellGuardrailArchitectureTests.cs`, `Cortex.Tests/Architecture/DesktopFirstArchitectureTests.cs`, and `Cortex.Tests/Architecture/RuntimeUiArchitectureTests.cs`.

### Dependency Changes

```text
Before:
Cortex.Host.Unity -> Cortex
Cortex -> IMGUI shell/controller/module execution
Cortex -> presentation shaping

After:
Cortex.Host.Unity -> Cortex.Shell.Unity.Imgui
Cortex.Shell.Unity.Imgui -> Cortex
Cortex.Shell.Unity.Imgui -> Cortex.Rendering.RuntimeUi
Cortex -> Cortex.Presentation
Cortex.Contracts -> desktop-shareable models/contracts
```

### Tests, Docs, Packaging Proof

- `Cortex.Tests/Architecture/RuntimeShellGuardrailArchitectureTests.cs` captured shell/runtime boundaries.
- `Cortex.Tests/Architecture/DesktopFirstArchitectureTests.cs` captured desktop-first topology.
- `Cortex.Tests/Architecture/RuntimeUiArchitectureTests.cs` was expanded for the new shell project and runtime UI direction.
- `Cortex.Tests/Shell/ShellOnboardingOverlayPresenterTests.cs` covered shell-side onboarding overlay behavior.
- Documentation updates included `Cortex_Architecture_Guide.md`, `Cortex_Portability_Report.md`, `Cortex_Runtime_Shell_Separation_Guardrails.md`, `Cortex_Renderer_Host_Extensibility_Guide.md`, `Cortex_Runtime_Rendering_Proposal.md`, and `Cortex_UI_Surface_Guide.md`.
- `Directory.Build.props`, `Cortex.sln`, and `ShelteredModManager.sln` were updated for the new shell assembly.

### Known Debt At Phase End

- The shell path was still Unity IMGUI-specific.
- The desktop path was still topology/contracts only; there was no implemented Avalonia bridge host in this phase.
- Some shell-local interaction details, such as splitter/dock-tab behavior, remained IMGUI-time behavior.
- Alternate renderers still had no complete implementation.

### Risk And Regression Notes

- `aaeceb7` after this phase fixed a Unity 5.3 Mono `TypeLoadException` caused by `System.IO.Pipes` in `Cortex.dll`.
- The TypeLoadException fix indicates that moving bridge/desktop-facing code across legacy Unity runtime boundaries created compatibility risk.
- `6cb8421` added guardrail tests to reduce regression back toward all-in-one shell ownership.

### Observed Changes

- Concrete Unity IMGUI shell execution moved out of generic `Cortex`.
- Shell-owned snapshot assembly moved behind the presentation boundary.
- Onboarding, editor, reference, and search display decisions moved into services that did not require IMGUI drawing code.
- Runtime/shell/host/backend ownership boundaries were captured in tests and documentation.

### Interpretation

Phase 2 made Unity IMGUI shell execution a named legacy shell assembly and separated more presentation decisions from concrete drawing code.

## Phase 3 - Desktop Host And Bridge

Date range: 2026-04-03 through 2026-04-05

Confidence: High for Avalonia/bridge ownership; medium for phase boundary because desktop host work overlaps presentation-mode and overlay work across adjacent commits.

### Why This Phase Existed

Goal: add an implemented out-of-process desktop host and bridge flow.

Problem being solved: Cortex still needed a non-Unity-hosted workbench surface and a transport boundary between runtime state and desktop UI.

Boundary being moved: desktop rendering and layout ownership moved into `Cortex.Host.Avalonia`; runtime state projection and intent handling moved through bridge DTOs and bridge feature lanes.

### Code Snapshots

| Kind | Commit | Notes |
| --- | --- | --- |
| Compare against | `ad5cf8d` | Parent of phase start `65c5aba`; shell extraction complete. |
| Start | `65c5aba` | Desktop bridge and Avalonia host scaffolding added. |
| End | `87b7be5` | External overlay bridge flow added. |
| Recommended inspection snapshot | `87b7be5` | Shows desktop host, bridge lanes, presentation modes, and overlay bridge flow. |

### Commits

- `65c5aba` 2026-04-03 - Add desktop bridge and Avalonia host scaffolding
- `51173ae` 2026-04-04 - Refactor desktop host startup and bridge workflows
- `58c0d73` 2026-04-04 - Add persisted Avalonia shell layout ownership
- `4c96d62` 2026-04-04 - Promote Avalonia desktop bundle authority
- `194b8a8` 2026-04-04 - Refine desktop host bundle path resolution
- `da171e3` 2026-04-04 - Add Sheltered external Avalonia host selection
- `8f2349d` 2026-04-04 - Generalize Unity render host selection
- `bf39484` 2026-04-04 - Add Avalonia host to ShelteredModManager solution
- `5f90578` 2026-04-04 - Refactor Cortex presentation modes
- `87b7be5` 2026-04-05 - Add external overlay host bridge flow

### Mini Diff Summary

| Change type | Summary |
| --- | --- |
| Added assemblies | `Cortex.Bridge`, `Cortex.Host.Avalonia`, `Cortex.Shell.Shared` |
| Removed ownership from | Unity-only shell presentation path and monolithic runtime desktop bridge session |
| New dependencies | runtime bridge used `Cortex.Bridge`; Avalonia host consumed bridge DTOs; Unity/Sheltered hosts used render host catalogs and presentation coordinators |
| Deleted transitional paths | implicit desktop bundle policy and single bridge-session shape were replaced by host-owned bundle/session services and bridge feature lanes |

### Before Architecture Snapshot

- The active supported shell path was Unity IMGUI.
- Desktop-first topology existed, but Avalonia was not yet a full runtime host path.
- Runtime state and desktop UI did not yet communicate through an implemented named-pipe bridge.
- Presentation mode selection was not yet split into integrated, overlay, and external host modes.

```text
Cortex.Host.Unity / Cortex.Host.Sheltered
  -> Cortex.Shell.Unity.Imgui
  -> Cortex.Renderers.Imgui

Cortex.Contracts
  -> desktop-ready lane exists

Desktop host
  -> not yet a full runtime participant
```

### After Architecture Snapshot

- `Cortex.Host.Avalonia` owned desktop startup, composition, Dock layout state, views, and overlay window management.
- `Cortex.Bridge` owned transport-safe DTOs.
- `Cortex/Shell/Bridge` owned runtime bridge sessions, feature lanes, snapshot building, and intent handling.
- `Cortex.Host.Unity` and `Cortex.Host.Sheltered` owned render host selection and external host launch paths.
- `Cortex.Renderers.Imgui/Overlay` and shell overlay compositions supported overlay presentation modes.

```text
Unity runtime process:
Cortex.Shell.Unity.Imgui
  -> Cortex/Shell/Bridge
  -> Cortex.Bridge DTOs

Desktop process:
Cortex.Host.Avalonia
  -> named pipe bridge client
  -> Avalonia/Dock views and state

Host selection:
Cortex.Host.Unity / Cortex.Host.Sheltered
  -> integrated IMGUI
  -> overlay
  -> external Avalonia
```

### Ownership Changes

| Area | Before owner | After owner | Evidence commits | Evidence files |
| --- | --- | --- | --- | --- |
| Desktop host UI | None / planned topology | `Cortex.Host.Avalonia` | `65c5aba`, `58c0d73` | `Cortex.Host.Avalonia/Cortex.Host.Avalonia.csproj`, `Cortex.Host.Avalonia/MainWindow.axaml`, `Cortex.Host.Avalonia/ViewModels/MainWindowViewModel.cs` |
| Bridge DTOs | None | `Cortex.Bridge` | `65c5aba`, `87b7be5` | `Cortex.Bridge/BridgeMessageModels.cs`, `Cortex.Bridge/OverlayPresentationModels.cs` |
| Runtime bridge session | Monolithic initial bridge session | Feature lanes in `Cortex/Shell/Bridge` | `51173ae` | `Cortex/Shell/Bridge/RuntimeDesktopBridgeSession.cs`, `Cortex/Shell/Bridge/RuntimeDesktopBridgeSnapshotBuilder.cs`, `Cortex/Shell/Bridge/RuntimeDesktopBridgeWorkbenchFeature.cs` |
| Desktop layout persistence | None | `Cortex.Host.Avalonia` Dock/state services | `58c0d73` | `Cortex.Host.Avalonia/Services/DesktopDockLayoutPersistenceService.cs`, `Cortex.Host.Avalonia/Models/DesktopDockLayoutState.cs`, `Cortex.Host.Avalonia/Services/DesktopShellStateStore.cs` |
| Desktop bundle policy | FutureHostReady / implicit paths | Avalonia desktop bundle services | `4c96d62`, `194b8a8` | `Cortex.Host.Avalonia/Composition/DesktopBundlePolicy.cs`, `Cortex.Host.Avalonia/Composition/DesktopHostEnvironmentPaths.cs`, `Cortex.Host.Avalonia/Composition/DesktopHostLaunchCoordinator.cs` |
| Render host selection | Sheltered-specific selection | Generalized Unity render host catalog/settings | `da171e3`, `8f2349d` | `Cortex.Host.Unity/Runtime/UnityRenderHostCatalog.cs`, `Cortex.Host.Unity/Runtime/UnityRenderHostSettings.cs`, `Cortex.Host.Sheltered/Runtime/ShelteredRenderHostCatalog.cs` |
| Presentation modes | Integrated shell path | Integrated IMGUI, overlay, external Avalonia | `5f90578`, `87b7be5` | `Cortex.Host.Unity/Runtime/UnityRenderPresentationCoordinator.cs`, `Cortex.Host.Unity/Runtime/UnityWorkbenchRuntimeUiFactorySelector.cs`, `Cortex.Shell.Unity.Imgui/Composition/ExternalOverlayWorkbenchRuntimeUiComposition.cs` |

### Code Moved Or Added

- `Cortex.Bridge/BridgeMessageModels.cs` and `Cortex.Bridge/OverlayPresentationModels.cs` defined bridge and overlay DTOs.
- Runtime bridge hosting was split under `Cortex/Shell/Bridge`, including `NamedPipeDesktopBridgeHost.cs`, `RuntimeDesktopBridgeSession.cs`, `RuntimeDesktopBridgeSettingsFeature.cs`, `RuntimeDesktopBridgeWorkspaceFeature.cs`, `RuntimeDesktopBridgeWorkbenchFeature.cs`, `RuntimeDesktopBridgeOverlayFeature.cs`, `RuntimeDesktopBridgeSnapshotBuilder.cs`, and `OverlayPresentationSnapshotBuilder.cs`.
- `Cortex.Host.Avalonia` added the Avalonia app, main window, named-pipe bridge client, startup composition, logging, view-models, Dock-backed shell state, persisted layout services, editor/search/reference/status/workspace views, overlay window management, and game-window tracking.
- `Cortex.Shell.Shared` added desktop-consumable shell models and services for onboarding, settings, workspace, catalog, and workbench workflows.
- `Cortex.Host.Sheltered` added render host settings, render host catalog, and external host launcher types.
- `Cortex.Host.Unity` added generalized render host settings/catalog types, `UnityExternalHostLauncher`, `UnityRenderPresentationCoordinator`, and `UnityWorkbenchRuntimeUiFactorySelector`.
- `Cortex.Renderers.Imgui/Overlay` added overlay runtime UI variants.
- `Cortex.Shell.Unity.Imgui` added matching overlay runtime UI compositions.
- `Manager/ManagerGUI.csproj`, `Directory.Build.props`, and `Directory.Build.targets` were updated for desktop and overlay packaging.

### Dependency Changes

```text
Before:
Cortex.Host.Unity -> Cortex.Shell.Unity.Imgui -> Cortex.Renderers.Imgui
Cortex.Contracts -> desktop-shareable lane only

After:
Cortex.Host.Unity -> UnityRenderHostCatalog / UnityRenderPresentationCoordinator
Cortex.Shell.Unity.Imgui -> Cortex/Shell/Bridge -> Cortex.Bridge
Cortex.Host.Avalonia -> Cortex.Bridge
Cortex.Host.Avalonia -> Avalonia/Dock host-local layout state
Cortex.Renderers.Imgui -> integrated/overlay IMGUI runtime UI variants
```

### Tests, Docs, Packaging Proof

- `Cortex.Tests/Architecture/DesktopFirstArchitectureTests.cs` and `RuntimeShellGuardrailArchitectureTests.cs` were updated.
- `Cortex.Tests/Shell/ShelteredRenderHostCatalogTests.cs` and `UnityRenderHostCatalogTests.cs` covered render host selection.
- `Cortex.Tests/Shell/UnityRenderPresentationCoordinatorTests.cs` covered presentation coordination.
- `Cortex.Tests/Shell/OverlayBridgeRevisionTests.cs` and `OverlayPresentationSnapshotBuilderTests.cs` covered overlay bridge behavior.
- Documentation updates included `Cortex_Avalonia_Host_Guide.md`, `Cortex_Desktop_Bridge_Guide.md`, `Cortex_Build_Topology_Guide.md`, and `Cortex_Portability_Report.md`.
- `Manager/ManagerGUI.csproj`, `Directory.Build.props`, and `Directory.Build.targets` were updated for desktop and overlay packaging.

### Known Debt At Phase End

- Desktop path existed, but still depended on the bridge DTO scope for what workflows Avalonia could see or drive.
- Named-pipe transport and bridge validation still needed hardening, as later hardening review notes describe.
- IMGUI remained the concrete in-process renderer for the Unity path.
- External overlay behavior added launch, lifetime, and synchronization paths to maintain.

### Risk And Regression Notes

- `aaeceb7` fixed a legacy Mono `TypeLoadException` from `System.IO.Pipes` exposure in `Cortex.dll`.
- `51173ae` split bridge session behavior after initial scaffolding.
- `87b7be5` added overlay bridge revision tests and snapshot builder tests to cover external overlay synchronization.
- Bundle path commits `4c96d62` and `194b8a8` show packaging/path resolution risk during desktop host rollout.

### Observed Changes

- An Avalonia desktop host and named-pipe bridge were added.
- Runtime bridge responsibilities were split into settings, workspace, workbench, overlay, and snapshot-builder lanes.
- Avalonia gained host-owned startup, session, bundle path, shell layout, and persisted layout services.
- Unity presentation modes were split into integrated IMGUI, in-process overlay, and external Avalonia host modes.
- External overlay hosting added overlay DTOs, snapshot building, overlay window management, and bridge revision tests.

### Interpretation

Phase 3 added an implemented desktop host path, a runtime-to-desktop bridge, and selectable presentation modes for integrated, overlay, and external desktop rendering.

## Phase 4 - Alternate Renderer Rollout

Date range: 2026-04-08 through 2026-04-10

Confidence: High. The renderer-neutral models, Dear ImGui project, host selection, packaging, and docs are all visible in the listed commits.

### Why This Phase Existed

Goal: add a second implemented in-process Unity renderer.

Problem being solved: alternate renderers still needed renderer-neutral shell/document snapshots instead of depending on IMGUI shell internals.

Boundary being moved: shell state exposed to renderers moved into renderer-neutral models; concrete rendering expanded from IMGUI-only to IMGUI plus Dear ImGui.

### Code Snapshots

| Kind | Commit | Notes |
| --- | --- | --- |
| Compare against | `87b7be5` | Parent of phase start `de4b2f8`; external overlay bridge flow complete. |
| Start | `de4b2f8` | Presentation mode surface fix before Dear ImGui rollout. |
| End | `bf03513` | Dear ImGui packaging and host documentation complete. |
| Recommended inspection snapshot | `bf03513` | Shows Dear ImGui renderer, host selection, packaging, and documentation wiring. |

### Commits

- `de4b2f8` 2026-04-08 - Fix Cortex presentation mode surface
- `937fa04` 2026-04-10 - Extract renderer-neutral shell frame models for alternate Unity renderers
- `08def26` 2026-04-10 - Add Dear ImGui as the Unity host's in-process Cortex renderer
- `bf03513` 2026-04-10 - Wire Dear ImGui into Cortex runtime packaging and Unity host documentation

### Mini Diff Summary

| Change type | Summary |
| --- | --- |
| Added assemblies | `Cortex.Renderers.DearImgui` |
| Removed ownership from | IMGUI shell/controller state as the only renderer-facing state source |
| New dependencies | Unity render host selection could choose Dear ImGui; Dear ImGui consumed renderer-neutral shell/document frame models |
| Deleted transitional paths | older presentation-mode aliases were documented as legacy while current in-process renderer lanes were packaged and documented |

### Before Architecture Snapshot

- Unity presentation could switch modes, but in-process concrete rendering was still IMGUI-centered.
- Shell layout/document state was not yet exposed as renderer-neutral shell frame models.
- Dear ImGui did not exist as a runtime UI project.

```text
Cortex.Host.Unity
  -> presentation mode selection
  -> Cortex.Shell.Unity.Imgui
  -> Cortex.Renderers.Imgui

Alternate renderer
  -> no full concrete implementation
```

### After Architecture Snapshot

- `Cortex.Shell.Shared` contained renderer-facing shell frame and document models.
- `Cortex.Shell.Unity.Imgui` prepared reusable shell frames for external renderers.
- `Cortex.Renderers.DearImgui` implemented a Dear ImGui runtime UI pipeline, Unity backend, input adapter, shell presenter, native loader, and bundled cimgui dependency.
- `Cortex.Host.Unity` render host catalog/settings/factory selection included Dear ImGui as an in-process renderer option.

```text
Cortex.Host.Unity
  -> UnityRenderHostCatalog
  -> UnityWorkbenchRuntimeUiFactorySelector
  -> Cortex.Shell.Unity.Imgui shell state

Cortex.Shell.Shared
  -> renderer shell/document frame models

Cortex.Renderers.Imgui
  -> legacy IMGUI renderer

Cortex.Renderers.DearImgui
  -> Dear ImGui renderer
```

### Ownership Changes

| Area | Before owner | After owner | Evidence commits | Evidence files |
| --- | --- | --- | --- | --- |
| Renderer-facing shell frame models | IMGUI shell/controller state | `Cortex.Shell.Shared` models | `937fa04` | `Cortex.Shell.Shared/Models/RendererShellFrameModels.cs`, `Cortex.Shell.Shared/Models/RendererDocumentModels.cs` |
| External renderer shell frame preparation | Not present | `Cortex.Shell.Unity.Imgui/CortexShell.ExternalRenderer.cs` | `937fa04` | `Cortex.Shell.Unity.Imgui/CortexShell.ExternalRenderer.cs`, `Cortex.Shell.Unity.Imgui/Shell/ShellLayoutCoordinator.cs` |
| Render host presentation ids | Scattered selection constants/settings | `Cortex.Presentation/Runtime/RenderHostPresentationIds.cs` | `937fa04` | `Cortex.Presentation/Runtime/RenderHostPresentationIds.cs` |
| Dear ImGui runtime UI | None | `Cortex.Renderers.DearImgui` | `08def26` | `Cortex.Renderers.DearImgui/DearImguiWorkbenchRuntimeUi.cs`, `Cortex.Renderers.DearImgui/DearImguiRenderPipeline.cs`, `Cortex.Renderers.DearImgui/DearImguiWorkbenchRuntimeUiFactory.cs` |
| Dear ImGui host selection | None | `Cortex.Host.Unity` catalog/settings/factory selector | `08def26` | `Cortex.Host.Unity/Runtime/UnityRenderHostCatalog.cs`, `Cortex.Host.Unity/Runtime/UnityRenderHostSettings.cs`, `Cortex.Host.Unity/Runtime/UnityWorkbenchRuntimeUiFactorySelector.cs` |
| Dear ImGui packaging | None | `Directory.Build.props`, `Manager/ManagerGUI.csproj` | `bf03513` | `Directory.Build.props`, `Manager/ManagerGUI.csproj`, `documentation/Cortex_Avalonia_Host_Guide.md` |

### Code Moved Or Added

- `Cortex.Presentation/Runtime/RenderHostPresentationIds.cs` added shared presentation ids for render host selection.
- `Cortex.Shell.Shared/Models/RendererShellFrameModels.cs` and `Cortex.Shell.Shared/Models/RendererDocumentModels.cs` added renderer-facing shell frame, layout, and document models.
- `Cortex.Shell.Unity.Imgui/CortexShell.ExternalRenderer.cs` added reusable shell frame preparation for non-IMGUI renderers.
- `Cortex.Shell.Unity.Imgui/Shell/ShellLayoutCoordinator.cs` synchronized layout state for alternate renderers.
- `Cortex/Services/Editor/Presentation/EditorClassificationService.cs` made editor classification presentation reusable outside the original IMGUI-only path.
- `Cortex.Renderers.DearImgui` was added with `DearImguiRenderPipeline.cs`, `DearImguiWorkbenchRenderer.cs`, `DearImguiWorkbenchRuntimeUi.cs`, `DearImguiWorkbenchRuntimeUiComposition.cs`, `DearImguiWorkbenchRuntimeUiFactory.cs`, `DearImguiWorkbenchUiSurface.cs`, `Runtime/DearImguiInputAdapter.cs`, `Runtime/DearImguiUnityBackend.cs`, `Shell/DearImguiPresentationBehaviour.cs`, `Shell/DearImguiShellPresenter.cs`, `Native/DearImguiNative.cs`, `Native/DearImguiNativeLoader.cs`, and bundled `Native/cimgui.dll`.
- `Cortex.Host.Unity` render host settings, catalog, presentation coordinator, factory selector, and shell behaviour were updated for Dear ImGui as an in-process renderer option.
- `Directory.Build.props`, `Manager/ManagerGUI.csproj`, and `documentation/Cortex_Avalonia_Host_Guide.md` were updated for Dear ImGui build/runtime packaging and host documentation.

### Dependency Changes

```text
Before:
Cortex.Host.Unity -> Cortex.Shell.Unity.Imgui -> Cortex.Renderers.Imgui
Cortex.Shell.Unity.Imgui -> IMGUI-owned shell/layout state

After:
Cortex.Host.Unity -> renderer catalog/settings/factory selector
Cortex.Shell.Unity.Imgui -> renderer-neutral shell frame preparation
Cortex.Shell.Shared -> renderer shell/document models
Cortex.Renderers.Imgui -> IMGUI renderer
Cortex.Renderers.DearImgui -> Dear ImGui renderer
```

### Tests, Docs, Packaging Proof

- Dear ImGui project files and native cimgui dependency were added.
- Unity host selection, settings, presentation coordinator, shell behaviour, and runtime UI factory selector were updated for Dear ImGui.
- Runtime logging and fallback hooks were added so renderer activation/failure could fall back to IMGUI.
- `Directory.Build.props` and `Manager/ManagerGUI.csproj` included Dear ImGui in runtime packaging.
- `documentation/Cortex_Avalonia_Host_Guide.md` was updated to describe current Unity host presentation lanes.

### Known Debt At Phase End

- Alternate renderers were supported in shape and Dear ImGui existed, but some shell/editor behavior still remained near legacy IMGUI execution.
- Native dependency loading introduced a runtime packaging and deployment surface.
- IMGUI remained the fallback renderer.
- External desktop and in-process renderer paths now both needed host-selection and presentation-mode consistency.

### Risk And Regression Notes

- `de4b2f8` fixed the presentation mode surface before Dear ImGui landed.
- `08def26` added renderer failure diagnostics and fallback to IMGUI.
- `bf03513` added packaging updates after implementation, showing the renderer was not complete until it was included in runtime payload wiring.

### Observed Changes

- Renderer-neutral shell frame and document models were added.
- The Unity shell controller began preparing shell state for non-IMGUI renderers.
- Dear ImGui was added as an in-process Unity renderer.
- Dear ImGui packaging and Unity host documentation were added.

### Interpretation

Phase 4 added Dear ImGui as a second concrete Unity renderer and added renderer-neutral shell/document snapshots that alternate renderers could consume.

## Phase Relationship To The Original Migration Plan

The original migration plan in `documentation/Cortex_Runtime_Rendering_Proposal.md` listed five steps.
The git history did not land those steps as five separate calendar phases:

- Plan phase 1 appears in historical phase 1 through host/runtime UI factories and removal of direct shell-owned IMGUI pipeline construction.
- Plan phase 2 appears through `Cortex.Rendering` contracts and recording backend tests rather than a separate command-list renderer project.
- Plan phase 3 appears in historical phase 1 through `Cortex.Rendering.RuntimeUi`.
- Plan phase 4 appears across historical phases 1 and 2 through host-selected `IWorkbenchUiSurface` composition and `Cortex.Shell.Unity.Imgui`.
- Plan phase 5 appears in historical phase 4 through `Cortex.Renderers.DearImgui`, with recording backend tests already present before the second implemented renderer landed.

## Cross-Phase Matrix

| Concern | Before phase 1 | Phase 1 | Phase 2 | Phase 3 | Phase 4 |
| --- | --- | --- | --- | --- | --- |
| Shell execution | Generic `Cortex` owned shell/editor IMGUI execution. | Renderer contracts and planners extracted, but shell execution still lived near generic Cortex. | `Cortex.Shell.Unity.Imgui` owned Unity IMGUI shell execution. | Shell execution could feed bridge/overlay/external presentation paths. | Shell state could be prepared for alternate in-process renderers. |
| Runtime UI behavior | Local/ad hoc in shell/editor/renderer paths. | `Cortex.Rendering.RuntimeUi` added runtime UI contracts and planners. | Shell-specific composition selected IMGUI runtime UI. | Runtime UI variants included integrated and overlay paths. | Dear ImGui added a second runtime UI implementation. |
| Concrete renderer | IMGUI-centered. | `Cortex.Renderers.Imgui` became the concrete backend over portable behavior. | IMGUI backend composition moved into shell assembly. | IMGUI overlay/external overlay variants added. | `Cortex.Renderers.DearImgui` added. |
| Host ownership | Mixed shell/runtime/host setup. | Host-owned frame/runtime UI composition started. | Unity/Sheltered referenced explicit shell assembly. | Unity/Sheltered/Avalonia host responsibilities split further. | Unity host selected between IMGUI and Dear ImGui in-process renderers. |
| Desktop host | Not present. | Desktop topology prepared indirectly. | `Cortex.Contracts` added a desktop-shareable lane. | `Cortex.Host.Avalonia` and named-pipe bridge added. | Desktop lane remained separate from Dear ImGui in-process renderer work. |
| Bridge | Not present for desktop host. | Not yet implemented as a desktop bridge. | Desktop-first topology prepared. | `Cortex.Bridge` and `Cortex/Shell/Bridge` feature lanes added. | Renderer-neutral shell snapshots were separate from bridge DTOs. |
| Presentation state | Mixed with runtime/shell rendering. | `Cortex.Presentation` gained models/services. | Workbench snapshot projection moved behind presentation boundary. | Bridge snapshots carried runtime state to Avalonia. | Renderer shell/document frames exposed state for Dear ImGui. |
| Proof | Existing behavior only. | Rendering, runtime UI, recording backend, and architecture tests. | Runtime-shell guardrail tests. | Bridge, render host catalog, overlay, and presentation coordinator tests. | Dear ImGui implementation, host selection, fallback, packaging, and docs. |

## Snapshot Index

| Phase | Compare against | Start | End | Recommended inspection snapshot |
| --- | --- | --- | --- | --- |
| Phase 1 | `132f363` | `6772ae5` | `f7c39e3` | `a1fcd8a` |
| Phase 2 | `0f3afcb` | `e9de45d` | `ad5cf8d` | `ad5cf8d` |
| Phase 3 | `ad5cf8d` | `65c5aba` | `87b7be5` | `87b7be5` |
| Phase 4 | `87b7be5` | `de4b2f8` | `bf03513` | `bf03513` |
