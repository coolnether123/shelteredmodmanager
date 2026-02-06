# Mod Settings Guide (Legacy & Low-Level)

> **Recommendation:** Most mods should use the **Spine Settings Framework** (see [Spine_Settings_Guide.md](Spine_Settings_Guide.md)) which provides automatic UI generation and object-based persistence.

---

## 1. Low-Level Key-Value API
The original ModAPI settings system is a simple key-value collection. Use this if you need a very lightweight setup or are porting a v0.x mod.

### Location
- `Config/default.json` - Default values bundled with your mod.
- `Config/user.json` - User overrides (automatically created by `SaveUser()`).

### JSON Format
```json
{
  "entries": [
    { "key": "difficulty", "type": "string", "value": "Normal" },
    { "key": "maxCount",   "type": "int",    "value": "10" },
    { "key": "spawnRate",  "type": "float",  "value": "1.25" },
    { "key": "enabled",    "type": "bool",   "value": "true" }
  ]
}
```

### Usage
```csharp
public void Initialize(IPluginContext ctx)
{
    var settings = ctx.Settings;
    int count = settings.GetInt("maxCount", 5);
    bool enabled = settings.GetBool("enabled", true);
}

public void SaveChanges(IPluginContext ctx)
{
    ctx.Settings.SetInt("maxCount", 20);
    ctx.Settings.SaveUser(); // Writes to Config/user.json
}
```

---

## 2. Global Mod Persistence
If you need to save arbitrary C# objects or per-save data, use `PersistentDataAPI`.

### Global Data (Persistent across all saves)
```csharp
public class MyModData { public int HighScore; }

// Save
PersistentDataAPI.SaveData("com.mymod.stats", new MyModData { HighScore = 100 });

// Load
var data = PersistentDataAPI.LoadData<MyModData>("com.mymod.stats");
```

### Per-Save Data
To attach data to a specific save file, see the `ISaveSystem` documentation.

---

## 3. Comparison
| Feature | Low-Level (This Doc) | Spine (Recommended) |
|---------|---------------------|----------------------|
| **UI** | Manual | Automatic (Attributes) |
| **Logic** | Key-Value Strings | Type-safe C# Objects |
| **Persistence**| `user.json` | `spine_settings.json` |
| **Complex UI** | N/A | Sliders, Toggles, Enums, Presets |
| **Best For** | Legacy Mods | New Mods |
