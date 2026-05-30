# ModAPI Project Map (v2.0)

This document is the current high-level map of the codebase. It is intentionally module-oriented rather than a stale file-by-file dump.

For exact callable signatures, use [API Signatures Reference](API_Signatures_Reference.md). Mod authors should make assembly choices from the canonical [assembly boundary](README.md#assembly-boundary-canonical); this project map describes implementation ownership.

The 2.0 line is a breaking clean API line.

## Compatibility Matrix

| Scope | Applies To | Status |
|-------|------------|--------|
| Module roles and architecture intent | Current codebase | Supported |
| Public signatures | Current codebase | Prefer signature reference |
| Historical file-role notes | Older docs/snippets | Historical only |

## 1. Core Runtime

Primary areas:
- `ModAPI/Core`
- `ModAPI/Loading`
- `ModAPI/Persistence`

Responsibilities:
- mod discovery and load ordering
- loader bootstrap
- plugin lifecycle orchestration
- plugin context creation
- logging
- deterministic random streams

Key files:
- `PluginManager.cs`
- `PluginRunner.cs`
- `PluginContextImpl.cs`
- `IPlugin.cs`
- `ModRegistry.cs`
- `ModDiscovery.cs`
- `ModLoadOrderReader.cs`
- `ModLoadPlanBuilder.cs`
- `PrefixedLogger.cs`
- `ModRandom.cs`
- `ModThreads.cs`

## 1.1 Persistence

Primary areas:
- `ModAPI/Persistence`

Responsibilities:
- per-mod save data registration implementation
- persistence storage and lifecycle hooks
- JSON file layout and legacy persistence-file migration

Key files:
- `SaveSystemImpl.cs`
- `ModPersistenceStore.cs`
- `IModPersistenceLogic.cs`

## 1.2 Deterministic Random

`ModAPI.Core.ModRandom` is the canonical deterministic random service. Do not create an additional random facade for save-replayable framework or mod behavior.

Rules:
- Use `ModRandom.GetStream(modId, featureId)` for gameplay features. The same save seed and scoped stream ID produce the same sequence, while draws from other named streams do not alter it.
- `ModManagerBase.Random` uses a canonical manager-type feature stream under the mod ID and participates in stream snapshot/restore.
- `ResetForSaveSeed(seed, mode)` restarts the master sequence, clears named streams, and raises `OnSeedChanged` so cached streams can be rebound.
- With `IsDeterministic == false`, a persisted save seed is reused on load and all streams restart at step zero. With `IsDeterministic == true`, the master state, step count, and named stream states are restored exactly.
- Diagnostics record initialization, stream creation, reset, snapshot, and restore boundaries; random draws are not logged individually.

## Background Work

`ModAPI.Core.ModThreads` remains the neutral background processing entry point. Its original fire-and-forget and calculated-result overloads are preserved, while overloads accepting `ModThreadOptions` return a `ModThreadHandle`.

Rules:
- Background delegates run on `ThreadPool` and must not touch Unity objects.
- Result and error continuations are delivered through `PluginRunner.Enqueue`, using the existing pending callback path until the runner is available.
- `SourceId` scopes both `WorkKey` matching and `MaxConcurrentPerSource` limits.
- `SkipIfSuperseded` permits older computation to finish but discards its main-thread continuation once newer keyed work exists.
- `CancelPreviousAndSkip` additionally requests cooperative cancellation; running work must poll `handle.IsCancellationRequested`.
- `GetDiagnostics()` exposes lifetime `Queued`, `Running`, `Completed`, `Canceled`, `Failed`, `StaleSkipped`, and `Throttled` counters plus current activity.

## 2. Actor System

Primary areas:
- `ModAPI/Actors`
- `ShelteredAPI/Actors`

Current 2.0 model:
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

## 3. Public Facades

Preferred 2.0 entry points:
- `ShelteredContent` for content registration, assets, localization, loot, recipes, and runtime item resolution
- `ShelteredSaves` and `ShelteredSaveEvents` for Sheltered save slots and save lifecycle
- `ShelteredEvents` for Sheltered game, UI, faction, and time events
- `ShelteredUI` for intended UI helpers
- `ShelteredInput` for Sheltered input integration and tuning
- `ShelteredActors` and `ShelteredCharacters` for Sheltered actor/character integration
- `ShelteredScenarios`, `ShelteredScenarioAuthoring`, and `ShelteredScenarioRuntime` for scenarios

Implementation classes, patch hosts, serializers, controllers, repositories, and manager-binding services are internal.

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
- `ShelteredContent.cs`
- `ContentResolver.cs`
- `ContentInjector.cs`
- `InventoryIntegration.cs`
- `AssetLoader.cs`

Ownership note:
- public content APIs now live in `ShelteredAPI.Content`
- `ModAPI.dll` owns only the neutral `IContentResolutionService` port; Sheltered item/content resolution is implemented by `ShelteredAPI.dll`

## 5. Settings and Persistence

Primary areas:
- `ModAPI/Spine`
- `ModAPI/Core`
- `ModAPI/Util`
- `ShelteredAPI/Core` and `ShelteredAPI/Persistence` for Sheltered save-backed compatibility helpers

Responsibilities:
- settings metadata scanning
- settings UI/controller generation
- neutral per-save mod state through `ISaveSystem`
- Sheltered save-slot facades through `ShelteredAPI.dll`

Main patterns:
- `ModManagerBase<T>`
- `ISettingsProvider`
- `ISaveSystem.RegisterModData(...)`
- `ShelteredSaves` / `ShelteredSaveEvents` when a mod intentionally works with Sheltered save slots

Related guides:
- `documentation/Spine_Settings_Guide.md`
- `documentation/SETTINGS.md`

## 6. Events

Primary areas:
- `ModAPI/Events`
- `ShelteredAPI/Events`

Responsibilities:
- `ModAPI/Events` owns only the neutral inter-mod event bus
- `ShelteredAPI/Events` owns Sheltered gameplay lifecycle events, UI lifecycle events, and deterministic scheduler triggers

Key files:
- `ShelteredAPI/Events/ShelteredEvents.cs`
- `ModAPI/Events/ModEventBus.cs`

Related guide:
- `documentation/Events_Guide.md`

## 7. Harmony and Transpilers

Primary area:
- `ModAPI/Harmony`

Responsibilities:
- Harmony bootstrap
- patch registration
- retained patch ownership and conflict diagnostic reports
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

Primary areas:
- `ModAPI/UI`
- `ShelteredAPI/UI`

Responsibilities:
- `ModAPI/UI` owns neutral Unity-level scroll/touch/input flow shims
- `ShelteredAPI/UI` owns Sheltered panel lifecycle bridging, UI factory helpers, settings UI, NGUI helpers, and debug UI internals

Representative files:
- `ModAPI/UI/UIFlowGuard.cs`
- `ModAPI/UI/ScrollInputBridge.cs`
- `ModAPI/UI/TouchInputBridge.cs`
- `ShelteredAPI/UI/Compatibility/UIUtil.cs`
- `ShelteredAPI/UI/Compatibility/UIHelper.cs`
- `ShelteredAPI/UI/Compatibility/UIPatches.cs`
- `ShelteredAPI/UI/Compatibility/Runtime/*`

## 9. Save Expansion

Primary area:
- `ShelteredAPI/Custom Saves`

Responsibilities:
- expanded save slots
- save metadata and manifests
- page navigation
- verification and diagnostics
- preview capture

Treat this as a Sheltered-owned subsystem layered under the loader rather than a neutral ModAPI helper.

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

1. Root [README documentation section](../readme.md#documentation)
2. [How to Develop a Plugin](how%20to%20develop%20a%20plugin.md)
3. [ModAPI Developer Guide](ModAPI_Developer_Guide.md)
4. [ShelteredAPI Guide](ShelteredAPI_Guide.md)
5. [API Signatures Reference](API_Signatures_Reference.md) when you need exact callable signatures
6. Task-specific guides such as events, settings, actors, content, scenarios, or transpilers
