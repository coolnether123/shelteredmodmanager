using ShelteredAPI.Networking.Map;
using ShelteredAPI.Networking.World;

namespace ShelteredAPI.Networking.Knowledge
{
    internal enum MapKnowledgeLevel
    {
        Unknown,
        Suspicious,
        Scouted,
        Identified,
        Confirmed,
        Allied,
        DebugFull
    }

    internal enum MapContactKind
    {
        Unknown,
        Bunker,
        Expedition,
        Caravan,
        RaidParty,
        Settlement,
        ResourceNode,
        FactionTerritory
    }

    internal sealed class MapKnowledgeRecord
    {
        public MapKnowledgeRecord()
        {
            EntityId = string.Empty;
            KnownDisplayName = string.Empty;
            DiscoveredByEventId = string.Empty;
        }

        public string EntityId;
        public int ViewerPlayerId;
        public MapKnowledgeLevel KnowledgeLevel;
        public MapContactKind KnownKind;
        public string KnownDisplayName;
        public int LastKnownGridX;
        public int LastKnownGridY;
        public long LastKnownWorldTick;
        public string DiscoveredByEventId;
        public bool IsStale;

        internal MapKnowledgeRecord Clone()
        {
            return new MapKnowledgeRecord
            {
                EntityId = EntityId ?? string.Empty,
                ViewerPlayerId = ViewerPlayerId,
                KnowledgeLevel = KnowledgeLevel,
                KnownKind = KnownKind,
                KnownDisplayName = KnownDisplayName ?? string.Empty,
                LastKnownGridX = LastKnownGridX,
                LastKnownGridY = LastKnownGridY,
                LastKnownWorldTick = LastKnownWorldTick,
                DiscoveredByEventId = DiscoveredByEventId ?? string.Empty,
                IsStale = IsStale
            };
        }
    }

    internal interface IShelteredMapKnowledgeService
    {
        MapKnowledgeRecord GetKnowledge(int viewerPlayerId, string entityId);
        MapKnowledgeRecord Reveal(int viewerPlayerId, string entityId, MapKnowledgeLevel level, string reason);
        bool Forget(int viewerPlayerId, string entityId, string reason);
        bool CanSeeExactLocation(int viewerPlayerId, string entityId);
        ShelteredMultiplayerMapMarker BuildDisplayMarker(int viewerPlayerId, ShelteredMapEntity entity);
    }
}
