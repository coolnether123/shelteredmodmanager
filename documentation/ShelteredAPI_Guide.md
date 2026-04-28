# ShelteredAPI Guide (Current v1.3 Line)

`ShelteredAPI` supplies Sheltered-specific runtime implementations while most public contracts remain in `ModAPI.*`.

Canonical signatures: `documentation/API_Signatures_Reference.md`.

Content-specific guidance: `documentation/ShelteredAPI_Content_Guide.md`.

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
- Sheltered-specific UI and input helpers under `ShelteredAPI.*`, plus 1.3 migration aliases that retain old `ModAPI.UI`, `ModAPI.Hooks`, and `ModAPI.Items` namespaces while living in `ShelteredAPI.dll`
- Sheltered-specific content registration and runtime injection under `ShelteredAPI.Content`
- Sheltered-specific custom scenario XML definitions, serializers, validators, authoring helpers, runtime binding, apply services, and runtime hooks under `ShelteredAPI.Scenarios`
- Sheltered save integration: `SaveManager` patches, expanded/custom save slots, `PersistentDataAPI`, `GameUtil`, `ModList`, `ModDictionary`, and custom-save APIs under old `ModAPI.*` namespaces for 1.3 source migration

## 2. Referencing It

Add assembly references:
- always: `ModAPI.dll`
- required for Sheltered hooks: `ShelteredAPI.dll` if you use Sheltered content, scenario, event, party, interaction, save, UI, input, inventory, or manager-state helpers, even when the namespace remains `ModAPI.*` for 1.3 source migration

If you only use neutral `IPluginContext` contracts, the public types come from `ModAPI.dll`; Sheltered runtime implementations are still supplied by `ShelteredAPI`.

`ModAPI.dll` no longer references `Assembly-CSharp` or `Manager`. ShelteredAPI owns those game/runtime references and registers its implementations through the neutral `GameRuntime.*` registry IDs plus 1.3 migration aliases.

Common imports:

```csharp
using ModAPI.Core;
using ModAPI.Events;
using ModAPI.Actors;
using ModAPI.Saves;
using ModAPI.Scenarios;
using ModAPI.Items;
using ModAPI.UI;
using ShelteredAPI.Adapters;
using ShelteredAPI.Content;
using ShelteredAPI.Input;
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
        FamilyMember firstMember = ctx.Game.FindFamilyMember("Alice");

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
                new ScenarioModDependency { modId = "com.mymod.contentpack", version = "1.2.0" }
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

- scheduler/events compatibility surfaces such as `GameEvents`, `GameTimeTriggerHelper`, `UIEvents`, `FactionEvents`, `PartyHelper`, `InteractionRegistry`, and `ManagerStateHelper` are hosted in `ShelteredAPI.dll` in the 1.3 line while retaining old `ModAPI.*` namespaces as migration aliases
- Sheltered UI/content compatibility surfaces such as `InventoryHelper`, `UIHooks`, `ContextMenuHelper`, `ModUIHooks`, `ModSettingsPanel`, `ModManagerPanel`, NGUI helpers, and Spine settings UI renderers are also hosted in `ShelteredAPI.dll`; `ModAPI.dll` keeps only neutral UI/input shims and contracts
- actor contracts live in `ModAPI.Actors`; `ShelteredAPI` provides the default runtime implementation
- item, recipe, loot, asset, and content-localization APIs live in `ShelteredAPI.Content`
- custom scenario registration contracts, lifecycle state/events, opaque definition factories, catalog metadata, dependency manifest conversion, and validation result containers live in `ModAPI.Scenarios`
- Sheltered scenario definitions, XML serializers, validators, runtime binding, `ScenarioDef` creation, and in-game hooks live in `ShelteredAPI.Scenarios`
- Sheltered save APIs live in `ShelteredAPI.dll`; `ModAPI.dll` owns only neutral per-mod persistence contracts and the `GameRuntime.SaveRuntime` port
- the content injector is manager-scoped and will rebind when a new family/session recreates Sheltered runtime managers
- register triggers and runtime behavior in `Start(...)`, not constructors
- use unique IDs for triggers, actor bindings, components, and adapters
