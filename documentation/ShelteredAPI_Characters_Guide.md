# ShelteredAPI Actors Guide

This guide covers the actor system exposed through:

```csharp
IPluginContext.Actors
```

## 1. Getting Started

```csharp
using ModAPI.Actors;
using ModAPI.Core;

public class MyMod : IModPlugin
{
    private IActorSystem _actors;

    public void Initialize(IPluginContext ctx)
    {
        _actors = ctx.Actors;
    }

    public void Start(IPluginContext ctx)
    {
    }
}
```

## 2. Actor Identity

Every actor uses a typed `ActorId`:

```csharp
var id = new ActorId(ActorKind.Citizen, 1, "mymod");
```

- `Kind` prevents collisions between players, visitors, factions, and synthetic actors.
- `Domain` is the mod namespace for custom actor spaces.
- `LocalId` is stable inside that `(Kind, Domain)` scope.

## 3. Creating Actors

```csharp
var actor = _actors.Create(new ActorCreateRequest
{
    Kind = ActorKind.Citizen,
    Domain = "mymod",
    LifecycleState = ActorLifecycleState.Registered,
    PresenceState = ActorPresenceState.Offscreen,
    Flags = ActorFlags.Persistent | ActorFlags.Synthetic,
    Origin = new ActorOrigin
    {
        SourceModId = "mymod",
        SourceKey = "mymod.citizen.1",
        Generator = "worldgen"
    }
});
```

## 4. Built-In Components

Profile data:

```csharp
_actors.Set(actor.Id, new ActorProfileComponent
{
    FirstName = "Aria",
    LastName = "Stone",
    Health = 85,
    MaxHealth = 100
}, "shelteredapi");
```

Attribute storage for mods:

```csharp
var attrs = new ActorAttributeSetComponent();
attrs.SetValue("mymod.reputation", 42f, "mymod");
attrs.SetValue("mymod.courage", 8f, "mymod");

_actors.Set(actor.Id, attrs, "mymod");
```

## 5. Custom Components

Define a namespaced component:

```csharp
[Serializable]
public class PersonalityComponent : IActorComponent
{
    public string Disposition;
    public int Loyalty;

    public string ComponentId { get { return "mymod.personality"; } }
    public int Version { get { return 1; } }
    public ActorConflictPolicy ConflictPolicy { get { return ActorConflictPolicy.Replace; } }
}
```

Register a serializer once:

```csharp
_actors.RegisterSerializer(
    new ActorJsonComponentSerializer<PersonalityComponent>("mymod.personality", 1));
```

Attach/query/remove:

```csharp
_actors.Set(actor.Id, new PersonalityComponent { Disposition = "calm", Loyalty = 7 }, "mymod");

PersonalityComponent personality;
if (_actors.TryGet(actor.Id, out personality))
{
    int loyalty = personality.Loyalty;
}

_actors.Remove(actor.Id, "mymod.personality", "mymod");
```

## 6. Queries

```csharp
var persistentCitizens = _actors.Enumerate(
    _actors.Query()
        .ByKind(ActorKind.Citizen)
        .OnlyPersistent()
        .WithComponent("mymod.personality")
        .Build());
```

## 7. Simulation

```csharp
public class LoyaltyDecaySystem : IActorSimulationSystem
{
    public string SystemId { get { return "mymod.loyalty_decay"; } }
    public int Priority { get { return 100; } }

    public void Tick(ActorSimulationContext context, int tickStep)
    {
        var actors = context.Registry.Enumerate(
            new ActorQueryBuilder()
                .ByKind(ActorKind.Citizen)
                .WithComponent("mymod.personality")
                .Build());
    }
}

_actors.RegisterSystem(new LoyaltyDecaySystem());
_actors.Tick(1, "mymod.sim");
```

## 8. Events and Persistence

```csharp
_actors.Subscribe(evt =>
{
    if (evt.EventType == ActorEventType.ComponentAdded)
    {
    }
});
```

- Persistent actors/components are saved through the actor save envelope.
- Unknown component payloads are preserved until a serializer is available.
- Component ids must be namespaced like `modid.component_name`.
