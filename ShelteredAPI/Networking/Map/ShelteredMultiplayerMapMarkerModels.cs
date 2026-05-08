using UnityEngine;

namespace ShelteredAPI.Networking.Map
{
    internal enum ShelteredMultiplayerMapMarkerVisualKind
    {
        Unknown,
        LocalBunker,
        RemoteBunker,
        Expedition,
        TradeCaravan,
        RaidParty,
        Settlement,
        ResourceNode,
        FactionMarker
    }

    internal sealed class ShelteredMultiplayerMapMarker
    {
        public ShelteredMultiplayerMapMarker(
            string markerId,
            string label,
            int bunkerOwnerId,
            byte peerId,
            Vector3 mapPixels,
            bool isLocal,
            bool isOnline)
            : this(
                markerId,
                label,
                bunkerOwnerId,
                peerId,
                mapPixels,
                isLocal,
                isOnline,
                ShelteredMultiplayerMapMarkerVisualKind.RemoteBunker,
                string.Empty,
                false)
        {
        }

        public ShelteredMultiplayerMapMarker(
            string markerId,
            string label,
            int bunkerOwnerId,
            byte peerId,
            Vector3 mapPixels,
            bool isLocal,
            bool isOnline,
            ShelteredMultiplayerMapMarkerVisualKind visualKind,
            string entityId,
            bool isStale)
        {
            MarkerId = markerId ?? string.Empty;
            Label = label ?? string.Empty;
            BunkerOwnerId = bunkerOwnerId;
            PeerId = peerId;
            MapPixels = mapPixels;
            IsLocal = isLocal;
            IsOnline = isOnline;
            VisualKind = visualKind;
            EntityId = entityId ?? string.Empty;
            IsStale = isStale;
        }

        public readonly string MarkerId;
        public readonly string Label;
        public readonly int BunkerOwnerId;
        public readonly byte PeerId;
        public readonly Vector3 MapPixels;
        public readonly bool IsLocal;
        public readonly bool IsOnline;
        public readonly ShelteredMultiplayerMapMarkerVisualKind VisualKind;
        public readonly string EntityId;
        public readonly bool IsStale;

        public bool IsUnknown
        {
            get { return VisualKind == ShelteredMultiplayerMapMarkerVisualKind.Unknown; }
        }
    }
}
