# Spine Settings API Guide

**Spine** is the high-level mod settings framework for Sheltered. It allows modders to create rich, interactive, and persistent configuration menus with almost zero UI code. It utilizes C# Attributes to automatically generate a professional-grade UI in the game's Mod Manager panel.

---

## 1. Basic Setup (v1.2 "Zero-Boilerplate")

To use Spine, define a **single** class to hold your settings data, mark it with `[ModConfiguration]`, and have your main plugin inherit from `ModManagerBase<T>`.

### The Settings Class
This class holds your configuration.

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

    // v1.2: Use 'Min' and 'Max' shorthand properties
    [ModSetting("Spawn Rate", Min = 0.1f, Max = 5.0f)]
    public float SpawnMultiplier = 1.0f;
}
```

### The Plugin Class
Your plugin automatically handles saving, loading, and UI generation.

```csharp
public class MyMod : ModManagerBase<MyModSettings>
{
    public override void Initialize(IPluginContext ctx)
    {
        base.Initialize(ctx); // Settings are auto-loaded!
        
        Log.Info($"Spawn Rate is: {Config.SpawnMultiplier}");
    }
}
```

### Type Mapping
Spine automatically maps C# types to UI widgets:
- `bool` -> Toggle Switch
- `int` -> Number Slider (snaps to whole numbers)
- `float` -> Decimal Slider
- `string` -> Text Input Field (includes an 'OK' button for mouse confirmation)
- `Color` -> Color Swatch (currently display only)
- `Enum` -> Cycle Button


### Slider Precision & Inputs
By default, sliders check the range to determine step size. You can override this with the `StepSize` property:
```csharp
[ModSetting("Precise Value", MinValue=0, MaxValue=100, StepSize=5)] // Snaps to 0, 5, 10...
```

---

## 2. Integrating with Your Plugin

### The Recommended Way (ModManagerBase&lt;T&gt;)

As shown above, inheriting from `ModManagerBase<T>` automates everything.
*   **Initialization:** `base.Initialize(ctx)` creates the instance, loads JSON, and binds it.
*   **Access:** Use `this.Config` to access settings.
*   **Validation:** It automatically calls `Validate()` (Phase 1) after load and `ValidateRuntime()` (Phase 2) on session start.

### The Manual Way (Legacy)

If you cannot inherit from `ModManagerBase`, you must verify:
1. Your class implements `ISettingsProvider`.
2. You instantiate your settings object.
3. You scan and return definitions.

```csharp
public class MyPlugin : IModPlugin, ISettingsProvider
{
    public static MyModSettings Settings;
    private IPluginContext _ctx;

    public void Initialize(IPluginContext context)
    {
        _ctx = context;
        Settings = new MyModSettings();
        
        // Manual loading
        new AutoSettingsProvider(typeof(MyModSettings), Settings, context).LoadInto(Settings);
    }

    public IEnumerable<SettingDefinition> GetSettings() 
    {
        return SpineSettingsHelper.Scan(Settings);
    }

    public object GetSettingsObject() => Settings;
    
    public void OnSettingsLoaded() { }
    
    public void ResetToDefaults() 
    {
         var defaults = new MyModSettings();
         // Deep copy logic...
    }

    public void Start(IPluginContext context) { }
}
```

**Lifecycle Order:**
1. `Initialize()` called -> You create default `Settings` object.
2. ModLoader checks for `json` file -> Overwrites values in your object.
3. `OnSettingsLoaded()` called (optional).
4. `Start()` called.

---

## 3. UI Organization

### Categories
Grouping settings is essential for large mods. Use the `Category` property:

```csharp
[ModSetting("Resolution", Category = "Graphics")]
public int Resolution = 1080;

[ModSetting("Volume", Category = "Audio")]
public float Volume = 0.8f;
```

### Ordering
Control the vertical position of settings using `SortOrder` (lower numbers appear first):

```csharp
[ModSetting("Master Switch", SortOrder = -100)]
public bool GlobalEnable = true;
```

### Simple vs Advanced View (Planned)
Spine includes infrastructure for two view modes to prevent overwhelming users with advanced "under-the-hood" tweaks. **Note: While the attributes exist, the UI implementation is currently unpolished and should be considered experimental.**

- By default, all settings are **Advanced**.
- Mark a setting with `Mode = SettingMode.Simple` (or `SettingMode.Both`) to prepare it for the simplified view.

```csharp
[ModSetting("Life Span", Mode = SettingMode.Simple)]
public int AgeLimit = 100;
```

### Search & Filtering
The Spine UI includes a built-in search bar. It filters settings by their `Label` or `Id` in real-time. Categories are automatically hidden if no settings within them match the search query.

---

## 4. Advanced Logic & Hooks

Spine supports dynamic behavior through reflection-based hooks. You provide the name of a method in your settings class to handle the logic.

### Validation (`ValidateMethod`)
Reject invalid user input. If validation fails, the UI reverts the change.

```csharp
[ModSetting("Username", ValidateMethod = "CheckName")]
public string Username = "Survivor";

public bool CheckName(object newVal) 
{
    string s = newVal as string;
    return !string.IsNullOrEmpty(s) && s.Length >= 3;
}
```

### Dynamic Choice Lists (`OptionsSource`)
Create dropdowns where the options are generated at runtime.

```csharp
[ModSetting("Teleport Location", Type = SettingType.Choice, OptionsSource = "GetLocations")]
public string TargetLoc = "Shelter";

