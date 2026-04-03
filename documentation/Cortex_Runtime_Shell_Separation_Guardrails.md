# Cortex Runtime and Shell Separation Guardrails

This document defines the guardrails for the current Cortex runtime/shell separation refactor phase.

Use it with:

- `documentation/Cortex_Architecture_Guide.md` for the long-lived Cortex role map
- `documentation/Cortex_Portability_Report.md` for project/package topology
- `documentation/Cortex_Renderer_Host_Extensibility_Guide.md` for renderer and host extension seams

This phase is intentionally narrow:

- tighten the ownership boundary between runtime, shell, bridge/host, and concrete backend code
- prevent portable Cortex layers from regaining Unity or IMGUI coupling
- keep IMGUI supported as a concrete backend path
- document the remaining violations clearly so later sections can remove them deliberately instead of preserving them accidentally

Desktop-first direction for this phase:

- Cortex is being prepared for a future `.NET 8` desktop host before that host is introduced
- the intended desktop stack is Avalonia for rendering, Dock for workbench/docking structure, and Serilog for structured desktop/worker logging
- `Cortex.Contracts` is the first dedicated desktop-shareable lane; new desktop-facing contracts/models must not stay trapped only in `net35` projects once they need to cross that boundary
- the Unity IMGUI path remains supported, but only as the legacy concrete host/shell/backend path during this refactor

## Current violations to shrink

The current codebase still has a few runtime/shell boundary violations or transitional seams that must shrink over later refactor sections:

- `CortexShellState` is still a catch-all state object. It mixes runtime-owned application state, shell-local interaction state, and Unity-facing layout geometry in one generic-looking type.
- `Cortex` is still the active Unity IMGUI shell assembly. That is acceptable for current execution, but it means shell code still contains Unity drawing/event handling and should not be treated as a headless runtime layer.
- `UnityWorkbenchRuntime` is host-owned, but its name still reads like the generic runtime instead of a Unity-hosted composition/runtime shell.
- `IWorkbenchRuntimeUi` is still a transitional seam that bundles render pipeline, UI surface, and frame context together. It is currently a shell/backend seam, not a pure headless runtime contract.
- Some shell-local layout/chrome behavior is still executed directly in IMGUI call sites even though the reusable policy is already moving into `Cortex.Rendering.RuntimeUi`.

These violations are tolerated only inside the current shell/host/backend path. They are not permission to reintroduce the same coupling into portable Cortex layers.

Current section focus:

- generic `CortexShellState` should stop owning window rectangles, collapsed chrome geometry, detached-log view toggles, and layout-tree bookkeeping
- shell-local window/chrome state should move under shell-owned types
- layout drawing should consume state while explicit shell lifecycle/services synchronize any runtime visibility flags
- runtime composition and startup/configuration logic should move into headless runtime services so Cortex can initialize settings, workbench services, and plugin discovery without a concrete UI host
- onboarding coordinator should stay headless while shell-owned onboarding presenter code handles modal geometry, IMGUI drawing, prompt placement, and overlay input presentation
- settings document building, validation, contribution collection, and apply logic should live in headless services/builders/models instead of the IMGUI settings module
- settings session/apply behavior and onboarding flow-step selection should be reusable services/models rather than IMGUI-only module logic
- editor decisions and status presentation should move into headless services while the IMGUI editor module stays focused on drawing, scroll state, and direct Unity event handling
- project workspace mapping/import flows, reference-browser selection and decompile coordination, and search-result shaping should live in headless services while the IMGUI modules stay focused on field widgets, scroll state, and click execution
- IMGUI-only status-strip/module-render presenters should live in `Cortex.Shell.Unity.Imgui` instead of the generic `Cortex` assembly so the legacy shell ownership is obvious

## Target ownership boundaries for this phase

### Runtime ownership

Runtime-owned Cortex code should be able to survive a future desktop shell, web shell, alternate game shell, or headless host.

Runtime ownership in this phase means:

- application/runtime orchestration
- document, project, command, and language workflow coordination
- durable runtime state ownership
- UI-neutral presentation shaping and snapshots
- portable rendering contracts and portable runtime-UI interaction/layout policy

Runtime-owned code must not take direct dependencies on Unity, IMGUI event semantics, or Sheltered-specific host composition.
Runtime also should not own shell/presentation-owned snapshot assembly when the work is presentation shaping rather than state ownership.

### Shell ownership

Shell-owned Cortex code is the user-facing client layer that consumes runtime state and presentation output.

Shell ownership in this phase means:

