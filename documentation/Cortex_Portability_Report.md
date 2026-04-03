# Cortex Portability Report

This document is the decision-complete portability map for Cortex after the Sheltered refactor.

For the current runtime/shell separation guardrails that sit on top of this topology, see `documentation/Cortex_Runtime_Shell_Separation_Guardrails.md`.

Use it when:

- auditing whether a change belongs in portable Cortex or a host adapter
- packaging host bundle A or host bundle B
- adding a future host without copying Sheltered assumptions into reusable assemblies
- wiring a new renderer or host composition without reverse-engineering IMGUI (`documentation/Cortex_Renderer_Host_Extensibility_Guide.md`)

## 0. Desktop-first direction and the net35 boundary

Cortex is being refactored toward a desktop-first architecture. The primary future client for this plan is a new `.NET 8` desktop host, while the Unity IMGUI path remains a supported legacy shell/backend.

The intended desktop host stack for this phase is:

- Avalonia for host rendering
- Dock for workbench and docking structure
- Serilog for structured desktop and worker logging

The current portability blocker is explicit: most reusable Cortex runtime assemblies are still `net35`, while the first external worker/tool projects are already `net8`. Any contract, protocol, or model that a future desktop host and current workers both need must not remain trapped only in the `net35` projects.

The first dedicated shared lane that crosses the `net35` boundary is now:

- `Cortex.Contracts`

It is multi-targeted for both `net35` and `net8.0` so the same sources can serve the current runtime path, external workers, and a future desktop host without linked-source duplication.

The first extracted contracts now living in that lane are:

- `LanguageServiceProtocol` worker request/response contracts
- `CompletionAugmentationPromptContract`
- `SemanticTokenClassification` and `SemanticTokenClassificationNames`

The categories most likely to move into that lane next are:

- additional UI-neutral runtime, presentation, and plugin contracts that a desktop host must consume directly
- stable workbench/document/editor/search models that should be shared between legacy runtime code and future desktop or worker processes

## 1. Desktop-shareable Cortex

Desktop-shareable Cortex currently means:

- `Cortex.Contracts`

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

Sheltered host projects:

- `Cortex`
- `Cortex.Renderers.Imgui`
- `Cortex.Host.Sheltered`
- `Cortex.Host.Unity`
- `Cortex.Platform.ModAPI`

Host-specific Cortex owns:

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

- `Cortex.Contracts -> none`

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
- concrete runtime UI/backend selection remains host-owned; Sheltered currently selects the IMGUI runtime UI in `Cortex.Host.Sheltered`.
- host-owned frame context adaptation now lives in `Cortex.Host.Unity`, while shell-generic Cortex and runtime UI backends consume the portable `Cortex.Rendering` contracts.
- the active module `IWorkbenchUiSurface` is selected by host composition. The current concrete implementation is `Cortex.Shell.Unity.Imgui.Ui.ImguiWorkbenchUiSurface`.
- `Cortex.Renderers.Imgui` should now be treated as a concrete executor/measurement adapter over those portable plans, not the owner of popup/panel/tooltip runtime policy.

## 7. Packaging Model

Packaging is profile-driven through `CortexBundleProfile`.

### Host bundle A: `Sheltered`

Outputs:

- portable runtime assemblies -> `Dist/SMM/bin/decompiler/`
- host runtime assemblies -> `Dist/SMM/bin/decompiler/`
- bundled plugins -> `Dist/SMM/bin/plugins/<plugin>/`
- external tools -> `Dist/SMM/bin/tools/<tool>/`

### Host bundle B: `FutureHostReady`

Outputs:

- portable runtime assemblies -> `artifacts/bundles/FutureHostReady/portable/lib/`
- host runtime assemblies -> reserved at `artifacts/bundles/FutureHostReady/host/lib/`
- bundled plugins -> reserved at `artifacts/bundles/FutureHostReady/plugins/`
- external tools -> `artifacts/bundles/FutureHostReady/tooling/<tool>/`

`FutureHostReady` is intentionally a packaging profile, not a second runnable host.

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
- `CortexSettings.CortexPluginSearchRoots`

Hosts may seed `CortexSettings.CortexPluginSearchRoots` through `ICortexHostEnvironment.ConfiguredPluginSearchRoots`.

Generic Cortex plugin discovery must not use:

- `RuntimeContentRootPath`
- implicit `Plugins` subfolders
- product-shaped fallback paths

## 10. Future Host Completion Steps

To add a real host behind `FutureHostReady`, complete these steps in new Cortex-prefixed host projects:

1. Add a new host path/layout model in the new host adapter.
2. Implement a new `ICortexHostEnvironment` and `ICortexHostServices` for that host.
3. Implement a new host/platform module for loader/runtime integration.
4. Add host-owned workbench composition for settings, onboarding, themes, and commands.
5. Decide which bundled plugins belong in that host and enable their package lane for the new profile.
6. Decide which external tools are required and package them under the new profile's tool lane.
7. Update centralized bundle props/targets with the new profile's host/plugin/tool routing.
8. Add architecture tests proving portable/tooling/desktop-shareable projects still do not reference the new host projects.
9. Add grep-style tests proving host-specific strings live only in the new host projects.
10. Add bundle verification showing runtime, plugin, and tool lanes are separated for the new profile.

Do not complete these steps by widening `Cortex.Core`, `Cortex.Presentation`, or `Cortex.Plugins.Abstractions` with host-specific fallbacks.

## 11. Remaining Debt

Remaining portability debt is intentionally short:

- more shared runtime, presentation, and plugin contracts still need to move into `Cortex.Contracts` or another desktop-consumable lane as later prompts justify them.
- `Cortex` is still a Unity-hosted shell assembly rather than a host-neutral shell runtime assembly.
- Shell/editor IMGUI call sites still execute raw drawing and event consumption locally even though their shared policy is increasingly portable.
- `CortexWindowChromeController` still owns IMGUI-time splitter drag and resize execution.
- End-to-end non-Cortex product packaging outside Cortex still assumes `Dist/SMM`.
- `FutureHostReady` has package lanes but no second host adapter yet.

## 12. Hard Boundaries

When modifying Cortex:

- put desktop-shareable contracts and models in `Cortex.Contracts` or another `.NET 8`-consumable shared lane once they need to cross the `net35` boundary
- put reusable logic in portable Cortex only if it does not encode host identity
- put host identity, host paths, host config readers, and host bundle assumptions in host projects only
- put feature behavior in plugins, not in the generic shell
- put out-of-process behavior in external-tool projects, not in host runtime assemblies
