# Cortex Architecture Guide

This document describes the permanent Cortex architecture in the current codebase.

Cortex is the host. Feature modules are plugins. Generic Cortex code provides typed extension seams, runtime capabilities, state ownership, and workbench/editor composition. Feature-specific behavior belongs in plugins such as the extracted Harmony module.

## 1. Core roles

### Cortex host

Generic Cortex assemblies own:

- workbench shell and layout orchestration
- document/session management
- command and contribution registries
- typed runtime capability interfaces
- module-owned state storage
- editor extension runtime and presentation hosts
- plugin discovery and registration

Generic Cortex assemblies do not own Harmony behavior, Harmony state, or Harmony-specific contracts.

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

The Harmony feature now follows this model through `Cortex.Plugin.Harmony`.

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

- Cortex is the host
- plugins are the feature owners
- first-party and third-party plugins follow the same discovery and registration model
- runtime access is typed and narrow
- state ownership belongs to modules
- editor/workbench extensibility is generic and reusable
- Harmony is a real externalized in-game plugin, not built-in host behavior
