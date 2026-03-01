# Spine Settings Framework Guide (Current v1.3 Line)

## Compatibility Matrix

| Doc Section | Applies To | Status |
|-------------|------------|--------|
| Pattern A (`ModManagerBase<T>`) | `ModAPI.dll` current | Supported |
| Pattern B (`ISettingsProvider` + `SpineSettingsHelper.Scan`) | `ModAPI.dll` current | Supported |
| `[ModConfiguration]` marker | Optional metadata only | Supported, not required for scanning |
| Old examples importing `[ModSetting]` from `ModAPI.Attributes` | Older docs/snippets | Deprecated |

Canonical signatures: `documentation/API_Signatures_Reference.md`.

## 1. Canonical Namespaces and Attributes

Use this import pattern in new mods:

```csharp
using ModAPI.Core;
using ModAPI.Spine;       // [ModSetting], [ModSettingPreset], SettingMode, SpineSettingsHelper
using ModAPI.Attributes;  // [ModConfiguration] (optional marker)
```

Notes:
- `[ModSetting]` and `[ModSettingPreset]` are defined in `ModAPI.Spine`.
- `[ModConfiguration]` is defined in `ModAPI.Attributes` and is optional for current scanning flow.

## 2. Two Supported Settings Patterns

### Pattern A: `ModManagerBase<T>` (Auto Settings, Recommended)

Use this when you want minimal boilerplate and typed `Config`.

```csharp
using ModAPI.Core;
using ModAPI.Spine;

public class MySettings
{
    [ModSetting("Enable Feature", Mode = SettingMode.Simple)]
    public bool Enabled = true;

    [ModSetting("Multiplier", Min = 0.5f, Max = 3f, StepSize = 0.1f)]
    public float Multiplier = 1f;
}

public class MyMod : ModManagerBase<MySettings>, IModPlugin
{
    public override void Initialize(IPluginContext ctx)
    {
        base.Initialize(ctx); // Creates SettingsController + loads from disk
        Log.Info("Multiplier = " + Config.Multiplier);
    }

    public void Start(IPluginContext ctx) { }
}
```

### Pattern B: `ISettingsProvider` + `SpineSettingsHelper.Scan` (Manual Control)

Use this when you need full control over settings ownership, scanning, or save semantics.

```csharp
using System.Collections.Generic;
using ModAPI.Core;
using ModAPI.Spine;

public class MyMod : IModPlugin, ISettingsProvider
{
    private IPluginContext _ctx;
    private readonly MySettings _settings = new MySettings();
    private List<SettingDefinition> _defs;

    public void Initialize(IPluginContext ctx)
    {
        _ctx = ctx;
        _defs = SpineSettingsHelper.Scan(_settings);
    }

    public void Start(IPluginContext ctx) { }

    public IEnumerable<SettingDefinition> GetSettings() => _defs;
    public object GetSettingsObject() => _settings;
    public void OnSettingsLoaded() { }
    public void ResetToDefaults() => _settings.Reset();
}

public class MySettings
{
    [ModSetting("Enable Feature")]
    public bool Enabled = true;

    public void Reset()
    {
        Enabled = true;
    }
}
```

## 3. SettingMode Visibility Defaults (Important)

`ModSettingAttribute.Mode` defaults to `SettingMode.Advanced`.

Behavior:
- `Mode = Advanced`: visible in Advanced view only.
- `Mode = Simple`: visible in both Simple and Advanced views.
- `Mode = Both`: visible in both Simple and Advanced views.

If a setting exists but is not visible:
1. Check `Mode` value.
2. Clear active search/category filters in the Mod Settings UI.
3. Confirm your plugin is exposing a provider (`ModManagerBase` auto or `ISettingsProvider` manual).
4. Confirm scanner logs exist:
   - `Scanning <TypeName> for settings...`
   - `Scan complete for <TypeName>. Found <N> definitions.`
5. Check scan errors such as:
   - `OnChanged method '<Name>' not found on type <Type>`
   - `VisibilityMethod '<Name>' not found on <Type>`
   - `ValidateMethod '<Name>' not found on <Type>`

## 4. Common Spine Features

```csharp
[ModSetting("Header", Type = SettingType.Header, Category = "General")]
public string Header;

[ModSetting("Danger Mode", DependsOnId = "Enabled")]
public bool DangerMode = false;

[ModSetting("Reset Cache")]
public void ResetCacheButton()
{
    MMLog.WriteInfo("Cache reset");
}

[ModSetting("Enemy HP")]
[ModSettingPreset("Easy", 50)]
[ModSettingPreset("Normal", 100)]
[ModSettingPreset("Hard", 250)]
public int EnemyHp = 100;
```

