using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ModAPI.Actors;

namespace ShelteredAPI.Queues
{
    /// <summary>
    /// Runtime state of one queued player job at the time a snapshot was captured.
    /// </summary>
    public enum PlayerQueueEntryState
    {
        Pending = 0,
        InTransit = 1,
        Started = 2,
        Finished = 3,
        Unknown = 4
    }

    /// <summary>
    /// Cancellation state reported by a queued player job at snapshot time.
    /// </summary>
    public enum PlayerQueueCancelState
    {
        Active = 0,
        Cancelled = 1,
        ForceCancelled = 2,
        Unknown = 3
    }

    /// <summary>
    /// Kind of player-queue membership or ordering change observed by ShelteredAPI.
    /// </summary>
    public enum PlayerQueueChangeKind
    {
        Added = 0,
        Removed = 1,
        ClearedOrCancelled = 2,
        Reordered = 3,
        Restored = 4
    }

    /// <summary>
    /// Stable identity of the family member that owns a player queue.
    /// </summary>
    public sealed class PlayerQueueOwnerIdentity
    {
        private readonly ActorId _actorId;

        internal PlayerQueueOwnerIdentity(ActorId actorId, int uniqueMemberId, string displayName)
        {
            _actorId = CloneActorId(actorId);
            UniqueMemberId = uniqueMemberId;
            DisplayName = displayName ?? string.Empty;
        }

        /// <summary>
        /// Player actor identity. A copy is returned so caller mutation cannot alter the snapshot.
        /// </summary>
        public ActorId ActorId
        {
            get { return CloneActorId(_actorId); }
        }

        public int UniqueMemberId { get; private set; }
        public string DisplayName { get; private set; }

        private static ActorId CloneActorId(ActorId actorId)
        {
            return actorId == null ? null : new ActorId(actorId.Kind, actorId.LocalId, actorId.Domain);
        }
    }

    /// <summary>
    /// Copied target coordinates for a queued player job.
    /// </summary>
    public sealed class PlayerQueuePosition
    {
        internal PlayerQueuePosition(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public float X { get; private set; }
        public float Y { get; private set; }
        public float Z { get; private set; }
    }

    /// <summary>
    /// Read-only metadata copied from a vanilla player-queue entry.
    /// No live Job, Obj_Base, or FamilyMember reference is exposed.
    /// </summary>
    public sealed class PlayerQueueEntry
    {
        internal PlayerQueueEntry(
            int index,
            string jobType,
            string interactionType,
            PlayerQueueEntryState state,
            PlayerQueueCancelState cancelState,
            PlayerQueuePosition target,
            int targetObjectId)
        {
            Index = index;
            JobType = jobType ?? string.Empty;
            InteractionType = interactionType ?? string.Empty;
            State = state;
            CancelState = cancelState;
            Target = target;
            TargetObjectId = targetObjectId;
        }

        public int Index { get; private set; }
        public string JobType { get; private set; }
        public string InteractionType { get; private set; }
        public PlayerQueueEntryState State { get; private set; }
        public PlayerQueueCancelState CancelState { get; private set; }
        public PlayerQueuePosition Target { get; private set; }

        /// <summary>
        /// Stable vanilla object ID, or -1 when the queued job has no object target.
        /// </summary>
        public int TargetObjectId { get; private set; }
    }

    /// <summary>
    /// Immutable view of one family member's vanilla player queue.
    /// Capacity is observed metadata only; ShelteredAPI does not set capacity.
    /// </summary>
    public sealed class PlayerQueueSnapshot
    {
        private readonly ReadOnlyCollection<PlayerQueueEntry> _entries;
        private readonly byte[] _restorePayload;

        internal PlayerQueueSnapshot(
            bool isAvailable,
            PlayerQueueOwnerIdentity owner,
            int capacity,
            IList<PlayerQueueEntry> entries,
            string unavailableReason,
            string restoreBlockReason,
            byte[] restorePayload)
        {
            IsAvailable = isAvailable;
            Owner = owner;
            Capacity = capacity;
            List<PlayerQueueEntry> copiedEntries = entries == null
                ? new List<PlayerQueueEntry>()
                : new List<PlayerQueueEntry>(entries);
            _entries = copiedEntries.AsReadOnly();
            UnavailableReason = unavailableReason ?? string.Empty;
            RestoreBlockReason = restoreBlockReason ?? string.Empty;
            _restorePayload = restorePayload == null ? null : (byte[])restorePayload.Clone();
        }

        public bool IsAvailable { get; private set; }
        public PlayerQueueOwnerIdentity Owner { get; private set; }
        public int Capacity { get; private set; }
        public int Count { get { return _entries.Count; } }
        public bool IsFull { get { return Capacity >= 0 && Count >= Capacity; } }
        public IList<PlayerQueueEntry> Entries { get { return _entries; } }
        public string UnavailableReason { get; private set; }
        public string RestoreBlockReason { get; private set; }
        public bool CanRestore
        {
            get
            {
                return IsAvailable
                    && _restorePayload != null
                    && _restorePayload.Length > 0
                    && string.IsNullOrEmpty(RestoreBlockReason);
            }
        }

        internal byte[] CopyRestorePayload()
        {
            return _restorePayload == null ? null : (byte[])_restorePayload.Clone();
        }
    }

    /// <summary>
    /// Result from attempting to restore a previously captured player-queue snapshot.
    /// </summary>
    public sealed class PlayerQueueRestoreResult
    {
        internal PlayerQueueRestoreResult(bool success, string message, PlayerQueueSnapshot queue)
        {
            Success = success;
            Message = message ?? string.Empty;
            Queue = queue;
        }

        public bool Success { get; private set; }
        public string Message { get; private set; }
        public PlayerQueueSnapshot Queue { get; private set; }
    }

    /// <summary>
    /// Raised after player-queue membership, ordering, cancellation, or facade restore changes.
    /// </summary>
    public sealed class PlayerQueueChangedEventArgs : EventArgs
    {
        internal PlayerQueueChangedEventArgs(PlayerQueueChangeKind changeKind, PlayerQueueSnapshot queue)
        {
            ChangeKind = changeKind;
            Queue = queue;
        }

        public PlayerQueueChangeKind ChangeKind { get; private set; }
        public PlayerQueueSnapshot Queue { get; private set; }
        public PlayerQueueOwnerIdentity Owner { get { return Queue != null ? Queue.Owner : null; } }
    }
}
