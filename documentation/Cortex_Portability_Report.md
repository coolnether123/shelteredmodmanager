# Cortex Portability Report

This document is the decision-complete portability map for Cortex after the Sheltered refactor.

For the current runtime/shell separation guardrails that sit on top of this topology, see `documentation/Cortex_Runtime_Shell_Separation_Guardrails.md`.

Use it when:

- auditing whether a change belongs in portable Cortex or a host adapter
- packaging host bundle A or host bundle B
- adding a future host without copying Sheltered assumptions into reusable assemblies
- wiring a new renderer or host composition without reverse-engineering IMGUI (`documentation/Cortex_Renderer_Host_Extensibility_Guide.md`)

## 0. Desktop-first direction and the net35 boundary

Cortex is being refactored toward a desktop-first architecture. The primary desktop client for this plan is now `Cortex.Host.Avalonia`, a real `.NET 8` host, while the Unity IMGUI path remains a supported legacy shell/backend.

The current desktop host stack for this phase is:

- Avalonia for host rendering
- Dock for desktop workbench structure
- Serilog for structured desktop and worker logging

The current host now proves the desktop lane through a Dock-owned workbench structure with onboarding, settings, workspace/project selection, and file-preview/editor-focused surfaces.

The current portability blocker is explicit: most reusable Cortex runtime assemblies are still `net35`, while the first external worker/tool projects are already `net8`. Any contract, protocol, or model that the desktop host and current workers both need must not remain trapped only in the `net35` projects.

The dedicated shared lanes that currently cross the `net35` boundary are now:

- `Cortex.Bridge`
- `Cortex.Contracts`
- `Cortex.Shell.Shared`

They are multi-targeted for both `net35` and `net8.0` so the same sources can serve the current runtime path, external workers, and the Avalonia host without linked-source duplication.

The first extracted contracts now living in that lane are:

- `LanguageServiceProtocol` worker request/response contracts
- `CompletionAugmentationPromptContract`
- `SemanticTokenClassification` and `SemanticTokenClassificationNames`
- named pipe bridge envelopes, handshake DTOs, snapshot DTOs, and semantic intent DTOs
- settings/onboarding/workspace models and application services for the desktop shell

The categories most likely to move into that lane next are:

- additional UI-neutral runtime, presentation, and plugin contracts that a desktop host must consume directly
- stable workbench/document/editor/search models that should be shared between legacy runtime code and future desktop or worker processes

## 1. Desktop-shareable Cortex

Desktop-shareable Cortex currently means:

- `Cortex.Bridge`
- `Cortex.Contracts`
- `Cortex.Shell.Shared`

Rules for this lane:

- it must stay host-neutral
- it must stay consumable from `.NET 8`
- it must not become another `net35` trap
- it is the preferred home for extracted shared contracts/models once they are genuinely cross-host or worker-facing

## 2. Portable Cortex

Portable Cortex projects:

- `Cortex.Core`
- `Cortex.Presentation`
- `Cortex.Rendering`
- `Cortex.Rendering.RuntimeUi`
- `Cortex.Plugins.Abstractions`
- `Cortex.CompletionProviders`
- `Cortex.Tabby`
- `Cortex.Ollama`
- `Cortex.OpenRouter`

Portable Cortex owns:

- generic shell/runtime contracts
- settings models and persistence
- source/reference/navigation abstractions
- low-level rendering and frame/input contracts
- reusable popup/panel/tooltip runtime-UI interaction and layout policy
- generic plugin loading contracts
- reusable AI/completion providers
- reusable build/layout/output policy

Portable Cortex must not own:

- Sheltered, SMM, or game-specific filesystem layouts
- loader-specific runtime bootstrap logic
- loader-specific logging/config readers
- bundled plugin root defaults invented from generic paths
- product-shaped output paths

## 3. Host-Specific Cortex

Host-specific Cortex projects:

- `Cortex`
- `Cortex.Host.Avalonia`
- `Cortex.Renderers.Imgui`
- `Cortex.Host.Sheltered`
- `Cortex.Host.Unity`
- `Cortex.Platform.ModAPI`

Host-specific Cortex owns:

- desktop-host startup, local persistence, and Dock workbench composition
- Unity-hosted shell runtime seams
- Sheltered path/config/environment mapping
- concrete Sheltered workbench composition
- ModAPI runtime integration
- host rendering/backend wiring
- host-bundle packaging decisions for the active host
- Unity-hosted build reference resolution through centralized Cortex build properties

The central Sheltered path/config authority is:

- `Cortex.Host.Sheltered.Runtime.ShelteredHostPathLayout`

If a future host needs a different game/layout, it should provide its own host path/layout model in its own Cortex host project. Do not generalize a second host by widening `ShelteredHostPathLayout`.

The current in-process host path is explicitly legacy:

- Unity/IMGUI remains supported
- IMGUI is a concrete backend/executor, not the architectural center of Cortex
- host-specific projects should not be widened just to satisfy the future desktop host

