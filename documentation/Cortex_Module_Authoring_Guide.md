# Cortex Module Authoring Guide

This guide describes how to build a Cortex workbench plugin against the permanent public module model.

It is for Cortex plugins, not general ModAPI gameplay plugins.

## 1. Public entry point

Implement:

- `IWorkbenchPluginContributor`

Register everything through:

- `WorkbenchPluginContext`

There is no alternate legacy contributor path in the permanent architecture.

## 2. What a plugin can contribute

A Cortex plugin may register:

- commands
- view containers
- views
- icons
- workbench modules
- editor context actions
- explorer filters
- method inspector sections
- editor adornments
- editor workflows

The Harmony plugin is the reference example for a full-featured module that uses all of these seams without depending on private Cortex internals.

## 3. Minimal plugin example

```csharp
using Cortex.Plugins.Abstractions;
using Cortex.Core.Models;

namespace Example.CortexPlugin
{
    public sealed class ReviewPluginContributor : IWorkbenchPluginContributor
    {
        public string PluginId
        {
            get { return "example.review"; }
        }

        public string DisplayName
        {
            get { return "Review"; }
        }

        public void Register(WorkbenchPluginContext context)
        {
            context.RegisterViewContainer(
                "example.review.container",
                "Review",
                WorkbenchHostLocation.SecondarySideHost,
                50,
                true,
                ModuleActivationKind.OnCommand,
                "example.review.open",
                "example.review.container");

            context.RegisterView(
                "example.review.view",
                "example.review.container",
                "Review",
                "example.review.view",
                0,
                true);

            context.RegisterCommand(
                "example.review.open",
                "Open Review",
                "Review",
                "Open the Review module.",
                string.Empty,
                100,
                true,
                true);
        }
    }
}
```

## 4. Runtime interaction rules

At registration time, use `WorkbenchPluginContext`.

At execution/render time, use the typed runtime contracts:

- `IWorkbenchRuntimeAccess`
- `IWorkbenchModuleRuntime`

Do not treat runtime access as a god object.

### `IWorkbenchRuntimeAccess`

Use this from contributed editor/inspector flows when you only need:

- `Modules`
- `Feedback`

### `IWorkbenchModuleRuntime`

Use this from a module when you need:

- `Lifecycle`
- `Commands`
- `Navigation`
- `Documents`
- `Projects`
- `Editor`
- `State`

If a feature needs more host power, add a coherent typed capability interface in Cortex. Do not add unrelated members to an existing runtime shell.

## 5. Module-owned state

Every plugin owns its own state through `runtime.State`.

Keep these lifetimes separate.

### Persistent

Use `runtime.State.Persistent` for values that can survive persistence boundaries.

Examples:

- preferred generation mode
- last-used view setting
- last-inspected symbol identifier

### Workflow

Use `runtime.State.Workflow` for in-session transient flows.

Examples:

- pending multi-step workflow state
- previews
- active summary models
- temporary selections

### Document/editor scope

Use `runtime.State.Contexts` with:

- `runtime.Editor.CreateDocumentScope(...)`
- `runtime.Editor.CreateEditorScope(...)`

Examples:

- document-specific notes
- editor-session placeholder navigation state
- per-surface insertion selections

Do not replace these with one plugin-owned bag.

## 6. Workbench modules

Register `IWorkbenchModuleContribution` when you need a renderable tool window.

The module should:

- render its own surface
- use runtime capabilities instead of shell internals
- report blocked state through `GetUnavailableMessage()`

Use `GetUnavailableMessage()` when the module depends on a runtime prerequisite. The Harmony plugin uses this to disable itself when `0Harmony` is not available in the active runtime.

## 7. Commands and actions

Prefer commands for behavior entry points.

Commands should be:

- stable by id
- reusable from module UI, menus, and editor surfaces
- gated by explicit enablement checks

Editor actions should remain declarative. The command handler should own the behavior.

## 8. Editor extension seams

Current reusable editor seams are:

- method inspector sections and actions
- editor adornments
- editor workflows
- editor context actions
- explorer filters

These seams must stay generic enough for future non-Harmony modules. If you add a new seam, validate it against a plausible non-Harmony scenario.

Examples:

- a review module can contribute an inspector notes section
- a diagnostics module can contribute a decompiler filter
- a templating module can contribute a multi-step editor workflow

## 9. How Harmony interacts with Cortex APIs

The extracted Harmony plugin is a concrete example of correct Cortex plugin usage.

It uses:

- `IWorkbenchPluginContributor` for entry
- `WorkbenchPluginContext` for registration
- `IWorkbenchModuleRuntime` for lifecycle, navigation, editor, document, project, and state access
- `IWorkbenchRuntimeAccess` for editor/inspector contribution callbacks
- `State.Persistent`, `State.Workflow`, and `State.Contexts` for distinct state lifetimes
- generic editor seams for inspector sections, adornments, and workflows

It does not use:

- private shell state
- private editor internals
- host-only Harmony fields
- renamed service-locator shells

## 10. Bundled vs third-party plugins

Bundled first-party plugins and third-party plugins follow the same model:

1. manifest discovery
2. plugin loader activation
3. contributor registration
4. runtime/module composition

Do not build first-party plugins around private shortcuts that third-party plugins cannot use.

## 11. Design rules

Follow these rules when authoring Cortex modules:

- keep the plugin entry class thin
- keep behavior split by responsibility
- prefer commands over cross-module reach-in
- prefer typed runtime capabilities over widening shared runtime shells
- keep persistent, workflow, and scoped state separate
- keep feature logic in the plugin, not the host
- design editor seams so a non-Harmony module could plausibly use them too

## 12. Packaging

A plugin must ship with:

- its assembly
- `cortex.plugin.json`

The manifest points Cortex at the assembly and `IWorkbenchPluginContributor` entry type. Bundled first-party plugins and third-party plugins use the same manifest/discovery/registration model.
