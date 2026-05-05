# Spine Settings Framework Guide (v1.3 Beta.3)

## Compatibility Matrix

| Doc Section | Applies To | Status |
|-------------|------------|--------|
| Pattern A (`ModManagerBase<T>`) | `ModAPI.dll` current | Supported |
| Pattern B (`ISettingsProvider` + `SpineSettingsHelper.Scan`) | `ModAPI.dll` current | Supported |
| `[ModConfiguration]` marker | Optional metadata only | Supported, not required for scanning |
| Old examples importing `[ModSetting]` from `ModAPI.Attributes` | Older docs/snippets | Deprecated |

Canonical signatures: [API Signatures Reference](API_Signatures_Reference.md).

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

Sliders are granular by default. `StepSize` controls the +/- button increment. To make slider dragging snap to that increment, opt in explicitly:

```csharp
[ModSetting("Spawn Rate", Min = 0f, Max = 5f, StepSize = 0.25f, SliderStepMode = SliderStepMode.Stepped)]
public float SpawnRate = 1f;
```

Numeric widgets also support display and control tuning:

```csharp
[ModSetting(
    "Pregnancy Duration",
    Min = 1f,
    Max = 14f,
    StepSize = 0.5f,
    FineStepSize = 0.25f,
    LargeStepSize = 2f,
    ValueFormat = "0.##",
    UnitSuffix = " days",
    Tooltip = "Drag, use +/- buttons, or click the value to type an exact duration.")]
public float PregnancyDurationDays = 4f;
```

Useful UI fields:
- `ValueFormat`: .NET numeric format used by the value label.
- `UnitSuffix`: text appended to displayed numeric values.
- `FineStepSize`: +/- button step. Falls back to `StepSize`, then a range-derived default.
- `LargeStepSize`: +/- button step while Shift is held.
- `ShowValueInput`: set false to hide exact numeric text entry.
- `ShowStepperButtons`: set false to hide +/- buttons.
- `TrueLabel` / `FalseLabel`: custom labels for bool toggles.
- `ActionLabel`: custom text for method/button settings.
- `Placeholder`: empty string setting placeholder.

Tooltips are shown when hovering labels and interactive controls. Numeric tooltips also include the active range and step behavior.

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