`Cortex.Host.Avalonia` is the current desktop host. It consumes `Cortex.Shell.Shared` and stays out of the legacy Unity/IMGUI host graph.

## 4. Plugin-Specific Cortex

Plugin-specific Cortex currently means:

- `Cortex.Plugin.Harmony`

Plugin rules:

- plugins load through `WorkbenchPluginLoader`
- plugins are discovered only from bundled host roots plus explicit configured roots
- plugins declare `cortex.plugin.json`
- plugins register through `IWorkbenchPluginContributor`
- plugins must not require private shell/editor internals

Bundled first-party plugins and third-party plugins follow the same discovery model. Packaging location is host-controlled, not hardcoded in plugin code.
`Cortex.Plugin.Harmony` stays in this plugin role even when the Sheltered bundle chooses to ship it.

## 5. External-Tool Cortex

External-tool projects:

- `Cortex.Roslyn.Worker`
- `Cortex.Tabby.Server`
- `Cortex.PathPicker.Host`

External-tool rules:

- they are not in-process host runtime assemblies
- they remain out-of-process even while Cortex moves toward a desktop-first host
- they package through explicit tool lanes
- host runtime code resolves them from bundled tool roots
- portable callers that launch bundled tools must receive host-owned paths through runtime context instead of probing package-relative layouts
- they must not depend on host-specific Cortex projects

Current resolution behavior preserves legacy fallback paths only for compatibility, but the authoritative package lane is the tool lane.

## 6. Dependency Rules

Permanent dependency rules:

- desktop-shareable Cortex projects may reference only other desktop-shareable or portable Cortex projects
- portable Cortex projects may reference only portable Cortex projects
- plugin-specific Cortex projects may reference only portable Cortex projects
- tooling projects may reference only portable Cortex projects or desktop-shareable projects
- host-specific Cortex projects may reference portable Cortex projects
- portable Cortex projects must not reference host-specific Cortex projects
- desktop-shareable Cortex projects must not reference host-specific Cortex projects
- generic/plugin discovery code must not derive roots from runtime content roots or implicit `Plugins` children

Desktop-shareable project reference inventory:

- `Cortex.Bridge -> Cortex.Shell.Shared`
- `Cortex.Contracts -> none`
- `Cortex.Shell.Shared -> none`

Current portable Cortex project reference inventory:

- `Cortex.CompletionProviders -> Cortex.Core, Cortex.Tabby, Cortex.Ollama, Cortex.OpenRouter`
- `Cortex.Core -> Cortex.Contracts`
- `Cortex.Ollama -> Cortex.Contracts, Cortex.Core`
- `Cortex.OpenRouter -> Cortex.Contracts, Cortex.Core`
- `Cortex.Plugins.Abstractions -> Cortex.Contracts, Cortex.Core, Cortex.Presentation`
- `Cortex.Presentation -> Cortex.Core, Cortex.Rendering`
- `Cortex.Rendering -> Cortex.Core`
- `Cortex.Rendering.RuntimeUi -> Cortex.Plugins.Abstractions, Cortex.Rendering`
- `Cortex.Tabby -> Cortex.Contracts, Cortex.Core`

Current tooling project reference inventory:

- `Cortex.PathPicker.Host -> none`
- `Cortex.Roslyn.Worker -> Cortex.Contracts`
- `Cortex.Tabby.Server -> Cortex.Contracts`

Runtime UI boundary notes:

- `Cortex.Rendering` remains the low-level render contract package.
- `Cortex.Rendering` owns the portable frame/input contract used by hosts and runtime UI backends, including `IWorkbenchFrameContext` and `WorkbenchFrameInputSnapshot`.
- `Cortex.Rendering.RuntimeUi` owns reusable popup/panel/tooltip interaction and layout behavior over those contracts, plus the extracted shell split-layout, shell menu popup, and shell overlay interaction policy that is already backend-neutral.
- concrete runtime UI/backend selection remains outside generic Cortex; Sheltered currently selects the legacy IMGUI shell composition in `Cortex.Host.Sheltered`.
- host-owned frame context adaptation now lives in `Cortex.Host.Unity`, while shell-generic Cortex and runtime UI backends consume the portable `Cortex.Rendering` contracts.
- the active module `IWorkbenchUiSurface` is selected by host/shell composition. The current concrete implementation is `Cortex.Shell.Unity.Imgui.Ui.ImguiWorkbenchUiSurface`.
- `Cortex.Renderers.Imgui` should now be treated as a concrete executor/measurement adapter over those portable plans, not the owner of popup/panel/tooltip runtime policy.

## 7. Packaging Model

Packaging is profile-driven through `CortexBundleProfile`.

### Host bundle A: `Sheltered`

Outputs:

