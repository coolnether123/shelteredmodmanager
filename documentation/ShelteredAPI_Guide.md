# ShelteredAPI Guide (Current v1.3 Line)

`ShelteredAPI` supplies Sheltered-specific runtime implementations while most public contracts remain in `ModAPI.*`.

Canonical signatures: `documentation/API_Signatures_Reference.md`.

Content-specific guidance: `documentation/ShelteredAPI_Content_Guide.md`.

## 1. What ShelteredAPI Adds

- `IGameHelper` implementation and `ShelteredAPI.Adapters.GameHelperExtensions`
- the default implementation behind `IPluginContext.Actors`
- built-in actor API registrations:
  - `ShelteredAPI.Actors`
  - `ShelteredAPI.ActorRegistry`
  - `ShelteredAPI.ActorComponents`
  - `ShelteredAPI.ActorBindings`
  - `ShelteredAPI.ActorAdapters`
  - `ShelteredAPI.ActorSimulation`
  - `ShelteredAPI.ActorEvents`
  - `ShelteredAPI.ActorSerialization`
- Sheltered-specific UI and input helpers under `ShelteredAPI.*`
- Sheltered-specific content registration and runtime injection under `ShelteredAPI.Content`
- Sheltered-specific custom scenario authoring and runtime hooks under `ShelteredAPI.Scenarios`

## 2. Referencing It

Add assembly references:
- always: `ModAPI.dll`
- optional: `ShelteredAPI.dll` if you use `ShelteredAPI.*` namespaces directly

If you only use `IPluginContext.Game` or `IPluginContext.Actors`, the public types come from `ModAPI.dll`.

Common imports:

```csharp
using ModAPI.Core;
using ModAPI.Events;
using ModAPI.Actors;
using ModAPI.Saves;
using ModAPI.Scenarios;
using ShelteredAPI.Adapters;
using ShelteredAPI.Content;
using ShelteredAPI.Scenarios;
```

## 3. Usage Example

```csharp
using ModAPI.Core;
using ModAPI.Events;
using ModAPI.Actors;
using ModAPI.Saves;
using ModAPI.Scenarios;
using ShelteredAPI.Adapters;

public class MyPlugin : IModPlugin
{
    public void Initialize(IPluginContext ctx) { }

    public void Start(IPluginContext ctx)
    {
        int ownedWater = ctx.Game.GetTotalOwned(ItemManager.ItemType.Water);
        ctx.Log.Info("Owned water: " + ownedWater);

        var actor = ctx.Actors.Ensure(new ActorCreateRequest
        {
            Kind = ActorKind.Custom,
            Domain = "com.mymod",
            LifecycleState = ActorLifecycleState.Active,
            PresenceState = ActorPresenceState.Offscreen,
            Flags = ActorFlags.Persistent | ActorFlags.Synthetic
        });

        GameTimeTriggerHelper.RegisterTrigger(
            triggerId: "com.mymod.economy.tick",
            priority: 50,
            cadence: TimeTriggerCadence.SixHour,
            callback: batch => ctx.Log.Info("Tick seq=" + batch.Sequence + " day=" + batch.Day));

        ICustomScenarioService scenarios;
        if (ModAPIRegistry.TryGetAPI(GameRuntimeApiIds.CustomScenarios, out scenarios))
        {
            CustomScenarioRegistration registration = new LongRoadScenario().ToRegistration();
            registration.RequiredMods = new[]
            {
                new LoadedModInfo { modId = "com.mymod.contentpack", version = "1.2.0" }
            };
            scenarios.Register(registration);
        }
    }
}

public sealed class LongRoadScenario : ShelteredCustomScenarioBase
{
    public override string Id { get { return "com.mymod.scenario.longroad"; } }
    public override string DisplayName { get { return "The Long Road"; } }
    public override string Description { get { return "Gather resources and survive the long road."; } }

    public override ScenarioDef BuildDefinition(CustomScenarioBuildContext context)
    {
        return CreateDefinition()
            .UseInModes(true, false, false)
            .AddSimpleStage("longroad_intro")
            .Build();
    }
}
```

## 4. Operational Notes

- scheduler/events compatibility surfaces such as `GameEvents` and `GameTimeTriggerHelper` are hosted in `ModAPI.dll` in the 1.3 line
- actor contracts live in `ModAPI.Actors`; `ShelteredAPI` provides the default runtime implementation
- item, recipe, loot, asset, and content-localization APIs live in `ShelteredAPI.Content`
- custom scenario contracts live in `ModAPI.Scenarios`; Sheltered `ScenarioDef` creation and in-game hooks live in `ShelteredAPI.Scenarios`
- the content injector is manager-scoped and will rebind when a new family/session recreates Sheltered runtime managers
- register triggers and runtime behavior in `Start(...)`, not constructors
- use unique IDs for triggers, actor bindings, components, and adapters
