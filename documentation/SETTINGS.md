# Settings and Persistence (v1.3 Beta.3)

The 1.3 Beta.3 line is a breaking clean API line.

## Assembly Rule

- Always reference `ModAPI.dll`.
- Reference `ShelteredAPI.dll` when your mod uses Sheltered content, saves, UI, input, events, actors, or scenarios.

## API Stability Rules

- Public facades are stable.
- Implementation classes are internal.
- Typed Sheltered escape hatches are explicit.
- Future migrations should happen behind facades.

## Compatibility Matrix

| Pattern / API | Applies To | Status |
|---------------|------------|--------|
| `ModManagerBase<T>` auto-settings | `ModAPI.dll` current | Recommended |
| `ISettingsProvider` + `SpineSettingsHelper.Scan` | `ModAPI.dll` current | Supported |
| `ISaveSystem.RegisterModData` | `ModAPI.dll` current | Recommended |
| `ShelteredSaves` / `ShelteredSaveEvents` | `ShelteredAPI.dll` | Sheltered save-slot APIs |
| Older settings accessor snippets | Older API style | Historical |

Canonical signatures: [API Signatures Reference](API_Signatures_Reference.md).

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

## 5. Sheltered Save Slots

`ctx.SaveSystem.RegisterModData(...)` is the neutral ModAPI persistence path.
Use `ShelteredSaves` and `ShelteredSaveEvents` only when you intentionally work with Sheltered save slots, descriptors, or save lifecycle.

```csharp
SaveEntry[] saves = ShelteredSaves.ListStandard(page: 0, pageSize: 20);
foreach (SaveEntry save in saves)
{
    // inspect or display save metadata
}
```
