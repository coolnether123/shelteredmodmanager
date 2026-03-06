using System;
using System.Collections.Generic;

namespace ModAPI.Actors
{
    public enum ActorKind
    {
        Player = 0,
        Faction = 1,
        Citizen = 2,
        Visitor = 3,
        NeutralShelter = 4,
        Synthetic = 5,
        Custom = 6
    }

    public enum ActorLifecycleState
    {
        Unknown = 0,
        Registered = 1,
        Active = 2,
        Inactive = 3,
        Unloaded = 4,
        Destroyed = 5
    }

    public enum ActorPresenceState
    {
        Unknown = 0,
        InShelter = 1,
        Expedition = 2,
        Encounter = 3,
        Offscreen = 4
    }

    [Flags]
    public enum ActorFlags
    {
        None = 0,
        Persistent = 1,
        RuntimeOnly = 2,
        Synthetic = 4,
        Loaded = 8
    }

    public enum ActorDestroyReason
    {
        Unknown = 0,
        Explicit = 1,
        SessionReset = 2,
        MissingSource = 3,
        Replaced = 4
    }

    public enum ActorSortMode
    {
        ActorId = 0,
        CreatedTick = 1,
        UpdatedTick = 2
    }

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
        ExportCompleted = 10
    }

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

    public sealed class ActorRecordMutation
    {
        public ActorLifecycleState? LifecycleState { get; set; }
        public ActorPresenceState? PresenceState { get; set; }
        public ActorFlags? Flags { get; set; }
        public ActorOrigin Origin { get; set; }
        public long? UpdatedTick { get; set; }
    }

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

    [Serializable]
    public sealed class ActorSaveEnvelope
    {
        public int SchemaVersion;
        public List<ActorRecordSaveEntry> Actors;
        public List<string> ActiveSerializerComponentIds;
        public List<ActorMetadataEntry> Metadata;
    }

    [Serializable]
    public sealed class ActorRecordSaveEntry
    {
        public ActorRecord Record;
        public List<ActorComponentSaveEntry> Components;
    }

    [Serializable]
    public sealed class ActorComponentSaveEntry
    {
        public string ComponentId;
        public string OwnerModId;
        public int Version;
        public string PayloadJson;
    }

    [Serializable]
    public sealed class ActorMetadataEntry
    {
        public string Key;
        public string Value;
    }
}
