# ModAPI Developer Guide (v1.2, Current)

This guide is aligned to the current codebase signatures.

Exact API signatures live in: `documentation/API_Signatures_Reference.md`.

## 1. Start Here

- Plugin lifecycle and context usage: `documentation/how to develop a plugin.md`
- Harmony + transpilers: `documentation/how to develop a patch with harmony.md`
- Transpiler safety/debugging: `documentation/Transpiler_and_Debugging_Guide.md`
- Loader/runtime architecture: `documentation/ModAPI_Architecture_guide.md`
- Spine settings UI: `documentation/Spine_Settings_Guide.md`

## 2. Minimal Plugin Template

```csharp
using ModAPI.Core;

public class MyPlugin : IModPlugin
{
    public void Initialize(IPluginContext ctx)
    {
        ctx.Log.Info("Initialize");
    }

    public void Start(IPluginContext ctx)
    {
        ctx.Log.Info("Start");
    }
}
```

## 3. Optional Lifecycle Interfaces

```csharp
public class MyPlugin : IModPlugin, IModUpdate, IModShutdown, IModSceneEvents, IModSessionEvents
{
    public void Initialize(IPluginContext ctx) { }
    public void Start(IPluginContext ctx) { }
    public void Update() { }
    public void Shutdown() { }
    public void OnSceneLoaded(string sceneName) { }
    public void OnSceneUnloaded(string sceneName) { }
    public void OnSessionStarted() { }
    public void OnNewGame() { }
}
```

## 4. Content Registration (Current API)

Register through `ContentRegistry` (not legacy `RegisterCustomItem`/`RegisterCustomRecipe` helpers).

```csharp
using ModAPI.Content;

public void Start(IPluginContext ctx)
{
    var item = new ItemDefinition()
        .WithId("com.mymod.power_cell")
        .WithDisplayName("Power Cell")
        .WithDescription("A high-capacity energy cell")
        .WithCategory(ItemCategory.Normal)
        .WithStackSize(10)
        .WithScrapValue(5f)
        .WithIcon("Assets/Icons/power_cell.png");

    var itemResult = ContentRegistry.RegisterItem(item);
    if (!itemResult.Success)
    {
        ctx.Log.Error("Item registration failed: " + itemResult.ErrorMessage);
        return;
    }

    ContentRegistry.RegisterRecipe(
        new RecipeDefinition()
            .WithId("recipe.power_cell")
            .WithResultItem("com.mymod.power_cell")
            .WithStation(CraftStation.Workbench)
            .WithLevel(1)
            .WithCraftTime(45f)
            .WithIngredient(VanillaItems.Component, 3)
            .WithIngredient(VanillaItems.Metal, 2));
}
```

## 5. Asset Loading Signatures

```csharp
using System.Reflection;
using ModAPI.Content;

public void Start(IPluginContext ctx)
{
    Sprite iconA = AssetLoader.LoadSprite(Assembly.GetExecutingAssembly(), "Assets/Icons/power_cell.png");
    Sprite iconB = AssetLoader.LoadSprite(ctx.Mod.RootPath, "Assets/Icons/power_cell.png");
}
```

## 6. Events

```csharp
using ModAPI.Events;

public void Start(IPluginContext ctx)
{
    GameEvents.OnNewDay += day => ctx.Log.Info("Day " + day);
    GameEvents.OnSixHourTick += batch => ctx.Log.Info("6h tick at day " + batch.Day + ", hour " + batch.Hour);
    GameEvents.OnStaggeredTick += batch => ctx.Log.Info("Staggered tick interval: " + batch.IntervalHours + "h");
    UIEvents.OnPanelOpened += panel => ctx.Log.Info("Opened: " + panel.GetType().Name);
}
```

## 7. Mod-to-Mod APIs

```csharp
// Publish
ModAPIRegistry.RegisterAPI<IMyApi>("com.mymod.api", new MyApiImpl(), ctx.Mod.Id);

// Consume
if (ModAPIRegistry.TryGetAPI<IMyApi>("com.othermod.api", out var api))
{
    api.DoWork();
}
```

## 8. Persistence

Use `ISaveSystem` for structured per-save data:

```csharp
public class SaveState { public int Counter; }

private readonly SaveState _state = new SaveState();

public void Initialize(IPluginContext ctx)
{
    ctx.SaveSystem.RegisterModData("state", _state);
}
```

Use `PersistentDataAPI` extension methods when you want key/value blobs on the plugin context:

```csharp
ctx.SaveData("stats", myStats);
if (ctx.LoadData("stats", out MyStats loaded))
{
    myStats = loaded;
}
```

## 9. Settings

Use Spine attributes with `ModManagerBase`:

```csharp
using ModAPI.Core;
using ModAPI.Spine;

public class MyMod : ModManagerBase, IModPlugin
{
    [ModSetting("Enable Feature")]
    public bool Enabled = true;

    [ModSetting("Multiplier", Min = 0.5f, Max = 3.0f, StepSize = 0.1f)]
    public float Multiplier = 1f;

    public override void Initialize(IPluginContext ctx)
    {
        base.Initialize(ctx);
    }

    public void Start(IPluginContext ctx) { }
}
```

## 10. Logging

- Preferred for mod logs: `ctx.Log.Info/Warn/Error/Debug`.
- Static/internal logs: `MMLog.WriteInfo`, `MMLog.WriteWarning`, `MMLog.WriteError`, `MMLog.WriteDebug`.

## 11. Transpiler Safety Reminder

- Prefer targeted replacements over broad IL rewrites.
- Use `Build(strict: true, validateStack: true)` by default.
- For advanced transforms, use transaction patterns (`WithTransaction`) and inspect dumps via `TranspilerDebugger.DumpWithDiff(...)`.
