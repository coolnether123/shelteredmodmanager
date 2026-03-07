# ModAPI Developer Guide (Current v1.3 Line)

## Compatibility Matrix

| Area | Assembly | Status |
|------|----------|--------|
| Core plugin lifecycle, context, settings, content APIs | `ModAPI.dll` | Current |
| Backward-compat game helpers/events used by v1.2 mods | `ModAPI.dll` | Current (Deprecated for future major) |
| Sheltered-specific adapters and implementations | `ShelteredAPI.dll` | Current |
| Docs labeled `v1.2` in this repo | Historical reference | Deprecated where conflicting |

Exact signatures: `documentation/API_Signatures_Reference.md`.

## 1. Start Here

- Plugin lifecycle and context usage: `documentation/how to develop a plugin.md`
- Harmony + transpilers: `documentation/how to develop a patch with harmony.md`
- Transpiler safety/debugging: `documentation/Transpiler_and_Debugging_Guide.md`
- Loader/runtime architecture: `documentation/ModAPI_Architecture_guide.md`
- Spine settings UI: `documentation/Spine_Settings_Guide.md`
- Settings + persistence patterns: `documentation/SETTINGS.md`
- ShelteredAPI helper surface: `documentation/ShelteredAPI_Guide.md`
- Actor registry/components/bindings/adapters: `documentation/ShelteredAPI_Characters_Guide.md`
- Failures and log signatures: `documentation/API_Troubleshooting.md`

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

## 3. Content Registration (Current API)

Register via `ContentRegistry` in `Start(...)` (safe lifecycle guidance below).

### 3.1 Type-Name Collision Warning

`ItemDefinition` exists both in game code and in `ModAPI.Content`. Use aliases in mod code:

```csharp
using ContentItemDefinition = ModAPI.Content.ItemDefinition;
using GameItemDefinition = global::ItemDefinition;
```

### 3.2 Recommended Registration Example

```csharp
using ModAPI.Content;
using ContentItemDefinition = ModAPI.Content.ItemDefinition;

public void Start(IPluginContext ctx)
{
    var item = new ContentItemDefinition()
        .WithId("com.mymod.power_cell")
        .WithDisplayNameText("Power Cell")
        .WithDescriptionText("A high-capacity energy cell")
        .WithCategory(ItemCategory.Normal)
        .WithStackSize(10)
        .WithScrapValue(5f)
        .WithIcon("Assets/Icons/power_cell.png");

    var result = ContentRegistry.RegisterItem(item);
    if (!result.Success)
    {
        ctx.Log.Error("Item registration failed: " + result.ErrorMessage);
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

### 3.3 Localization Keys vs Text (ModAPI v1.3)

Use explicit APIs when possible:
- `.WithDisplayNameKey("mymod.items.power_cell.name")`
- `.WithDescriptionKey("mymod.items.power_cell.desc")`
- `.WithDisplayNameText("Power Cell")`
- `.WithDescriptionText("A high-capacity energy cell")`

Backward-compatible behavior:
- Legacy `.WithDisplayName(...)` / `.WithDescription(...)` still work.
- Values are treated as keys if they look like keys (`.` and no spaces), otherwise treated as literal text.
- For literal text, ModAPI generates and registers internal keys before item UI reads localization.
- This prevents vanilla fallback lowercasing issues when key lookup misses.

### 3.4 Registration Timing and Lifecycle

Use this ordering:
1. `Initialize(...)`: cache context, wire events, set up state only.
2. `Start(...)`: register items/recipes/patches.

Rationale:
- `ContentInjector` bootstraps only when managers are ready (`ItemManager.Instance` and `CraftingManager.Instance`).
- Definitions registered by `Start(...)` are available by the time injector bootstraps.
- Registering in constructors is unsafe and can race before loader context exists.

Guaranteed-safe recipe:
- Put all `ContentRegistry.RegisterItem/RegisterRecipe/RegisterCookingRecipe` calls in `Start(...)`.
- Do not require managers directly in `Start(...)`; let injector consume registry entries.

## 4. Settings Patterns

Two supported patterns:
- Pattern A: `ModManagerBase<T>` auto-controller and auto-load.
- Pattern B: `ISettingsProvider` manual provider with `SpineSettingsHelper.Scan`.

Use A unless you explicitly need B. Full examples are in:
- `documentation/Spine_Settings_Guide.md`
- `documentation/SETTINGS.md`

## 5. Events (ModAPI + ShelteredAPI)

```csharp
using ModAPI.Events;

public void Start(IPluginContext ctx)
{
    GameEvents.OnNewDay += day => ctx.Log.Info("Day " + day);
    GameEvents.OnSixHourTick += batch => ctx.Log.Info("6h tick seq=" + batch.Sequence);
    GameEvents.OnStaggeredTick += batch => ctx.Log.Info("Staggered every " + batch.IntervalHours + "h");
}
```

## 6. ShelteredAPI-Specific Helpers

`ShelteredAPI` ships additional helpers under existing namespaces (`ModAPI.Core`, `ModAPI.Events`).

Example: explicit trigger registration and priority ordering.

```csharp
using ModAPI.Events;

public void Start(IPluginContext ctx)
{
    GameTimeTriggerHelper.RegisterTrigger(
        triggerId: "com.mymod.economy.tick",
        priority: 50,
        cadence: TimeTriggerCadence.SixHour,
        callback: batch => ctx.Log.Info("Economy tick " + batch.TotalHours));
}
```

Actor services are exposed through `ctx.Actors`:

```csharp
using ModAPI.Actors;

public void Start(IPluginContext ctx)
{
    var actor = ctx.Actors.Ensure(new ActorCreateRequest
    {
        Kind = ActorKind.Faction,
        Domain = "com.mymod",
        LifecycleState = ActorLifecycleState.Active,
        PresenceState = ActorPresenceState.Offscreen,
        Flags = ActorFlags.Persistent | ActorFlags.Synthetic
    });
}
```

## 7. Persistence

```csharp
public class SaveState { public int Counter; }
private readonly SaveState _state = new SaveState();

public void Initialize(IPluginContext ctx)
{
    ctx.SaveSystem.RegisterModData("state", _state);
}
```

```csharp
ctx.SaveData("stats", myStats);
if (ctx.LoadData("stats", out MyStats loaded))
{
    myStats = loaded;
}
```

## 8. Logging

- Preferred for mod logs: `ctx.Log.Info/Warn/Error/Debug`.
- Internal/static logs: `MMLog.WriteInfo`, `MMLog.WriteWarning`, `MMLog.WriteError`, `MMLog.WriteDebug`.
