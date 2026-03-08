# ModAPI v1.3 Architecture Guide

This document summarizes the current loader/runtime architecture.

For exact signatures, use `documentation/API_Signatures_Reference.md`.

## Compatibility Matrix

| Scope | Applies To | Status |
|-------|------------|--------|
| Loader flow and lifecycle sequencing | Current codebase | Supported |
| Runtime host responsibilities | Current codebase | Supported |
| Public interface details | See signature reference | Prefer signature reference |

## 1. Startup Pipeline

Entry path:
- Doorstop/bootstrap calls `PluginManager.getInstance().loadAssemblies(...)`.

High-level flow:
1. `InitializeLoader(...)`
2. `ReadLoadOrderFromFile(...)`
3. `DiscoverAndOrderMods(...)`
4. `AttachInspectorTools()`
5. `LoadAndInitializePlugins(...)`

## 2. Loader Initialization

`InitializeLoader(...)` is responsible for:
- resolving `GameRoot` and `ModsRoot`
- creating or reusing `ModAPI.Loader`
- ensuring `PluginRunner` exists
- applying Harmony bootstrap and save-protection patches
- wiring save/session lifecycle hooks
- registering built-in APIs exposed by the current runtime

That last step includes actor API registration when `ShelteredAPI` is present:
- `ShelteredAPI.Actors`
- `ShelteredAPI.ActorRegistry`
- `ShelteredAPI.ActorComponents`
- `ShelteredAPI.ActorBindings`
- `ShelteredAPI.ActorAdapters`
- `ShelteredAPI.ActorSimulation`
- `ShelteredAPI.ActorEvents`
- `ShelteredAPI.ActorSerialization`

## 3. Discovery and Load Order

Discovery is driven by `ModDiscovery.DiscoverAllMods()`:
- scans `<GameRoot>/mods/*`
- skips reserved folders such as `disabled` and `ModAPI`
- requires `About/About.json`
- normalizes mod IDs to lowercase for matching

Load order is driven by `mods/loadorder.json`:
- missing file means all discovered mods are enabled
- unknown IDs are ignored
- duplicates are removed case-insensitively

## 4. Plugin Instantiation

For each enabled mod:
- all DLLs under `Assemblies/` are loaded via `Assembly.Load(byte[])`
- the mod is registered with `ModRegistry`
- concrete `IModPlugin` types are instantiated

For each plugin instance:
1. a `Mod-[ModId]` GameObject is created under the loader root
2. `PluginContextImpl` is built
3. optional interfaces are registered
4. `Initialize(context)` runs
5. `Start(context)` runs

Optional interfaces currently recognized:
- `IModUpdate`
- `IModShutdown`
- `IModSceneEvents`
- `IModSessionEvents`

## 5. Runtime Host

`PluginRunner` is the main runtime host. It is responsible for:
- draining the main-thread queue
- fanout of per-frame `IModUpdate.Update()`
- scene lifecycle bridging
- quit-boundary shutdown

Runtime tooling shortcuts:
- `F9`: Runtime Inspector
- `F10`: Runtime IL Inspector
- `F11`: UI Debug Inspector
- `F12`: Runtime Debugger UI

## 6. `IPluginContext`

Per-plugin context exposes:
- `LoaderRoot`
- `PluginRoot`
- `Mod`
- `Settings`
- `Log`
- `Game`
- `Actors`
- `SaveSystem`
- `GameRoot`
- `ModsRoot`
- `IsModernUnity`
- `RunNextFrame(...)`
- `StartCoroutine(...)`
- `FindPanel(...)`
- `AddComponentToPanel<T>(...)`

`Actors` is the registry-first actor facade. It combines:
- registry CRUD
- component storage
- binding resolution
- adapter registration
- event subscriptions
- simulation scheduling
- serialization

## 7. `ModManagerBase`

`ModManagerBase` is the high-level base class for larger mods. It provides:
- `Context`
- `Log`
- `SaveSystem`
- deterministic `Random`
- event registry/disposal support
- automatic settings discovery and loading
- automatic persistence scanning

`ModManagerBase<T>` adds a strongly typed `Config` surface.

## 8. Practical Guidance

- Keep constructors side-effect free.
- Put lightweight wiring in `Initialize(...)`.
- Apply patches and register runtime behavior in `Start(...)`.
- Use `RunNextFrame(...)` when scene objects may not yet exist.
- Unsubscribe/cleanup in `Shutdown()` if you implement `IModShutdown`.
- Prefer documented extension points before reaching for invasive patches.
