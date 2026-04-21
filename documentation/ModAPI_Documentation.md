# ModAPI Project Map (Current v1.3 Line)

This document is the current high-level map of the codebase. It is intentionally module-oriented rather than a stale file-by-file dump.

For exact callable signatures, use `documentation/API_Signatures_Reference.md`.

## Compatibility Matrix

| Scope | Applies To | Status |
|-------|------------|--------|
| Module roles and architecture intent | Current codebase | Supported |
| Public signatures | Current codebase | Prefer signature reference |
| Historical v1.2 file-role notes | Older docs/snippets | Deprecated where conflicting |

## 1. Core Runtime

Primary areas:
- `ModAPI/Core`
- `ModAPI/Hooks`

Responsibilities:
- mod discovery and load ordering
- loader bootstrap
- plugin lifecycle orchestration
- plugin context creation
- logging
- deterministic random streams
- per-mod save registration

Key files:
- `PluginManager.cs`
- `PluginRunner.cs`
- `PluginContextImpl.cs`
- `IPlugin.cs`
- `ModRegistry.cs`
- `ModDiscovery.cs`
- `PrefixedLogger.cs`
- `ModRandom.cs`
- `SaveSystemImpl.cs`

## 2. Actor System

Primary areas:
- `ModAPI/Actors`
- `ShelteredAPI/Actors`

Current 1.3 model:
- public contracts live in `ModAPI.Actors`
- the default runtime implementation is supplied by `ShelteredAPI`
- `IPluginContext.Actors` is the main entry point

Capabilities:
- registry CRUD
- namespaced components
- stable bindings
- modular adapters
- event subscriptions
- simulation scheduling
- JSON persistence

Related guide:
- `documentation/ShelteredAPI_Characters_Guide.md`

## 3. Compatibility Surfaces

These remain available in the 1.3 line to preserve older mod integrations:
- `GameEvents`
- `GameTimeTriggerHelper`
- `UIEvents`
- `FactionEvents`
- `PartyHelper`
- `InteractionRegistry`
- `GameUtil`
- `PersistentDataAPI`

Current location:
- `ModAPI.dll`

Status:
- supported in 1.3
- deprecated for a future major version where cleaner replacements exist

## 4. Content System

Primary area:
- `ShelteredAPI/Content`

Responsibilities:
- item registration
- recipe registration
- localization binding for content
- asset loading
- inventory/content integration

Key files:
- `ContentRegistry.cs`
- `ContentResolver.cs`
- `ContentInjector.cs`
- `InventoryIntegration.cs`
- `AssetLoader.cs`

Ownership note:
- public content APIs now live in `ShelteredAPI.Content`
- `ModAPI.dll` only keeps thin internal bridges where shared runtime helpers need to query Sheltered content without creating an assembly cycle

## 5. Settings and Persistence

Primary areas:
- `ModAPI/Spine`
- `ModAPI/Core`
- `ModAPI/Util`

Responsibilities:
- settings metadata scanning
- settings UI/controller generation
- per-save mod state
- convenience save/load helpers

Main patterns:
- `ModManagerBase<T>`
- `ISettingsProvider`
- `ISaveSystem.RegisterModData(...)`
- `ctx.SaveData(...)` / `ctx.LoadData(...)`

Related guides:
- `documentation/Spine_Settings_Guide.md`
- `documentation/SETTINGS.md`

## 6. Events

Primary area:
- `ModAPI/Events`

Responsibilities:
- core gameplay lifecycle events
- UI lifecycle events
- inter-mod event bus
- deterministic scheduler triggers

Key files:
- `GameEvents.cs`
- `GameTimeTriggerHelper.cs`
- `UIEvents.cs`
- `FactionEvents.cs`
- `ModEventBus.cs`

Related guide:
- `documentation/Events_Guide.md`

## 7. Harmony and Transpilers

Primary area:
- `ModAPI/Harmony`

Responsibilities:
- Harmony bootstrap
- patch registration
- fluent transpiler surface
- cooperative patching
- stack validation and debugging

Key files:
- `HarmonyBootstrap.cs`
- `PatchRegistry.cs`
- `FluentTranspiler.cs`
- `IntentAPI.cs`
- `CooperativePatcher.cs`
- `TranspilerDebugger.cs`
- `TranspilerTestHarness.cs`

Related guides:
- `documentation/how to develop a patch with harmony.md`
- `documentation/Transpiler_and_Debugging_Guide.md`

## 8. UI Runtime

Primary area:
- `ModAPI/UI`

Responsibilities:
- runtime UI helpers
- panel lifecycle bridging
- UI factory helpers
- settings UI
- debug UI

Representative files:
- `UIUtil.cs`
- `UIHelper.cs`
- `UIPatches.cs`
- `UIPatchCoordinator.cs`
- `UIFactory.cs`
- `Runtime/*`

## 9. Save Expansion

Primary area:
- `ModAPI/Custom Saves`

Responsibilities:
- expanded save slots
- save metadata and manifests
- page navigation
- verification and diagnostics
- preview capture

Treat this as a standalone subsystem layered under the loader rather than a small helper.

## 10. Inspector and Debugging

Primary areas:
- `ModAPI/Inspector`
- `ModAPI/Debugging`

Responsibilities:
- runtime inspection
- hierarchy and bounds helpers
- IL inspection
- source snapshotting
- debugger UI

## 11. ShelteredAPI Layer

Primary area:
- `ShelteredAPI`

What it adds:
- Sheltered-specific runtime implementations
- `IGameHelper` implementation
- actor-system implementation
- Sheltered-specific UI/input adapters

Important distinction:
- many public contracts are in `ModAPI`
- `ShelteredAPI` usually supplies the concrete runtime behavior

Related guide:
- `documentation/Custom_Scenarios_Guide.md`

## 12. Recommended Reading Order

1. `documentation/API_Signatures_Reference.md`
2. `documentation/ModAPI_Developer_Guide.md`
3. `documentation/how to develop a plugin.md`
4. `documentation/ShelteredAPI_Guide.md`
5. task-specific guides such as events, settings, actors, or transpilers
