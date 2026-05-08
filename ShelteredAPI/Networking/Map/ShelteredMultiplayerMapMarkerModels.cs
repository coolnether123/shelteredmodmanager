using UnityEngine;

namespace ShelteredAPI.Networking.Map
{
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
        {
            MarkerId = markerId ?? string.Empty;
            Label = label ?? string.Empty;
            BunkerOwnerId = bunkerOwnerId;
            PeerId = peerId;
            MapPixels = mapPixels;
            IsLocal = isLocal;
            IsOnline = isOnline;
        }

        public readonly string MarkerId;
        public readonly string Label;
        public readonly int BunkerOwnerId;
        public readonly byte PeerId;
        public readonly Vector3 MapPixels;
        public readonly bool IsLocal;
        public readonly bool IsOnline;
    }
}
