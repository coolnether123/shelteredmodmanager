# Cortex Renderer and Host Extensibility Guide

This is the shortest path for adding a new host or renderer without reverse-engineering IMGUI behavior.

Follow these rules:

- portable behavior belongs in `Cortex.Rendering` or `Cortex.Rendering.RuntimeUi`
- host projects own backend selection and host input/frame adaptation
- concrete renderers execute portable plans and interactions; they do not become the behavior source
- public workbench modules keep targeting `IWorkbenchUiSurface`

## Current boundary map

- `Cortex.Rendering`
  - low-level render contracts, geometry, placement, shared models, and frame/input contracts
- `Cortex.Rendering.RuntimeUi`
  - portable popup/panel/tooltip interaction and layout planners
- `Cortex.Host.Unity`
  - Unity lifecycle, `IWorkbenchFrameContext`, viewport/input snapshot adaptation
- `Cortex.Renderers.Imgui`
  - concrete drawing, measurement, and execution over portable runtime-UI output
- `Cortex.Host.Sheltered`
  - host composition that selects the IMGUI runtime UI today

## Add a renderer

Implement a renderer when you want a new concrete executor for the same portable runtime UI behavior.

Required steps:

1. Add a new `Cortex.Renderers.<Name>` project.
2. Implement `IWorkbenchRuntimeUiFactory`.
3. Implement `IWorkbenchRuntimeUi`.
4. Implement `IRenderPipeline`.
5. Implement `IOverlayRendererFactory`, `IPanelRenderer`, and any backend-specific measurement helpers.
6. Consume `PanelLayoutPlanner`, `PopupMenuLayoutPlanner`, `PopupMenuInteractionController`, `HoverTooltipLayoutPlanner`, and `HoverTooltipInteractionController` instead of re-deriving those policies locally.
7. Consume `IWorkbenchFrameContext` and `RuntimeUiPointerInputAdapter` instead of reading host events directly in generic or portable code.
8. Add a proof test or command-recording backend test showing the renderer can execute portable output without IMGUI assumptions.

Do not:

- instantiate the renderer from generic shell code
- move popup, tooltip, panel, or focus policy into the renderer if portable runtime UI already owns it
- add renderer-specific types to public workbench module contracts

## Add a host

Implement a host when you need a different runtime environment, lifecycle, or backend selection policy.

Required steps:

1. Add a new `Cortex.Host.<Name>` project.
2. Implement `ICortexHostEnvironment`.
3. Implement `ICortexHostServices`.
4. Implement an `IWorkbenchFrameContext` that adapts host screen, pointer, keyboard, wheel, and frame timing into `WorkbenchFrameInputSnapshot`.
5. Construct the chosen `IWorkbenchRuntimeUiFactory` inside host composition.
6. Pass the same host-owned `IWorkbenchFrameContext` into the runtime UI/backend so overlay input adaptation stays host-owned.
7. Keep the generic shell consuming only `IWorkbenchRuntimeUi`, `IRenderPipeline`, `IWorkbenchUiSurface`, and `IWorkbenchFrameContext`.

Do not:

- add host-specific references from portable Cortex projects
- hard-code a renderer in `Cortex` shell/editor code
- invent host paths or plugin roots in portable projects

## Input and frame ownership

`IWorkbenchFrameContext` is the portable host-to-runtime boundary for frame state.

It should provide:

- viewport size
- current event kind and key
- pointer position
- mouse button
- wheel delta
- analog scroll delta if the host exposes one
- frame id
- whether the current frame allows visual refresh

Concrete renderers should adapt that snapshot to runtime UI through `RuntimeUiPointerInputAdapter`.

That keeps host event semantics in the host adapter instead of inside popup or tooltip renderers.

## What still belongs in a concrete renderer

Keep these renderer-local:

- actual draw calls
- backend text measurement
- backend texture/material caching
- backend-specific clipping/group execution

Keep these portable:

- popup item layout and close/scroll/activation policy
- tooltip visible-model, sticky-hover, placement, and content layout policy
- panel section/header/card/content layout policy

## Proof strategy

A second renderer is best, but a recording backend is enough to prove the boundary.

The test proof backend should:

- implement the same rendering interfaces
- record commands instead of drawing
- consume the portable planners/controllers
- run without Unity or IMGUI types

See the recording backend test coverage in `Cortex.Tests/Rendering/RecordingRuntimeUiBackendTests.cs`.

## Current Sheltered composition

Today the composition flow is:

1. `Cortex.Host.Sheltered` creates `UnityWorkbenchFrameContext`.
2. `Cortex.Host.Sheltered` selects `ImguiWorkbenchRuntimeUiFactory`.
3. `Cortex.Renderers.Imgui` builds `ImguiRenderPipeline`.
4. IMGUI renderers consume portable planners/controllers plus the host-owned frame context.

If you add another renderer or host, preserve that direction:

- host chooses
- portable runtime UI owns behavior
- backend executes
