# Spine Settings Framework Guide (v1.2.0)

**Spine** is the high-level mod settings framework for Sheltered ModAPI. It allows modders to create rich, interactive, and persistent configuration menus with almost zero UI code. It utilizes C# Attributes to automatically generate a professional-grade UI in the game's Mod Manager panel.

---

## 1. Quick Start: Zero-Boilerplate

To use Spine, define a class to hold your settings data, mark it with `[ModConfiguration]`, and have your main plugin inherit from `ModManagerBase<T>`.

### 1.1 The Settings Class
```csharp
using ModAPI.Attributes;
using ModAPI.Core;
using UnityEngine;

[ModConfiguration]
public class MyModSettings
{
    [ModSetting("Enable Super Speed", Tooltip = "Makes your characters move 2x faster.")]
    public bool SuperSpeed = false;

    [ModSetting("Atmosphere Color")]
    public Color SkyColor = Color.cyan;

    [ModSetting("Spawn Rate", Min = 0.1f, Max = 5.0f, StepSize = 0.1f)]
    public float SpawnMultiplier = 1.0f;
    
    [ModSetting("Difficulty Mode")]
    public MyEnum Difficulty = MyEnum.Normal;
}
```

### 1.2 The Plugin Class
```csharp
public class MyMod : ModManagerBase<MyModSettings>
{
    public override void Initialize(IPluginContext ctx)
    {
        base.Initialize(ctx); // Settings are auto-loaded here!
        
        Log.Info($"Spawn Rate initialized as: {Config.SpawnMultiplier}");
    }
}
```

---

## 2. UI Features & Configuration

### 2.1 View Modes (Simple vs. Advanced)
Spine features a dual-mode UI to avoid overwhelming users.
- **Advanced Mode** (Default): Shows all settings.
- **Simple Mode**: Only shows settings explicitly marked for it.

```csharp
[ModSetting("Simple Switch", Mode = SettingMode.Simple)]
public bool BasicOption = true;

[ModSetting("Elite Tweak", Mode = SettingMode.Advanced)]
public float InternalMultiplier = 0.045f;
```

### 2.2 Layout & Categories
The UI automatically organizes settings into a **2-column grid**. Use categories to group related items under headers.

```csharp
[ModSetting("Shadow Quality", Category = "Graphics")]
public int Shadows = 2;

[ModSetting("Music Volume", Category = "Audio")]
public float Volume = 0.5f;

[ModSetting("Master Header", Type = SettingType.Header)]
public string MyHeader; // Value is ignored for headers
```

### 2.3 Action Buttons (Methods)
You can create "Execute" buttons by marking a method with `[ModSetting]`.

```csharp
[ModSetting("Reset Shelter", Tooltip = "Immediately cleans house.")]
public void DoReset()
{
    MMLog.WriteInfo("Reset executed!");
}
```

---

## 3. Custom Logic & Validation

### 3.1 Live Validation
Use `ValidateMethod` to reject user input dynamically.

```csharp
[ModSetting("Survivor Name", ValidateMethod = "CheckName")]
public string Name = "Stan";

public bool CheckName(object newVal) 
{
    return (newVal as string).Length > 2;
}
```

### 3.2 Dynamic Choices
Create dropdown cycles from dynamic data.

```csharp
[ModSetting("Spawn Location", Type = SettingType.Choice, OptionsSource = "GetLocs")]
public string Loc = "Shelter";

public IEnumerable<string> GetLocs() => new [] { "Shelter", "Bunker", "House" };
```

---

## 4. Setting Presets & Difficuty

Spine supports a global **Preset Bar**. If the user clicks "EASY", all settings with an "Easy" preset defined will update instantly. Any manual change by the user switches the UI state to **"CUSTOM"**.

```csharp
[ModSetting("Enemy Health")]
[ModSettingPreset("Easy", 50)]
[ModSettingPreset("Normal", 100)]
[ModSettingPreset("Hard", 250)]
public int HP = 100;
```

---

## 5. Persistence & Safety

### 5.1 Storage
Settings are stored in `YourMod/Config/spine_settings.json`. 
- **Auto-Load**: Occurs during `base.Initialize(ctx)`.
- **Auto-Save**: Occurs when clicking "SAVE & CLOSE" in the UI.

### 5.2 Performance & Threading
- **Main Thread Only**: UI code runs on the Unity main thread.
- **Reflective Access**: Spine uses optimized reflection to read/write your settings object.
- **Fast Exit Protection**: ModAPI intercepts game shutdown to ensure your JSON is written to disk before the app terminates.

---

## 6. Type Mapping Table

| C# Type | Spine Widget | Notes |
|---------|--------------|-------|
| `bool` | Toggle Switch | ON/OFF state |
| `int` | Int Slider | Snaps to integers |
| `float` | Float Slider | Precision based on `StepSize` |
| `string` | Text Box | Validated on 'OK' or Enter |
| `Enum` | Cycle Button | Cycles all enum values |
| `Color` | Label | (Currently Read-Only) |
| `void Method()` | Button | Triggered on click |

---

## 7. Advanced: Responsive Parent/Child
Settings can react to the state of other settings.

- `DependsOnId`: Grays out the setting if the parent is False.
- `ControlsChildVisibility`: Completely hides children if parent is False.

```csharp
[ModSetting("Use Weather", ControlsChildVisibility = true)]
public bool WeatherEnabled = true;

[ModSetting("Rain Intensity", DependsOnId = "WeatherEnabled")]
public float Rain = 0.5f;
```
