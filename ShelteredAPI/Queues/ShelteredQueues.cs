using System;
using ModAPI.Actors;
using ShelteredAPI.Characters.Abstractions;
using ShelteredAPI.Queues.Internal;

namespace ShelteredAPI.Queues
{
    /// <summary>
    /// Stable facade for inspecting and conservatively restoring vanilla family-member player queues.
    /// </summary>
    public static class ShelteredQueues
    {
        /// <summary>
        /// Raised after a vanilla player queue changes membership/order or after a successful facade restore.
        /// </summary>
        public static event Action<PlayerQueueChangedEventArgs> QueueChanged
        {
            add { PlayerQueueRuntime.QueueChanged += value; }
            remove { PlayerQueueRuntime.QueueChanged -= value; }
        }

        /// <summary>
        /// Queries current player-queue metadata for a family-member actor.
        /// This inexpensive query view is not a restore token; use <see cref="SnapshotQueue(ActorId)"/> for restore.
        /// </summary>
        public static PlayerQueueSnapshot GetPlayerQueue(ActorId owner)
        {
            return PlayerQueueRuntime.Capture(owner, false);
        }

        /// <summary>
        /// Queries current player-queue metadata for a real family-member character proxy.
        /// </summary>
        public static PlayerQueueSnapshot GetPlayerQueue(ICharacterProxy owner)
        {
            return PlayerQueueRuntime.Capture(owner, false);
        }

        /// <summary>
        /// Queries current player-queue metadata using a vanilla family-member unique ID.
        /// </summary>
        public static PlayerQueueSnapshot GetPlayerQueue(int uniqueMemberId)
        {
            return PlayerQueueRuntime.Capture(uniqueMemberId, false);
        }

        /// <summary>
        /// Captures a restore-capable snapshot when queued work is safe to reconstruct.
        /// </summary>
        public static PlayerQueueSnapshot SnapshotQueue(ActorId owner)
        {
            return PlayerQueueRuntime.Capture(owner, true);
        }

        /// <summary>
        /// Captures a restore-capable snapshot for a real family-member character proxy.
        /// </summary>
        public static PlayerQueueSnapshot SnapshotQueue(ICharacterProxy owner)
        {
            return PlayerQueueRuntime.Capture(owner, true);
        }

        /// <summary>
        /// Captures a restore-capable snapshot using a vanilla family-member unique ID.
        /// </summary>
        public static PlayerQueueSnapshot SnapshotQueue(int uniqueMemberId)
        {
            return PlayerQueueRuntime.Capture(uniqueMemberId, true);
        }

        /// <summary>
        /// Restores pending safe-to-reconstruct work into an empty live player queue.
        /// Capacity is validated as metadata and is never changed by this method.
        /// </summary>
        public static PlayerQueueRestoreResult RestoreQueue(PlayerQueueSnapshot snapshot)
        {
            return PlayerQueueRuntime.Restore(snapshot);
        }
    }
}
