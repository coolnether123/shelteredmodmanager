using System.Collections.Generic;
using ModAPI.Networking;

namespace ShelteredAPI.Networking.World
{
    internal static class ShelteredWorldEvents
    {
        private static readonly ShelteredWorldEventJournal _journal = new ShelteredWorldEventJournal();

        public static IShelteredWorldEventJournal Journal
        {
            get { return _journal; }
        }

        public static ShelteredWorldEventAppendResult AppendAuthoritative(
            string kind,
            string correlationId,
            string payloadJson,
            int sourcePlayerId,
            byte sourcePeerId)
        {
            ShelteredMultiplayerSessionContext context = ShelteredMultiplayerSessionCoordinator.Instance.Context;
            return _journal.Append(CreateRecord(
                context,
                kind,
                correlationId,
                payloadJson,
                sourcePlayerId > 0 ? sourcePlayerId : ResolveContextPlayerId(context),
                sourcePeerId != NetworkDefaults.UnassignedPeerId ? sourcePeerId : ResolveContextPeerId(context),
                true));
        }

        public static ShelteredWorldEventAppendResult AppendLocalPrediction(
            string kind,
            string correlationId,
            string payloadJson)
        {
            ShelteredMultiplayerSessionContext context = ShelteredMultiplayerSessionCoordinator.Instance.Context;
            return _journal.Append(CreateRecord(
                context,
                kind,
                correlationId,
                payloadJson,
                ResolveContextPlayerId(context),
                ResolveContextPeerId(context),
                false));
        }

        public static IList<ShelteredWorldEventRecord> GetSince(long tick)
        {
            return _journal.GetSince(tick);
        }

        public static void Clear(string reason)
        {
            _journal.Clear(reason);
        }

        private static ShelteredWorldEventRecord CreateRecord(
            ShelteredMultiplayerSessionContext context,
            string kind,
            string correlationId,
            string payloadJson,
            int sourcePlayerId,
            byte sourcePeerId,
            bool authoritative)
        {
            long worldTick = context != null ? context.WorldTick : 0;
            float worldDeltaSeconds = context != null ? context.WorldDeltaSeconds : 0f;

            return new ShelteredWorldEventRecord
            {
                EventId = CreateStableEventId(kind, correlationId, authoritative),
                EventKind = kind ?? string.Empty,
                CorrelationId = correlationId ?? string.Empty,
                SourcePlayerId = sourcePlayerId,
                SourceNetworkPeerId = sourcePeerId,
                WorldTick = worldTick,
                WorldDeltaSeconds = worldDeltaSeconds,
                PayloadJson = payloadJson ?? string.Empty,
                Authoritative = authoritative
            };
        }

        private static string CreateStableEventId(string kind, string correlationId, bool authoritative)
        {
            string normalizedCorrelation = Normalize(correlationId);
            if (normalizedCorrelation.Length == 0)
                return string.Empty;

            return "worldevent:"
                + (authoritative ? "authoritative:" : "prediction:")
                + Normalize(kind)
                + ":"
                + normalizedCorrelation;
        }

        private static string Normalize(string value)
        {
            return (value ?? string.Empty).Trim();
        }

        private static int ResolveContextPlayerId(ShelteredMultiplayerSessionContext context)
        {
            return context != null ? context.LocalPlayerId : 0;
        }

        private static byte ResolveContextPeerId(ShelteredMultiplayerSessionContext context)
        {
            return context != null ? context.NetworkLocalPeerId : NetworkDefaults.UnassignedPeerId;
        }
    }
}
