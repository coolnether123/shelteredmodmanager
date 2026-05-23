using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using ModAPI.Actors;
using ShelteredAPI.Actors;
using ShelteredAPI.Characters.Abstractions;
using UnityEngine;

namespace ShelteredAPI.Queues.Internal
{
    /// <summary>
    /// Keeps vanilla FamilyMember, JobQueue, Job, and SaveData access behind the public queue facade.
    /// </summary>
    internal static class PlayerQueueRuntime
    {
        private const string SnapshotGroupName = "ShelteredAPI_PlayerQueueSnapshot";
        private const int UnknownCapacity = -1;
        private static readonly FieldInfo JobListField = typeof(JobQueue).GetField("jobs", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo QueueCapacityField = typeof(JobQueue).GetField("max_size", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo CurrentQueueField = typeof(FamilyMember).GetField("current_queue", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo CurrentJobField = typeof(FamilyMember).GetField("current_job", BindingFlags.Instance | BindingFlags.NonPublic);

        internal static event Action<PlayerQueueChangedEventArgs> QueueChanged;

        internal static PlayerQueueSnapshot Capture(ActorId owner, bool includeRestorePayload)
        {
            if (!IsFamilyActorId(owner))
                return Unavailable(null, "A family-member player actor identity is required.");

            return Capture(owner.LocalId, includeRestorePayload);
        }

        internal static PlayerQueueSnapshot Capture(ICharacterProxy owner, bool includeRestorePayload)
        {
            if (owner == null)
                return Unavailable(null, "A family-member character identity is required.");
            if (owner.Source != CharacterSource.RealFamily)
                return Unavailable(null, "Only live family-member characters own vanilla player queues.");

            return Capture(owner.UniqueId, includeRestorePayload);
        }

        internal static PlayerQueueSnapshot Capture(int uniqueMemberId, bool includeRestorePayload)
        {
            PlayerQueueOwnerIdentity unresolvedOwner = uniqueMemberId >= 0
                ? new PlayerQueueOwnerIdentity(ShelteredActors.FamilyMemberActorId(uniqueMemberId), uniqueMemberId, string.Empty)
                : null;

            FamilyMember member;
            string unavailableReason;
            if (!TryResolveMember(uniqueMemberId, out member, out unavailableReason))
                return Unavailable(unresolvedOwner, unavailableReason);

            return Capture(member, includeRestorePayload);
        }

        internal static PlayerQueueRestoreResult Restore(PlayerQueueSnapshot snapshot)
        {
            if (snapshot == null)
                return Failed("A player-queue snapshot is required.", null);
            if (!snapshot.IsAvailable || snapshot.Owner == null)
                return Failed(
                    string.IsNullOrEmpty(snapshot.UnavailableReason)
                        ? "The player-queue snapshot is unavailable."
                        : snapshot.UnavailableReason,
                    snapshot);
            if (!snapshot.CanRestore)
                return Failed(
                    string.IsNullOrEmpty(snapshot.RestoreBlockReason)
                        ? "The player-queue snapshot cannot be restored."
                        : snapshot.RestoreBlockReason,
                    snapshot);

            FamilyMember member;
            string unavailableReason;
            if (!TryResolveMember(snapshot.Owner.UniqueMemberId, out member, out unavailableReason))
                return Failed(unavailableReason, Capture(snapshot.Owner.ActorId, false));

            JobQueue liveQueue = member.job_queue;
            int liveCapacity = GetCapacity(liveQueue);
            if (liveCapacity < 0 || snapshot.Capacity < 0 || liveCapacity != snapshot.Capacity)
                return Failed(
                    "The live player-queue capacity does not match the snapshot; capacity policy is not changed by ShelteredAPI.",
                    Capture(member, false));
            if (snapshot.Count > liveCapacity)
                return Failed("The snapshot contains more jobs than the live player queue can hold.", Capture(member, false));
            if (!CanReplaceEmptyQueue(member, liveQueue))
                return Failed("Restore requires an empty player queue with no active player job.", Capture(member, false));

            List<Job> restoredJobs;
            string restoreFailure;
            if (!TryDeserializeJobs(snapshot, member, liveCapacity, out restoredJobs, out restoreFailure))
                return Failed(restoreFailure, Capture(member, false));
            if (JobListField == null)
                return Failed("The vanilla player queue storage field could not be resolved.", Capture(member, false));

            string beforeStamp = BuildMutationStamp(liveQueue);
            try
            {
                JobListField.SetValue(liveQueue, restoredJobs);
            }
            catch (Exception ex)
            {
                return Failed("The vanilla player queue could not be restored: " + ex.Message, Capture(member, false));
            }

            PlayerQueueSnapshot restored = Capture(member, false);
            if (!string.Equals(beforeStamp, BuildMutationStamp(liveQueue), StringComparison.Ordinal))
                RaiseChanged(PlayerQueueChangeKind.Restored, restored);
            return new PlayerQueueRestoreResult(true, "Player queue restored.", restored);
        }

        internal static string CaptureMutationStamp(JobQueue queue)
        {
            if (QueueChanged == null || queue == null)
                return null;

            FamilyMember member;
            if (!TryResolveOwner(queue, out member))
                return null;

            return BuildMutationStamp(queue);
        }

        internal static void CompleteMutation(JobQueue queue, string beforeStamp, PlayerQueueChangeKind changeKind)
        {
            if (string.IsNullOrEmpty(beforeStamp) || QueueChanged == null || queue == null)
                return;
            if (string.Equals(beforeStamp, BuildMutationStamp(queue), StringComparison.Ordinal))
                return;

            FamilyMember member;
            if (!TryResolveOwner(queue, out member))
                return;

            RaiseChanged(changeKind, Capture(member, false));
        }

        private static PlayerQueueSnapshot Capture(FamilyMember member, bool includeRestorePayload)
        {
            if (member == null || member.job_queue == null)
                return Unavailable(null, "No live player queue is available.");

            JobQueue queue = member.job_queue;
            PlayerQueueOwnerIdentity owner = CreateOwner(member);
            int capacity = GetCapacity(queue);
            List<PlayerQueueEntry> entries = CaptureEntries(queue);
            string restoreBlockReason = includeRestorePayload
                ? GetRestoreBlockReason(capacity, entries)
                : "Use SnapshotQueue to capture restore material.";
            byte[] restorePayload = null;

            if (includeRestorePayload && string.IsNullOrEmpty(restoreBlockReason))
            {
                string serializationFailure;
                if (!TrySerializeQueue(queue, out restorePayload, out serializationFailure))
                    restoreBlockReason = serializationFailure;
            }

            return new PlayerQueueSnapshot(
                true,
                owner,
                capacity,
                entries,
                string.Empty,
                restoreBlockReason,
                restorePayload);
        }

        private static PlayerQueueSnapshot Unavailable(PlayerQueueOwnerIdentity owner, string reason)
        {
            return new PlayerQueueSnapshot(
                false,
                owner,
                UnknownCapacity,
                new List<PlayerQueueEntry>(),
                reason,
                reason,
                null);
        }

        private static PlayerQueueRestoreResult Failed(string message, PlayerQueueSnapshot queue)
        {
            return new PlayerQueueRestoreResult(false, message, queue);
        }

        private static bool TryResolveMember(int uniqueMemberId, out FamilyMember member, out string unavailableReason)
        {
            member = null;
            if (uniqueMemberId < 0)
            {
                unavailableReason = "A valid family-member unique ID is required.";
                return false;
            }
            if (SaveManager.instance == null)
            {
                unavailableReason = "No active save session is available.";
                return false;
            }
            if (FamilyManager.Instance == null)
            {
                unavailableReason = "No active family session is available.";
                return false;
            }

            member = FamilyManager.Instance.GetFamilyMember(uniqueMemberId);
            if (member == null)
            {
                unavailableReason = "The requested family member is not available in the current session.";
                return false;
            }
            if (member.job_queue == null)
            {
                unavailableReason = "The requested family member does not expose a live player queue.";
                return false;
            }

            unavailableReason = string.Empty;
            return true;
        }

        private static bool TryResolveOwner(JobQueue queue, out FamilyMember owner)
        {
            owner = null;
            if (queue == null || SaveManager.instance == null || FamilyManager.Instance == null)
                return false;

            List<FamilyMember> members;
            try
            {
                members = FamilyManager.Instance.GetAllFamilyMembers();
            }
            catch
            {
                return false;
            }

            for (int i = 0; members != null && i < members.Count; i++)
            {
                FamilyMember member = members[i];
                if (member != null && ReferenceEquals(member.job_queue, queue))
                {
                    owner = member;
                    return true;
                }
            }

            return false;
        }

        private static bool IsFamilyActorId(ActorId actorId)
        {
            return actorId != null
                && actorId.Kind == ActorKind.Player
                && actorId.LocalId >= 0
                && string.IsNullOrEmpty(actorId.Domain);
        }

        private static PlayerQueueOwnerIdentity CreateOwner(FamilyMember member)
        {
            int id = member.GetId();
            string firstName = member.firstName ?? string.Empty;
            string lastName = member.lastName ?? string.Empty;
            string displayName = (firstName + " " + lastName).Trim();
            return new PlayerQueueOwnerIdentity(ShelteredActors.FamilyMemberActorId(id), id, displayName);
        }

        private static int GetCapacity(JobQueue queue)
        {
            if (queue == null || QueueCapacityField == null)
                return UnknownCapacity;

            try
            {
                return (int)QueueCapacityField.GetValue(queue);
            }
            catch
            {
                return UnknownCapacity;
            }
        }

        private static List<PlayerQueueEntry> CaptureEntries(JobQueue queue)
        {
            List<PlayerQueueEntry> entries = new List<PlayerQueueEntry>();
            if (queue == null)
                return entries;

            for (int i = 0; i < queue.size; i++)
            {
                Job job = queue.GetAt(i);
                if (job == null)
                    continue;

                int targetObjectId = job.obj != null ? job.obj.objectId : -1;
                entries.Add(new PlayerQueueEntry(
                    i,
                    GetJobType(job),
                    job.type,
                    MapState(job.state),
                    MapCancelState(job.GetCancelState()),
                    new PlayerQueuePosition(job.location.x, job.location.y, job.location.z),
                    targetObjectId));
            }

            return entries;
        }

        private static string GetRestoreBlockReason(int capacity, IList<PlayerQueueEntry> entries)
        {
            if (capacity < 0)
                return "Vanilla player-queue capacity metadata is unavailable.";

            for (int i = 0; entries != null && i < entries.Count; i++)
            {
                PlayerQueueEntry entry = entries[i];
                if (entry == null)
                    return "A player-queue entry could not be captured.";
                if (entry.State != PlayerQueueEntryState.Pending || entry.CancelState != PlayerQueueCancelState.Active)
                    return "Only pending, active player jobs can be restored safely.";
                if (!IsSafelyRestorableType(entry.JobType))
                    return "Player job type '" + entry.JobType + "' owns state that ShelteredAPI cannot safely restore.";
            }

            return string.Empty;
        }

        private static bool IsSafelyRestorableType(string jobType)
        {
            return string.Equals(jobType, "Job", StringComparison.Ordinal)
                || string.Equals(jobType, "Job_GoToLocation", StringComparison.Ordinal);
        }

        private static bool TrySerializeQueue(JobQueue queue, out byte[] payload, out string failure)
        {
            payload = null;
            failure = string.Empty;
            try
            {
                SaveData data = new SaveData();
                data.StartSaveable();
                queue.SaveLoadJobQueue(data, SnapshotGroupName);
                data.Finished();
                payload = data.GetBytes();
                if (payload == null || payload.Length == 0)
                {
                    failure = "Vanilla player-queue serialization produced no restore data.";
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                failure = "Vanilla player-queue serialization failed: " + ex.Message;
                return false;
            }
        }

        private static bool TryDeserializeJobs(
            PlayerQueueSnapshot snapshot,
            FamilyMember member,
            int capacity,
            out List<Job> jobs,
            out string failure)
        {
            jobs = null;
            failure = string.Empty;
            byte[] payload = snapshot.CopyRestorePayload();
            if (payload == null || payload.Length == 0)
            {
                failure = "The player-queue snapshot contains no restore data.";
                return false;
            }

            JobQueue deserialized = new JobQueue(capacity);
            try
            {
                SaveData data = new SaveData(payload);
                if (data.isFinished)
                {
                    failure = "The player-queue snapshot restore data is invalid.";
                    return false;
                }
                data.StartSaveable();
                deserialized.SaveLoadJobQueue(data, SnapshotGroupName);
                data.Finished();
            }
            catch (Exception ex)
            {
                failure = "Vanilla player-queue restore decoding failed: " + ex.Message;
                return false;
            }

            if (deserialized.size != snapshot.Count)
            {
                failure = "Vanilla player-queue restore did not reconstruct every captured entry.";
                return false;
            }

            jobs = new List<Job>(deserialized.size);
            for (int i = 0; i < deserialized.size; i++)
            {
                Job job = deserialized.GetAt(i);
                PlayerQueueEntry entry = snapshot.Entries[i];
                if (job == null || !string.Equals(GetJobType(job), entry.JobType, StringComparison.Ordinal))
                {
                    failure = "Vanilla player-queue restore changed a captured job type.";
                    return false;
                }
                if (job.character != member)
                {
                    failure = "A restored player job no longer resolves to its owning family member.";
                    return false;
                }
                if (entry.TargetObjectId >= 0 && job.obj == null)
                {
                    failure = "A restored player job target object is no longer available.";
                    return false;
                }

                jobs.Add(job);
            }

            return true;
        }

        private static bool CanReplaceEmptyQueue(FamilyMember member, JobQueue queue)
        {
            if (member == null || queue == null || !queue.is_empty)
                return false;

            try
            {
                JobQueue currentQueue = CurrentQueueField != null ? CurrentQueueField.GetValue(member) as JobQueue : null;
                Job currentJob = CurrentJobField != null ? CurrentJobField.GetValue(member) as Job : null;
                return !ReferenceEquals(currentQueue, queue) || currentJob == null;
            }
            catch
            {
                return false;
            }
        }

        private static string BuildMutationStamp(JobQueue queue)
        {
            StringBuilder stamp = new StringBuilder();
            if (queue == null)
                return stamp.ToString();

            stamp.Append(queue.size).Append('|');
            for (int i = 0; i < queue.size; i++)
            {
                Job job = queue.GetAt(i);
                if (job == null)
                {
                    stamp.Append("<null>;");
                    continue;
                }

                stamp.Append(GetJobType(job)).Append(':')
                    .Append(job.type ?? string.Empty).Append(':')
                    .Append((int)job.state).Append(':')
                    .Append((int)job.GetCancelState()).Append(';');
            }
            return stamp.ToString();
        }

        private static void RaiseChanged(PlayerQueueChangeKind changeKind, PlayerQueueSnapshot queue)
        {
            Action<PlayerQueueChangedEventArgs> handler = QueueChanged;
            if (handler == null)
                return;

            try
            {
                handler(new PlayerQueueChangedEventArgs(changeKind, queue));
            }
            catch (Exception ex)
            {
                Debug.LogError("[ShelteredQueues] QueueChanged subscriber failed: " + ex.Message);
            }
        }

        private static string GetJobType(Job job)
        {
            if (job == null)
                return string.Empty;
            try
            {
                return job.GetJobType() ?? string.Empty;
            }
            catch
            {
                return job.GetType().Name ?? string.Empty;
            }
        }

        private static PlayerQueueEntryState MapState(Job.JobState state)
        {
            switch (state)
            {
                case Job.JobState.Pending:
                    return PlayerQueueEntryState.Pending;
                case Job.JobState.InTransit:
                    return PlayerQueueEntryState.InTransit;
                case Job.JobState.Started:
                    return PlayerQueueEntryState.Started;
                case Job.JobState.Finished:
                    return PlayerQueueEntryState.Finished;
                default:
                    return PlayerQueueEntryState.Unknown;
            }
        }

        private static PlayerQueueCancelState MapCancelState(Job.JobCancelState state)
        {
            switch (state)
            {
                case Job.JobCancelState.Active:
                    return PlayerQueueCancelState.Active;
                case Job.JobCancelState.Cancelled:
                    return PlayerQueueCancelState.Cancelled;
                case Job.JobCancelState.ForceCancelled:
                    return PlayerQueueCancelState.ForceCancelled;
                default:
                    return PlayerQueueCancelState.Unknown;
            }
        }
    }
}
