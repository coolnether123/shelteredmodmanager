using System.Collections.Generic;
using ShelteredAPI.Networking.Persistence;
using ShelteredAPI.Networking.World;

namespace ShelteredAPI.Networking.Recovery
{
    internal enum ShelteredMultiplayerCatchupDecisionKind
    {
        EventOnly = 0,
        SnapshotAndEvents = 1,
        RejectRejoin = 2
    }

    internal sealed class ShelteredMultiplayerCatchupRequest
    {
        public string SessionId = string.Empty;
        public long LastAppliedTick;
        public string LastAppliedEventId = string.Empty;
        public string CompatibilityHash = string.Empty;
    }

    internal sealed class ShelteredMultiplayerCatchupDecision
    {
        public ShelteredMultiplayerCatchupDecisionKind Kind;
        public string Reason = string.Empty;
        public long ReplayFromTick;
        public bool RequiresSnapshot;
        public bool Accepted
        {
            get { return Kind != ShelteredMultiplayerCatchupDecisionKind.RejectRejoin; }
        }
    }

    internal sealed class ShelteredMultiplayerCatchupPackage
    {
        public ShelteredMultiplayerCatchupDecision Decision;
        public ShelteredMultiplayerWorldSnapshot Snapshot;
        public readonly List<ShelteredWorldEventRecord> Events = new List<ShelteredWorldEventRecord>();
        public long HostTick;
    }

    internal sealed class ShelteredMultiplayerCatchupApplyResult
    {
        public bool Success;
        public string Error = string.Empty;
        public int AppliedEventCount;
        public long ResumeTick;
    }
}