public IEnumerable<string> GetLocations() 
{
    // You could pull these from game data!
    return new List<string> { "Shelter", "Hidden Bunker", "Radio Tower" };
}
```

### Action Buttons
You can mark **Methods** with `[ModSetting]` to create clickable buttons in the UI.

```csharp
[ModSetting("Clean House", Tooltip = "Immediately removes all dirt from the shelter.")]
public void ClearDirt() 
{
    // Logic to clear dirt...
    MMLog.WriteInfo("House cleaned via Settings!");
}
```

---

## 5. Dependencies (Parent/Child)

You can make settings appear or become enabled only when another switch is on.

- `DependsOnId`: Grays out the setting if the parent is False.
- `ControlsChildVisibility`: Completely hides children in the UI if False.

```csharp
[ModSetting("Enable Visual Effects", ControlsChildVisibility = true)]
public bool UseFX = true;

[ModSetting("Particles Amount", DependsOnId = "UseFX")]
public int ParticleCount = 50;
```

---

## 6. Dual-Phase Validation (v1.2)

To solve "Game Start vs Mod Load" timing issues, settings can implement `IModSettingsValidator`.

```csharp
public class MySettings : IModSettingsValidator 
{
    // Phase 1: Immediate (Safe for math/null checks)
    public void Validate() 
    {
        Speed = Mathf.Clamp(Speed, 0, 100);
    }

    // Phase 2: Runtime (Safe for Game Managers)
    public void ValidateRuntime() 
    {
        if (GameModeManager.Instance.IsHardcore) Speed = 10;
    }
}
```

---

## 7. Multiplayer Sync Contracts (v1.2)

The `SyncMode` property defines how settings behave in future multiplayer scenarios.

> **Note:** These hooks are currently for **future planning/architecture only** and are not fully tested for any multiplayer environment. They are provided now to prepare settings contracts in advance.

- **LocalOnly** (Default): Client preference (e.g. Volume). Never synced.
- **HostAuthoritative**: Host dictates value. Clients see the value but UI is locked.
- **ClientOptional**: Host default is sent, but Client can override.

```csharp
[ModSetting("Friendly Fire", SyncMode = SyncMode.HostAuthoritative)]
public bool FriendlyFire = false;
```

---

## 8. Persistence
Spine automatically handles JSON serialization. Settings are stored in:
`YourModFolder/Config/spine_settings.json`

Data is loaded automatically before `Start()` runs, and saved automatically when the user clicks **SAVE & CLOSE** in the settings panel. You **do not** need to manually call Save/Load methods.

---

## 9. Inter-Mod Communication

Mods can read (and if permitted, write) settings from other mods using the `ModSettingsDatabase`.

```csharp
// Check if another mod has a specific feature enabled
var npgSettings = ModSettingsDatabase.GetSettingsObject("new_game_plus");
if (npgSettings != null) 
{
    bool isHardcore = (bool)npgSettings.GetType().GetField("HardcoreMode").GetValue(npgSettings);
}
```

---

## 10. Presets
Spine supports a global preset bar (currently visible only in the experimental Simple View). The cleanest way to define these is using the `[ModSettingPreset]` attribute. When multiple settings share the same preset names (e.g., "Easy", "Hard"), they will all be adjusted simultaneously when the user cycles the preset in the UI.

```csharp
[ModSetting("Elderly Age", Category = "Difficulty")]
[ModSettingPreset("Easy", 75)]
[ModSettingPreset("Medium", 60)]
[ModSettingPreset("Hard", 50)]
public int elderAgeYears = 60;

[ModSetting("Illness Chance", Category = "Difficulty")]
[ModSettingPreset("Easy", 0.05f)]
[ModSettingPreset("Medium", 0.1f)]
[ModSettingPreset("Hard", 0.2f)]
public float elderIllnessBaseChance = 0.1f;
```

If you need dynamic control, you can still manually populate the `Presets` dictionary on the `SettingDefinition` objects returned by `SpineSettingsHelper.Scan()`, but the attribute approach is recommended for most cases.

### 8.1 Preset Strategy (Difficulty vs. Static)
When implementing presets, consider that **only settings with that preset name defined will change**. If a setting like "General UI Scale" doesn't have an `[ModSettingPreset("Easy", 1.0)]` attribute, it will remain at its current value even if the user selects "Easy" difficulty.

**Best Practice:**
1.  **Group by Category:** Use the `Category` attribute to separate settings that are part of the difficulty balance from those that are general preferences.
2.  **Explicit Defaults:** Ensure every setting you want to be "difficulty-aware" has a value defined for every preset (Easy, Medium, Hard).
3.  **Advanced Sections:** Keep technical or non-balance settings in a separate category (e.g., `Category = "Advanced"`) so users know they won't be affected by the preset bar.

```csharp
// Part of the "Easy/Hard" cycle
[ModSetting("Enemy HP", Category = "Difficulty")]
[ModSettingPreset("Easy", 50)]
[ModSettingPreset("Hard", 200)]
public int hp = 100;

// Remains static regardless of preset choice
[ModSetting("Menu Color", Category = "General")]
public Color uiColor = Color.white;
```

---

## Premium UI Tips
- **Tooltips**: Always provide a `Tooltip` for complex settings.
- **Header Colors**: Use `HeaderColor = "#FF0000"` on a Header-type setting to separate sections visually. 
- **Grouping**: Fields sharing the same `Category` are automatically grouped under a blue header in the UI.
- **Restart Required**: Set `RequiresRestart = true` for settings that can't be applied live; the framework will notify the user with a subtle red warning.
