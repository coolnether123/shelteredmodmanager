# Cortex Module Authoring Guide

This guide describes the current public workbench authoring surface for Cortex.

The goal is to make contributions look more like a workbench platform and less like "reach into `CortexShell` and hope for the best."

## Current model

Today Cortex supports three public layers:

1. Commands
2. Declarative workbench contributions
3. Optional renderable workbench modules

The preferred plugin entry point is `IWorkbenchPluginContributor` from `Cortex.Plugins.Abstractions`.

`IWorkbenchPlugin` still works, but it is the legacy low-level form that exposes raw registries directly.

## Plugin entry point

Implement `IWorkbenchPluginContributor` when creating a Cortex workbench plugin:

```csharp
using Cortex.Core.Models;
using Cortex.Plugins.Abstractions;

namespace Example.CortexPlugin
{
    public sealed class ExamplePlugin : IWorkbenchPluginContributor
    {
        public string PluginId
        {
            get { return "example.plugin"; }
        }

        public string DisplayName
        {
            get { return "Example Plugin"; }
        }

        public void Register(WorkbenchPluginContext context)
        {
            context.RegisterCommand(
                "example.hello",
                "Say Hello",
                "Example",
                "Write a test command to the workbench.",
                string.Empty,
                0,
                true,
                false);

            context.RegisterMenu(
                "example.hello",
                MenuProjectionLocation.MainMenu,
                "Example",
                0,
                string.Empty);
        }
    }
}
```

## Registering commands

Use `WorkbenchPluginContext.RegisterCommand(...)` for command metadata and `RegisterCommandHandler(...)` when the runtime should execute logic.

Commands should be:

- stable by id
- declarative by default
- reusable from menus, palette, and module UI

Avoid hardwiring UI buttons directly to shell internals when a command id can express the action instead.

## Registering containers and views

Workbench containers and views are registered declaratively.

Typical pattern:

```csharp
context.RegisterViewContainer(
    "example.container",
    "Example",
    WorkbenchHostLocation.PanelHost,
    0,
    true,
    ModuleActivationKind.OnContainerOpen,
    "example.container",
    "example.container");

context.RegisterView(
    "example.container.main",
    "example.container",
    "Example",
    "example.container.main",
    0,
    true);
```

Container registration decides:

- identity
- default host
- default pinned state
- activation policy
- icon projection

View registration decides:

- view identity
- title
- persistence id
- ordering
- default visibility

## Registering a module

If your contribution needs custom rendering, register an `IWorkbenchModuleContribution`.

```csharp
using System;
using Cortex.Plugins.Abstractions;
using UnityEngine;

namespace Example.CortexPlugin
{
    public sealed class ExampleModuleContribution : IWorkbenchModuleContribution
    {
        public WorkbenchModuleDescriptor Descriptor
        {
            get { return new WorkbenchModuleDescriptor("example.container", typeof(ExampleModule)); }
        }

        public IWorkbenchModule CreateModule()
        {
            return new ExampleModule();
        }
    }

    public sealed class ExampleModule : IWorkbenchModule
    {
        public string GetUnavailableMessage()
        {
            return string.Empty;
        }

        public void Render(WorkbenchModuleRenderContext context, bool detachedWindow)
        {
            if (context != null && context.Ui != null)
            {
                context.Ui.DrawSectionPanel("Example", delegate
                {
                    GUILayout.Label("Hello from Cortex.");
                });
            }
            else
            {
                GUILayout.Label("Hello from Cortex.");
            }

            if (GUILayout.Button("Run Command") &&
                context.CommandRegistry != null)
            {
                context.CommandRegistry.Execute(
                    "example.hello",
                    new Cortex.Core.Models.CommandExecutionContext());
            }
        }
    }
}
```

Then register it from your plugin:

```csharp
context.RegisterModule(new ExampleModuleContribution());
```

## Render context contract

`WorkbenchModuleRenderContext` is intentionally narrow.

Modules currently receive:

- `ContainerId`
- `Snapshot`
- `CommandRegistry`
- `ContributionRegistry`
- `Ui`

This is deliberate. The public module API is meant to be workbench-facing, not shell-facing.

`Ui` is the host-owned workbench surface for shared Cortex chrome such as search bars, section headers, property rows, and property-page navigation widgets.
Prefer it for reusable shell patterns instead of copy/pasting raw IMGUI blocks.

## Current limits

This is a foundation layer, not a full VS Code extension host.

Current public module contributions should assume:

- the current backend implementation is Unity IMGUI-based
- module UI should be self-contained
- command execution is the preferred integration path
- internal shell services are not public plugin contracts yet

For shared module chrome, prefer `context.Ui` over directly reproducing the same `GUILayout` patterns in multiple modules.

That means built-in Cortex modules still have richer internal dependencies than external modules. This is intentional for now. The public API is being kept narrow until those service contracts are ready to be made stable.

## Recommended design rules

When authoring modules and contributions:

- Prefer commands over direct cross-module calls.
- Keep container/view registration declarative.
- Keep module rendering focused on UI and user interaction.
- Use `context.Ui` for shared workbench patterns and raw IMGUI only for custom widgets.
- Avoid storing global mutable state inside modules when a command or persisted setting can represent it.
- Treat `WorkbenchPluginContext` as the composition boundary, not as a place to hide runtime logic.

## Diagnostics

For opt-in tracing and investigation paths, use the Cortex diagnostics channel system instead of adding direct loader-specific logging in module code.

The rule is:

- use `Cortex.Core.Diagnostics`
- define a stable channel name
- let the active platform module decide how channels are enabled and where messages are routed

Current guidance and examples live in [Cortex_Diagnostics_Guide.md](/D:/Projects/_Archived/Sheltered%20Modding/shelteredmodmanager/documentation/Cortex_Diagnostics_Guide.md).

Full UI-surface guidance and examples live in [Cortex_UI_Surface_Guide.md](/D:/Projects/_Archived/Sheltered%20Modding/shelteredmodmanager/documentation/Cortex_UI_Surface_Guide.md).

## Registering settings

Modules can also contribute settings metadata through `WorkbenchPluginContext`.

Recommended pattern:

1. Register one section with `RegisterSettingSection(...)`
2. Register one or more settings under the same `Scope`
3. Use callback-backed settings when your module owns persistence or validation
4. Let Cortex build the property-page grouping, left-nav anchors, search indexing, validation chrome, and per-setting gear actions

Full guidance and examples live in [Cortex_Settings_Authoring_Guide.md](/D:/Projects/_Archived/Sheltered%20Modding/shelteredmodmanager/documentation/Cortex_Settings_Authoring_Guide.md).

## Legacy plugin interface

`IWorkbenchPlugin` is still supported for compatibility:

```csharp
void Register(ICommandRegistry commandRegistry, IContributionRegistry contributionRegistry)
```

Prefer `IWorkbenchPluginContributor` for new work because it also supports module registration and keeps authoring code aligned with the newer platform shape.
