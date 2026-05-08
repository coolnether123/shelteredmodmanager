using System;
using ShelteredAPI.Networking.World;

namespace ShelteredAPI.Networking.Recovery
{
    internal sealed class ShelteredMultiplayerResyncPolicy
    {
        public int SnapshotEventThreshold = 256;
        public bool RejectOnCompatibilityMismatch = true;

        public ShelteredMultiplayerCatchupDecision Choose(
            ShelteredMultiplayerCatchupRequest request,
            ShelteredMultiplayerSessionContext hostContext,
            IShelteredWorldEventJournal journal,
            string hostCompatibilityHash)
        {
            if (request == null)
                return Reject("missing-request");

            if (hostContext == null || !hostContext.IsMultiplayerActive)
                return Reject("host-session-inactive");

            if (!string.Equals(hostContext.SessionId ?? string.Empty, request.SessionId ?? string.Empty, StringComparison.Ordinal))
                return Reject("session-id-mismatch");

            if (RejectOnCompatibilityMismatch
                && !string.IsNullOrEmpty(hostCompatibilityHash)
                && !string.Equals(hostCompatibilityHash, request.CompatibilityHash ?? string.Empty, StringComparison.Ordinal))
            {
                return Reject("compatibility-hash-mismatch");
            }

            long hostTick = hostContext.WorldTick < 0 ? 0 : hostContext.WorldTick;
            long clientTick = request.LastAppliedTick < 0 ? 0 : request.LastAppliedTick;
            if (clientTick > hostTick)
                return Reject("client-is-ahead-of-host");

            int eventCount = journal != null ? journal.Count : 0;
            bool historyMayBeTrimmed = journal != null
                && eventCount >= journal.MaxRetainedEvents
                && !string.IsNullOrEmpty(request.LastAppliedEventId)
                && !journal.Contains(request.LastAppliedEventId);

            if (historyMayBeTrimmed)
                return Snapshot("event-history-trimmed", clientTick);

            if (hostTick - clientTick > SnapshotEventThreshold)
                return Snapshot("client-too-far-behind", clientTick);

            return new ShelteredMultiplayerCatchupDecision
            {
                Kind = ShelteredMultiplayerCatchupDecisionKind.EventOnly,
                Reason = "event-only",
                ReplayFromTick = clientTick,
                RequiresSnapshot = false
            };
        }

        private static ShelteredMultiplayerCatchupDecision Snapshot(string reason, long replayFromTick)
        {
            return new ShelteredMultiplayerCatchupDecision
            {
                Kind = ShelteredMultiplayerCatchupDecisionKind.SnapshotAndEvents,
                Reason = reason,
                ReplayFromTick = replayFromTick,
                RequiresSnapshot = true
            };
        }

        private static ShelteredMultiplayerCatchupDecision Reject(string reason)
        {
            return new ShelteredMultiplayerCatchupDecision
            {
                Kind = ShelteredMultiplayerCatchupDecisionKind.RejectRejoin,
                Reason = reason,
                ReplayFromTick = 0,
                RequiresSnapshot = false
            };
        }
    }
}
