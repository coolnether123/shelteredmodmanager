# Input Keybindings Guide

The 1.3 line is a breaking clean API line.

## Assembly Rule

- Always reference `ModAPI.dll`.
- Reference `ShelteredAPI.dll` when your mod uses Sheltered content, saves, UI, input, events, actors, or scenarios.

## API Stability Rules

- Public facades are stable.
- Implementation classes are internal.
- Typed Sheltered escape hatches are explicit.
- Future migrations should happen behind facades.

Sheltered Mod Manager v1.3 exposes rebindable controls through a split runtime:

- `ModAPI.dll` owns neutral input contracts: `ModInputAction`, `InputBinding`, `InputActionRegistry`, scroll/touch input service contracts, and action query helpers.
- `ShelteredAPI.dll` owns Sheltered integration: vanilla action registration, `PlatformInput_PC` patches, settings UI, key validation, conflict handling, persistence, and runtime input tuning.

Use `ModAPI.InputActions` when your mod needs a configurable action. Reference `ShelteredAPI.dll` only when you need Sheltered-specific action ids, context lookup, or runtime tuning through `ShelteredAPI.Input.ShelteredInput`.

## Registering A Mod Action

Register actions during plugin initialization/startup before players open the Controls UI:

```csharp
using ModAPI.Core;
using ModAPI.InputActions;
using UnityEngine;

public sealed class MyPlugin : IModPlugin
{
    public void Initialize(IPluginContext ctx)
    {
        InputActionRegistry.Register(new ModInputAction(
            id: "com.example.quick_toggle",
            label: "Quick Toggle",
            description: "Toggles the example mod overlay.",
            category: "Mods",
            defaultBinding: new InputBinding(KeyCode.F10, KeyCode.None)));
    }

    public void Start(IPluginContext ctx) { }
}
```

Action ids should be stable, unique, and namespaced to your mod id. Changing an action id breaks persisted user bindings for that action.

## Reading Input

Read actions by id instead of calling Unity input directly:

```csharp
if (InputActionRegistry.IsDown("com.example.quick_toggle"))
{
    ToggleOverlay();
}
```

Available query helpers:

- `InputActionRegistry.IsDown(actionId)`
- `InputActionRegistry.IsHeld(actionId)`
- `InputActionRegistry.IsUp(actionId)`
- `InputActionRegistry.TryGetBinding(actionId, out binding)`

## Controls UI

ShelteredAPI registers vanilla Sheltered actions and displays all registered actions in the in-game Controls screen.

The provider listens for late `InputActionRegistry.OnActionRegistered` events. If a plugin registers an action after ShelteredAPI bootstrap, persisted bindings are loaded for that action and the cached settings definitions are invalidated so it can appear the next time the Controls UI is built.

Built-in Sheltered actions are grouped ahead of mod actions. Third-party actions appear under the Mods keybindings section unless they use a recognized Sheltered category.

## Validation And Conflicts

ShelteredAPI applies this pipeline for binding changes:

1. Validate the requested key.
2. Normalize duplicate primary/secondary slots on the same action.
3. Detect conflicts across registered actions.
4. Prompt for override/cancel when UI is available.
5. Apply and persist the resulting binding.

Reserved system keys are rejected for user rebinding. `KeyCode.None` is accepted to clear a slot. Default bindings can still use keys that would be unsafe for arbitrary user assignment, such as vanilla `Escape` behavior.

Same-context conflicts can be overridden. Cross-context conflicts are treated more conservatively and recommend canceling unless the user explicitly overrides.

## Persistence

Bindings are stored in `ModPrefs` with the `ShelteredAPI.Keybind.` prefix. `ShelteredKeybindPersistenceGuard` saves on runtime shutdown paths, and the Controls UI also persists changed bindings.

Because bindings are global preferences, do not encode save-slot-specific behavior into action ids.

## Vanilla Sheltered Input

ShelteredAPI maps vanilla `PlatformInput.InputButton` and `PlatformInput.MenuInputButton` values to registered actions and patches `PlatformInput_PC` button/axis reads. This makes vanilla controls and mod controls share the same registry, conflict policy, and persistence path.

Scenario authoring can temporarily own gameplay input. During that mode, ShelteredAPI blocks gameplay buttons and axes that would otherwise interfere with editor interactions.

## Runtime Tuning

Sheltered runtime tuning is exposed through `ShelteredInput`:

- Zoom speed
- Touchpad movement speed
- Mouse scroll speed

```csharp
using ShelteredAPI.Input;

ShelteredInput.ZoomSpeed = 1.25f;
ShelteredInput.TouchpadMovementSpeed = ShelteredInput.DefaultTouchpadMovementSpeed;
ShelteredInput.MouseScrollSpeed = ShelteredInput.NormalizeSpeedScale(1.1f, ShelteredInput.DefaultMouseScrollSpeed);
```

These values are persisted by the Controls provider when changed through the settings UI and applied when settings load.

## Sheltered-Specific Helpers

Most mods do not need Sheltered-specific input APIs. When needed:

- `ShelteredInput.EnsureReady()` registers vanilla Sheltered actions and loads persisted keybinds.
- `ShelteredInput.RegisterVanillaActions()` registers the vanilla catalog without forcing provider load.
- `ShelteredInput.GetContextForActionId(actionId)` returns the Sheltered conflict/validation context for known Sheltered action ids.
- `ShelteredInputActions.IsShelteredAction(actionId)` is a lightweight action-id helper.

Implementation details such as the keybind provider, validation policy, conflict resolver, PlatformInput patches, capture listener, and conflict dialog are internal.
