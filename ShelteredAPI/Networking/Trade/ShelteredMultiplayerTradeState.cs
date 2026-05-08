using System;

namespace ShelteredAPI.Networking.Trade
{
    internal enum ShelteredMultiplayerTradeStateKind
    {
        Offered,
        Accepted,
        Rejected,
        CargoReserved,
        CaravanLaunched,
        Arrived,
        Completed,
        Cancelled,
        Failed
    }

    internal sealed class ShelteredMultiplayerTradeState
    {
        public ShelteredMultiplayerTradeState()
        {
            TradeId = string.Empty;
            SourceOwnerId = string.Empty;
            TargetOwnerId = string.Empty;
            LastEventId = string.Empty;
            LastEventKind = string.Empty;
            FailureReason = string.Empty;
        }

        public string TradeId { get; set; }
        public string SourceOwnerId { get; set; }
        public string TargetOwnerId { get; set; }
        public ShelteredMultiplayerTradeStateKind State { get; set; }
        public long LastAuthoritativeTick { get; set; }
        public string LastEventId { get; set; }
        public string LastEventKind { get; set; }
        public string FailureReason { get; set; }

        public ShelteredMultiplayerTradeState Copy()
        {
            return new ShelteredMultiplayerTradeState
            {
                TradeId = TradeId ?? string.Empty,
                SourceOwnerId = SourceOwnerId ?? string.Empty,
                TargetOwnerId = TargetOwnerId ?? string.Empty,
                State = State,
                LastAuthoritativeTick = LastAuthoritativeTick,
                LastEventId = LastEventId ?? string.Empty,
                LastEventKind = LastEventKind ?? string.Empty,
                FailureReason = FailureReason ?? string.Empty
            };
        }
    }

    internal sealed class ShelteredMultiplayerTradeApplyResult
    {
        public static readonly ShelteredMultiplayerTradeApplyResult Applied =
            new ShelteredMultiplayerTradeApplyResult(true, string.Empty);

        public static readonly ShelteredMultiplayerTradeApplyResult IgnoredDuplicate =
            new ShelteredMultiplayerTradeApplyResult(false, "duplicate-event-id");

        public static ShelteredMultiplayerTradeApplyResult Ignored(string reason)
        {
            return new ShelteredMultiplayerTradeApplyResult(false, reason);
        }

        private ShelteredMultiplayerTradeApplyResult(bool applied, string reason)
        {
            AppliedEvent = applied;
            Reason = reason ?? string.Empty;
        }

        public bool AppliedEvent { get; private set; }
        public string Reason { get; private set; }
    }
}
