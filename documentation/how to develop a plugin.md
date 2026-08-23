# Develop a plugin

This guide builds the smallest plugin that uses the current `IModPlugin` lifecycle.

## Create the project

Create a C# class library that targets .NET Framework 3.5. Add references to:

- `ModAPI.dll`;
- `UnityEngine.dll` from the game's managed folder.

Load this minimal project before adding more references. Use the [assembly boundary](README.md#assembly-boundary-canonical) when the mod needs ShelteredAPI, vanilla game types, or Harmony.

The managed folder is usually:

- Steam or GOG: `<Sheltered>/Sheltered_Data/Managed`
- Epic: `<Sheltered>/ShelteredWindows64_EOS_Data/Managed`

## Package the mod

The loader expects:

```text
Sheltered/
  mods/
    MyPlugin/
      About/
        About.json
      Assemblies/
        MyPlugin.dll
      Config/              optional
```

`About/About.json` requires a non-empty `id`, `name`, `version`, `description`, and `authors` array:

```json
{
  "id": "yourname.myplugin",
  "name": "My Plugin",
  "version": "1.0.0",
  "authors": ["Your Name"],
  "description": "What this plugin does",
  "requiredModApiVersion": "2.0.0.0"
}
```

Add `requiredShelteredApiVersion` when the mod references `ShelteredAPI.dll`. `dependsOn`, `loadBefore`, and `loadAfter` name other mods, not the API assemblies supplied by SMM.

`entryType` is a parsed legacy field, but the current loader does not use it to select a plugin. The loader activates every concrete `IModPlugin` type found in the mod's assemblies.

## Implement the lifecycle

```csharp
using ModAPI.Core;

public sealed class MyPlugin : IModPlugin, IModShutdown
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

    public void Shutdown()
    {
        _ctx.Log.Info("Shutdown");
    }
}
```

The loader calls `Initialize(...)` and then `Start(...)`.

- Use `Initialize(...)` to cache the context and register neutral mod data.
- Use `Start(...)` to subscribe runtime behavior, register content, and apply patches.
- Use `Shutdown()` to unsubscribe events, remove owned patches, and release resources.

Keep constructors free of runtime work. Scene objects may not exist during startup. Use `RunNextFrame(...)` or `IModSceneEvents` when code needs a loaded scene.

## Use the plugin context

Common members are:

| Member | Use |
| --- | --- |
| `Log` | Mod-scoped logging |
| `SaveSystem` | Ordinary per-save mod data |
| `Actors` | Neutral actor registry and components |
| `RunNextFrame(...)` | Defer work to the next Unity frame |
| `StartCoroutine(...)` | Run a coroutine through the loader host |
| `FindPanel(...)`, `AddComponentToPanel<T>(...)` | Focused Unity panel integration |
| `GameRoot`, `ModsRoot` | Resolved installation paths |

Use [Settings and persistence](SETTINGS.md) for configuration and save data. Use the [ShelteredAPI guide](ShelteredAPI_Guide.md) for content, Sheltered saves, events, UI, input, characters, maps, or scenarios.

## Optional lifecycle interfaces

Implement only the callbacks the mod needs:

| Interface | Callback |
| --- | --- |
| `IModUpdate` | Per-frame `Update()` |
| `IModShutdown` | Application-quit cleanup |
| `IModSceneEvents` | Scene load and unload |
| `IModSessionEvents` | Session start and new game |

The current loader calls `IModShutdown.Shutdown()` at application quit, not on every loader teardown.

## `ModManagerBase` status

Do not use `ModManagerBase` as a new-plugin template yet. It derives from `MonoBehaviour`, while the current loader creates plugin types with `Activator.CreateInstance(...)` instead of attaching them to a `GameObject`. Its Unity lifecycle and `OnDestroy()` cleanup need an in-game activation test or a loader fix before the base class can be recommended.

Use `IModPlugin` directly and register settings, persistence, patches, and cleanup explicitly.

## Check the first load

Confirm that:

1. The manager discovers the mod without a manifest warning.
2. The log shows both lifecycle messages.
3. No work runs from the constructor.
4. Event subscriptions and Harmony patches have matching shutdown cleanup.
5. A second game session in the same process does not duplicate handlers or content.
