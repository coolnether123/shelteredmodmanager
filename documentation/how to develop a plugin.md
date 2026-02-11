# How to Develop a Plugin | Sheltered Mod Manager v1.2

This guide is for writing a mod plugin that runs under the current `IModPlugin` lifecycle.

## 1. Project Setup

Create a C# Class Library targeting `.NET Framework 3.5`.

Add references:
- `ModAPI.dll` (from SMM install)
- `0Harmony.dll` (if patching)
- `Assembly-CSharp.dll` (game managed folder)
- `UnityEngine.dll` (game managed folder)

Game managed folder examples:
- Steam: `<Sheltered>/Sheltered_Data/Managed`
- Epic: `<Sheltered>/ShelteredWindows64_EOS_Data/Managed`

## 2. Folder Layout Required by Loader

Place your mod here:

```text
Sheltered/
\- mods/
   \- MyPlugin/
      |- About/
      |  \- About.json
      |- Assemblies/
      |  \- MyPlugin.dll
      \- Config/             (optional)
```

Loader discovery requires `About/About.json`.

## 3. `About.json` Requirements

Required fields:
- `id`
- `name`
- `version`
- `description`
- `authors` (non-empty array)

Example:

```json
{
  "id": "yourname.myplugin",
  "name": "My Plugin",
  "version": "1.0.0",
  "authors": ["Your Name"],
  "description": "What this plugin does"
}
```

Optional fields supported by `ModAbout` include `entryType`, `dependsOn`, `loadBefore`, `loadAfter`, `tags`, `website`, `missingModWarning`, `debugLogging`.

## 4. Lifecycle Contract

Every plugin must implement `IModPlugin`:

```csharp
public interface IModPlugin
{
    void Initialize(IPluginContext ctx);
    void Start(IPluginContext ctx);
}
```

Runtime order is:
1. `Initialize(ctx)`
2. `Start(ctx)`

`Initialize` should wire dependencies and cache context/log.
`Start` is where you usually apply Harmony patches and begin runtime behavior.

## 5. Recommended Base Class: `ModManagerBase`

Use `ModManagerBase` if you want built-in settings and save helpers.

```csharp
using ModAPI.Core;
using UnityEngine;

public class MyMod : ModManagerBase, IModPlugin
{
    [ModToggle("Enable Boost", "Enable or disable speed boost")]
    public bool BoostActive = true;

    [ModSlider("Speed", 1f, 10f)]
    public float SpeedValue = 5f;

    public override void Initialize(IPluginContext ctx)
    {
        base.Initialize(ctx); // Important: wires settings + random stream + persistence scan
        Log.Info("MyMod initialized");
    }

    public void Start(IPluginContext ctx)
    {
        Log.Info("MyMod started");
    }
}
```

## 6. Manual Style: `IModPlugin` Directly

Use this when you want full explicit control.

```csharp
using ModAPI.Core;

public class MyPlugin : IModPlugin
{
    private IPluginContext _ctx;

    public void Initialize(IPluginContext ctx)
    {
        _ctx = ctx;
        _ctx.Log.Info("Initialize");
    }

    public void Start(IPluginContext ctx)
    {
        _ctx.Log.Info("Start");
    }
}
```

## 7. Optional Interfaces

Implement only what you need:
- `IModUpdate`: per-frame `Update()` callback from loader
- `IModShutdown`: cleanup during quit/teardown
- `IModSceneEvents`: `OnSceneLoaded/OnSceneUnloaded`
- `IModSessionEvents`: `OnSessionStarted/OnNewGame`

## 8. `IPluginContext` Quick Use

High-value members:
- `Log`: mod-prefixed logging
- `SaveSystem`: per-save mod data
- `RunNextFrame(Action)`: defer to next Unity frame
- `StartCoroutine(...)`: run coroutine from loader host
- `FindPanel(...)` and `AddComponentToPanel<T>(...)`: UI integration
- `GameRoot` / `ModsRoot`: path roots

## 9. Common Pitfalls

- Forgetting `base.Initialize(ctx)` when using `ModManagerBase`.
- Doing heavy work in constructors instead of lifecycle methods.
- Assuming scene objects exist immediately; use `RunNextFrame` or scene callbacks.
- Not implementing `IModShutdown` for event unsubscription.
