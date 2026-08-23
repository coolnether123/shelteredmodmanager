# Spine settings UI

This reference covers Spine setting widgets and metadata. Follow [Settings and persistence](SETTINGS.md#add-settings-to-a-plugin) to register and load a settings holder.

## Namespaces

```csharp
using ModAPI.Spine;
using ModAPI.Attributes; // ModConfiguration only
```

`ModSettingAttribute` and `ModSettingPresetAttribute` are in `ModAPI.Spine`. `ModConfigurationAttribute` is optional metadata; the loader does not require it to discover an attributed settings holder.

## Visibility modes

`ModSettingAttribute.Mode` defaults to `SettingMode.Advanced`.

| Mode | Simple view | Advanced view |
| --- | --- | --- |
| `Simple` | Visible | Visible |
| `Advanced` | Hidden | Visible |
| `Both` | Visible | Visible |

If a setting is missing, clear search and category filters before changing its mode.

## Widget selection

Spine infers a widget from the member type unless `ModSettingAttribute.Type` selects one. `SettingType` supports Boolean, integer, floating-point, string, enum, color, button, header, spacer, numeric input, keybind, and choice widgets.

Common metadata is:

| Field | Effect |
| --- | --- |
| `LabelKey`, `TooltipKey` | Resolve translated text while `Label` and `Tooltip` remain fallbacks |
| `Category`, `SortOrder` | Group and order rows |
| `DependsOnId` | Disable a row until another Boolean setting is true |
| `ControlsChildVisibility` | Hide dependent rows while the controlling Boolean is false |
| `RequiresRestart` | Show the restart requirement |
| `TrueLabel`, `FalseLabel` | Replace the default `ON` and `OFF` text |
| `ActionLabel` | Set button text for a method setting |
| `Placeholder` | Set empty string input text |

## Numeric controls

```csharp
[ModSetting(
    "Pregnancy duration",
    Min = 1f,
    Max = 14f,
    StepSize = 0.5f,
    FineStepSize = 0.25f,
    LargeStepSize = 2f,
    ValueFormat = "0.##",
    UnitSuffix = " days")]
public float PregnancyDurationDays = 4f;
```

Slider dragging is granular by default. Set `SliderStepMode = SliderStepMode.Stepped` to snap dragging to `StepSize`.

- `FineStepSize` controls the normal step buttons.
- `LargeStepSize` applies while Shift is held.
- `ShowValueInput` controls direct numeric text entry.
- `ShowStepperButtons` controls the increment and decrement buttons.

## Callbacks and choices

`OnChanged`, `VisibilityMethod`, `ValidateMethod`, and `OptionsSource` contain member names on the settings object. Keep those methods and properties when trimming unused code because the scanner resolves them by reflection.

```csharp
[ModSetting(
    "Alert sound",
    Type = SettingType.Choice,
    OptionsSource = "GetAlertSounds",
    OnChanged = "OnAlertSoundChanged")]
public string AlertSound = "Bell";
```

Use `ModSettingPresetAttribute` to declare named values:

```csharp
[ModSetting("Enemy health")]
[ModSettingPreset("Easy", 50)]
[ModSettingPreset("Normal", 100)]
[ModSettingPreset("Hard", 250)]
public int EnemyHealth = 100;
```

## Per-save settings

Set `Scope = SettingsScope.PerSave` for a value that belongs to the active save. `CarryOverToNewGamePlus` and `NewGamePlusMerge` control New Game Plus migration.

Use `IPluginContext.SaveSystem` instead when the data is runtime state rather than a player-facing setting.

## Raw providers

Implement `ISettingsProvider` only when the plugin owns scanning, defaults, loading, and saving. The current loader registers a raw provider but does not create or load a `SettingsController` for it. A raw provider must not rely on `OnSettingsLoaded()` being called automatically before `Start(...)`.

## Diagnose scanning

Search `SMM/mod_manager.log` for:

```text
Scanning <TypeName> for settings...
Scan complete for <TypeName>. Found <N> definitions.
OnChanged method '<Name>' not found on type <Type>
VisibilityMethod '<Name>' not found on <Type>
ValidateMethod '<Name>' not found on <Type>
```
