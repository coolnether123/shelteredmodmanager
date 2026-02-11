# Settings and Persistence (Current v1.2)

Use `Spine` for mod configuration UI and `ISaveSystem`/`PersistentDataAPI` for data persistence.

Canonical signatures: `documentation/API_Signatures_Reference.md`.

## 1. Recommended Settings Flow (Spine + ModManagerBase)

```csharp
public class MyMod : ModManagerBase, IModPlugin
{
    [ModSetting("Enable Feature")]
    public bool Enabled = true;

    [ModSetting("Multiplier", Min = 0.5f, Max = 3.0f, StepSize = 0.1f)]
    public float Multiplier = 1.0f;

    public override void Initialize(IPluginContext ctx)
    {
        base.Initialize(ctx); // settings controller is discovered and loaded here
    }
}
```

## 2. Runtime Toggles (Global ModAPI Flags)

Use `ModPrefs` for loader/runtime flags:

```csharp
ModPrefs.DebugTranspilers = true;
ModPrefs.TranspilerSafeMode = true;
ModPrefs.Save();
```

## 3. Per-Save Typed Data (`ISaveSystem`)

```csharp
public class MySaveState
{
    public int Visits;
}

public class MyMod : IModPlugin
{
    private readonly MySaveState _state = new MySaveState();

    public void Initialize(IPluginContext ctx)
    {
        ctx.SaveSystem.RegisterModData("state", _state);
    }

    public void Start(IPluginContext ctx) { }
}
```

## 4. SaveData/LoadData Extension Methods (`PersistentDataAPI`)

These are extension methods on `IPluginContext`:

```csharp
// Save
ctx.SaveData("custom_blob", myData);

// Load
if (ctx.LoadData("custom_blob", out MyData loaded))
{
    myData = loaded;
}
```

## 5. What Not to Use

Legacy calls below are no longer current API:
- `ctx.Settings.GetInt(...)`
- `ctx.Settings.SetInt(...)`
- `ctx.Settings.SaveUser()`
- `PersistentDataAPI.SaveData(...)` (static style without `ctx`)
