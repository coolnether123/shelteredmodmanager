using System;
using System.Collections.Generic;

namespace ModAPI.Actors
{
    /// <summary>
    /// Broad actor category used for identity, sorting, and cross-system filtering.
    /// Game integrations may map several host object types into the same ModAPI actor kind.
    /// </summary>
    public enum ActorKind
    {
        Player = 0,
        Faction = 1,
        Citizen = 2,
        Visitor = 3,
        NeutralGroup = 4,
        Synthetic = 5,
        Custom = 6
    }

    /// <summary>
    /// Lifetime state of an actor record in the current runtime.
    /// Use this to distinguish active actors from unloaded or intentionally destroyed identities.
    /// </summary>
    public enum ActorLifecycleState
    {
        Unknown = 0,
        Registered = 1,
        Active = 2,
        Inactive = 3,
        Unloaded = 4,
        Destroyed = 5
    }

    /// <summary>
    /// Coarse location of an actor in the game flow.
    /// This is intentionally host-neutral so mods can reason about home, travel, and encounter state consistently.
    /// </summary>
    public enum ActorPresenceState
    {
        Unknown = 0,
        Home = 1,
        Travel = 2,
        Encounter = 3,
        Offscreen = 4
    }

    /// <summary>
    /// Persistence and runtime-origin flags attached to an actor record.
    /// Flags are additive; callers should test for the bits they need rather than comparing exact values.
    /// </summary>
    [Flags]
    public enum ActorFlags
    {
        None = 0,
        Persistent = 1,
        RuntimeOnly = 2,
        Synthetic = 4,
        Loaded = 8
    }

    /// <summary>
    /// Reason an actor identity was removed from the registry.
    /// Event consumers can use this to decide whether to clean up save data or wait for a later reload.
    /// </summary>
    public enum ActorDestroyReason
    {
        Unknown = 0,
        Explicit = 1,
        SessionReset = 2,
        MissingSource = 3,
        Replaced = 4
    }

    /// <summary>
    /// Built-in sort orders supported by <see cref="ActorQuery"/>.
    /// </summary>
    public enum ActorSortMode
    {
        ActorId = 0,
        CreatedTick = 1,
        UpdatedTick = 2
    }

    /// <summary>
    /// Event categories emitted by the actor system.
    /// These values are diagnostic and integration-facing; mods should not persist behavior-critical state from them alone.
    /// </summary>
    public enum ActorEventType
    {
        ActorCreated = 0,
        ActorDestroyed = 1,
        ActorStateChanged = 2,
        ComponentAdded = 3,
        ComponentUpdated = 4,
        ComponentRemoved = 5,
        SerializationWarning = 6,
        SerializationError = 7,
        SerializerRegistered = 8,
        ImportCompleted = 9,
        ExportCompleted = 10,
        AdapterFailed = 11,
        AdapterRecovered = 12,
        SimulationFailed = 13,
        SimulationRecovered = 14,
        LiveSyncFailed = 15,
        LiveSyncRecovered = 16
    }

    /// <summary>
    /// Stable actor identity composed from kind, local ID, and optional domain.
    /// Use the domain to keep mod-created or integration-specific IDs from colliding with game-owned IDs.
    /// </summary>
    [Serializable]
    public sealed class ActorId : IEquatable<ActorId>, IComparable<ActorId>
    {
        public ActorId()
        {
        }

        public ActorId(ActorKind kind, int localId, string domain)
        {
            Kind = kind;
            LocalId = localId;
            Domain = domain ?? string.Empty;
        }

        public ActorKind Kind;
        public int LocalId;
        public string Domain;

