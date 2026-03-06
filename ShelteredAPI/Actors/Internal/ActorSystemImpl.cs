using HarmonyLib;
using ModAPI.Core;
using ModAPI.Events;
using ModAPI.Harmony;
using ModAPI.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ModAPI.Actors.Internal
{
    [PatchPolicy(PatchDomain.World, "ShelteredActorSystemTicks",
        TargetBehavior = "Sheltered actor system registration and ticking from GameTime lifecycle updates",
        FailureMode = "Actor simulation systems stop registering or advancing with the game clock.",
        RollbackStrategy = "Disable the World patch domain or remove the Sheltered actor system tick patch host.")]
    internal sealed class ActorSystemImpl : IActorSystem, ISaveable
    {
        private const string SaveGroupName = "ModAPI_Actors_V1";
        private const int MaxRecentEvents = 256;
        private const string BuiltInOwner = "shelteredapi";

        private readonly object _sync = new object();
        private readonly Dictionary<ActorId, ActorRecord> _records = new Dictionary<ActorId, ActorRecord>();
        private readonly Dictionary<ActorId, Dictionary<string, ActorComponentSlot>> _components = new Dictionary<ActorId, Dictionary<string, ActorComponentSlot>>();
        private readonly Dictionary<ActorKind, HashSet<ActorId>> _kindIndex = new Dictionary<ActorKind, HashSet<ActorId>>();
        private readonly Dictionary<ActorLifecycleState, HashSet<ActorId>> _lifecycleIndex = new Dictionary<ActorLifecycleState, HashSet<ActorId>>();
        private readonly Dictionary<ActorPresenceState, HashSet<ActorId>> _presenceIndex = new Dictionary<ActorPresenceState, HashSet<ActorId>>();
        private readonly Dictionary<string, HashSet<ActorId>> _originIndex = new Dictionary<string, HashSet<ActorId>>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, HashSet<ActorId>> _componentIndex = new Dictionary<string, HashSet<ActorId>>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, IActorComponentSerializer> _serializers = new Dictionary<string, IActorComponentSerializer>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, int> _nextIdsByScope = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private readonly List<IActorSimulationSystem> _systems = new List<IActorSimulationSystem>();
        private readonly List<ActorSubscription> _subscriptions = new List<ActorSubscription>();
        private readonly List<ActorEventEnvelope> _recentEvents = new List<ActorEventEnvelope>();

        private bool _registered;
        private int _nextSubscriptionId = 1;
        private long _currentTick;

        public event Action<IActorRecord> ActorCreated;
        public event Action<IActorRecord, ActorDestroyReason> ActorDestroyed;
        public event Action<IActorRecord> ActorStateChanged;
        public event Action<ActorEventEnvelope> EventPublished;

        public int CurrentSchemaVersion { get { return 1; } }
        public long CurrentTick { get { lock (_sync) { return _currentTick; } } }

        internal ActorSystemImpl()
        {
            RegisterSerializerInternal(new ActorJsonComponentSerializer<ActorProfileComponent>(ActorProfileComponent.DefaultComponentId, 1), false);
            RegisterSerializerInternal(new ActorJsonComponentSerializer<ActorAttributeSetComponent>(ActorAttributeSetComponent.DefaultComponentId, 1), false);

            try { GameEvents.OnSessionStarted += HandleSessionReset; } catch { }
            try { GameEvents.OnNewGame += HandleSessionReset; } catch { }
        }

        public IActorRecord Get(ActorId id)
        {
            if (id == null) return null;
            lock (_sync)
            {
                ActorRecord record;
                return _records.TryGetValue(id, out record) ? record.Clone() : null;
            }
        }

        public bool TryGet(ActorId id, out IActorRecord actor)
        {
            actor = Get(id);
            return actor != null;
        }

        public IActorRecord Create(ActorCreateRequest request)
        {
            if (request == null) return null;
            EnsureRegistered();

            ActorRecord created = null;
            lock (_sync)
            {
                ActorId id = request.Id ?? AllocateIdLocked(request.Kind, request.Domain);
                if (id == null) return null;

                if (_records.ContainsKey(id))
                {
                    PublishLocked(ActorEventType.SerializationWarning, BuiltInOwner, id, null, "Actor id collision: " + id);
                    return _records[id].Clone();
                }

                long tick = request.CreatedTick ?? NowTick();
                created = new ActorRecord();
                created.Id = new ActorId(id.Kind, id.LocalId, id.Domain);
                created.LifecycleState = request.LifecycleState == ActorLifecycleState.Unknown ? ActorLifecycleState.Registered : request.LifecycleState;
                created.PresenceState = request.PresenceState;
                created.Flags = request.Flags;
                created.Origin = CloneOrigin(request.Origin) ?? BuildDefaultOrigin(id);
                created.CreatedTick = tick;
                created.UpdatedTick = request.UpdatedTick ?? tick;
                AddRecordLocked(created);
            }

            RaiseActorCreated(created.Clone());
            Publish(ActorEventType.ActorCreated, BuiltInOwner, created.Id, null, "Actor created");
            return created.Clone();
        }

        public bool Update(ActorId id, ActorRecordMutation mutation)
        {
            if (id == null || mutation == null) return false;

            ActorRecord changed = null;
            lock (_sync)
            {
                ActorRecord record;
                if (!_records.TryGetValue(id, out record)) return false;
                if (!ApplyMutationLocked(record, mutation)) return false;
                changed = record.Clone();
            }

            RaiseActorStateChanged(changed);
            Publish(ActorEventType.ActorStateChanged, BuiltInOwner, changed.Id, null, "Actor updated");
            return true;
        }

        public bool Destroy(ActorId id, ActorDestroyReason reason)
        {
            if (id == null) return false;

            ActorRecord removed = null;
            lock (_sync)
            {
                ActorRecord record;
                if (!_records.TryGetValue(id, out record)) return false;
                removed = record.Clone();
                RemoveRecordLocked(record);
            }

            RaiseActorDestroyed(removed, reason);
            Publish(ActorEventType.ActorDestroyed, BuiltInOwner, removed.Id, null, "Actor destroyed: " + reason);
            return true;
        }

        public IReadOnlyList<IActorRecord> Enumerate(ActorQuery query)
        {
            ActorQuery effective = query ?? new ActorQuery();
            List<ActorRecord> results = new List<ActorRecord>();

            lock (_sync)
            {
                IEnumerable<ActorId> candidateIds = ResolveCandidateIdsLocked(effective);
                foreach (ActorId id in candidateIds)
                {
                    ActorRecord record;
                    if (!_records.TryGetValue(id, out record) || record == null) continue;
                    if (!MatchesQueryLocked(record, effective)) continue;
                    results.Add(record.Clone());
                }
            }

            results.Sort(CreateSortComparison(effective.SortMode, effective.Descending));
            List<IActorRecord> cast = new List<IActorRecord>(results.Count);
            for (int i = 0; i < results.Count; i++) cast.Add(results[i]);
            return cast.ToReadOnlyList();
        }

        public ActorQueryBuilder Query()
        {
            return new ActorQueryBuilder();
        }

        public ActorComponentWriteResult Set(ActorId actorId, IActorComponent component, string sourceModId)
        {
            if (actorId == null || component == null) return ActorComponentWriteResult.MissingActor;

            ActorComponentWriteResult result;
            ActorEventType eventType;
            string message;
            lock (_sync)
            {
                result = SetComponentLocked(actorId, component, sourceModId, out eventType, out message);
            }

            if (!string.Equals(message, "Component unchanged", StringComparison.Ordinal)
                && (result == ActorComponentWriteResult.Added
                || result == ActorComponentWriteResult.Updated
                || result == ActorComponentWriteResult.Replaced
                || result == ActorComponentWriteResult.Merged))
            {
                Publish(eventType, sourceModId, actorId, component.ComponentId, message);
            }

            return result;
        }

        public ActorComponentWriteResult Set<TComponent>(ActorId actorId, TComponent component, string sourceModId)
            where TComponent : class, IActorComponent
        {
            return Set(actorId, (IActorComponent)component, sourceModId);
        }

        public bool TryGet<TComponent>(ActorId actorId, out TComponent component)
            where TComponent : class, IActorComponent
        {
            component = null;
            IActorComponent raw;
            if (!TryGet(actorId, ResolveComponentId(typeof(TComponent)), out raw)) return false;
            component = raw as TComponent;
            return component != null;
        }

        public bool TryGet(ActorId actorId, string componentId, out IActorComponent component)
        {
            component = null;
            if (actorId == null || string.IsNullOrEmpty(componentId)) return false;

            lock (_sync)
            {
                Dictionary<string, ActorComponentSlot> map;
                if (!_components.TryGetValue(actorId, out map) || map == null) return false;

                ActorComponentSlot slot;
                if (!map.TryGetValue(componentId, out slot) || slot == null || slot.Component == null) return false;
                component = CloneComponentLocked(slot);
                return component != null;
            }
        }

        public IActorComponent GetByComponentId(ActorId actorId, string componentId)
        {
            IActorComponent component;
            return TryGet(actorId, componentId, out component) ? component : null;
        }

        public bool HasComponent(ActorId actorId, string componentId)
        {
            if (actorId == null || string.IsNullOrEmpty(componentId)) return false;
            lock (_sync)
            {
                Dictionary<string, ActorComponentSlot> map;
                return _components.TryGetValue(actorId, out map) && map != null && map.ContainsKey(componentId);
            }
        }

        public bool Remove(ActorId actorId, string componentId, string sourceModId)
        {
            if (actorId == null || string.IsNullOrEmpty(componentId)) return false;

            bool removed = false;
            lock (_sync)
            {
                Dictionary<string, ActorComponentSlot> map;
                if (!_components.TryGetValue(actorId, out map) || map == null) return false;

                ActorComponentSlot slot;
                if (!map.TryGetValue(componentId, out slot) || slot == null) return false;
                if (!string.IsNullOrEmpty(sourceModId)
                    && !string.Equals(slot.OwnerModId ?? string.Empty, sourceModId ?? string.Empty, StringComparison.OrdinalIgnoreCase))
                    return false;

                map.Remove(componentId);
                RemoveFromStringIndex(_componentIndex, componentId, actorId);
                if (map.Count == 0) _components.Remove(actorId);
                removed = true;
            }

            if (removed) Publish(ActorEventType.ComponentRemoved, sourceModId, actorId, componentId, "Component removed");
            return removed;
        }

        public IReadOnlyList<IActorComponent> GetAllComponents(ActorId actorId)
        {
            List<IActorComponent> components = new List<IActorComponent>();
            if (actorId == null) return components.ToReadOnlyList();

            lock (_sync)
            {
                Dictionary<string, ActorComponentSlot> map;
                if (!_components.TryGetValue(actorId, out map) || map == null) return components.ToReadOnlyList();

                foreach (ActorComponentSlot slot in map.Values)
                {
                    if (slot == null || slot.Component == null) continue;
                    IActorComponent clone = CloneComponentLocked(slot);
                    if (clone != null) components.Add(clone);
                }
            }

            return components.ToReadOnlyList();
        }

        public IReadOnlyList<string> GetComponentIds(ActorId actorId)
        {
            List<string> ids = new List<string>();
            if (actorId == null) return ids.ToReadOnlyList();

            lock (_sync)
            {
                Dictionary<string, ActorComponentSlot> map;
                if (!_components.TryGetValue(actorId, out map) || map == null) return ids.ToReadOnlyList();
                foreach (string key in map.Keys) ids.Add(key);
            }

            ids.Sort(StringComparer.OrdinalIgnoreCase);
            return ids.ToReadOnlyList();
        }

        public IDisposable Subscribe(Action<ActorEventEnvelope> handler)
        {
            return Subscribe(null, handler);
        }

        public IDisposable Subscribe(Predicate<ActorEventEnvelope> filter, Action<ActorEventEnvelope> handler)
        {
            if (handler == null) return NullSubscription.Instance;

            lock (_sync)
            {
                ActorSubscription subscription = new ActorSubscription(_nextSubscriptionId++, filter, handler, RemoveSubscription);
                _subscriptions.Add(subscription);
                return subscription;
            }
        }

        public IReadOnlyList<ActorEventEnvelope> GetRecentEvents()
        {
            List<ActorEventEnvelope> events = new List<ActorEventEnvelope>();
            lock (_sync)
            {
                for (int i = 0; i < _recentEvents.Count; i++)
                    events.Add(CloneEvent(_recentEvents[i]));
            }
            return events.ToReadOnlyList();
        }

        public void RegisterSystem(IActorSimulationSystem system)
        {
            if (system == null || string.IsNullOrEmpty(system.SystemId)) return;
            lock (_sync)
            {
                for (int i = 0; i < _systems.Count; i++)
                {
                    if (!string.Equals(_systems[i].SystemId, system.SystemId, StringComparison.OrdinalIgnoreCase)) continue;
                    _systems[i] = system;
                    SortSystemsLocked();
                    return;
                }

                _systems.Add(system);
                SortSystemsLocked();
            }
        }

        public bool UnregisterSystem(string systemId)
        {
            if (string.IsNullOrEmpty(systemId)) return false;
            lock (_sync)
            {
                for (int i = _systems.Count - 1; i >= 0; i--)
                {
                    if (!string.Equals(_systems[i].SystemId, systemId, StringComparison.OrdinalIgnoreCase)) continue;
                    _systems.RemoveAt(i);
                    return true;
                }
            }
            return false;
        }

        public IReadOnlyList<IActorSimulationSystem> GetSystems()
        {
            List<IActorSimulationSystem> copy;
            lock (_sync)
            {
                copy = new List<IActorSimulationSystem>(_systems);
            }
            return copy.ToReadOnlyList();
        }

        public void Tick(int tickStep, string streamName)
        {
            if (tickStep <= 0) tickStep = 1;

            List<IActorSimulationSystem> systems;
            long tick;
            lock (_sync)
            {
                _currentTick += tickStep;
                tick = _currentTick;
                systems = new List<IActorSimulationSystem>(_systems);
            }

            string resolvedStream = string.IsNullOrEmpty(streamName) ? "ShelteredAPI.Actors" : streamName;
            ActorSimulationContext context = new ActorSimulationContext(this, this, this, ModRandom.GetStream(resolvedStream), tick);

            for (int i = 0; i < systems.Count; i++)
            {
                try { systems[i].Tick(context, tickStep); }
                catch (Exception ex)
                {
                    Publish(ActorEventType.SerializationError, BuiltInOwner, null, null, "Simulation system '" + systems[i].SystemId + "' failed: " + ex.Message);
                }
            }
        }

        public void RegisterSerializer(IActorComponentSerializer serializer)
        {
            RegisterSerializerInternal(serializer, true);
        }

        public bool TryGetSerializer(string componentId, out IActorComponentSerializer serializer)
        {
            if (string.IsNullOrEmpty(componentId))
            {
                serializer = null;
                return false;
            }

            lock (_sync)
            {
                return _serializers.TryGetValue(componentId, out serializer) && serializer != null;
            }
        }

        public string ExportJson()
        {
            ActorSaveEnvelope envelope = new ActorSaveEnvelope();
            envelope.SchemaVersion = CurrentSchemaVersion;
            envelope.Actors = new List<ActorRecordSaveEntry>();
            envelope.ActiveSerializerComponentIds = new List<string>();
            envelope.Metadata = new List<ActorMetadataEntry>();

            lock (_sync)
            {
                List<string> serializerIds = _serializers.Keys.ToList();
                serializerIds.Sort(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < serializerIds.Count; i++)
                    envelope.ActiveSerializerComponentIds.Add(serializerIds[i]);

                envelope.Metadata.Add(new ActorMetadataEntry { Key = "currentTick", Value = _currentTick.ToString() });

                List<ActorRecord> records = _records.Values.ToList();
                records.Sort(delegate(ActorRecord left, ActorRecord right) { return left.Id.CompareTo(right.Id); });

                for (int i = 0; i < records.Count; i++)
                {
                    ActorRecord record = records[i];
                    if ((record.Flags & ActorFlags.RuntimeOnly) == ActorFlags.RuntimeOnly) continue;
                    if ((record.Flags & ActorFlags.Persistent) != ActorFlags.Persistent) continue;

                    ActorRecordSaveEntry entry = new ActorRecordSaveEntry();
                    entry.Record = record.Clone();
                    entry.Components = new List<ActorComponentSaveEntry>();

                    Dictionary<string, ActorComponentSlot> map;
                    if (_components.TryGetValue(record.Id, out map) && map != null)
                    {
                        List<string> componentIds = map.Keys.ToList();
                        componentIds.Sort(StringComparer.OrdinalIgnoreCase);
                        for (int c = 0; c < componentIds.Count; c++)
                        {
                            ActorComponentSaveEntry componentEntry = BuildComponentSaveEntryLocked(map[componentIds[c]]);
                            if (componentEntry != null) entry.Components.Add(componentEntry);
                        }
                    }

                    envelope.Actors.Add(entry);
                }
            }

            string json = JsonUtility.ToJson(envelope, true);
            Publish(ActorEventType.ExportCompleted, BuiltInOwner, null, null, "Exported actor registry");
            return json;
        }

        public bool ImportJson(string json)
        {
            if (string.IsNullOrEmpty(json)) return false;

            ActorSaveEnvelope envelope = null;
            try { envelope = JsonUtility.FromJson<ActorSaveEnvelope>(json); }
            catch (Exception ex)
            {
                Publish(ActorEventType.SerializationError, BuiltInOwner, null, null, "Import failed: " + ex.Message);
                return false;
            }

            if (envelope == null) return false;

            lock (_sync)
            {
                ClearStateLocked();

                if (envelope.Metadata != null)
                {
                    for (int i = 0; i < envelope.Metadata.Count; i++)
                    {
                        ActorMetadataEntry entry = envelope.Metadata[i];
                        if (entry == null) continue;
                        if (!string.Equals(entry.Key, "currentTick", StringComparison.OrdinalIgnoreCase)) continue;
                        long parsed;
                        if (long.TryParse(entry.Value, out parsed)) _currentTick = parsed;
                    }
                }

                if (envelope.Actors != null)
                {
                    for (int i = 0; i < envelope.Actors.Count; i++)
                    {
                        ActorRecordSaveEntry entry = envelope.Actors[i];
                        if (entry == null || entry.Record == null || entry.Record.Id == null) continue;

                        ActorRecord record = entry.Record.Clone();
                        AddRecordLocked(record);
                        if (entry.Components == null) continue;

                        for (int c = 0; c < entry.Components.Count; c++)
                        {
                            ActorComponentSaveEntry componentEntry = entry.Components[c];
                            if (componentEntry == null || string.IsNullOrEmpty(componentEntry.ComponentId)) continue;
                            RestoreComponentLocked(record.Id, componentEntry);
                        }
                    }
                }
            }

            Publish(ActorEventType.ImportCompleted, BuiltInOwner, null, null, "Imported actor registry");
            return true;
        }

        public bool IsRelocationEnabled() { return true; }
        public bool IsReadyForLoad() { return true; }

        public bool SaveLoad(SaveData data)
        {
            if (data == null) return false;

            try { data.GroupStart(SaveGroupName); }
            catch (SaveData.MissingGroupException) { return true; }

            string json = data.isSaving ? ExportJson() : string.Empty;
            data.SaveLoad("json", ref json);

            if (data.isLoading && !string.IsNullOrEmpty(json))
                ImportJson(json);

            data.GroupEnd();
            return true;
        }

        internal void EnsureRegistered()
        {
            if (_registered) return;
            SaveManager manager = SaveManager.instance;
            if (manager == null) return;
            manager.RegisterSaveable(this);
            _registered = true;
        }

        internal void Update()
        {
            EnsureRegistered();
            RefreshLiveActors();
        }

        private void HandleSessionReset()
        {
            lock (_sync)
            {
                ClearStateLocked();
                _currentTick = 0;
            }
        }

        private void RefreshLiveActors()
        {
            HashSet<int> seenFamily = new HashSet<int>();
            HashSet<int> seenVisitors = new HashSet<int>();

            try
            {
                FamilyManager familyManager = FamilyManager.Instance;
                if (familyManager != null)
                {
                    IList<FamilyMember> members = familyManager.GetAllFamilyMembers();
                    if (members != null)
                    {
                        for (int i = 0; i < members.Count; i++)
                        {
                            FamilyMember member = members[i];
                            if (member == null) continue;
                            seenFamily.Add(member.GetId());
                            UpsertFamilyActor(member);
                        }
                    }
                }
            }
            catch { }

            try
            {
                NpcVisitManager manager = NpcVisitManager.Instance;
                if (manager != null && manager.Visitors != null)
                {
                    for (int i = 0; i < manager.Visitors.Count; i++)
                    {
                        NpcVisitor visitor = manager.Visitors[i];
                        if (visitor == null) continue;
                        seenVisitors.Add(visitor.npcId);
                        UpsertVisitorActor(visitor);
                    }
                }
            }
            catch { }

            List<ActorRecord> changed = new List<ActorRecord>();
            lock (_sync)
            {
                foreach (ActorRecord record in _records.Values)
                {
                    if (record == null || record.Id == null || record.Origin == null) continue;
                    if (!string.Equals(record.Origin.SourceModId ?? string.Empty, "core", StringComparison.OrdinalIgnoreCase)) continue;

                    bool missing = false;
                    if (record.Id.Kind == ActorKind.Player) missing = !seenFamily.Contains(record.Id.LocalId);
                    else if (record.Id.Kind == ActorKind.Visitor) missing = !seenVisitors.Contains(record.Id.LocalId);
                    if (!missing) continue;

                    ActorFlags flags = record.Flags & ~ActorFlags.Loaded;
                    if (UpdateRecordStateLocked(record, ActorLifecycleState.Unloaded, ActorPresenceState.Offscreen, flags, NowTick()))
                        changed.Add(record.Clone());
                }
            }

            for (int i = 0; i < changed.Count; i++)
            {
                RaiseActorStateChanged(changed[i]);
                Publish(ActorEventType.ActorStateChanged, BuiltInOwner, changed[i].Id, null, "Core actor unloaded");
            }
        }

        private void UpsertFamilyActor(FamilyMember member)
        {
            ActorId id = new ActorId(ActorKind.Player, member.GetId(), string.Empty);
            ActorRecord snapshot = null;
            bool created = false;
            bool changed = false;

            lock (_sync)
            {
                ActorRecord record;
                if (!_records.TryGetValue(id, out record))
                {
                    record = new ActorRecord();
                    record.Id = new ActorId(id.Kind, id.LocalId, id.Domain);
                    record.LifecycleState = ActorLifecycleState.Active;
                    record.PresenceState = ResolvePresence(member);
                    record.Flags = ActorFlags.Persistent | ActorFlags.Loaded;
                    record.Origin = ActorOrigin.Core("family");
                    record.CreatedTick = NowTick();
                    record.UpdatedTick = record.CreatedTick;
                    AddRecordLocked(record);
                    created = true;
                }
                else
                {
                    ActorFlags desiredFlags = record.Flags | ActorFlags.Persistent | ActorFlags.Loaded;
                    changed = UpdateRecordStateLocked(record, ActorLifecycleState.Active, ResolvePresence(member), desiredFlags, NowTick());
                }

                snapshot = record.Clone();
            }

            Set(id, BuildProfile(member), BuiltInOwner);

            if (created)
            {
                RaiseActorCreated(snapshot);
                Publish(ActorEventType.ActorCreated, BuiltInOwner, id, null, "Family actor discovered");
            }
            else if (changed)
            {
                RaiseActorStateChanged(snapshot);
                Publish(ActorEventType.ActorStateChanged, BuiltInOwner, id, null, "Family actor refreshed");
            }
        }

        private void UpsertVisitorActor(NpcVisitor visitor)
        {
            ActorId id = new ActorId(ActorKind.Visitor, visitor.npcId, string.Empty);
            ActorRecord snapshot = null;
            bool created = false;
            bool changed = false;

            lock (_sync)
            {
                ActorRecord record;
                if (!_records.TryGetValue(id, out record))
                {
                    record = new ActorRecord();
                    record.Id = new ActorId(id.Kind, id.LocalId, id.Domain);
                    record.LifecycleState = ActorLifecycleState.Active;
                    record.PresenceState = ResolvePresence(visitor);
                    record.Flags = ActorFlags.Loaded;
                    record.Origin = ActorOrigin.Core("visitor");
                    record.CreatedTick = NowTick();
                    record.UpdatedTick = record.CreatedTick;
                    AddRecordLocked(record);
                    created = true;
                }
                else
                {
                    ActorFlags desiredFlags = record.Flags | ActorFlags.Loaded;
                    changed = UpdateRecordStateLocked(record, ActorLifecycleState.Active, ResolvePresence(visitor), desiredFlags, NowTick());
                }

                snapshot = record.Clone();
            }

            Set(id, BuildProfile(visitor), BuiltInOwner);

            if (created)
            {
                RaiseActorCreated(snapshot);
                Publish(ActorEventType.ActorCreated, BuiltInOwner, id, null, "Visitor actor discovered");
            }
            else if (changed)
            {
                RaiseActorStateChanged(snapshot);
                Publish(ActorEventType.ActorStateChanged, BuiltInOwner, id, null, "Visitor actor refreshed");
            }
        }

        private void RegisterSerializerInternal(IActorComponentSerializer serializer, bool publish)
        {
            if (serializer == null || string.IsNullOrEmpty(serializer.ComponentId)) return;

            lock (_sync)
            {
                _serializers[serializer.ComponentId] = serializer;
                HydrateUnknownSlotsLocked(serializer.ComponentId);
            }

            if (publish)
                Publish(ActorEventType.SerializerRegistered, BuiltInOwner, null, serializer.ComponentId, "Serializer registered");
        }

        private void HydrateUnknownSlotsLocked(string componentId)
        {
            IActorComponentSerializer serializer;
            if (!_serializers.TryGetValue(componentId, out serializer) || serializer == null) return;

            foreach (Dictionary<string, ActorComponentSlot> map in _components.Values)
            {
                if (map == null) continue;

                ActorComponentSlot slot;
                if (!map.TryGetValue(componentId, out slot) || slot == null || slot.Component != null || string.IsNullOrEmpty(slot.RawPayload))
                    continue;

                try
                {
                    slot.Component = serializer.Deserialize(slot.RawPayload, slot.Version);
                    slot.RawPayload = null;
                }
                catch (Exception ex)
                {
                    PublishLocked(ActorEventType.SerializationWarning, BuiltInOwner, null, componentId, "Deferred hydration failed: " + ex.Message);
                }
            }
        }

        private ActorComponentSaveEntry BuildComponentSaveEntryLocked(ActorComponentSlot slot)
        {
            if (slot == null || string.IsNullOrEmpty(slot.ComponentId)) return null;

            if (!string.IsNullOrEmpty(slot.RawPayload))
            {
                ActorComponentSaveEntry rawEntry = new ActorComponentSaveEntry();
                rawEntry.ComponentId = slot.ComponentId;
                rawEntry.OwnerModId = slot.OwnerModId ?? string.Empty;
                rawEntry.Version = slot.Version;
                rawEntry.PayloadJson = slot.RawPayload;
                return rawEntry;
            }

            if (slot.Component == null) return null;

            IActorComponentSerializer serializer;
            if (!_serializers.TryGetValue(slot.ComponentId, out serializer) || serializer == null)
            {
                PublishLocked(ActorEventType.SerializationWarning, BuiltInOwner, null, slot.ComponentId, "Missing serializer for component '" + slot.ComponentId + "'");
                return null;
            }

            try
            {
                ActorComponentSaveEntry entry = new ActorComponentSaveEntry();
                entry.ComponentId = slot.ComponentId;
                entry.OwnerModId = slot.OwnerModId ?? string.Empty;
                entry.Version = serializer.CurrentVersion;
                entry.PayloadJson = serializer.Serialize(slot.Component) ?? string.Empty;
                return entry;
            }
            catch (Exception ex)
            {
                PublishLocked(ActorEventType.SerializationError, BuiltInOwner, null, slot.ComponentId, "Component serialization failed: " + ex.Message);
                return null;
            }
        }

        private void RestoreComponentLocked(ActorId actorId, ActorComponentSaveEntry entry)
        {
            Dictionary<string, ActorComponentSlot> map;
            if (!_components.TryGetValue(actorId, out map))
            {
                map = new Dictionary<string, ActorComponentSlot>(StringComparer.OrdinalIgnoreCase);
                _components[actorId] = map;
            }

            ActorComponentSlot slot = new ActorComponentSlot();
            slot.ComponentId = entry.ComponentId;
            slot.OwnerModId = entry.OwnerModId ?? string.Empty;
            slot.Version = entry.Version;

            IActorComponentSerializer serializer;
            if (_serializers.TryGetValue(entry.ComponentId, out serializer) && serializer != null)
            {
                try { slot.Component = serializer.Deserialize(entry.PayloadJson ?? string.Empty, entry.Version); }
                catch (Exception ex)
                {
                    slot.RawPayload = entry.PayloadJson ?? string.Empty;
                    PublishLocked(ActorEventType.SerializationWarning, BuiltInOwner, actorId, entry.ComponentId, "Component load failed: " + ex.Message);
                }
            }
            else
            {
                slot.RawPayload = entry.PayloadJson ?? string.Empty;
                PublishLocked(ActorEventType.SerializationWarning, BuiltInOwner, actorId, entry.ComponentId, "Unknown component preserved");
            }

            map[entry.ComponentId] = slot;
            AddToStringIndex(_componentIndex, entry.ComponentId, actorId);
        }

        private ActorComponentWriteResult SetComponentLocked(ActorId actorId, IActorComponent component, string sourceModId, out ActorEventType eventType, out string message)
        {
            eventType = ActorEventType.ComponentUpdated;
            message = "Component updated";

            ActorRecord record;
            if (!_records.TryGetValue(actorId, out record))
            {
                message = "Actor not found";
                return ActorComponentWriteResult.MissingActor;
            }

            string componentId = component.ComponentId ?? string.Empty;
            if (!IsValidComponentId(componentId))
            {
                message = "Component ids must be namespaced";
                PublishLocked(ActorEventType.SerializationWarning, sourceModId, actorId, componentId, message);
                return ActorComponentWriteResult.Rejected;
            }

            if (!IsOwnedComponentId(componentId, sourceModId))
            {
                message = "Component ownership mismatch";
                PublishLocked(ActorEventType.SerializationWarning, sourceModId, actorId, componentId, message);
                return ActorComponentWriteResult.Rejected;
            }

            Dictionary<string, ActorComponentSlot> map;
            if (!_components.TryGetValue(actorId, out map))
            {
                map = new Dictionary<string, ActorComponentSlot>(StringComparer.OrdinalIgnoreCase);
                _components[actorId] = map;
            }

            ActorComponentSlot slot;
            if (!map.TryGetValue(componentId, out slot))
            {
                slot = new ActorComponentSlot();
                slot.ComponentId = componentId;
                slot.Component = component;
                slot.OwnerModId = sourceModId ?? string.Empty;
                slot.Version = component.Version;
                map[componentId] = slot;
                AddToStringIndex(_componentIndex, componentId, actorId);
                eventType = ActorEventType.ComponentAdded;
                message = "Component added";
                return ActorComponentWriteResult.Added;
            }

            if (!string.IsNullOrEmpty(slot.OwnerModId)
                && !string.Equals(slot.OwnerModId, sourceModId ?? string.Empty, StringComparison.OrdinalIgnoreCase)
                && component.ConflictPolicy == ActorConflictPolicy.Reject)
            {
                message = "Component rejected by conflict policy";
                return ActorComponentWriteResult.Rejected;
            }

            if (component.ConflictPolicy == ActorConflictPolicy.Merge)
            {
                IMergeableActorComponent mergeable = component as IMergeableActorComponent;
                if (mergeable != null)
                {
                    slot.Component = mergeable.Merge(slot.Component);
                    slot.OwnerModId = sourceModId ?? slot.OwnerModId;
                    slot.Version = slot.Component != null ? slot.Component.Version : component.Version;
                    slot.RawPayload = null;
                    message = "Component merged";
                    return ActorComponentWriteResult.Merged;
                }
            }

            if (AreComponentsEquivalentLocked(slot, component))
            {
                message = "Component unchanged";
                return ActorComponentWriteResult.Updated;
            }

            bool ownerChanged = !string.Equals(slot.OwnerModId ?? string.Empty, sourceModId ?? string.Empty, StringComparison.OrdinalIgnoreCase);
            slot.Component = component;
            slot.OwnerModId = sourceModId ?? string.Empty;
            slot.Version = component.Version;
            slot.RawPayload = null;
            message = ownerChanged ? "Component replaced" : "Component updated";
            return ownerChanged ? ActorComponentWriteResult.Replaced : ActorComponentWriteResult.Updated;
        }

        private bool AreComponentsEquivalentLocked(ActorComponentSlot existing, IActorComponent incoming)
        {
            if (existing == null || existing.Component == null || incoming == null) return false;

            IActorComponentSerializer serializer;
            if (_serializers.TryGetValue(existing.ComponentId, out serializer) && serializer != null)
            {
                try
                {
                    string currentJson = serializer.Serialize(existing.Component) ?? string.Empty;
                    string newJson = serializer.Serialize(incoming) ?? string.Empty;
                    return string.Equals(currentJson, newJson, StringComparison.Ordinal);
                }
                catch { }
            }

            return false;
        }

        private IActorComponent CloneComponentLocked(ActorComponentSlot slot)
        {
            if (slot == null || slot.Component == null) return null;

            IActorComponentSerializer serializer;
            if (_serializers.TryGetValue(slot.ComponentId, out serializer) && serializer != null)
            {
                try
                {
                    string json = serializer.Serialize(slot.Component);
                    return serializer.Deserialize(json, serializer.CurrentVersion);
                }
                catch { }
            }

            return slot.Component;
        }

        private bool ApplyMutationLocked(ActorRecord record, ActorRecordMutation mutation)
        {
            bool changed = UpdateRecordStateLocked(
                record,
                mutation.LifecycleState ?? record.LifecycleState,
                mutation.PresenceState ?? record.PresenceState,
                mutation.Flags ?? record.Flags,
                mutation.UpdatedTick ?? NowTick());

            if (mutation.Origin != null)
            {
                RemoveFromStringIndex(_originIndex, NormalizeKey(record.Origin != null ? record.Origin.SourceModId : null), record.Id);
                record.Origin = CloneOrigin(mutation.Origin);
                AddToStringIndex(_originIndex, NormalizeKey(record.Origin != null ? record.Origin.SourceModId : null), record.Id);
                changed = true;
            }

            return changed;
        }

        private bool UpdateRecordStateLocked(ActorRecord record, ActorLifecycleState lifecycle, ActorPresenceState presence, ActorFlags flags, long updatedTick)
        {
            bool changed = false;

            if (record.LifecycleState != lifecycle)
            {
                RemoveFromEnumIndex(_lifecycleIndex, record.LifecycleState, record.Id);
                record.LifecycleState = lifecycle;
                AddToEnumIndex(_lifecycleIndex, record.LifecycleState, record.Id);
                changed = true;
            }

            if (record.PresenceState != presence)
            {
                RemoveFromEnumIndex(_presenceIndex, record.PresenceState, record.Id);
                record.PresenceState = presence;
                AddToEnumIndex(_presenceIndex, record.PresenceState, record.Id);
                changed = true;
            }

            if (record.Flags != flags)
            {
                record.Flags = flags;
                changed = true;
            }

            if (changed) record.UpdatedTick = updatedTick;
            return changed;
        }

        private IEnumerable<ActorId> ResolveCandidateIdsLocked(ActorQuery query)
        {
            HashSet<ActorId> candidate = null;

            if (query.Kind.HasValue)
                candidate = Intersect(candidate, GetIndexValues(_kindIndex, query.Kind.Value));
            if (query.LifecycleState.HasValue)
                candidate = Intersect(candidate, GetIndexValues(_lifecycleIndex, query.LifecycleState.Value));
            if (query.PresenceState.HasValue)
                candidate = Intersect(candidate, GetIndexValues(_presenceIndex, query.PresenceState.Value));
            if (!string.IsNullOrEmpty(query.OriginModId))
                candidate = Intersect(candidate, GetIndexValues(_originIndex, NormalizeKey(query.OriginModId)));
            if (query.ComponentIds != null)
            {
                for (int i = 0; i < query.ComponentIds.Count; i++)
                    candidate = Intersect(candidate, GetIndexValues(_componentIndex, query.ComponentIds[i]));
            }

            if (candidate != null) return candidate;
            return _records.Keys;
        }

        private bool MatchesQueryLocked(ActorRecord record, ActorQuery query)
        {
            if (query.PersistentOnly.HasValue)
            {
                bool persistent = (record.Flags & ActorFlags.Persistent) == ActorFlags.Persistent;
                if (query.PersistentOnly.Value != persistent) return false;
            }

            if (query.Predicate != null && !query.Predicate(record)) return false;
            return true;
        }

        private static Comparison<ActorRecord> CreateSortComparison(ActorSortMode sortMode, bool descending)
        {
            return delegate(ActorRecord left, ActorRecord right)
            {
                int result;
                switch (sortMode)
                {
                    case ActorSortMode.CreatedTick:
                        result = left.CreatedTick.CompareTo(right.CreatedTick);
                        break;
                    case ActorSortMode.UpdatedTick:
                        result = left.UpdatedTick.CompareTo(right.UpdatedTick);
                        break;
                    default:
                        result = left.Id.CompareTo(right.Id);
                        break;
                }
                return descending ? -result : result;
            };
        }

        private void AddRecordLocked(ActorRecord record)
        {
            _records[record.Id] = record;
            AddToEnumIndex(_kindIndex, record.Id.Kind, record.Id);
            AddToEnumIndex(_lifecycleIndex, record.LifecycleState, record.Id);
            AddToEnumIndex(_presenceIndex, record.PresenceState, record.Id);
            AddToStringIndex(_originIndex, NormalizeKey(record.Origin != null ? record.Origin.SourceModId : null), record.Id);
        }

        private void RemoveRecordLocked(ActorRecord record)
        {
            if (record == null || record.Id == null) return;

            _records.Remove(record.Id);
            _components.Remove(record.Id);
            RemoveFromEnumIndex(_kindIndex, record.Id.Kind, record.Id);
            RemoveFromEnumIndex(_lifecycleIndex, record.LifecycleState, record.Id);
            RemoveFromEnumIndex(_presenceIndex, record.PresenceState, record.Id);
            RemoveFromStringIndex(_originIndex, NormalizeKey(record.Origin != null ? record.Origin.SourceModId : null), record.Id);

            List<string> keys = _componentIndex.Keys.ToList();
            for (int i = 0; i < keys.Count; i++)
                RemoveFromStringIndex(_componentIndex, keys[i], record.Id);
        }

        private void ClearStateLocked()
        {
            _records.Clear();
            _components.Clear();
            _kindIndex.Clear();
            _lifecycleIndex.Clear();
            _presenceIndex.Clear();
            _originIndex.Clear();
            _componentIndex.Clear();
            _nextIdsByScope.Clear();
        }

        private void RaiseActorCreated(IActorRecord record)
        {
            Action<IActorRecord> handler = ActorCreated;
            if (handler != null) handler(record);
        }

        private void RaiseActorDestroyed(IActorRecord record, ActorDestroyReason reason)
        {
            Action<IActorRecord, ActorDestroyReason> handler = ActorDestroyed;
            if (handler != null) handler(record, reason);
        }

        private void RaiseActorStateChanged(IActorRecord record)
        {
            Action<IActorRecord> handler = ActorStateChanged;
            if (handler != null) handler(record);
        }

        private void Publish(ActorEventType type, string sourceModId, ActorId actorId, string componentId, string message)
        {
            ActorEventEnvelope envelope;
            List<ActorSubscription> subscriptions;
            lock (_sync)
            {
                envelope = PublishLocked(type, sourceModId, actorId, componentId, message);
                subscriptions = new List<ActorSubscription>(_subscriptions);
            }

            Action<ActorEventEnvelope> handler = EventPublished;
            if (handler != null) handler(CloneEvent(envelope));

            for (int i = 0; i < subscriptions.Count; i++)
            {
                try { subscriptions[i].Dispatch(envelope); }
                catch { }
            }
        }

        private ActorEventEnvelope PublishLocked(ActorEventType type, string sourceModId, ActorId actorId, string componentId, string message)
        {
            ActorEventEnvelope envelope = new ActorEventEnvelope();
            envelope.Tick = _currentTick;
            envelope.SourceModId = sourceModId ?? string.Empty;
            envelope.ActorId = actorId == null ? null : new ActorId(actorId.Kind, actorId.LocalId, actorId.Domain);
            envelope.EventType = type;
            envelope.ComponentId = componentId ?? string.Empty;
            envelope.Message = message ?? string.Empty;

            _recentEvents.Add(envelope);
            if (_recentEvents.Count > MaxRecentEvents)
                _recentEvents.RemoveAt(0);

            return envelope;
        }

        private void RemoveSubscription(int id)
        {
            lock (_sync)
            {
                for (int i = _subscriptions.Count - 1; i >= 0; i--)
                {
                    if (_subscriptions[i].Id != id) continue;
                    _subscriptions.RemoveAt(i);
                    return;
                }
            }
        }

        private void SortSystemsLocked()
        {
            _systems.Sort(delegate(IActorSimulationSystem left, IActorSimulationSystem right)
            {
                int byPriority = left.Priority.CompareTo(right.Priority);
                if (byPriority != 0) return byPriority;
                return string.Compare(left.SystemId, right.SystemId, StringComparison.OrdinalIgnoreCase);
            });
        }

        private ActorId AllocateIdLocked(ActorKind kind, string domain)
        {
            string scope = kind + "|" + NormalizeKey(domain);
            int next;
            if (!_nextIdsByScope.TryGetValue(scope, out next)) next = 1;

            ActorId id = new ActorId(kind, next, domain ?? string.Empty);
            while (_records.ContainsKey(id))
            {
                next++;
                id = new ActorId(kind, next, domain ?? string.Empty);
            }

            _nextIdsByScope[scope] = next + 1;
            return id;
        }

        private static ActorOrigin CloneOrigin(ActorOrigin origin)
        {
            if (origin == null) return null;
            ActorOrigin clone = new ActorOrigin();
            clone.SourceModId = origin.SourceModId;
            clone.SourceKey = origin.SourceKey;
            clone.Generator = origin.Generator;
            return clone;
        }

        private static ActorOrigin BuildDefaultOrigin(ActorId id)
        {
            ActorOrigin origin = new ActorOrigin();
            origin.SourceModId = string.IsNullOrEmpty(id.Domain) ? "mod" : id.Domain;
            origin.SourceKey = id.ToString();
            origin.Generator = "actor-system";
            return origin;
        }

        private static ActorProfileComponent BuildProfile(FamilyMember member)
        {
            ActorProfileComponent component = new ActorProfileComponent();
            if (member == null) return component;
            component.FirstName = member.firstName;
            component.LastName = member.lastName;
            component.Health = member.health;
            component.MaxHealth = member.maxHealth;
            return component;
        }

        private static ActorProfileComponent BuildProfile(NpcVisitor visitor)
        {
            ActorProfileComponent component = new ActorProfileComponent();
            if (visitor == null) return component;
            component.FirstName = visitor.firstName;
            component.LastName = visitor.lastName;
            component.Health = visitor.health;
            component.MaxHealth = visitor.maxHealth;
            return component;
        }

        private static ActorPresenceState ResolvePresence(FamilyMember member)
        {
            if (member == null) return ActorPresenceState.Offscreen;
            return member.isAway ? ActorPresenceState.Expedition : ActorPresenceState.InShelter;
        }

        private static ActorPresenceState ResolvePresence(NpcVisitor visitor)
        {
            return visitor == null ? ActorPresenceState.Offscreen : ActorPresenceState.InShelter;
        }

        private static string NormalizeKey(string value)
        {
            return string.IsNullOrEmpty(value) ? string.Empty : value.Trim();
        }

        private static bool IsValidComponentId(string componentId)
        {
            return !string.IsNullOrEmpty(componentId) && componentId.Contains(".");
        }

        private static bool IsOwnedComponentId(string componentId, string ownerModId)
        {
            if (string.IsNullOrEmpty(componentId) || string.IsNullOrEmpty(ownerModId)) return true;
            if (string.Equals(ownerModId, BuiltInOwner, StringComparison.OrdinalIgnoreCase))
                return componentId.StartsWith("sheltered.", StringComparison.OrdinalIgnoreCase);
            return componentId.StartsWith(ownerModId + ".", StringComparison.OrdinalIgnoreCase);
        }

        private static long NowTick()
        {
            try { return (((GameTime.Day - 1) * 1440L) + (GameTime.Hour * 60L) + GameTime.Minute) * 60L; }
            catch { return (long)Time.time; }
        }

        private static string ResolveComponentId(Type type)
        {
            if (type == null) return string.Empty;
            try
            {
                IActorComponent component = Activator.CreateInstance(type) as IActorComponent;
                return component != null ? component.ComponentId : type.FullName;
            }
            catch { return type.FullName; }
        }

        private static ActorEventEnvelope CloneEvent(ActorEventEnvelope envelope)
        {
            if (envelope == null) return null;
            ActorEventEnvelope clone = new ActorEventEnvelope();
            clone.Tick = envelope.Tick;
            clone.SourceModId = envelope.SourceModId;
            clone.ActorId = envelope.ActorId == null ? null : new ActorId(envelope.ActorId.Kind, envelope.ActorId.LocalId, envelope.ActorId.Domain);
            clone.EventType = envelope.EventType;
            clone.ComponentId = envelope.ComponentId;
            clone.Message = envelope.Message;
            return clone;
        }

        private static void AddToEnumIndex<T>(Dictionary<T, HashSet<ActorId>> index, T key, ActorId actorId)
        {
            HashSet<ActorId> set;
            if (!index.TryGetValue(key, out set))
            {
                set = new HashSet<ActorId>();
                index[key] = set;
            }
            set.Add(actorId);
        }

        private static void RemoveFromEnumIndex<T>(Dictionary<T, HashSet<ActorId>> index, T key, ActorId actorId)
        {
            HashSet<ActorId> set;
            if (!index.TryGetValue(key, out set) || set == null) return;
            set.Remove(actorId);
            if (set.Count == 0) index.Remove(key);
        }

        private static void AddToStringIndex(Dictionary<string, HashSet<ActorId>> index, string key, ActorId actorId)
        {
            key = NormalizeKey(key);
            HashSet<ActorId> set;
            if (!index.TryGetValue(key, out set))
            {
                set = new HashSet<ActorId>();
                index[key] = set;
            }
            set.Add(actorId);
        }

        private static void RemoveFromStringIndex(Dictionary<string, HashSet<ActorId>> index, string key, ActorId actorId)
        {
            key = NormalizeKey(key);
            HashSet<ActorId> set;
            if (!index.TryGetValue(key, out set) || set == null) return;
            set.Remove(actorId);
            if (set.Count == 0) index.Remove(key);
        }

        private static HashSet<ActorId> GetIndexValues<T>(Dictionary<T, HashSet<ActorId>> index, T key)
        {
            HashSet<ActorId> set;
            if (!index.TryGetValue(key, out set) || set == null) return new HashSet<ActorId>();
            return new HashSet<ActorId>(set);
        }

        private static HashSet<ActorId> Intersect(HashSet<ActorId> current, HashSet<ActorId> next)
        {
            if (current == null) return next;
            current.IntersectWith(next);
            return current;
        }

        private sealed class ActorComponentSlot
        {
            public string ComponentId;
            public IActorComponent Component;
            public string OwnerModId;
            public int Version;
            public string RawPayload;
        }

        private sealed class ActorSubscription : IDisposable
        {
            private readonly Predicate<ActorEventEnvelope> _filter;
            private readonly Action<ActorEventEnvelope> _handler;
            private readonly Action<int> _disposeAction;
            private bool _disposed;

            public ActorSubscription(int id, Predicate<ActorEventEnvelope> filter, Action<ActorEventEnvelope> handler, Action<int> disposeAction)
            {
                Id = id;
                _filter = filter;
                _handler = handler;
                _disposeAction = disposeAction;
            }

            public int Id { get; private set; }

            public void Dispatch(ActorEventEnvelope envelope)
            {
                if (_disposed || _handler == null || envelope == null) return;
                if (_filter != null && !_filter(envelope)) return;
                _handler(CloneEvent(envelope));
            }

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                if (_disposeAction != null) _disposeAction(Id);
            }
        }

        private sealed class NullSubscription : IDisposable
        {
            public static readonly NullSubscription Instance = new NullSubscription();
            public void Dispose() { }
        }

        [HarmonyPatch(typeof(GameTime), "Awake")]
        private static class GameTime_Awake
        {
            private static void Postfix()
            {
                ActorSystem.InternalInstance.EnsureRegistered();
            }
        }

        [HarmonyPatch(typeof(GameTime), "Update")]
        private static class GameTime_Update
        {
            private static void Postfix()
            {
                ActorSystem.InternalInstance.Update();
            }
        }
    }
}
