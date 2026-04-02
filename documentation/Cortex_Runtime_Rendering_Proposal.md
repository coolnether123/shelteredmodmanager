# Cortex Runtime Rendering Proposal

This document reviews the current IMGUI render path and proposes a new portable runtime rendering module for in-game and application-hosted UI.

The goal is not to remove `Cortex.Renderers.Imgui`.
The goal is to stop treating IMGUI as the behavior model for Cortex UI.

## Status after the rendering-boundary refactor

The first boundary refactor is now in place:

- `Cortex.Rendering` remains the low-level contract layer.
- `Cortex.Rendering` now also owns the portable frame/input contract consumed by hosts and runtime UI backends.
- `Cortex.Rendering.RuntimeUi` owns shared popup/panel/tooltip layout and interaction behavior over those contracts.
- `Cortex.Renderers.Imgui` is isolated as a concrete runtime UI/backend implementation.
- generic shell/runtime paths no longer construct `ImguiRenderPipeline` directly.
- Sheltered host composition now selects the IMGUI runtime UI explicitly.
- `Cortex.Host.Unity` now owns frame-context and Unity-input snapshot adaptation, and shell-generic Cortex consumes the portable runtime-UI frame contract instead of a shell-specific host UI adapter.
- portable runtime-UI controllers no longer depend on Unity IMGUI event types or `Event.current`.
- IMGUI now consumes portable popup draw layouts, panel element layouts, and tooltip layout plans instead of re-owning those geometry decisions locally.

The remaining sections explain the rationale and longer-term direction for deeper behavior extraction.

For the concrete seams to follow today, use `documentation/Cortex_Renderer_Host_Extensibility_Guide.md`.

## 1. Current state review

The current architecture direction is correct:

- `documentation/Cortex_Architecture_Guide.md` places renderer-neutral behavior in portable Cortex and keeps Unity-specific rendering integration in host-specific Cortex.
- `documentation/Cortex_UI_Surface_Guide.md` says public modules should target `IWorkbenchUiSurface`, while renderer-neutral geometry/color models live in `Cortex.Rendering` and IMGUI implementations live in `Cortex.Renderers.Imgui`.
- `documentation/Cortex_Portability_Report.md` says host-specific Cortex owns host rendering/backend wiring.

The current codebase already reflects part of that split:

- `Cortex.Rendering` contains portable geometry, panel, popup, tooltip, text-input, and virtualization contracts.
- `Cortex.Renderers.Imgui` provides a concrete backend implementation.
- editor surfaces consume `IPanelRenderer` and `IOverlayRendererFactory` instead of directly drawing every overlay themselves.

That is a good foundation.

## 2. Current problems

### 2.1 Backend selection and frame adaptation must stay host-owned

That boundary is now enforced:

- generic shell/runtime code no longer instantiates the IMGUI pipeline directly
- `Cortex.Host.Sheltered` selects the IMGUI runtime UI explicitly
- `Cortex.Host.Unity` owns viewport/frame-context and Unity input snapshot adaptation

That keeps backend selection and host event semantics out of portable and shell-generic Cortex code.

### 2.2 IMGUI backend owns behavior, not just drawing

`Cortex.Renderers.Imgui` currently owns:

- layout policy
- theme materialization
- text measurement assumptions
- pointer capture rules
- scroll behavior
- tooltip placement behavior
- popup interaction state
- texture cache lifetime

Examples:

- `Cortex.Renderers.Imgui/ImguiPanelRenderer.cs`
- `Cortex.Renderers.Imgui/ImguiPopupMenuRenderer.cs`
- `Cortex.Renderers.Imgui/ImguiHoverTooltipRenderer.cs`

That makes the backend an execution engine plus renderer. A new backend would need to re-implement interaction policy, not just drawing.

### 2.3 Portable consumers still depend on Unity-shaped rendering concepts

The editor path uses renderer abstractions for overlays and panels, but the surrounding surfaces still depend on `UnityEngine.Rect`, `GUIStyle`, `Event`, and `GUILayout`-era assumptions.

Examples:

- `Cortex/Modules/Editor/CodeViewSurface.cs`
- `Cortex/Modules/Editor/EditableCodeViewSurface.cs`
- `Cortex/Modules/Editor/EditorMethodInspectorSurface.cs`

This means the current renderer abstraction is partial. It abstracts popup/panel drawing, but not the frame model.

### 2.4 Module UI surface implementation is still shell-local and IMGUI-backed

`Cortex/Layout/CortexUi.cs` creates a static IMGUI-backed `IWorkbenchUiSurface`.

That is acceptable as a transitional implementation, but it means the public module UI surface is not actually host-selected yet.

### 2.5 Tooltip behavior exists in more than one place

There is backend tooltip logic in:

- `Cortex.Renderers.Imgui/ImguiHoverTooltipRenderer.cs`

There is also overlapping tooltip presenter behavior in:

- `Cortex/Modules/Shared/HoverTooltipPresenter.cs`

That overlap is a signal that behavior and drawing responsibilities are not fully separated.

## 3. Proposed module

Add a new portable module:

- `Cortex.Rendering.RuntimeUi`

This module should be the renderer-agnostic runtime UI engine for overlays, tool panels, menus, tooltips, and HUD-like surfaces in games or desktop applications.

It should not know about:

- Unity
- IMGUI
- Sheltered
- ModAPI
- game-specific paths
- loader-specific lifecycle

It should know about:

- frame input
- focus and capture
- layout
- hit testing
- scrolling
- keyboard routing
- overlay layering
- render command generation
- theme tokens

## 4. Target architecture

Split responsibilities into four layers.

### 4.1 Portable UI model and runtime

Project:

- `Cortex.Rendering.RuntimeUi`

Responsibilities:

