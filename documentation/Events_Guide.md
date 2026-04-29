# ModAPI + ShelteredAPI Events Guide
## Current v1.3 Line

The 1.3 line is a breaking clean API line.

Use `documentation/API_Signatures_Reference.md` for exact current signatures.

## Assembly Rule

- Always reference `ModAPI.dll`.
- Reference `ShelteredAPI.dll` when your mod uses Sheltered content, saves, UI, input, events, actors, or scenarios.

## API Stability Rules

- Public facades are stable.
- Implementation classes are internal.
- Typed Sheltered escape hatches are explicit.
- Future migrations should happen behind facades.

## Compatibility Matrix

| Scope | Applies To | Status |
|-------|------------|--------|
| Sheltered gameplay/UI events and scheduler examples | Current `ShelteredAPI.dll` under `ShelteredAPI.Events` | Supported |
| Inter-mod communication examples | Current `ModAPI.dll` | Supported |

## 1. Event Systems

Available event systems:

| System | Purpose | Location |
|--------|---------|----------|
| `ShelteredEvents` | Sheltered game/UI/faction lifecycle and time scheduler | `ShelteredAPI.Events.ShelteredEvents` in `ShelteredAPI.dll` |
| `ModEventBus` | Inter-mod custom events | `ModAPI.Events.ModEventBus` in `ModAPI.dll` |
| `ModAPIRegistry` | Service discovery | `ModAPI.Core.ModAPIRegistry` |
| `ShelteredSaveEvents` | Sheltered custom save lifecycle | `ShelteredAPI.Saves.ShelteredSaveEvents` in `ShelteredAPI.dll` |

## 2. `ShelteredEvents`

Use `ShelteredEvents` when you want the Sheltered event surface. Reference both `ModAPI.dll` and `ShelteredAPI.dll`; if your handlers mention Sheltered game types such as `SaveData`, `EncounterCharacter`, `BasePanel`, or `ExplorationParty`, also reference `Assembly-CSharp.dll`.

Important events:

```csharp
public static event Action<int> NewDay;
public static event Action<TimeTriggerBatch> SixHourTick;
public static event Action<TimeTriggerBatch> StaggeredTick;
public static event Action<SaveData> BeforeSave;
public static event Action<SaveData> AfterLoad;
public static event Action<EncounterCharacter, EncounterCharacter> CombatStarted;
public static event Action SessionStarted;
public static event Action NewGame;
public static event Action<ExplorationParty> PartyReturned;
```

Time events and named trigger registration are exposed through the same facade.

Example:

```csharp
using ModAPI.Core;
using ShelteredAPI.Events;

public class MyMod : IModPlugin
{
    public void Initialize(IPluginContext ctx) { }

    public void Start(IPluginContext ctx)
    {
        ShelteredEvents.NewDay += day => ctx.Log.Info("Day " + day);
        ShelteredEvents.SixHourTick += batch => ctx.Log.Info("6h tick seq=" + batch.Sequence);
        ShelteredEvents.CombatStarted += (player, enemy) => ctx.Log.Info("Combat started");
    }
}
```

## 3. Time Triggers

Use `ShelteredEvents` when you want explicit named trigger registration and priority ordering in Sheltered runtime time.

Typical APIs:

```csharp
ShelteredEvents.RegisterTimeTrigger(string triggerId);
ShelteredEvents.RegisterTimeTrigger(string triggerId, int priority);
ShelteredEvents.RegisterTimeTrigger(string triggerId, int priority, TimeTriggerCadence cadence);
ShelteredEvents.RegisterTimeTrigger(string triggerId, int priority, TimeTriggerCadence cadence, Action<TimeTriggerBatch> callback);
ShelteredEvents.UnregisterTimeTrigger(string triggerId);
ShelteredEvents.GetTimeTriggerPriorityList(TimeTriggerCadence cadence);
ShelteredEvents.ConfigureStaggeredTimeRange(int minInclusiveHours, int maxInclusiveHours);
```

Example:

```csharp
using ModAPI.Core;
using ShelteredAPI.Events;

public class SchedulerMod : IModPlugin
{
    public void Initialize(IPluginContext ctx) { }

    public void Start(IPluginContext ctx)
    {
        ShelteredEvents.RegisterTimeTrigger(
            triggerId: "com.mymod.economy.tick",
            priority: 50,
            cadence: TimeTriggerCadence.SixHour,
            callback: batch => ctx.Log.Info("Tick seq=" + batch.Sequence));
    }
}
```

## 4. UI Events

Use `ShelteredEvents` when you need Sheltered panel lifecycle hooks without adding your own Harmony patches.

Available events:

```csharp
public static event Action<BasePanel> PanelOpened;
public static event Action<BasePanel> PanelClosed;
public static event Action<BasePanel> PanelResumed;
public static event Action<BasePanel> PanelPaused;
public static event Action<GameObject, string> ButtonClicked;
```

Example:

```csharp
using ModAPI.Core;
using ShelteredAPI.Events;

public class CraftingHelperMod : IModPlugin
{
    public void Initialize(IPluginContext ctx) { }

    public void Start(IPluginContext ctx)
    {
        ShelteredEvents.PanelOpened += panel =>
        {
            if (panel.GetType().Name == "CraftingPanel")
                ctx.Log.Info("Crafting panel opened");
        };
    }
}
```

## 5. Save Lifecycle Events

The Sheltered custom saves layer exposes additional save/load events under `ShelteredAPI.Saves.ShelteredSaveEvents`.
Reference `ShelteredAPI.dll` for these APIs.

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
- Use `ISaveSystem.RegisterModData(...)` for neutral persisted state instead of static globals.

## 8. Troubleshooting

When events do not fire:
1. confirm your plugin reached `Start(...)`
2. confirm registration/subscription code executed
3. search logs for exact event-helper signatures
4. confirm the game state actually reached the expected lifecycle boundary
5. if using triggers, inspect `GetPriorityList(...)` for your cadence
