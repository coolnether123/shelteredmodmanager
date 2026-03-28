# Cortex UI Surface Guide

`WorkbenchModuleRenderContext.Ui` is the host-owned UI surface for Cortex workbench modules.

Its job is simple:

- keep shared workbench chrome in one place
- reduce direct dependence on raw IMGUI patterns
- let Cortex evolve its runtime UI implementation without forcing every module to rewrite the same controls

This guide is about the public workbench UI surface for modules.
It is not the same thing as the internal renderer pipeline used by the shell/editor.

## When to use `context.Ui`

Use `context.Ui` for shared Cortex patterns such as:

- search toolbars
- property-page section headers
- left-nav items and group headers
- property rows
- standard section panels
- popup-style gear/context menus
- label/description columns for settings-like rows

`BeginPropertyRow()` / `EndPropertyRow()` are host-owned on purpose. That lets Cortex add shared interaction
affordances such as hover highlighting without every module re-implementing row chrome.

Use raw `GUILayout` only when:

- you are drawing a truly custom control
- the control is domain-specific and not reusable shell chrome
- Cortex does not already expose a suitable surface method

The rule is:

- shared shell chrome goes through `context.Ui`
- module-specific widgets can still use IMGUI directly inside that shared chrome

Built-in Cortex editor surfaces are moving further than this by using renderer abstractions for overlays and panels.
External modules should still treat `context.Ui` as the preferred public contract.

## Current surface

The current `IWorkbenchUiSurface` contract includes:

- `DrawSearchToolbar(...)`
- `DrawNavigationGroupHeader(...)`
- `DrawNavigationItem(...)`
- `DrawCollapsedNavigationItem(...)`
- `DrawSectionHeader(...)`
- `DrawSectionPanel(...)`
- `DrawPopupMenuPanel(...)`
- `BeginPropertyRow()`
- `EndPropertyRow()`
- `DrawPropertyLabelColumn(...)`

This is intentionally small. Cortex should expose reusable workbench patterns, not a one-to-one wrapper over every `GUILayout` call.

## Example

```csharp
using Cortex.Plugins.Abstractions;
using UnityEngine;

public sealed class ExampleModule : IWorkbenchModule
{
    private string _search = string.Empty;

    public string GetUnavailableMessage()
    {
        return string.Empty;
    }

    public void Render(WorkbenchModuleRenderContext context, bool detachedWindow)
    {
        var ui = context != null ? context.Ui : null;
        if (ui == null)
        {
            GUILayout.Label("UI surface unavailable.");
            return;
        }

        _search = ui.DrawSearchToolbar("Search items", _search, 42f, true);

        ui.DrawSectionPanel("Example", delegate
        {
            ui.BeginPropertyRow();
            GUILayout.BeginHorizontal();
            ui.DrawPropertyLabelColumn("Name", "A simple module-owned field.");
            GUILayout.BeginVertical(GUILayout.Width(320f));
            _search = GUILayout.TextField(_search ?? string.Empty, GUILayout.Height(24f));
            GUILayout.EndVertical();
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            ui.EndPropertyRow();
        });
    }
}
```

## Design guidance

- Prefer the UI surface for anything that should look and behave like Cortex.
- Keep the interface small and additive.
- If multiple modules need the same UI pattern, add it to the surface instead of copy/pasting IMGUI blocks.
- Do not leak shell internals through the UI surface. Keep it presentation-focused.

## Current backend

Today the default implementation is IMGUI-backed inside Cortex.

That is an implementation detail, not the module contract.

Modules should code against `IWorkbenchUiSurface`, not against `CortexUi` or `CortexShell`.

Internally, Cortex is also moving toward renderer-agnostic shell/editor infrastructure:
- the shell owns the active render pipeline
- renderer-neutral geometry/color models live in `Cortex.Rendering`
- IMGUI implementations live in `Cortex.Renderers.Imgui`

That does not change the module authoring rule:
- public workbench modules should target `IWorkbenchUiSurface`
- they should not depend on IMGUI-only editor internals or renderer-specific classes
