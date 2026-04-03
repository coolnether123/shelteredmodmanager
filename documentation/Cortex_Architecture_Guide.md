# Cortex Architecture Guide

This document describes the permanent Cortex architecture in the current codebase.

For the current refactor-phase guardrails around runtime, shell, bridge/host, and IMGUI backend ownership, see `documentation/Cortex_Runtime_Shell_Separation_Guardrails.md`.

The current direction is desktop-first. Cortex is being prepared for a future `.NET 8` desktop host while the existing Unity IMGUI path remains a supported legacy host/shell path. This prompt does not add the desktop host yet, but it does make the desktop-facing lanes explicit in the repo structure so later prompts can extract real contracts and models into them deliberately.

Portable Cortex code provides typed extension seams, runtime capabilities, state ownership, and workbench/editor composition. Host-bound behavior is isolated in Sheltered-specific host projects. Feature-specific behavior belongs in plugins such as the extracted Harmony module.

For the final portability boundary map, bundle profile intent, and future-host completion checklist, see `documentation/Cortex_Portability_Report.md`.

## 0. Solution topology

The Cortex solution is intentionally split into explicit lanes:

- desktop-shareable contracts/models: `Cortex.Contracts`
- portable core/libraries: `Cortex.Core`, `Cortex.Presentation`, `Cortex.Rendering`, `Cortex.Rendering.RuntimeUi`, `Cortex.Plugins.Abstractions`, `Cortex.CompletionProviders`, `Cortex.Tabby`, `Cortex.Ollama`, `Cortex.OpenRouter`
- legacy Unity IMGUI host path: `Cortex`, `Cortex.Shell.Unity.Imgui`, `Cortex.Renderers.Imgui`, `Cortex.Host.Unity`, `Cortex.Host.Sheltered`, `Cortex.Platform.ModAPI`
- plugin-specific feature modules: `Cortex.Plugin.Harmony`
- external tooling: `Cortex.Roslyn.Worker`, `Cortex.Tabby.Server`, `Cortex.PathPicker.Host`
- future desktop host lane: reserved in the solution under `Desktop Host` until the shared runtime/contracts work is ready

Portable projects must not reference host-specific Cortex projects.
Desktop-shareable projects must also stay host-neutral and must remain consumable from `.NET 8`.

The intended desktop host stack for this phase is:

- Avalonia for host rendering
- Dock for workbench and docking structure
- Serilog for structured desktop/worker logging

Product-shaped bundle layouts are no longer declared inside reusable project files. Neutral project outputs now come from shared Cortex build props/targets, and bundle layouts are selected through centralized build profiles.

Unity-hosted Cortex project files also no longer commit a machine-local Sheltered `UnityEngine.dll` path. That reference is supplied through the shared build contract in `Directory.Build.props` and `Directory.Build.targets`.

Reusable settings and environment contracts are host-neutral. Portable Cortex code works with `WorkspaceRootPath`, `RuntimeContentRootPath`, and `ReferenceAssemblyRootPath`; only host adapters provide concrete host-specific values for those paths.
The remaining hard boundary is `net35`: most portable runtime assemblies are still `.NET Framework 3.5`, so contracts/models that a future desktop host, workers, or optional bridges must consume cannot stay trapped there indefinitely. `Cortex.Contracts` is now the first real cross-boundary lane, housing the Roslyn language-service protocol, completion prompt contract, and semantic token classification helpers that both the current runtime graph and future host or worker processes can consume before the Avalonia host is introduced.

## 1. Core roles

### Portable Cortex libraries

Portable Cortex assemblies own:

- workbench shell and layout orchestration
- headless runtime composition and startup/configuration services for settings, service maps, and plugin discovery
- document/session management
- command and contribution registries
- typed runtime capability interfaces
- module-owned state storage
- editor extension runtime and presentation hosts
- shell-facing snapshot construction and projection shaping in `Cortex.Presentation`
- reusable settings session/apply services and onboarding flow/workspace preparation services
- plugin discovery and registration
- low-level render, geometry, and frame/input contracts in `Cortex.Rendering`
- popup/panel/tooltip runtime interaction and layout behavior in `Cortex.Rendering.RuntimeUi`
- shell split-layout math, menu popup placement-dismissal policy, and overlay capture/onboarding prompt policy in `Cortex.Rendering.RuntimeUi`

Portable Cortex assemblies do not own Harmony behavior, Harmony state, Harmony-specific contracts, or Sheltered-specific filesystem/package layouts.

Shell IMGUI callers in `Cortex` still execute draw calls and Unity event consumption locally, but the backend-neutral shell geometry and interaction rules that are shared across current shell surfaces should live in `Cortex.Rendering.RuntimeUi` rather than stay buried inline in IMGUI branches.

### Sheltered host layer

Sheltered-specific Cortex assemblies own:

- Unity-hosted shell composition
- concrete Sheltered host composition
- Sheltered/ModAPI runtime adapters
- Unity-specific rendering integration
- centralized Sheltered path/layout modeling via `Cortex.Host.Sheltered.Runtime.ShelteredHostPathLayout`

Sheltered-specific projects may depend on portable Cortex libraries, but not the other way around.

`Cortex.Host.Unity` is the reusable Unity-host runtime layer. `Cortex.Host.Sheltered` is the concrete Sheltered adapter that supplies environment paths, bundled workbench contributions, and concrete host composition.

