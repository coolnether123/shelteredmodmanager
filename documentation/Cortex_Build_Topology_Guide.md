# Cortex Build Topology Guide

This document records the Cortex project/build topology after the portability refactor.

For the decision-complete portability model and future-host checklist, see `documentation/Cortex_Portability_Report.md`.

## 1. Project roles

### Desktop-shareable contracts and models

- `Cortex.Contracts`

This lane exists to make the desktop-first direction real before the desktop host is introduced. It is intentionally thin in this prompt, but it is the first dedicated home for contracts/models that must be consumable from `.NET 8` by a future desktop host, existing workers, or optional external bridges.

Rules for this lane:

- do not put Unity, IMGUI, Sheltered, or ModAPI identities here
- do not leave new desktop-facing shared code trapped only in `net35` projects once it needs to cross the boundary
- keep it buildable through the centralized Cortex build/output topology

### Portable core/libraries

- `Cortex.Core`
- `Cortex.Presentation`
- `Cortex.Rendering`
- `Cortex.Rendering.RuntimeUi`
- `Cortex.Plugins.Abstractions`
- `Cortex.CompletionProviders`
- `Cortex.Tabby`
- `Cortex.Ollama`
- `Cortex.OpenRouter`

These projects may depend only on other portable Cortex projects plus approved non-Cortex shared dependencies.

Within that portable layer, ownership is split deliberately:

- `Cortex.Rendering` owns low-level render and frame/input contracts such as `IWorkbenchFrameContext` and `WorkbenchFrameInputSnapshot`
- `Cortex.Rendering.RuntimeUi` owns popup/panel/tooltip interaction and layout behavior over those contracts plus the extracted shell split-layout, menu popup, and overlay interaction planners that are already backend-neutral

`Cortex.Rendering.RuntimeUi` may depend on stable portable semantic models from `Cortex.Core` when an interaction planner needs them, such as the hover-tooltip planners consuming `EditorHoverContentPart` and `EditorHoverSection`.

### Legacy Unity IMGUI host path

- `Cortex`
- `Cortex.Shell.Unity.Imgui`
- `Cortex.Renderers.Imgui`
- `Cortex.Host.Unity`
- `Cortex.Host.Sheltered`
- `Cortex.Platform.ModAPI`

These projects are allowed to depend on portable Cortex libraries. Portable projects and desktop-shareable projects are not allowed to depend on these projects.

`Cortex.Host.Unity` now owns only the reusable Unity host runtime seam. Sheltered filesystem/configuration assumptions and host-owned workbench composition live in `Cortex.Host.Sheltered`, centralized through `Cortex.Host.Sheltered.Runtime.ShelteredHostPathLayout`.

Host/platform ownership also includes:

- selecting the active `IWorkbenchRuntimeUiFactory`
- supplying the active `IWorkbenchUiSurface`
- sharing the host-owned `IWorkbenchFrameContext` with the runtime UI/backend

This path is explicitly legacy but supported. IMGUI remains a concrete backend/executor, not the center of the architecture.

### Plugin-specific modules

- `Cortex.Plugin.Harmony`

Plugin projects may depend on portable Cortex libraries, but they must not depend on host-specific Cortex modules. Hosts decide whether to bundle them.

### External workers and tools

- `Cortex.Roslyn.Worker`
- `Cortex.Tabby.Server`
- `Cortex.PathPicker.Host`

Tooling projects build outside the in-process assemblies and are packaged through centralized bundle profiles instead of project-local `Dist\SMM` outputs. They remain out-of-process.

### Future desktop host lane

The solution now reserves a `Desktop Host` lane even though no Avalonia project has been added yet.

The intended stack for that lane is:

- Avalonia for host rendering
- Dock for workbench and docking structure
- Serilog for structured desktop/worker logging

The host project itself is deferred until the required shared runtime/contracts infrastructure is ready.

## 2. Output layout

Shared Cortex build settings now live in:

- `Directory.Build.props`
- `Directory.Build.targets`

Default project outputs are neutral:

- binaries: `artifacts\bin\<ProjectName>\...`
- intermediates: `artifacts\obj\<ProjectName>\...`

No Cortex project file should declare a product-shaped output path directly.

