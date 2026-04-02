# Cortex Build Topology Guide

This document records the Cortex project/build topology after the portability refactor.

For the decision-complete portability model and future-host checklist, see `documentation/Cortex_Portability_Report.md`.

## 1. Project roles

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

### Host/platform modules and adapters

- `Cortex`
- `Cortex.Renderers.Imgui`
- `Cortex.Host.Unity`
- `Cortex.Host.Sheltered`
- `Cortex.Platform.ModAPI`

These projects are allowed to depend on portable Cortex libraries. Portable projects are not allowed to depend on these projects.

`Cortex.Host.Unity` now owns only the reusable Unity host runtime seam. Sheltered filesystem/configuration assumptions and host-owned workbench composition live in `Cortex.Host.Sheltered`, centralized through `Cortex.Host.Sheltered.Runtime.ShelteredHostPathLayout`.

Host/platform ownership also includes:

- selecting the active `IWorkbenchRuntimeUiFactory`
- supplying the active `IWorkbenchUiSurface`
- sharing the host-owned `IWorkbenchFrameContext` with the runtime UI/backend

### Plugin-specific modules

- `Cortex.Plugin.Harmony`

Plugin projects may depend on portable Cortex libraries, but they must not depend on host-specific Cortex modules. Hosts decide whether to bundle them.

### External tooling

- `Cortex.Roslyn.Worker`
- `Cortex.Tabby.Server`
- `Cortex.PathPicker.Host`

Tooling projects build outside the in-game assemblies and are packaged through centralized bundle profiles instead of project-local `Dist\SMM` outputs.

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

After this pass, the main rendering debt is no longer ownership ambiguity. It is limited to:

- IMGUI still being the only concrete renderer
- shell/editor call sites in `Cortex` still executing IMGUI draw and event code directly
- a small amount of shell chrome execution logic still living in IMGUI-specific helpers rather than portable runtime-ui planners