`Cortex.Host.Unity` may depend on Unity build references, but it must not own any committed Sheltered install path. The active Unity managed reference path is supplied externally through the centralized Cortex build properties.

### Cortex plugins

Plugins own feature behavior. A plugin may contribute:

- commands
- view containers and views
- workbench modules
- editor context actions
- explorer filters
- method inspector sections and actions
- editor adornments
- editor workflows

The Harmony feature now follows this model through `Cortex.Plugin.Harmony`. It is a feature plugin that a host bundle may choose to ship, not a Sheltered host adapter.

## 2. One plugin model

Bundled first-party plugins and third-party plugins use the same model:

1. discovery through `cortex.plugin.json`
2. loading through `WorkbenchPluginLoader`
3. activation through `IWorkbenchPluginContributor`
4. registration through `WorkbenchPluginContext`

There is no separate built-in Harmony registration path.

Bundled plugins are only different in where their manifests are found. They are not different in contracts, discovery semantics, or registration flow.

## 3. Public composition boundary

The only public plugin entry point is:

- `IWorkbenchPluginContributor`

The contributor receives `WorkbenchPluginContext`, which is the composition boundary for registering:

- commands
- declarative contributions
- optional workbench modules
- editor extension contributions

Plugins must not depend on private shell/editor internals or loader-specific host classes.

## 4. Typed runtime capability access

At runtime, modules receive `IWorkbenchModuleRuntime`. Contributed editor/inspector workflows receive `IWorkbenchRuntimeAccess`.

The capability model is intentionally narrow.

`IWorkbenchRuntimeAccess` exposes:

- `Modules`
- `Feedback`

`IWorkbenchModuleRuntime` exposes:

- `Lifecycle`
- `Commands`
- `Navigation`
- `Documents`
- `Projects`
- `Editor`
- `State`

This is the anti-god-object rule in practice.

No single public runtime object may expose unrelated host capabilities directly unless it is only a composition shell over smaller typed interfaces. If a new feature needs more power, add a coherent typed capability interface instead of widening an existing runtime object into a service locator.

## 5. Module-owned state lifetimes

Plugins own their own state through `IWorkbenchModuleStateRuntime`.

The host distinguishes three state categories and they must stay distinct:

### Persistent module state

Stored in `State.Persistent`.

Use this for data that should survive persistence/session boundaries when appropriate, such as:

- last-inspected symbol
- preferred generation mode
- module-level settings or last-used paths

### Ephemeral workflow state

Stored in `State.Workflow`.

Use this for transient in-session flows, such as:

- active multi-step workflow state
- previews
- active summary data
- pending insertion selection

### Document-scoped and editor-scoped state

Stored in `State.Contexts`, keyed by:

- document scopes
- editor session scopes

Use this for data attached to a specific document/editor context, such as:

- last inspected symbol for one document
- insertion selection coordinates in one editor session
- template navigation placeholders for one session

Do not collapse these lifetimes into one generic bag.

## 6. Reusable editor extension seams

Generic editor code composes contributed behavior through reusable seams. Current public seams include:

- method inspector sections and actions
- editor adornments
- editor workflows
- editor context actions
- explorer filters

These seams are generic Cortex contracts. They are not Harmony-shaped aliases.

They are intended to support future non-Harmony modules too, for example:

- a review module that contributes inspector notes and anchor-picking workflows
- a diagnostics module that adds decompiler-focused explorer filters
- a navigation module that contributes editor badges and symbol-driven actions

## 7. Shell responsibility boundary

The shell stays orchestration-focused.

The shell may:

- create runtime services
- coordinate workbench composition
- assemble presentation snapshots from runtime state through `Cortex.Presentation`
- route plugin discovery
- host generic editor/workbench runtime seams
- render blocked/unavailable module messages

The shell must not:

- own feature-specific business logic
- expose private state as renamed public runtime objects
- keep module-specific fields in generic shell/editor state
- special-case Harmony or any other module in generic code

## 8. Harmony as a real plugin

Harmony now lives in `Cortex.Plugin.Harmony`, a separate in-game `.NET Framework 3.5` plugin assembly.

It is discovered through its manifest and loaded through the same plugin loader path as any other Cortex plugin.

Harmony owns:

- its module window
- its commands
- its explorer filter
- its inspector contribution
- its editor adornments and workflows
- its runtime inspection and generation logic
- its module-owned state

Harmony consumes only public Cortex plugin APIs plus its legitimate runtime dependencies, such as `0Harmony`.

### Runtime availability

The Harmony plugin is disabled when `0Harmony` is not available in the active runtime.

That availability state is enforced consistently through:

- module blocked-state messaging
- command enablement
- workflow/adornment gating
- explorer filter gating

## 9. Authoring rules for future modules

When adding a new plugin or extension seam:

- keep the host generic
- keep feature behavior in the plugin
- prefer typed capability interfaces over widened runtime shells
- keep state ownership module-local
- separate persistent, workflow, and scoped state
- prove new seams can support non-Harmony modules
- do not add feature-specific fields to shell/editor state

## 10. Practical summary

Permanent Cortex architecture means:

- portable Cortex libraries stay host-neutral
- Sheltered host projects are explicitly isolated
- plugins are the feature owners
- first-party and third-party plugins follow the same discovery and registration model
- runtime access is typed and narrow
- state ownership belongs to modules
- editor/workbench extensibility is generic and reusable
- Harmony is a real externalized in-game plugin, not built-in host behavior
