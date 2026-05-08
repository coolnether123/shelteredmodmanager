using System;

namespace ShelteredAPI.Networking.Raids
{
    public enum ShelteredRaidLifecycleState
    {
        Intent,
        Accepted,
        Rejected,
        Launched,
        Warning,
        Arrived,
        Resolved,
        Cancelled
    }

    public sealed class ShelteredRaidState
    {
        public ShelteredRaidState()
        {
            RaidId = string.Empty;
            ResultPayloadJson = string.Empty;
            LastEventId = string.Empty;
            State = ShelteredRaidLifecycleState.Intent;
        }

        public string RaidId;
        public int AttackerPlayerId;
        public int DefenderPlayerId;
        public int TargetBunkerOwnerId;
        public long StartTick;
        public long ArrivalTick;
        public int RaidStrength;
        public long WarningTick;
        public int DefenseScore;
        public string ResultPayloadJson;
        public ShelteredRaidLifecycleState State;
        public string LastEventId;

        public ShelteredRaidState Copy()
        {
            return new ShelteredRaidState
            {
                RaidId = RaidId ?? string.Empty,
                AttackerPlayerId = AttackerPlayerId,
                DefenderPlayerId = DefenderPlayerId,
                TargetBunkerOwnerId = TargetBunkerOwnerId,
                StartTick = StartTick,
                ArrivalTick = ArrivalTick,
                RaidStrength = RaidStrength,
                WarningTick = WarningTick,
                DefenseScore = DefenseScore,
                ResultPayloadJson = ResultPayloadJson ?? string.Empty,
                State = State,
                LastEventId = LastEventId ?? string.Empty
            };
        }
    }

    [Serializable]
    internal sealed class ShelteredRaidEvent
    {
        public ShelteredRaidEvent()
        {
            RaidId = string.Empty;
            EventKind = string.Empty;
            ResultPayloadJson = string.Empty;
            RejectionReason = string.Empty;
        }

        public string RaidId;
        public string EventKind;
        public int AttackerPlayerId;
        public int DefenderPlayerId;
        public int TargetBunkerOwnerId;
        public long StartTick;
        public long ArrivalTick;
        public int RaidStrength;
        public long WarningTick;
        public int DefenseScore;
        public string ResultPayloadJson;
        public string RejectionReason;

        public ShelteredRaidEvent Copy()
        {
            return new ShelteredRaidEvent
            {
                RaidId = RaidId ?? string.Empty,
                EventKind = EventKind ?? string.Empty,
                AttackerPlayerId = AttackerPlayerId,
                DefenderPlayerId = DefenderPlayerId,
                TargetBunkerOwnerId = TargetBunkerOwnerId,
                StartTick = StartTick,
                ArrivalTick = ArrivalTick,
                RaidStrength = RaidStrength,
                WarningTick = WarningTick,
                DefenseScore = DefenseScore,
                ResultPayloadJson = ResultPayloadJson ?? string.Empty,
                RejectionReason = RejectionReason ?? string.Empty
            };
        }
    }

    internal sealed class ShelteredRaidApplyResult
    {
        public static readonly ShelteredRaidApplyResult Applied = new ShelteredRaidApplyResult(true, string.Empty);
        public static readonly ShelteredRaidApplyResult IgnoredDuplicate = new ShelteredRaidApplyResult(false, "duplicate-event-id");
        public static readonly ShelteredRaidApplyResult IgnoredOutOfOrder = new ShelteredRaidApplyResult(false, "out-of-order-event");

        public static ShelteredRaidApplyResult Ignored(string reason)
        {
            return new ShelteredRaidApplyResult(false, reason);
        }

        private ShelteredRaidApplyResult(bool applied, string reason)
        {
            AppliedEvent = applied;
            Reason = reason ?? string.Empty;
        }

        public bool AppliedEvent { get; private set; }
        public string Reason { get; private set; }
    }
}
