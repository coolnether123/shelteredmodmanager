# Settings and Persistence (Current v1.3 Line)

## Compatibility Matrix

| Pattern / API | Applies To | Status |
|---------------|------------|--------|
| `ModManagerBase<T>` auto-settings | `ModAPI.dll` current | Recommended |
| `ISettingsProvider` + `SpineSettingsHelper.Scan` | `ModAPI.dll` current | Supported |
| `ISaveSystem.RegisterModData` | `ModAPI.dll` current | Recommended |
| `ctx.SaveData/ctx.LoadData` extensions | `ModAPI.Util` current | Supported |
| Legacy `ctx.Settings.GetInt/SetInt/SaveUser` style | Older API style | Deprecated |

Canonical signatures: `documentation/API_Signatures_Reference.md`.

## 1. Settings Pattern A: `ModManagerBase<T>` (Recommended)

```csharp
using ModAPI.Core;
using ModAPI.Spine;

public class MySettings
{
    [ModSetting("Enable Feature")]
    public bool Enabled = true;
}

public class MyMod : ModManagerBase<MySettings>, IModPlugin
{
    public override void Initialize(IPluginContext ctx)
    {
        base.Initialize(ctx); // auto controller + load
    }

    public void Start(IPluginContext ctx) { }
}
```

## 2. Settings Pattern B: Manual `ISettingsProvider`

```csharp
using System.Collections.Generic;
using ModAPI.Core;
using ModAPI.Spine;

public class MyMod : IModPlugin, ISettingsProvider
{
    private readonly MySettings _settings = new MySettings();
    private List<SettingDefinition> _defs;

    public void Initialize(IPluginContext ctx)
    {
        _defs = SpineSettingsHelper.Scan(_settings);
    }

    public void Start(IPluginContext ctx) { }

    public IEnumerable<SettingDefinition> GetSettings() => _defs;
    public object GetSettingsObject() => _settings;
    public void OnSettingsLoaded() { }
    public void ResetToDefaults() => _settings.Enabled = true;
}

public class MySettings
{
    [ModSetting("Enable Feature")]
    public bool Enabled = true;
}
```

When to use Pattern B:
- You keep settings in a separate object graph.
- You need custom save/reset/load behavior beyond base-controller defaults.
- You want explicit control over scanning and definition caching.

## 3. Runtime Toggles (Global ModAPI Flags)

```csharp
ModPrefs.DebugTranspilers = true;
ModPrefs.TranspilerSafeMode = true;
ModPrefs.Save();
```

## 4. Per-Save Typed Data (`ISaveSystem`)

```csharp
public class MySaveState
{
    public int Visits;
}

private readonly MySaveState _state = new MySaveState();

public void Initialize(IPluginContext ctx)
{
    ctx.SaveSystem.RegisterModData("state", _state);
}
```

## 5. SaveData/LoadData (`PersistentDataAPI` Extensions)

```csharp
ctx.SaveData("custom_blob", myData);
if (ctx.LoadData("custom_blob", out MyData loaded))
{
    myData = loaded;
}
```
