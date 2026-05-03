# ModAPI Developer Guide (v1.3 Beta.3)

## Compatibility Matrix

| Area | Assembly | Status |
|------|----------|--------|
| Core plugin lifecycle, context, settings APIs | `ModAPI.dll` | Current |
| Neutral input, actor, event-bus, scenario, and persistence contracts | `ModAPI.dll` | Current |
| Sheltered content, saves, UI, input, events, actors, and scenarios | `ShelteredAPI.dll` | Current |
| Sheltered-specific adapters and implementations | `ShelteredAPI.dll` | Internal unless exposed by a facade |

Exact signatures: `documentation/API_Signatures_Reference.md`.

The 1.3 Beta.3 line is a breaking clean API line. `ModAPI.dll` is the neutral framework assembly and no longer references Sheltered `Assembly-CSharp` or the manager application. Sheltered runtime behavior is provided by `ShelteredAPI.dll` through public facades and neutral `GameRuntime.*` services.

## Assembly Rule

- Always reference `ModAPI.dll`.
- Reference `ShelteredAPI.dll` when your mod uses Sheltered content, saves, UI, input, events, actors, or scenarios.

## API Stability Rules

- Public facades are the stable mod-author surface.
- Implementation classes are internal and may move.
- Typed Sheltered escape hatches are explicit.
- Future migrations should happen behind facades.

## 1. Start Here

