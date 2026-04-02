# Cortex Portability Report

This document is the decision-complete portability map for Cortex after the Sheltered refactor.

Use it when:

- auditing whether a change belongs in portable Cortex or a host adapter
- packaging host bundle A or host bundle B
- adding a future host without copying Sheltered assumptions into reusable assemblies

## 1. Portable Cortex

Portable Cortex projects:

- `Cortex.Core`
- `Cortex.Presentation`
- `Cortex.Rendering`
- `Cortex.Plugins.Abstractions`
- `Cortex.CompletionProviders`
- `Cortex.Tabby`
- `Cortex.Ollama`
- `Cortex.OpenRouter`

Portable Cortex owns:

- generic shell/runtime contracts
- settings models and persistence
- source/reference/navigation abstractions
- generic plugin loading contracts
- reusable AI/completion providers
- reusable build/layout/output policy

Portable Cortex must not own:

- Sheltered, SMM, or game-specific filesystem layouts
- loader-specific runtime bootstrap logic
- loader-specific logging/config readers
- bundled plugin root defaults invented from generic paths
- product-shaped output paths

## 2. Host-Specific Cortex

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

## 3. Plugin-Specific Cortex

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

## 4. External-Tool Cortex

External-tool projects:

- `Cortex.Roslyn.Worker`
- `Cortex.Tabby.Server`
- `Cortex.PathPicker.Host`

External-tool rules:

- they are not in-process host runtime assemblies
- they package through explicit tool lanes
- host runtime code resolves them from bundled tool roots
- portable callers that launch bundled tools must receive host-owned paths through runtime context instead of probing package-relative layouts
- they must not depend on host-specific Cortex projects

Current resolution behavior preserves legacy fallback paths only for compatibility, but the authoritative package lane is the tool lane.

## 5. Dependency Rules

Permanent dependency rules:

- portable Cortex projects may reference only portable Cortex projects
- plugin-specific Cortex projects may reference only portable Cortex projects
- tooling projects may reference only portable Cortex projects
- host-specific Cortex projects may reference portable Cortex projects
- portable Cortex projects must not reference host-specific Cortex projects
- generic/plugin discovery code must not derive roots from runtime content roots or implicit `Plugins` children

Current portable Cortex project reference inventory:

- `Cortex.CompletionProviders -> Cortex.Core, Cortex.Tabby, Cortex.Ollama, Cortex.OpenRouter`
- `Cortex.Core -> none`
- `Cortex.Ollama -> Cortex.Core`
- `Cortex.OpenRouter -> Cortex.Core`
- `Cortex.Plugins.Abstractions -> Cortex.Core, Cortex.Presentation`
- `Cortex.Presentation -> Cortex.Core`
- `Cortex.Rendering -> Cortex.Core, Cortex.Presentation`
- `Cortex.Tabby -> Cortex.Core`

Current tooling project reference inventory:

- `Cortex.PathPicker.Host -> none`
- `Cortex.Roslyn.Worker -> none`
- `Cortex.Tabby.Server -> none`

## 6. Packaging Model

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

## 7. Unity/Game Build References

Unity-hosted Cortex projects do not commit machine-local game install paths in their `.csproj` files.

Shared build input now comes from:

- `/p:CortexUnityManagedDir=<Unity Managed folder>`
- `/p:CortexUnityEngineReferencePath=<full path to UnityEngine.dll>`
- environment variables `CORTEX_UNITY_MANAGED_DIR` / `CORTEX_UNITY_ENGINE_PATH`

That contract is applied centrally in `Directory.Build.props` and validated in `Directory.Build.targets`.

## 8. Plugin Discovery Rule

Generic Cortex plugin discovery is allowed to use only:

- `ICortexHostEnvironment.BundledPluginSearchRoots`
- `CortexSettings.CortexPluginSearchRoots`

Hosts may seed `CortexSettings.CortexPluginSearchRoots` through `ICortexHostEnvironment.ConfiguredPluginSearchRoots`.

Generic Cortex plugin discovery must not use:

- `RuntimeContentRootPath`
- implicit `Plugins` subfolders
- product-shaped fallback paths

## 9. Future Host Completion Steps

To add a real host behind `FutureHostReady`, complete these steps in new Cortex-prefixed host projects:

1. Add a new host path/layout model in the new host adapter.
2. Implement a new `ICortexHostEnvironment` and `ICortexHostServices` for that host.
3. Implement a new host/platform module for loader/runtime integration.
4. Add host-owned workbench composition for settings, onboarding, themes, and commands.
5. Decide which bundled plugins belong in that host and enable their package lane for the new profile.
6. Decide which external tools are required and package them under the new profile's tool lane.
7. Update centralized bundle props/targets with the new profile's host/plugin/tool routing.
8. Add architecture tests proving portable/tooling projects still do not reference the new host projects.
9. Add grep-style tests proving host-specific strings live only in the new host projects.
10. Add bundle verification showing runtime, plugin, and tool lanes are separated for the new profile.

Do not complete these steps by widening `Cortex.Core`, `Cortex.Presentation`, or `Cortex.Plugins.Abstractions` with host-specific fallbacks.

## 10. Remaining Debt

Remaining portability debt is intentionally short:

- `Cortex` is still a Unity-hosted shell assembly rather than a host-neutral shell runtime assembly.
- End-to-end non-Cortex product packaging outside Cortex still assumes `Dist/SMM`.
- `FutureHostReady` has package lanes but no second host adapter yet.

## 11. Hard Boundaries

When modifying Cortex:

- put reusable logic in portable Cortex only if it does not encode host identity
- put host identity, host paths, host config readers, and host bundle assumptions in host projects only
- put feature behavior in plugins, not in the generic shell
- put out-of-process behavior in external-tool projects, not in host runtime assemblies
