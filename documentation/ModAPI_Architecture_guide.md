# ModAPI v2.0 Architecture Guide

This document summarizes loader/runtime architecture for maintainers and advanced integration work. The 2.0 line is a breaking clean API line.

Mod authors should use the canonical [assembly boundary](README.md#assembly-boundary-canonical) rather than treating this internal map as setup instructions. For exact public signatures, use [API Signatures Reference](API_Signatures_Reference.md).

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
2. `ModLoadOrderReader.Read(...)`
3. `DiscoverAndOrderMods(...)`
4. `AttachInspectorTools()`
5. `LoadAndInitializePlugins(...)`

## 2. Loader Initialization

`InitializeLoader(...)` is responsible for:
- resolving `GameRoot` and `ModsRoot`
- creating or reusing `ModAPI.Loader`
- ensuring `PluginRunner` exists
- resolving shared runtime assemblies from the SMM runtime folders
- applying `ModAPI` Harmony bootstrap plus patches from runtime assemblies that expose `IGameRuntimeBootstrap`
- initializing game runtime bootstraps
- wiring save/session lifecycle hooks through neutral `GameRuntime.*` registry IDs
- leaving Sheltered-specific content, save, UI, input, actor, and scenario runtime ownership to `ShelteredAPI`
- discovering optional runtime assemblies independently, so `ShelteredScenarioEditor.dll` may bootstrap only when deployed without becoming a dependency of ModAPI or ShelteredAPI

The scenario dependency direction is `ShelteredScenarioEditor -> ShelteredAPI -> ModAPI`. The editor owns interactive authoring UI and drafts. ShelteredAPI owns mod-facing scenario facades and the installed-scenario runtime/browser lane. The editor may be physically absent; when present but disabled by `ShelteredScenarioEditor.Enabled`, it must not initialize its authoring composition graph.

ShelteredAPI registers neutral runtime IDs when it is present. Neutral IDs used by `ModAPI` include:
- `GameRuntime.Actors`
- `GameRuntime.ActorRegistry`
- `GameRuntime.ActorComponents`
- `GameRuntime.ActorBindings`
- `GameRuntime.ActorAdapters`
- `GameRuntime.ActorSimulation`
- `GameRuntime.ActorEvents`
- `GameRuntime.ActorSerialization`

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

`ModThreads` runs neutral calculations on `ThreadPool` and submits result/error continuations to the same main-thread queue. New keyed options add cooperative cancellation, stale-result suppression, and per-source throttling without introducing another runtime host or dispatch route.

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

`SaveSystem` is neutral ModAPI per-mod persistence. It receives the active host slot path through `GameRuntime.SaveRuntime`; Sheltered's `SaveManager`, custom save slots, and save verification live in `ShelteredAPI.dll`.

`Actors` is the registry-first actor facade. It combines:
- registry CRUD
- component storage
- binding resolution
- adapter registration
- event subscriptions
- simulation scheduling
- serialization

`ModAPI.Actors` is host-neutral. Sheltered-specific character access,
`FamilyMember`/`NpcVisitor` escape hatches, and actor-id helpers live in
`ShelteredAPI.Actors` and `ShelteredAPI.Characters`.

## 7. `ModManagerBase`

`ModManagerBase` is the high-level base class for larger mods. It provides:
- `Context`
- `Log`
- `SaveSystem`
- deterministic `Random`
- event registry/disposal support
- automatic settings discovery and loading
- automatic persistence scanning

Sheltered save-slot APIs are exposed through `ShelteredSaves` and `ShelteredSaveEvents` in `ShelteredAPI.dll`.

`ModManagerBase<T>` adds a strongly typed `Config` surface.

## 7.1 Deterministic Random Ownership

`ModAPI.Core.ModRandom` remains the single game-neutral deterministic random service. It owns the master seed, stable named-stream derivation, stream snapshot/restore, and save-seed reset semantics.

- Use `ModRandom.GetStream(modId, featureId)` to isolate feature decisions from unrelated consumption order.
- `ModManagerBase.Random` resolves to a manager-type feature stream under `modId`; separate manager types cannot consume each other's sequence, and the stream is included in deterministic save restoration.
- Save-backed seed files are written by `ModRandomState` under the host-provided neutral save path. Sheltered slot routing remains in `ShelteredAPI`.
- A save-seed reset clears named streams and notifies listeners to obtain fresh stream instances. An exact deterministic restore retains named-stream state and notifies listeners to rebind to the restored instances.
- Lifecycle diagnostics are emitted for seed/reset/stream/snapshot events only. Per-draw logging is intentionally excluded.

## 7.2 Manager-owned runtime options

`ModAPI.Core.ManagerBooleanOptions` is the only supported runtime entry point for boolean options shown
by the desktop manager. Mods register a `ManagerBooleanOptionDefinition` and query the selected value;
they do not read or write `manager_options.json` directly.

The public definition belongs only to `ModAPI.dll`; `Manager.exe` does not export a second type with the
same fully qualified name. The desktop Manager and Unity runtime compile the same internal descriptor,
persisted contract, and merge policy from `Shared/ManagerOptions`. Their environment adapters remain
separate: Manager uses `JavaScriptSerializer`, while ModAPI uses its Unity-compatible manual JSON adapter.
Metadata refreshes preserve the current selected value, identifiers compare case-insensitively, and the
persisted file/record DTOs are deliberately internal in the 2.0 API.

## 7.3 Decompiler sidecar

The .NET 8 decompiler executable has one execution path: `Program` validates CLI input and calls
`DecompilerEngine`. `SemanticExtensions.GetILRanges` is the single syntax/IL mapping primitive currently
required by that engine. New service facades or semantic APIs should be added only with a concrete second
consumer.

## 8. Practical Guidance

- Keep constructors side-effect free.
- Put lightweight wiring in `Initialize(...)`.
- Apply patches and register runtime behavior in `Start(...)`.
- Use `RunNextFrame(...)` when scene objects may not yet exist.
- Unsubscribe/cleanup in `Shutdown()` if you implement `IModShutdown`.
- Prefer documented extension points before reaching for invasive patches.
