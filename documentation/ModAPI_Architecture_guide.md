# ModAPI v1.2 Architecture Guide

This document describes how the loader actually behaves at runtime based on the current `ModAPI/Core` implementation.

For exact interface/method signatures referenced by this architecture doc, use `documentation/API_Signatures_Reference.md`.

## 1. Startup Pipeline

Entry path:
- Doorstop/bootstrap calls `PluginManager.getInstance().loadAssemblies(...)`.

Inside `loadAssemblies(...)`:
1. `InitializeLoader(...)`
2. `ReadLoadOrderFromFile(...)`
3. `DiscoverAndOrderMods(...)`
4. `AttachInspectorTools()`
5. `LoadAndInitializePlugins(...)`

## 2. Loader Initialization Details

`InitializeLoader(...)` does the following:
- Registers an `AssemblyResolve` bridge so plugins loaded from bytes can resolve `ModAPI` to the already-loaded assembly.
- Resolves game/mod roots:
  - `GameRoot = Directory.GetParent(Application.dataPath)`
  - `ModsRoot = Path.Combine(GameRoot, "mods")`
- Creates or reuses `ModAPI.Loader` and marks it `DontDestroyOnLoad`.
- Ensures `PluginRunner` exists and attaches manager reference.
- Applies Harmony bootstrap and save protection patches.
- Wires session/save events:
  - `ModAPI.Saves.Events.OnAfterLoad -> ModRandomState.Load`
  - `ModAPI.Saves.Events.OnBeforeSave -> ModRandomState.Save`
  - `GameEvents.OnSessionStarted -> PluginManager.OnSessionStarted`
  - `GameEvents.OnNewGame -> PluginManager.OnNewGame`

## 3. Mod Discovery and Load Order

Discovery source: `ModDiscovery.DiscoverAllMods()`
- Scans `<GameRoot>/mods/*`
- Skips reserved folders: `disabled`, `ModAPI`
- Requires `About/About.json`
- Required About fields:
  - `id`, `name`, `version`, `description`, `authors[]`
- Normalizes `id` to lowercase for matching.

Load order source: `mods/loadorder.json`
- Missing file: all discovered mods are enabled.
- Present but empty `order`: no mods enabled.
- Unknown IDs in `order`: ignored.
- Duplicates: deduped case-insensitively.

## 4. Assembly Loading and Plugin Instantiation

For each ordered mod:
- Loads all DLLs under `Assemblies/` with `Assembly.Load(byte[])` (avoids locking files during development).
- Registers mod and assembly with `ModRegistry`.
- Scans each assembly type and instantiates concrete classes implementing `IModPlugin`.

For each plugin instance:
- Creates `GameObject` named `Mod-[ModId]` under loader root.
- Builds `IPluginContext` via `PluginContextImpl`.
- Registers optional interfaces if implemented:
  - `IModUpdate`
  - `IModShutdown`
  - `IModSceneEvents`
  - `IModSessionEvents`
- Calls lifecycle in order:
  1. `Initialize(context)`
  2. `Start(context)`

## 5. Runtime Host (`PluginRunner`)

`PluginRunner` responsibilities:
- Main-thread queue via `Enqueue(...)` + drain in `Update()`.
- Per-frame update fanout: `PluginManager.OnUnityUpdate()`.
- Scene event bridge:
  - Modern path: reflection hook into `SceneManager.sceneLoaded/sceneUnloaded`
  - Fallback path: `OnLevelWasLoaded(int)` for legacy runtime
- Quit boundary handling:
  - `IsQuitting = true`
  - Calls `PluginManager.ShutdownAll()`

Runtime tool toggles:
- `RuntimeInspector`: `F9`
- `RuntimeILInspector`: `F10`
- `UIDebugInspector`: `F11`
- `RuntimeDebuggerUI`: `F12`

## 6. `IPluginContext` Services

Per-plugin context exposes:
- `Log`: mod-prefixed logger (`PrefixedLogger`)
- `SaveSystem`: per-mod data persistence API
- `Game`: helper access to game state wrappers
- `RunNextFrame(Action)`: schedule onto next Unity frame
- `StartCoroutine(IEnumerator)`: coroutine host on loader runner
- `FindPanel(...)` and `AddComponentToPanel<T>(...)`
- Paths: `GameRoot`, `ModsRoot`
- Runtime mode: `IsModernUnity`

## 7. `ModManagerBase` Behavior

`ModManagerBase` is a convenience base class for plugin authors:
- Stores `Context`, exposes `Log`, `SaveSystem`, deterministic `Random` stream.
- If settings attributes exist on the plugin class, it auto-creates `SettingsController`, loads config, and wires session reload.
- Supports manual settings creation through `CreateSettings<T>()`.
- Supports persistence registration via `RegisterPersistentData<T>()`.

## 8. Practical Guidance for Mod Authors

- Put lightweight setup in `Initialize`; patch application is usually safe in `Start`.
- If you subscribe to events, implement `IModShutdown` and unsubscribe in `Shutdown`.
- Use `RunNextFrame(...)` when scene objects may not yet exist.
- Do not assume all discovered mods load; load order filtering may exclude them.
- Keep plugin constructors side-effect free; rely on context lifecycle.
- If you use transpilers, expect safety policy defaults to favor runtime stability over permissive patching (`TranspilerSafeMode`, cooperative strict build, quarantine-on-failure).
