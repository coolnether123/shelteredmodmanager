using System;
using System.Collections.Generic;
using ModAPI.Core;

namespace ModAPI.Actors
{
    public interface IActorRegistry
    {
        IActorRecord Get(ActorId id);
        bool TryGet(ActorId id, out IActorRecord actor);
        IActorRecord Create(ActorCreateRequest request);
        bool Update(ActorId id, ActorRecordMutation mutation);
        bool Destroy(ActorId id, ActorDestroyReason reason);
        IReadOnlyList<IActorRecord> Enumerate(ActorQuery query);
        ActorQueryBuilder Query();

        event Action<IActorRecord> ActorCreated;
        event Action<IActorRecord, ActorDestroyReason> ActorDestroyed;
        event Action<IActorRecord> ActorStateChanged;
    }

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

    public interface IActorEvents
    {
        event Action<ActorEventEnvelope> EventPublished;

        IDisposable Subscribe(Action<ActorEventEnvelope> handler);
        IDisposable Subscribe(Predicate<ActorEventEnvelope> filter, Action<ActorEventEnvelope> handler);
        IReadOnlyList<ActorEventEnvelope> GetRecentEvents();
    }

    public interface IActorSimulationSystem
    {
        string SystemId { get; }
        int Priority { get; }
        void Tick(ActorSimulationContext context, int tickStep);
    }

    public interface IActorSimulationScheduler
    {
        long CurrentTick { get; }

        void RegisterSystem(IActorSimulationSystem system);
        bool UnregisterSystem(string systemId);
        IReadOnlyList<IActorSimulationSystem> GetSystems();
        void Tick(int tickStep, string streamName);
    }

    public interface IActorSerializationService
    {
        int CurrentSchemaVersion { get; }

        void RegisterSerializer(IActorComponentSerializer serializer);
        bool TryGetSerializer(string componentId, out IActorComponentSerializer serializer);
        string ExportJson();
        bool ImportJson(string json);
    }

    public interface IActorSystem :
        IActorRegistry,
        IActorComponentStore,
        IActorEvents,
        IActorSimulationScheduler,
        IActorSerializationService
    {
    }

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