- UI tree or retained scene model
- layout engine
- hit testing
- focus navigation
- pointer capture
- scroll state
- tooltip/menu/panel interaction policies
- render command generation
- backend-neutral theme model

This is where behavior should live.

### 4.2 Portable render contract layer

Project:

- `Cortex.Rendering`

Responsibilities:

- primitive geometry/color models
- text/image abstractions
- render command contracts
- backend capability contracts

`Cortex.Rendering` remains the low-level contract package.
`Cortex.Rendering.RuntimeUi` builds on it.

### 4.3 Host adapter layer

Projects:

- `Cortex.Host.Unity`
- future host adapters

Responsibilities:

- collect frame timing
- collect pointer/keyboard/text input
- expose viewport and DPI/scale
- create the active backend
- map host lifecycle to UI runtime lifecycle

The host chooses the backend. The shell should not construct `ImguiRenderPipeline` directly.

### 4.4 Concrete renderer backends

Projects:

- `Cortex.Renderers.Imgui`
- future `Cortex.Renderers.*`

Responsibilities:

- execute draw commands
- provide text measurement
- provide image/texture upload hooks
- provide optional backend-specific optimizations

Backends should not own menu policy, tooltip policy, or scroll behavior.

## 5. Core contracts

The new module should expose contracts shaped more like a UI runtime and less like backend-specific widgets.

Suggested contracts:

```csharp
public interface IRuntimeUiHost
{
    UiFrameContext CreateFrameContext();
    IRuntimeUiBackend Backend { get; }
}

public interface IRuntimeUiBackend
{
    RuntimeUiCapabilities Capabilities { get; }
    ITextMeasurementService TextMeasurement { get; }
    void Execute(UiCommandList commands, UiRenderTarget target);
}

public interface IRuntimeUiSurface
{
    UiFrameResult RunFrame(UiFrameContext context, UiNode root);
}
```

Suggested data models:

- `UiFrameContext`
- `UiInputSnapshot`
- `UiPointerState`
- `UiKeyboardState`
- `UiNavigationState`
- `UiTheme`
- `UiStyle`
- `UiNode`
- `UiLayoutResult`
- `UiCommandList`
- `UiCommand`
- `UiFrameResult`

Suggested command primitives:

- draw rect
- draw border
- draw text
- draw image
- begin clip
- end clip
- push transform
- pop transform
- set cursor

## 6. Why this is better than the current IMGUI-shaped pipeline

### 6.1 Backends become replaceable

If the runtime emits commands and owns behavior, the backend only needs to:

- measure text
- resolve textures
- draw primitives

That is what makes the system portable across games and applications.

### 6.2 Input becomes host-neutral

A game using Unity, MonoGame, SDL, WinForms, or another host can all provide the same `UiInputSnapshot`.

### 6.3 Layout and interaction become testable

Portable layout and interaction code can be unit tested without Unity `Event.current` and without `OnGUI`.

### 6.4 Shared behavior stops being duplicated

Tooltip stickiness, popup scrolling, focus rules, and capture logic can live in one portable place instead of being repeated or split between shell code and backend code.

## 7. What should move out of IMGUI first

The first migration should move behavior, not pixels.

Move these concerns into `Cortex.Rendering.RuntimeUi`:

- popup menu item layout and activation policy
- tooltip placement and sticky hover policy
- panel section expansion behavior
- scroll viewport behavior
- pointer capture and close-on-outside-click policy
- focus and keyboard routing state

Keep these in `Cortex.Renderers.Imgui`:

- `GUI` and `GUILayout` drawing calls
- texture/material caching
- conversion between render commands and Unity IMGUI operations
- IMGUI-specific text measurement adapter

## 8. Minimum viable migration plan

### Phase 1

Stop hard-coding IMGUI construction:

- move render pipeline creation behind host-owned factories
- let `Cortex.Host.Unity` choose the backend
- remove direct `new ImguiRenderPipeline()` calls from shell code

### Phase 2

Introduce command-list rendering in `Cortex.Rendering`:

- define `UiCommandList`
- define backend execution interface
- add an IMGUI command executor

### Phase 3

Introduce `Cortex.Rendering.RuntimeUi`:

- move popup, tooltip, panel interaction behavior into portable presenters/runtime controllers
- make IMGUI backend consume runtime-generated commands

### Phase 4

Replace static shell-local UI surface construction:

- host provides `IWorkbenchUiSurface`
- default Unity host may still return an IMGUI-backed implementation
- public modules stay unchanged

### Phase 5

Prove agnosticism with a second backend:

- a simple test backend that records commands, or
- a second real backend in another host

Without a second backend or a command-recording test backend, the architecture remains theoretical.

## 9. Recommended project layout

Recommended long-term layout:

- `Cortex.Rendering`
  - low-level render contracts and primitives
- `Cortex.Rendering.RuntimeUi`
  - portable UI runtime, layout, interaction, command generation
- `Cortex.Renderers.Imgui`
  - IMGUI backend executor
- `Cortex.Host.Unity`
  - Unity lifecycle, input snapshot, backend selection
- `Cortex.Host.<FutureGame>`
  - future host lifecycle and backend selection

This stays consistent with the current portability documents.

## 10. Recommendation

Do not create a new generic in-game rendering module by copying the IMGUI module and renaming it.

Do this instead:

1. Keep `Cortex.Renderers.Imgui` as a backend package.
2. Add `Cortex.Rendering.RuntimeUi` as the portable runtime UI module.
3. Move behavior out of IMGUI and into the runtime module.
4. Make the host choose the backend.
5. Keep public workbench modules targeting `IWorkbenchUiSurface`.

That gives Cortex a rendering stack that can work in any game or application because the portable layer owns UI behavior and the backend only owns drawing.
