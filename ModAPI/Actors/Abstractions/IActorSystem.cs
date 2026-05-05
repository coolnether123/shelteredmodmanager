using System;
using System.Collections.Generic;
using ModAPI.Core;

namespace ModAPI.Actors
{
    /// <summary>
    /// Creates, updates, queries, and destroys actor records in the shared actor graph.
    /// Use this when a mod needs a stable identity for a family member, visitor, scenario actor, or synthetic actor.
    /// </summary>
    public interface IActorRegistry
    {
        IActorRecord Get(ActorId id);
        bool TryGet(ActorId id, out IActorRecord actor);
        IActorRecord Create(ActorCreateRequest request);
        IActorRecord Ensure(ActorCreateRequest request);
        bool Update(ActorId id, ActorRecordMutation mutation);
        bool Destroy(ActorId id, ActorDestroyReason reason);
        IReadOnlyList<IActorRecord> Enumerate(ActorQuery query);
        ActorQueryBuilder Query();

        event Action<IActorRecord> ActorCreated;
        event Action<IActorRecord, ActorDestroyReason> ActorDestroyed;
        event Action<IActorRecord> ActorStateChanged;
    }

    /// <summary>
    /// Stores mod-owned components on actors without requiring mods to share concrete runtime types.
    /// Components should be small, serializable state packets identified by <see cref="IActorComponent.ComponentId"/>.
    /// </summary>
    public interface IActorComponentStore
    {
        ActorComponentWriteResult Set(ActorId actorId, IActorComponent component, string sourceModId);
        ActorComponentWriteResult Set<TComponent>(ActorId actorId, TComponent component, string sourceModId)
            where TComponent : class, IActorComponent;
        bool TryGet<TComponent>(ActorId actorId, out TComponent component)
            where TComponent : class, IActorComponent;
        bool TryGet(ActorId actorId, string componentId, out IActorComponent component);
        IActorComponent GetByComponentId(ActorId actorId, string componentId);
        bool HasComponent(ActorId actorId, string componentId);
        bool Remove(ActorId actorId, string componentId, string sourceModId);
        IReadOnlyList<IActorComponent> GetAllComponents(ActorId actorId);
        IReadOnlyList<string> GetComponentIds(ActorId actorId);
    }

    /// <summary>
    /// Maps host objects or external identifiers to actor IDs.
    /// Use bindings when the actor must be found again from a game object, save key, or integration-specific handle.
    /// </summary>
    public interface IActorBindingStore
    {
        bool Bind(ActorId actorId, ActorBinding binding, bool replaceExisting);
        bool Unbind(string bindingType, string bindingKey);
        bool TryResolve(string bindingType, string bindingKey, out ActorId actorId);
        IReadOnlyList<ActorBinding> GetBindings(ActorId actorId);
        IReadOnlyList<ActorId> GetBoundActors(string bindingType);
    }

    /// <summary>
    /// Publishes actor-system events and exposes a short recent-event buffer for diagnostics.
    /// Subscribe here for actor lifecycle and component changes instead of polling the registry every frame.
    /// </summary>
    public interface IActorEvents
    {
        event Action<ActorEventEnvelope> EventPublished;

        IDisposable Subscribe(Action<ActorEventEnvelope> handler);
        IDisposable Subscribe(Predicate<ActorEventEnvelope> filter, Action<ActorEventEnvelope> handler);
        IReadOnlyList<ActorEventEnvelope> GetRecentEvents();
    }

    /// <summary>
    /// Deterministic actor simulation hook run by the actor scheduler.
    /// Implement this for systems that advance actor-owned state on ModAPI ticks.
    /// </summary>
    public interface IActorSimulationSystem
    {
        string SystemId { get; }
        int Priority { get; }
        void Tick(ActorSimulationContext context, int tickStep);
    }

    /// <summary>
    /// Synchronizes external game state into the actor graph.
    /// Adapters are the bridge between host runtime objects and ModAPI's neutral actor records.
    /// </summary>
    public interface IActorAdapter
    {
        string AdapterId { get; }
        int Priority { get; }
        void Synchronize(IActorSystem actors, long currentTick);
    }

    /// <summary>
    /// Adapter variant that can skip work when ticks, registry versions, or live bindings have not changed.
    /// Use this for expensive host scans.
    /// </summary>
    public interface IConditionalActorAdapter : IActorAdapter
    {
        bool ShouldSynchronize(ActorAdapterContext context);
    }

    /// <summary>
    /// Registers live-sync adapters with the actor runtime.
    /// Game integrations usually register adapters during bootstrap; mods may register their own bridges when needed.
    /// </summary>
    public interface IActorAdapterRegistry
    {
        void RegisterAdapter(IActorAdapter adapter);
        bool UnregisterAdapter(string adapterId);
        IReadOnlyList<IActorAdapter> GetAdapters();
    }

    /// <summary>
    /// Read-only diagnostics for actor runtime health.
    /// Use this for debug panels, health checks, and support logs without mutating actor state.
    /// </summary>
    public interface IActorDiagnostics
    {
        ActorRuntimeSnapshot GetRuntimeSnapshot();
        IReadOnlyList<ActorFailureRecord> GetFailureRecords();
    }

    /// <summary>
    /// Orders and runs actor simulation systems.
    /// Manual callers should tick this only from the main runtime loop unless they own the full actor lifecycle.
    /// </summary>
    public interface IActorSimulationScheduler
    {
        long CurrentTick { get; }

        void RegisterSystem(IActorSimulationSystem system);
        bool UnregisterSystem(string systemId);
        IReadOnlyList<IActorSimulationSystem> GetSystems();
        void Tick(int tickStep, string streamName);
    }

    /// <summary>
    /// Serializes actor records, bindings, and components through registered component serializers.
    /// Register serializers before importing saved actors so custom components can be restored.
    /// </summary>
    public interface IActorSerializationService
    {
        int CurrentSchemaVersion { get; }

        void RegisterSerializer(IActorComponentSerializer serializer);
        bool TryGetSerializer(string componentId, out IActorComponentSerializer serializer);
        string ExportJson();
        bool ImportJson(string json);
    }

    /// <summary>
    /// Complete actor API surface exposed by a game runtime.
    /// Prefer this aggregate when a caller needs registry, components, bindings, events, simulation, and diagnostics together.
    /// </summary>
    public interface IActorSystem :
        IActorRegistry,
        IActorComponentStore,
        IActorBindingStore,
        IActorEvents,
        IActorAdapterRegistry,
        IActorDiagnostics,
        IActorSimulationScheduler,
        IActorSerializationService
    {
    }

    /// <summary>
    /// Runtime services passed to an <see cref="IActorSimulationSystem"/> for one scheduler tick.
    /// The context keeps simulation code focused on actor state and deterministic random streams.
    /// </summary>
    public sealed class ActorSimulationContext
    {
        public ActorSimulationContext(
            IActorRegistry registry,
            IActorComponentStore components,
            IActorEvents eventsApi,
            ModRandomStream random,
            long currentTick)
        {
            Registry = registry;
            Components = components;
            Events = eventsApi;
            Random = random;
            CurrentTick = currentTick;
        }

        public IActorRegistry Registry { get; private set; }
        public IActorComponentStore Components { get; private set; }
        public IActorEvents Events { get; private set; }
        public ModRandomStream Random { get; private set; }
        public long CurrentTick { get; private set; }
    }
}
