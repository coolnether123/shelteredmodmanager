# ShelteredAPI Guide (Current v1.3 Line)

`ShelteredAPI` extends the mod surface with Sheltered-specific adapters/implementations while keeping namespaces under `ModAPI.*` where needed.

Canonical signatures: `documentation/API_Signatures_Reference.md`.

## 1. What ShelteredAPI Adds

- `IGameHelper` adapter extensions: `ShelteredAPI.Adapters.GameHelperExtensions`
- Sheltered-specific implementations registered into ModAPI registries (for example `IGameHelper`, character effect runtime)

## 2. Referencing It

Add assembly references:
- Always: `ModAPI.dll`
- Optional: `ShelteredAPI.dll` (only if you use `ShelteredAPI.*` adapter namespaces directly)

Common imports:

```csharp
using ModAPI.Core;
using ModAPI.Events;
using ShelteredAPI.Adapters;
```

## 3. Usage Example

```csharp
using ModAPI.Core;
using ModAPI.Events;
using ShelteredAPI.Adapters;

public class MyPlugin : IModPlugin
{
    public void Initialize(IPluginContext ctx) { }

    public void Start(IPluginContext ctx)
    {
        // Core helper from context (type lives in ModAPI.Core)
        int ownedWater = ctx.Game.GetTotalOwned(ItemManager.ItemType.Water);
        ctx.Log.Info("Owned water: " + ownedWater);

        // Register deterministic scheduler trigger (lives in ModAPI.Events)
        GameTimeTriggerHelper.RegisterTrigger(
            triggerId: "com.mymod.economy.tick",
            priority: 50,
            cadence: TimeTriggerCadence.SixHour,
            callback: batch => ctx.Log.Info("Tick seq=" + batch.Sequence + " day=" + batch.Day));
    }
}
```

## 4. Operational Notes

- Scheduler/events compatibility surfaces (`GameEvents`, `GameTimeTriggerHelper`) are hosted in `ModAPI.dll` in the 1.3 line.
- Register triggers in `Start(...)`, not constructors.
- Use unique trigger IDs (`your.mod.id.feature`).
- Keep callbacks lightweight; offload heavy work with `ModThreads` or `RunInBackground`.