`Manager\ManagerGUI.csproj` is the legacy app-side entry point that triggers the Sheltered Cortex packaging pass. Its `BuildCortexRuntime` target rebuilds the full in-process Sheltered Cortex runtime set with `CortexBundleProfile=Sheltered`, so `Dist\SMM\` is refreshed coherently even when portable Cortex assemblies would otherwise stay up to date in `artifacts\bin\...`.

## 3. Bundle profiles

The package layer exposes two bundle profiles through `CortexBundleProfile`.

Bundle routing is explicit by content kind:

- portable runtime assemblies
- host runtime assemblies
- bundled plugins
- external tools/processes

### `Sheltered`

Packages the current Sheltered host bundle into `Dist\SMM\`:

- portable and Sheltered host libraries: `Dist\SMM\bin\decompiler\`
- Harmony plugin: `Dist\SMM\bin\plugins\Harmony\`
- Roslyn worker: `Dist\SMM\bin\tools\roslyn\`
- Tabby server: `Dist\SMM\bin\tools\tabby\`
- Windows path picker host helper: `Dist\SMM\bin\tools\windows-path-picker\`

### `FutureHostReady`

Packages a host-neutral starter bundle into `artifacts\bundles\FutureHostReady\`:

- portable libraries: `portable\lib\`
- Roslyn worker: `tooling\roslyn\`
- Tabby server: `tooling\tabby\`
- Windows path picker host helper: `tooling\windows-path-picker\`
- bundled plugin root reserved for future host packaging: `plugins\`
- host lane reserved for the later desktop host: `host\lib\`

This profile is intentionally not a second implemented host. It is a reusable package/profile for future host work.

Portable Cortex binaries are copied once per profile into the profile's runtime assembly lane. Tool and plugin outputs are copied into their own lanes instead of being emitted as runtime assemblies.
The centralized bundle target also removes stale plugin/tool files from runtime lanes before copying, so old `decompiler`-style placements do not survive a later packaging run.

## 4. Plugin discovery roots

Generic Cortex plugin discovery now uses only explicitly configured roots:

- bundled roots from host configuration
- explicit `CortexPluginSearchRoots` from settings

Hosts may seed `CortexPluginSearchRoots` through `ICortexHostEnvironment.ConfiguredPluginSearchRoots`, but generic discovery still reads only the bundled roots and the effective settings value.

Generic Cortex code no longer scans runtime content roots implicitly or invents `Plugins` child roots automatically.

Bundled plugin packaging is likewise profile-configured through the centralized bundle properties rather than per-project output paths.

## 5. Host-neutral path contracts

Reusable Cortex settings and host-environment contracts now use neutral path names:

- `WorkspaceRootPath`
- `RuntimeContentRootPath`
- `ReferenceAssemblyRootPath`

Sheltered-specific values such as `SMM\mods` and `Sheltered_Data\Managed` are supplied only by `Cortex.Host.Sheltered` and its setting/onboarding contributions.

## 6. Unity/Game reference supply

Unity-hosted Cortex projects no longer commit a machine-local `UnityEngine.dll` `HintPath`.

The shared build contract is:

- `/p:CortexUnityManagedDir=<Unity Managed folder>`
- `/p:CortexUnityEngineReferencePath=<full path to UnityEngine.dll>`
- or environment variables `CORTEX_UNITY_MANAGED_DIR` / `CORTEX_UNITY_ENGINE_PATH`

`Directory.Build.props` applies that contract to the Unity-hosted Cortex projects and `Cortex.Tests`, while `Directory.Build.targets` fails fast if the reference is missing.

This keeps `Cortex.Host.Unity` Unity-generic and keeps the concrete Sheltered install path out of committed project files.
`Cortex.Tests.Testing.UnityManagedAssemblyResolver` follows the same environment-variable contract at runtime and also falls back to the copied local `UnityEngine.dll` in the test output.

## 7. Remaining debt

After this pass, the main topology debt is explicit rather than hidden:

- `Cortex.Contracts` is only a seed lane; real contract/model extraction happens in later prompts
- most portable runtime assemblies are still `net35`, so the desktop-first boundary is visible but not yet resolved
- IMGUI is still the only concrete renderer
- shell/editor call sites in `Cortex` still execute IMGUI draw and event code directly
- no Avalonia host project exists yet even though the desktop host lane and package lane now exist
