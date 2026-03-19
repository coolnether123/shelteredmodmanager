# Cortex UI Surface Guide

`WorkbenchModuleRenderContext.Ui` is the host-owned UI surface for Cortex workbench modules.

Its job is simple:

- keep shared workbench chrome in one place
- reduce direct dependence on raw IMGUI patterns
- let Cortex evolve its runtime UI implementation without forcing every module to rewrite the same controls

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