For a guided reading path, start with the root [README documentation section](../readme.md#documentation). For specific tasks:

- Plugin lifecycle and context usage: [How to Develop a Plugin](how%20to%20develop%20a%20plugin.md)
- Harmony + transpilers: [How to Develop a Harmony Patch](how%20to%20develop%20a%20patch%20with%20harmony.md)
- Transpiler safety/debugging: [Transpiler and Debugging Guide](Transpiler_and_Debugging_Guide.md)
- Loader/runtime architecture: [ModAPI Architecture Guide](ModAPI_Architecture_guide.md)
- Spine settings UI: [Spine Settings Guide](Spine_Settings_Guide.md)
- Settings + persistence patterns: [Settings and Persistence](SETTINGS.md)
- ShelteredAPI helper surface: [ShelteredAPI Guide](ShelteredAPI_Guide.md)
- Input/keybinding registration: [Input Keybindings Guide](Input_Keybindings_Guide.md)
- Custom scenario registration and authoring: [Custom Scenarios Guide](Custom_Scenarios_Guide.md)
- Sheltered content registration/runtime: [ShelteredAPI Content Guide](ShelteredAPI_Content_Guide.md)
- Actor registry/components/bindings/adapters: [ShelteredAPI Actors Guide](ShelteredAPI_Characters_Guide.md)
- Failures and log signatures: [API Troubleshooting](API_Troubleshooting.md)

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

Register via `ShelteredAPI.Content.ShelteredContent` in `Start(...)` (safe lifecycle guidance below).

### 3.1 Type-Name Collision Warning

`ItemDefinition` exists both in game code and in `ShelteredAPI.Content`. Use aliases in mod code:

```csharp
using ShelteredAPI.Content;
using ContentItemDefinition = ShelteredAPI.Content.ItemDefinition;
using GameItemDefinition = global::ItemDefinition;
```

### 3.2 Recommended Registration Example

```csharp
using ShelteredAPI.Content;
using ContentItemDefinition = ShelteredAPI.Content.ItemDefinition;

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

    var result = ShelteredContent.RegisterItem(item);
    if (!result.Success)
    {
        ctx.Log.Error("Item registration failed: " + result.ErrorMessage);
        return;
    }

    ShelteredContent.RegisterRecipe(
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

### 3.3 Localization Keys vs Text (ShelteredAPI v1.3)

Use explicit APIs when possible:
- `.WithDisplayNameKey("mymod.items.power_cell.name")`
- `.WithDescriptionKey("mymod.items.power_cell.desc")`
- `.WithDisplayNameText("Power Cell")`
- `.WithDescriptionText("A high-capacity energy cell")`

Avoid ambiguous display-name helpers in new 1.3 code. Prefer explicit key/text methods so future content migrations can happen behind the facade.

### 3.4 Registration Timing and Lifecycle

Use this ordering:
1. `Initialize(...)`: cache context, wire events, set up state only.
2. `Start(...)`: register items/recipes/patches.

Rationale:
- `ContentInjector` binds against the active manager pair (`ItemManager.Instance` and `CraftingManager.Instance`) and rebinds when the game creates fresh managers for a new family/session.
- Definitions registered by `Start(...)` are available by the time injector bootstraps.
- Registering in constructors is unsafe and can race before loader context exists.

Guaranteed-safe recipe:
- Put all `ShelteredContent.RegisterItem/RegisterRecipe/RegisterCookingRecipe` calls in `Start(...)`.
- Do not require managers directly in `Start(...)`; let injector consume registry entries.

## 4. Settings Patterns

Two supported patterns:
- Pattern A: `ModManagerBase<T>` auto-controller and auto-load.
- Pattern B: `ISettingsProvider` manual provider with `SpineSettingsHelper.Scan`.

Use A unless you explicitly need B. Full examples are in:
- `documentation/Spine_Settings_Guide.md`
- `documentation/SETTINGS.md`

## 5. Events (ModAPI + ShelteredAPI)

`ModEventBus` is neutral and lives in `ModAPI.dll`. Sheltered gameplay/UI/faction/time events are exposed through `ShelteredAPI.Events.ShelteredEvents`.

```csharp
using ShelteredAPI.Events;

public void Start(IPluginContext ctx)
{
    ShelteredEvents.NewDay += day => ctx.Log.Info("Day " + day);
    ShelteredEvents.SixHourTick += batch => ctx.Log.Info("6h tick seq=" + batch.Sequence);
    ShelteredEvents.StaggeredTick += batch => ctx.Log.Info("Staggered every " + batch.IntervalHours + "h");
}
```

## 6. ShelteredAPI-Specific Helpers

Use the Sheltered facades directly:
- `ShelteredContent` for content registration and item resolution.
- `ShelteredSaves` and `ShelteredSaveEvents` for Sheltered save slots and lifecycle.
- `ShelteredUI` for intended UI helpers.
- `ShelteredInput` for Sheltered input tuning and vanilla action IDs.
- `ShelteredEvents` for Sheltered events.
- `ShelteredActors` and `ShelteredCharacters` for actor/character integration.
- `ShelteredScenarios`, `ShelteredScenarioAuthoring`, and `ShelteredScenarioRuntime` for scenarios.

Example: explicit trigger registration and priority ordering.

```csharp
using ShelteredAPI.Events;

public void Start(IPluginContext ctx)
{
    ShelteredEvents.RegisterTimeTrigger(
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

`ModAPI.dll` owns neutral per-mod JSON persistence through `ctx.SaveSystem`.
The active game supplies slot paths through the `GameRuntime.SaveRuntime` adapter; Sheltered's adapter is hosted by `ShelteredAPI.dll`.

```csharp
public class SaveState { public int Counter; }
private readonly SaveState _state = new SaveState();

public void Initialize(IPluginContext ctx)
{
    ctx.SaveSystem.RegisterModData("state", _state);
}
```

Use `ShelteredSaves` when your mod intentionally works with Sheltered save slots or descriptors. Keep ordinary per-mod state on `ctx.SaveSystem`.

```csharp
SaveEntry[] saves = ShelteredSaves.ListStandard(page: 0, pageSize: 20);
foreach (SaveEntry save in saves)
{
    ctx.Log.Info(save.DisplayName);
}
```

## 8. Logging

- Preferred for mod logs: `ctx.Log.Info/Warn/Error/Debug`.
- Internal/static logs: `MMLog.WriteInfo`, `MMLog.WriteWarning`, `MMLog.WriteError`, `MMLog.WriteDebug`.
