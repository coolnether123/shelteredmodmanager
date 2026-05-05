using System;

namespace ModAPI.Actors
{
    /// <summary>
    /// Runtime area that produced an actor-system failure record.
    /// </summary>
    public enum ActorFailureKind
    {
        Unknown = 0,
        Adapter = 1,
        SimulationSystem = 2,
        LiveSync = 3,
        Serialization = 4
    }

    /// <summary>
    /// State passed to conditional actor adapters before synchronization.
    /// Use this to skip expensive live scans when the relevant runtime counters have not changed.
    /// </summary>
    [Serializable]
    public sealed class ActorAdapterContext
    {
        public string AdapterId { get; set; }
        public long CurrentTick { get; set; }
        public long UpdateSequence { get; set; }
        public long LastSynchronizedTick { get; set; }
        public long LastSuccessfulTick { get; set; }
        public long LastSynchronizedUpdateSequence { get; set; }
        public long LastSynchronizedRegistryVersion { get; set; }
        public bool IsTickAdvanced { get; set; }
        public bool HasLiveActorChanges { get; set; }
        public bool HasRegistryChanges { get; set; }
        public bool ShouldRunByDefault { get; set; }
        public int ConsecutiveFailureCount { get; set; }
    }

    /// <summary>
    /// Point-in-time actor runtime counters for diagnostics and health displays.
    /// This snapshot is read-only telemetry, not a save contract.
    /// </summary>
    [Serializable]
    public sealed class ActorRuntimeSnapshot
    {
        public long CurrentTick { get; set; }
        public long UpdateSequence { get; set; }
        public long RegistryVersion { get; set; }
        public long LastObservedGameTick { get; set; }
        public long LastLiveRefreshTick { get; set; }
        public long LastLiveRefreshUpdateSequence { get; set; }
        public long LastAdapterRunTick { get; set; }
        public long LastAdapterRunUpdateSequence { get; set; }
        public int ActorCount { get; set; }
        public int ComponentCount { get; set; }
        public int BindingCount { get; set; }
        public int AdapterCount { get; set; }
        public int SimulationSystemCount { get; set; }
        public int RecentEventCount { get; set; }
        public int ActiveFailureCount { get; set; }
    }

    /// <summary>
    /// Aggregated failure information for actor adapters, simulation systems, live sync, or serialization.
    /// Repeated failures are counted so logs can stay readable while diagnostics remain actionable.
    /// </summary>
    [Serializable]
    public sealed class ActorFailureRecord
    {
        public string FailureKey { get; set; }
        public ActorFailureKind FailureKind { get; set; }
        public string SubjectId { get; set; }
        public string SourceModId { get; set; }
        public string Message { get; set; }
        public string ExceptionType { get; set; }
        public long FirstTick { get; set; }
        public long LastTick { get; set; }
        public int Count { get; set; }
        public int SuppressedCount { get; set; }
        public bool IsActive { get; set; }

        public ActorFailureRecord Clone()
        {
            return new ActorFailureRecord
            {
                FailureKey = FailureKey,
                FailureKind = FailureKind,
                SubjectId = SubjectId,
                SourceModId = SourceModId,
                Message = Message,
                ExceptionType = ExceptionType,
                FirstTick = FirstTick,
                LastTick = LastTick,
                Count = Count,
                SuppressedCount = SuppressedCount,
                IsActive = IsActive
            };
        }
    }
}