- portable runtime assemblies -> `Dist/SMM/bin/decompiler/`
- host runtime assemblies -> `Dist/SMM/bin/decompiler/`
- bundled plugins -> `Dist/SMM/bin/plugins/<plugin>/`
- external tools -> `Dist/SMM/bin/tools/<tool>/`

### Host bundle B: `Desktop`

Outputs:

- portable runtime assemblies -> `artifacts/bundles/Desktop/portable/lib/`
- host runtime assemblies -> `artifacts/bundles/Desktop/host/lib/`
- bundled plugins -> `artifacts/bundles/Desktop/plugins/<plugin>/`
- external tools -> `artifacts/bundles/Desktop/tooling/<tool>/`

`Desktop` now packages the real desktop host lane through `Cortex.Host.Avalonia`, `Cortex.Bridge`, and `Cortex.Shell.Shared`. The desktop bundle policy is explicit in Cortex-owned code: it currently ships Harmony as the bundled plugin lane and Roslyn plus Tabby as the required tool lanes, while the Windows path picker remains on the Sheltered/Unity path.

Portable Cortex binaries are copied once per profile into the profile's runtime assembly lane. Tool and plugin outputs are copied into their own lanes instead of being emitted as runtime assemblies.
The centralized bundle target also removes stale plugin/tool files from runtime lanes before copying, so old `decompiler`-style placements do not survive a later packaging run.

## 8. Unity/Game Build References

Unity-hosted Cortex projects do not commit machine-local game install paths in their `.csproj` files.

Shared build input now comes from:

- `/p:CortexUnityManagedDir=<Unity Managed folder>`
- `/p:CortexUnityEngineReferencePath=<full path to UnityEngine.dll>`
- repo-local fallback at `libs/UnityEngine.dll`
- environment variables `CORTEX_UNITY_MANAGED_DIR` / `CORTEX_UNITY_ENGINE_PATH`

That contract is applied centrally in `Directory.Build.props` and validated in `Directory.Build.targets`.

## 9. Plugin Discovery Rule

Generic Cortex plugin discovery is allowed to use only:

- `ICortexHostEnvironment.BundledPluginSearchRoots`
- `ICortexHostEnvironment.BundledToolRootPath` for bundled tool resolution
- `CortexSettings.CortexPluginSearchRoots`

Hosts may seed `CortexSettings.CortexPluginSearchRoots` through `ICortexHostEnvironment.ConfiguredPluginSearchRoots`.

Generic Cortex plugin discovery must not use:

- `RuntimeContentRootPath`
- implicit `Plugins` subfolders
- product-shaped fallback paths

## 10. Desktop Host Completion Steps

To continue the real host behind `Desktop`, complete these steps in Cortex-prefixed desktop host projects:

1. Add persisted and user-directed Dock layout policy without pushing docking assumptions back into generic shared contracts.
2. Extract any additional UI-neutral runtime, presentation, shell, or bridge contracts the desktop host now proves are genuinely cross-boundary.
3. Broaden the desktop editor/workbench surfaces beyond the current onboarding, settings, workspace, and file-preview path.
4. Extend or adjust the desktop plugin set only when the desktop host proves it needs more than the current Harmony lane.
5. Decide which external tools are required and keep packaging them under the profile's tool lane.
6. Add architecture tests proving portable/tooling/desktop-shareable projects still do not reference new desktop host code incorrectly.
7. Add bundle verification showing runtime, host, plugin, and tool lanes are separated for the profile.

Do not complete these steps by widening `Cortex.Core`, `Cortex.Presentation`, or `Cortex.Plugins.Abstractions` with host-specific fallbacks.

## 11. Remaining Debt

Remaining portability debt is intentionally short:

- more shared runtime, presentation, and plugin contracts still need to move into `Cortex.Contracts` or another desktop-consumable lane as later prompts justify them.
- `Cortex` is still a Unity-hosted shell assembly rather than a host-neutral shell runtime assembly.
- Shell/editor IMGUI call sites still execute raw drawing and event consumption locally even though their shared policy is increasingly portable.
- `CortexWindowChromeController` still owns IMGUI-time splitter drag and resize execution.
- End-to-end non-Cortex product packaging outside Cortex still assumes `Dist/SMM`.
- end-to-end non-Cortex product startup still flows through `Manager`, so true external default launch remains outside the Cortex-owned boundary.
- the current named pipe bridge is intentionally narrow and only carries onboarding, settings, workspace/project browsing, and file-preview/editor-oriented snapshots and intents.

## 12. Hard Boundaries

When modifying Cortex:

- put desktop-shareable contracts and models in `Cortex.Contracts`, `Cortex.Bridge`, `Cortex.Shell.Shared`, or another `.NET 8`-consumable shared lane once they need to cross the `net35` boundary
- put reusable logic in portable Cortex only if it does not encode host identity
- put host identity, host paths, host config readers, and host bundle assumptions in host projects only
- put feature behavior in plugins, not in the generic shell
- put out-of-process behavior in external-tool projects, not in host runtime assemblies
