using ShelteredAPI.Networking;
using ShelteredAPI.Networking.World;

namespace ShelteredAPI.Networking.Knowledge
{
    internal static class ShelteredMapDiscoveryEvents
    {
        public const string KnowledgeRevealed = "MapKnowledge.Revealed";
        public const string KnowledgeForgotten = "MapKnowledge.Forgotten";

        public static string AppendReveal(MapKnowledgeRecord record, string reason)
        {
            if (record == null)
                return string.Empty;

            ShelteredWorldEventAppendResult result = ShelteredWorldEvents.AppendAuthoritative(
                KnowledgeRevealed,
                CreateCorrelationId("reveal", record.ViewerPlayerId, record.EntityId, record.LastKnownWorldTick),
                BuildPayload(record, reason));
            return result != null && result.Success ? result.EventId : string.Empty;
        }

        public static string AppendForget(int viewerPlayerId, string entityId, string reason)
        {
            ShelteredWorldEventAppendResult result = ShelteredWorldEvents.AppendAuthoritative(
                KnowledgeForgotten,
                CreateCorrelationId("forget", viewerPlayerId, entityId, ResolveWorldTick()),
                "viewer=" + viewerPlayerId + ";entity=" + Normalize(entityId) + ";reason=" + Normalize(reason));
            return result != null && result.Success ? result.EventId : string.Empty;
        }

        private static string BuildPayload(MapKnowledgeRecord record, string reason)
        {
            return "viewer=" + record.ViewerPlayerId
                + ";entity=" + Normalize(record.EntityId)
                + ";level=" + record.KnowledgeLevel
                + ";kind=" + record.KnownKind
                + ";name=" + Normalize(record.KnownDisplayName)
                + ";grid=" + record.LastKnownGridX + "," + record.LastKnownGridY
                + ";tick=" + record.LastKnownWorldTick
                + ";stale=" + record.IsStale
                + ";reason=" + Normalize(reason);
        }

        private static string CreateCorrelationId(string verb, int viewerPlayerId, string entityId, long tick)
        {
            return verb + ":" + viewerPlayerId + ":" + Normalize(entityId) + ":" + tick;
        }

        private static long ResolveWorldTick()
        {
            ShelteredMultiplayerSessionContext context = ShelteredMultiplayerSessionCoordinator.Instance.Context;
            return context != null && context.WorldTick > 0 ? context.WorldTick : 0;
        }

        private static string Normalize(string value)
        {
            return (value ?? string.Empty).Trim();
        }
    }
}
