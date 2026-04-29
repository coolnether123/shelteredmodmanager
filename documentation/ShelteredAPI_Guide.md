# ShelteredAPI Guide (Current v1.3 Line)

`ShelteredAPI` supplies Sheltered-specific product APIs and runtime implementations while `ModAPI` stays host-neutral. The 1.3 line is a breaking clean API line.

Canonical signatures: `documentation/API_Signatures_Reference.md`.

Content-specific guidance: `documentation/ShelteredAPI_Content_Guide.md`.

Input/keybinding guidance: `documentation/Input_Keybindings_Guide.md`.

Custom scenario authoring guidance: `documentation/Custom_Scenarios_Guide.md`.

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
- Sheltered-specific custom scenario XML definitions, `ShelteredScenarios`, `ShelteredScenarioAuthoring`, `ShelteredScenarioRuntime`, and `ShelteredScenarioDefBuilder` under `ShelteredAPI.Scenarios`
- Sheltered save integration through `ShelteredSaves`, `ShelteredSaveEvents`, and save descriptor/result DTOs

## API Stability Rules

- Public facades are the stable mod-author surface.
- Implementation classes are internal and may move.
- Typed Sheltered escape hatches are explicit.
- Future migrations should happen behind facades.

## 2. Assembly Rule

Add assembly references:
- always: `ModAPI.dll`
- required for Sheltered hooks: `ShelteredAPI.dll` if you use Sheltered content, scenario, event, party, interaction, save, UI, input, inventory, or game-state helpers

If you only use neutral `IPluginContext` contracts, the public types come from `ModAPI.dll`; Sheltered runtime implementations are still supplied by `ShelteredAPI`.

`ModAPI.dll` no longer references `Assembly-CSharp` or `Manager`. ShelteredAPI owns those game/runtime references and registers its implementations through the neutral `GameRuntime.*` registry IDs.

Common imports:

```csharp
using ModAPI.Core;
using ModAPI.Actors;
using ModAPI.Scenarios;
using ShelteredAPI.Adapters;
using ShelteredAPI.Content;
using ShelteredAPI.Events;
using ShelteredAPI.Input;
using ShelteredAPI.Saves;
using ShelteredAPI.Scenarios;
using ShelteredAPI.UI;
```

## 3. Usage Example

```csharp
using ModAPI.Core;
using ModAPI.Actors;
using ModAPI.Scenarios;
using ShelteredAPI.Adapters;
using ShelteredAPI.Events;
using ShelteredAPI.Saves;

public class MyPlugin : IModPlugin
{
    public void Initialize(IPluginContext ctx) { }

    public void Start(IPluginContext ctx)
    {
        int ownedWater = ctx.Game.GetTotalOwned(ItemManager.ItemType.Water);
        ctx.Log.Info("Owned water: " + ownedWater);
        FamilyMember firstMember = ctx.Game.FindFamilyMember("Alice");

        var actor = ctx.Actors.Ensure(new ActorCreateRequest
        {
            Kind = ActorKind.Custom,
            Domain = "com.mymod",
            LifecycleState = ActorLifecycleState.Active,
            PresenceState = ActorPresenceState.Offscreen,
            Flags = ActorFlags.Persistent | ActorFlags.Synthetic
        });

        ShelteredEvents.RegisterTimeTrigger(
            triggerId: "com.mymod.economy.tick",
            priority: 50,
            cadence: TimeTriggerCadence.SixHour,
            callback: batch => ctx.Log.Info("Tick seq=" + batch.Sequence + " day=" + batch.Day));

        CustomScenarioRegistration registration = new LongRoadScenario().ToRegistration();
        registration.RequiredMods = new[]
        {
            new ScenarioModDependency { modId = "com.mymod.contentpack", version = "1.0.0" }
        };
        ShelteredScenarios.Register(registration);
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

- use `ShelteredEvents` for game, UI, faction, and time-trigger events
- use `ShelteredContent` for item, recipe, loot, asset, localization, and inventory helper operations
- use `ShelteredSaves` and `ShelteredSaveEvents` for custom save operations
- use `ShelteredUI` for panel takeovers and Sheltered keybind UI
- actor contracts live in `ModAPI.Actors`; `ShelteredAPI` provides the default runtime implementation
- item, recipe, loot, asset, and content-localization APIs live in `ShelteredAPI.Content`
- custom scenario registration contracts, lifecycle state/events, opaque definition factories, catalog metadata, dependency manifest conversion, and validation result containers live in `ModAPI.Scenarios`
- Sheltered scenario definitions, `ShelteredScenarios`, `ShelteredScenarioAuthoring`, `ShelteredScenarioRuntime`, and `ShelteredScenarioDefBuilder` live in `ShelteredAPI.Scenarios`; serializers, validators, runtime binding, browser controllers, and apply services are internal
- Sheltered save APIs live in `ShelteredAPI.dll`; `ModAPI.dll` owns only neutral per-mod persistence contracts and the `GameRuntime.SaveRuntime` port
- the content injector is manager-scoped and will rebind when a new family/session recreates Sheltered runtime managers
- register triggers and runtime behavior in `Start(...)`, not constructors
- use unique IDs for triggers, actor bindings, components, and adapters
