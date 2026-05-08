using System;
using System.Collections.Generic;

namespace ShelteredAPI.Networking.World
{
    internal sealed class ShelteredWorldEventRecord
    {
        public string EventId;
        public string EventKind;
        public string CorrelationId;
        public int SourcePlayerId;
        public byte SourceNetworkPeerId;
        public long WorldTick;
        public float WorldDeltaSeconds;
        public string PayloadJson;
        public bool Authoritative;
        public DateTime CreatedUtc;

        internal ShelteredWorldEventRecord Clone()
        {
            return new ShelteredWorldEventRecord
            {
                EventId = EventId ?? string.Empty,
                EventKind = EventKind ?? string.Empty,
                CorrelationId = CorrelationId ?? string.Empty,
                SourcePlayerId = SourcePlayerId,
                SourceNetworkPeerId = SourceNetworkPeerId,
                WorldTick = WorldTick,
                WorldDeltaSeconds = WorldDeltaSeconds,
                PayloadJson = PayloadJson ?? string.Empty,
                Authoritative = Authoritative,
                CreatedUtc = CreatedUtc
            };
        }
    }

    internal sealed class ShelteredWorldEventAppendResult
    {
        public bool Success;
        public string EventId;
        public string ErrorMessage;

        internal static ShelteredWorldEventAppendResult Accepted(string eventId)
        {
            return new ShelteredWorldEventAppendResult
            {
                Success = true,
                EventId = eventId ?? string.Empty,
                ErrorMessage = string.Empty
            };
        }

        internal static ShelteredWorldEventAppendResult Rejected(string eventId, string errorMessage)
        {
            return new ShelteredWorldEventAppendResult
            {
                Success = false,
                EventId = eventId ?? string.Empty,
                ErrorMessage = errorMessage ?? string.Empty
            };
        }
    }

    internal interface IShelteredWorldEventJournal
    {
        ShelteredWorldEventAppendResult Append(ShelteredWorldEventRecord record);
        IList<ShelteredWorldEventRecord> GetSince(long worldTick);
        IList<ShelteredWorldEventRecord> GetRange(long startTick, long endTick);
        ShelteredWorldEventRecord GetById(string eventId);
        bool Contains(string eventId);
        void Clear(string reason);
        void TrimToMaxRetained();
        int MaxRetainedEvents { get; }
        long LatestTick { get; }
        int Count { get; }
    }
}
