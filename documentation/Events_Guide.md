# ModAPI Events Guide
## Current v1.3 Line

Use `documentation/API_Signatures_Reference.md` for exact current signatures.

## Compatibility Matrix

| Scope | Applies To | Status |
|-------|------------|--------|
| Event concepts and examples | Current `ModAPI.dll` | Supported |
| Scheduler trigger examples | Current `ModAPI.dll` | Supported |
| Inter-mod communication examples | Current `ModAPI.dll` | Supported |

## 1. Event Systems

Available event systems:

| System | Purpose | Location |
|--------|---------|----------|
| `GameEvents` | Core game lifecycle | `ModAPI.Events.GameEvents` |
| `GameTimeTriggerHelper` | Deterministic time-trigger scheduler | `ModAPI.Events.GameTimeTriggerHelper` |
| `UIEvents` | Panel open/close/resume/pause | `ModAPI.Events.UIEvents` |
| `ModEventBus` | Inter-mod custom events | `ModAPI.Events.ModEventBus` |
| `ModAPIRegistry` | Service discovery | `ModAPI.Core.ModAPIRegistry` |
| `ModAPI.Saves.Events` | Custom save lifecycle | `ModAPI.Saves.Events` |

## 2. `GameEvents`

Use `GameEvents` when you want the compatibility event surface.

Important events:

```csharp
public static event Action<int> OnNewDay;
public static event Action<TimeTriggerBatch> OnSixHourTick;
public static event Action<TimeTriggerBatch> OnStaggeredTick;
public static event Action<SaveData> OnBeforeSave;
public static event Action<SaveData> OnAfterLoad;
public static event Action<EncounterCharacter, EncounterCharacter> OnCombatStarted;
public static event Action OnSessionStarted;
public static event Action OnNewGame;
public static event Action<ExplorationParty> OnPartyReturned;
```

`OnSixHourTick` and `OnStaggeredTick` are forwarded from `GameTimeTriggerHelper`.

Example:

```csharp
using ModAPI.Core;
using ModAPI.Events;

public class MyMod : IModPlugin
{
    public void Initialize(IPluginContext ctx) { }

    public void Start(IPluginContext ctx)
    {
        GameEvents.OnNewDay += day => ctx.Log.Info("Day " + day);
        GameEvents.OnSixHourTick += batch => ctx.Log.Info("6h tick seq=" + batch.Sequence);
        GameEvents.OnCombatStarted += (player, enemy) => ctx.Log.Info("Combat started");
    }
}
```

## 3. `GameTimeTriggerHelper`

Use `GameTimeTriggerHelper` when you want explicit named trigger registration and priority ordering.

Typical APIs:

```csharp
GameTimeTriggerHelper.RegisterTrigger(string triggerId);
GameTimeTriggerHelper.RegisterTrigger(string triggerId, int priority);
GameTimeTriggerHelper.RegisterTrigger(string triggerId, int priority, TimeTriggerCadence cadence);
GameTimeTriggerHelper.RegisterTrigger(string triggerId, int priority, TimeTriggerCadence cadence, Action<TimeTriggerBatch> callback);
GameTimeTriggerHelper.UnregisterTrigger(string triggerId);
GameTimeTriggerHelper.GetPriorityList(TimeTriggerCadence cadence);
GameTimeTriggerHelper.ConfigureStaggeredRange(int minInclusive, int maxInclusive);
```

Example:

```csharp
using ModAPI.Core;
using ModAPI.Events;

public class SchedulerMod : IModPlugin
{
    public void Initialize(IPluginContext ctx) { }

    public void Start(IPluginContext ctx)
    {
        GameTimeTriggerHelper.RegisterTrigger(
            triggerId: "com.mymod.economy.tick",
            priority: 50,
            cadence: TimeTriggerCadence.SixHour,
            callback: batch => ctx.Log.Info("Tick seq=" + batch.Sequence));
    }
}
```

## 4. `UIEvents`

Use `UIEvents` when you need panel lifecycle hooks without adding your own Harmony patches.

Available events:

```csharp
public static event Action<BasePanel> OnPanelOpened;
public static event Action<BasePanel> OnPanelClosed;
public static event Action<BasePanel> OnPanelResumed;
public static event Action<BasePanel> OnPanelPaused;
public static event Action<GameObject, string> OnButtonClicked;
```

Example:

```csharp
using ModAPI.Core;
using ModAPI.Events;

public class CraftingHelperMod : IModPlugin
{
    public void Initialize(IPluginContext ctx) { }

    public void Start(IPluginContext ctx)
    {
        UIEvents.OnPanelOpened += panel =>
        {
            if (panel.GetType().Name == "CraftingPanel")
                ctx.Log.Info("Crafting panel opened");
        };
    }
}
```

## 5. Save Lifecycle Events

The custom saves layer exposes additional save/load events under `ModAPI.Saves.Events`.

Common ones:
- `OnBeforeSave`
- `OnAfterSave`
- `OnBeforeLoad`
- `OnAfterLoad`
- `OnPageChanged`

Use these when you are integrating with the expanded save-slot system rather than the base gameplay lifecycle.

## 6. Inter-Mod Communication

### `ModEventBus`

Use `ModEventBus` for broadcast-style communication:

```csharp
ModEventBus.Publish("Author.Quests.Completed", payload);
ModEventBus.Subscribe<MyPayload>("Author.Quests.Completed", handler);
ModEventBus.Unsubscribe<MyPayload>("Author.Quests.Completed", handler);
```

### `ModAPIRegistry`

Use `ModAPIRegistry` for service discovery:

```csharp
ModAPIRegistry.RegisterAPI<IMyApi>("com.mymod.api", impl, "com.mymod");

IMyApi api;
if (ModAPIRegistry.TryGetAPI<IMyApi>("com.mymod.api", out api))
{
}
```

## 7. Best Practices

- Subscribe in `Start(...)`, not constructors.
- Unsubscribe in `Shutdown()` if your mod implements `IModShutdown`.
- Keep handlers lightweight.
- Use unique IDs for triggers and registry keys.
- Prefer named callbacks over anonymous lambdas when you need clean unsubscription.
- Use `ctx.SaveData(...)` and `ctx.LoadData(...)` or `ISaveSystem.RegisterModData(...)` for persisted state instead of static globals.

## 8. Troubleshooting

When events do not fire:
1. confirm your plugin reached `Start(...)`
2. confirm registration/subscription code executed
3. search logs for exact event-helper signatures
4. confirm the game state actually reached the expected lifecycle boundary
5. if using triggers, inspect `GetPriorityList(...)` for your cadence