- workbench window/chrome behavior
- shell-local layout state
- shell-local interaction flow
- concrete IMGUI execution in the current in-game shell
- orchestration that is specifically about presenting/runtime-hosting the workbench client

Shell code may stay Unity-hosted in this phase, but shell-only state should not keep leaking into generic runtime contracts or generic state models.
This includes shell/presentation-owned snapshot assembly: the runtime exposes state and registries, while the shell/presentation path builds the current `WorkbenchPresentationSnapshot`.
Window placement, collapsed chrome geometry, detached-log window visibility, and layout-tree bookkeeping are shell-local view concerns and should live in shell-owned state rather than generic shared runtime state.
When runtime visibility flags need to reflect shell-owned layout intent, update them from an explicit shell lifecycle/service step instead of mutating runtime state during draw/layout tree construction.
The same rule applies to onboarding overlays: the coordinator/service layer should own onboarding state transitions, selection, preview, completion, and workspace application, while a shell-owned onboarding presenter owns modal geometry, IMGUI drawing, prompt placement, focus, and input capture presentation.
The same ownership split applies to startup/configuration and settings/editor/workspace modules: headless runtime services should own host-neutral startup/configuration, plugin loading, workbench service composition, project workspace orchestration, reference-browser state, search-result shaping, and editor presentation decisions, while shell code keeps only host bootstrap and window/view concerns. Shell modules may keep IMGUI widgets, textures, scroll state, and draw-time composition, but settings document shaping, settings session/apply behavior, validation, contribution collection, onboarding flow-step selection, editor mode/status decisions, shortcut interpretation, project mapping workflows, reference selection/decompile coordination, and search summaries should move into headless services or presentation builders.

### Bridge/host ownership

Bridge/host code adapts Cortex to the active platform, game, and loader/runtime environment.

Bridge/host ownership in this phase means:

- runtime bootstrap and composition roots
- platform/runtime integration
- filesystem/environment/path layout for the active host
- frame/input adaptation from the host into portable frame contracts
- selection of the active runtime UI/backend path

Generic Cortex projects must not reference host-specific projects. Host-specific identities such as Sheltered, ModAPI, or Unity host composition stay isolated in host projects.

### Concrete backend ownership

`Cortex.Renderers.Imgui` remains the concrete backend/executor path for this phase.

IMGUI remains supported, but only as:

- concrete draw execution
- backend-specific measurement/material behavior
- the concrete shell/runtime UI path selected by host composition

IMGUI is not the generic runtime model. Portable runtime behavior belongs in `Cortex.Rendering` and `Cortex.Rendering.RuntimeUi`, and backend selection remains host-owned.

## Naming guardrails for generic layers

When a type or contract is truly generic, prefer terminology that still makes sense with multiple shells or hosts:

- use `Runtime` for headless orchestration/state ownership
- use `Shell` for user-facing client behavior
- use `Bridge` or `Host` for platform/game integration
- use `Presentation` for UI-neutral projection/shaping

Avoid introducing Unity-, IMGUI-, Sheltered-, or ModAPI-shaped names into portable/generic Cortex layers.

Current transitional examples called out for later cleanup:

- `CortexShellState`
- `UnityWorkbenchRuntime`
- `IWorkbenchRuntimeUi`

They are documented here so later sections can either rename or reshape them intentionally instead of normalizing them as permanent architecture.

## Guardrail rules enforced in tests

The architecture tests for this refactor phase protect these rules:

- headless/runtime-facing Cortex layers do not gain Unity or IMGUI dependencies
- portable/generic Cortex projects do not reference host-specific Cortex projects
- IMGUI stays a concrete backend path rather than a generic runtime dependency
- generic source files do not start using Unity-, IMGUI-, Sheltered-, or ModAPI-shaped runtime/backend names
- the runtime/shell separation goals for this phase stay documented in-repo

Current test coverage lives in:

- `Cortex.Tests/Architecture/RuntimeShellGuardrailArchitectureTests.cs`
- `Cortex.Tests/Architecture/RuntimeUiArchitectureTests.cs`
- `Cortex.Tests/Architecture/CortexProjectTopologyBuildTests.cs`
- `Cortex.Tests/Architecture/HostPlatformDependencyArchitectureTests.cs`

## What this section does not do

This guardrail pass does not itself:

- move major shell/editor modules
- redesign workbench visuals or interaction flow
- build a generalized transport/protocol stack
- replace IMGUI

It exists to make later runtime/shell separation work safer and harder to regress.