        public bool Equals(ActorId other)
        {
            if (ReferenceEquals(other, null)) return false;
            return Kind == other.Kind
                && LocalId == other.LocalId
                && string.Equals(NormalizeDomain(Domain), NormalizeDomain(other.Domain), StringComparison.OrdinalIgnoreCase);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as ActorId);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)Kind;
                hash = (hash * 397) ^ LocalId;
                hash = (hash * 397) ^ NormalizeDomain(Domain).ToLowerInvariant().GetHashCode();
                return hash;
            }
        }

        public int CompareTo(ActorId other)
        {
            if (ReferenceEquals(other, null)) return 1;

            int kindCompare = Kind.CompareTo(other.Kind);
            if (kindCompare != 0) return kindCompare;

            int domainCompare = string.Compare(
                NormalizeDomain(Domain),
                NormalizeDomain(other.Domain),
                StringComparison.OrdinalIgnoreCase);
            if (domainCompare != 0) return domainCompare;

            return LocalId.CompareTo(other.LocalId);
        }

        public override string ToString()
        {
            string domain = NormalizeDomain(Domain);
            if (string.IsNullOrEmpty(domain))
                return Kind + ":" + LocalId;
            return Kind + ":" + domain + ":" + LocalId;
        }

        private static string NormalizeDomain(string domain)
        {
            return domain ?? string.Empty;
        }
    }

    /// <summary>
    /// Describes where an actor record came from.
    /// Mods should set this when creating synthetic actors so diagnostics and save data can trace ownership.
    /// </summary>
    [Serializable]
    public sealed class ActorOrigin
    {
        public string SourceModId;
        public string SourceKey;
        public string Generator;

        public static ActorOrigin Core(string sourceKey)
        {
            return new ActorOrigin
            {
                SourceModId = "core",
                SourceKey = sourceKey ?? string.Empty,
                Generator = "core"
            };
        }
    }

    /// <summary>
    /// Link from an external runtime identifier to an actor ID.
    /// Bindings make it possible to resolve an actor from a host object key without exposing host types in ModAPI.
    /// </summary>
    [Serializable]
    public sealed class ActorBinding
    {
        public string BindingType;
        public string BindingKey;
        public string SourceModId;
        public bool Persistent;

        public ActorBinding Clone()
        {
            return new ActorBinding
            {
                BindingType = BindingType,
                BindingKey = BindingKey,
                SourceModId = SourceModId,
                Persistent = Persistent
            };
        }

        public override string ToString()
        {
            return (BindingType ?? string.Empty) + ":" + (BindingKey ?? string.Empty);
        }
    }

    /// <summary>
    /// Read-only view of actor identity, lifecycle, presence, and ownership metadata.
    /// Query APIs return this interface so callers cannot mutate registry records accidentally.
    /// </summary>
    public interface IActorRecord
    {
        ActorId Id { get; }
        ActorLifecycleState LifecycleState { get; }
        ActorPresenceState PresenceState { get; }
        ActorFlags Flags { get; }
        ActorOrigin Origin { get; }
        long CreatedTick { get; }
        long UpdatedTick { get; }
    }

    /// <summary>
    /// Serializable actor record stored by the runtime.
    /// Prefer registry methods for mutation so lifecycle events and version tracking stay consistent.
    /// </summary>
    [Serializable]
    public sealed class ActorRecord : IActorRecord
    {
        public ActorId Id;
        public ActorLifecycleState LifecycleState;
        public ActorPresenceState PresenceState;
        public ActorFlags Flags;
        public ActorOrigin Origin;
        public long CreatedTick;
        public long UpdatedTick;

        ActorId IActorRecord.Id { get { return Id; } }
        ActorLifecycleState IActorRecord.LifecycleState { get { return LifecycleState; } }
        ActorPresenceState IActorRecord.PresenceState { get { return PresenceState; } }
        ActorFlags IActorRecord.Flags { get { return Flags; } }
        ActorOrigin IActorRecord.Origin { get { return Origin; } }
        long IActorRecord.CreatedTick { get { return CreatedTick; } }
        long IActorRecord.UpdatedTick { get { return UpdatedTick; } }

        public ActorRecord Clone()
        {
            return new ActorRecord
            {
                Id = Id == null ? null : new ActorId(Id.Kind, Id.LocalId, Id.Domain),
                LifecycleState = LifecycleState,
                PresenceState = PresenceState,
                Flags = Flags,
                Origin = Origin == null
                    ? null
                    : new ActorOrigin
                    {
                        SourceModId = Origin.SourceModId,
                        SourceKey = Origin.SourceKey,
                        Generator = Origin.Generator
                    },
                CreatedTick = CreatedTick,
                UpdatedTick = UpdatedTick
            };
        }
    }

    /// <summary>
    /// Request object for creating or ensuring an actor.
    /// Leave <see cref="Id"/> null when the runtime should allocate an identity from kind and domain.
    /// </summary>
    public sealed class ActorCreateRequest
    {
        public ActorId Id { get; set; }
        public ActorKind Kind { get; set; }
        public string Domain { get; set; }
        public ActorLifecycleState LifecycleState { get; set; }
        public ActorPresenceState PresenceState { get; set; }
        public ActorFlags Flags { get; set; }
        public ActorOrigin Origin { get; set; }
        public long? CreatedTick { get; set; }
        public long? UpdatedTick { get; set; }
    }

    /// <summary>
    /// Partial actor update used by registry mutation APIs.
    /// Null properties mean "do not change this field".
    /// </summary>
    public sealed class ActorRecordMutation
    {
        public ActorLifecycleState? LifecycleState { get; set; }
        public ActorPresenceState? PresenceState { get; set; }
        public ActorFlags? Flags { get; set; }
        public ActorOrigin Origin { get; set; }
        public long? UpdatedTick { get; set; }
    }

    /// <summary>
    /// Filter object for actor enumeration.
    /// Construct directly for serialization-friendly queries or use <see cref="ActorQueryBuilder"/> for fluent code.
    /// </summary>
    public sealed class ActorQuery
    {
        public ActorKind? Kind { get; set; }
        public ActorLifecycleState? LifecycleState { get; set; }
        public ActorPresenceState? PresenceState { get; set; }
        public string OriginModId { get; set; }
        public List<string> ComponentIds { get; set; }
        public bool? PersistentOnly { get; set; }
        public ActorSortMode SortMode { get; set; }
        public bool Descending { get; set; }
        public Predicate<IActorRecord> Predicate { get; set; }

        public ActorQuery()
        {
            ComponentIds = new List<string>();
            SortMode = ActorSortMode.ActorId;
        }

        public ActorQuery Clone()
        {
            ActorQuery copy = new ActorQuery();
            copy.Kind = Kind;
            copy.LifecycleState = LifecycleState;
            copy.PresenceState = PresenceState;
            copy.OriginModId = OriginModId;
            copy.PersistentOnly = PersistentOnly;
            copy.SortMode = SortMode;
            copy.Descending = Descending;
            copy.Predicate = Predicate;
            if (ComponentIds != null)
            {
                for (int i = 0; i < ComponentIds.Count; i++)
                    copy.ComponentIds.Add(ComponentIds[i]);
            }
            return copy;
        }
    }

    /// <summary>
    /// Fluent builder for common actor queries.
    /// Each call refines the query; call <see cref="Build"/> before passing it to the registry.
    /// </summary>
    public sealed class ActorQueryBuilder
    {
        private readonly ActorQuery _query = new ActorQuery();

        public ActorQueryBuilder ByKind(ActorKind kind)
        {
            _query.Kind = kind;
            return this;
        }

        public ActorQueryBuilder WithLifecycle(ActorLifecycleState state)
        {
            _query.LifecycleState = state;
            return this;
        }

        public ActorQueryBuilder WithPresence(ActorPresenceState state)
        {
            _query.PresenceState = state;
            return this;
        }

        public ActorQueryBuilder FromOrigin(string originModId)
        {
            _query.OriginModId = originModId;
            return this;
        }

        public ActorQueryBuilder WithComponent(string componentId)
        {
            if (!string.IsNullOrEmpty(componentId))
                _query.ComponentIds.Add(componentId);
            return this;
        }

        public ActorQueryBuilder OnlyPersistent()
        {
            _query.PersistentOnly = true;
            return this;
        }

        public ActorQueryBuilder OnlyRuntime()
        {
            _query.PersistentOnly = false;
            return this;
        }

        public ActorQueryBuilder OrderBy(ActorSortMode sortMode, bool descending)
        {
            _query.SortMode = sortMode;
            _query.Descending = descending;
            return this;
        }

        public ActorQueryBuilder Where(Predicate<IActorRecord> predicate)
        {
            _query.Predicate = predicate;
            return this;
        }

        public ActorQuery Build()
        {
            return _query.Clone();
        }
    }

    /// <summary>
    /// Event payload published when actor state, components, or runtime integration status changes.
    /// </summary>
    [Serializable]
    public sealed class ActorEventEnvelope
    {
        public long Tick;
        public string SourceModId;
        public ActorId ActorId;
        public ActorEventType EventType;
        public string ComponentId;
        public string Message;
    }

    /// <summary>
    /// Root save payload for actor registry persistence.
    /// Component payloads remain serializer-owned JSON strings to keep ModAPI decoupled from component assemblies.
    /// </summary>
    [Serializable]
    public sealed class ActorSaveEnvelope
    {
        public int SchemaVersion;
        public List<ActorRecordSaveEntry> Actors;
        public List<string> ActiveSerializerComponentIds;
        public List<ActorMetadataEntry> Metadata;
    }

    /// <summary>
    /// Serialized actor plus its component and binding records.
    /// </summary>
    [Serializable]
    public sealed class ActorRecordSaveEntry
    {
        public ActorRecord Record;
        public List<ActorComponentSaveEntry> Components;
        public List<ActorBinding> Bindings;
    }

    /// <summary>
    /// Serialized component payload owned by a registered <see cref="IActorComponentSerializer"/>.
    /// </summary>
    [Serializable]
    public sealed class ActorComponentSaveEntry
    {
        public string ComponentId;
        public string OwnerModId;
        public int Version;
        public string PayloadJson;
    }

    /// <summary>
    /// Extensible key/value metadata stored beside actor save data.
    /// </summary>
    [Serializable]
    public sealed class ActorMetadataEntry
    {
        public string Key;
        public string Value;
    }
}
