using System;
using System.Collections.Generic;
using System.Net;

namespace ModAPI.Networking.Connections
{
    internal sealed class NetworkPeerRegistry
    {
        private readonly Dictionary<byte, NetworkPeer> _byPeerId = new Dictionary<byte, NetworkPeer>();
        private readonly Dictionary<string, NetworkPeer> _byEndPoint = new Dictionary<string, NetworkPeer>(StringComparer.OrdinalIgnoreCase);

        public int Count { get { return _byPeerId.Count; } }

        public void Clear()
        {
            _byPeerId.Clear();
            _byEndPoint.Clear();
        }

        public bool Add(NetworkPeer peer)
        {
            if (peer == null)
                throw new ArgumentNullException("peer");
            if (_byPeerId.ContainsKey(peer.PeerId))
                return false;

            string key = ToKey(peer.EndPoint);
            if (key.Length > 0 && _byEndPoint.ContainsKey(key))
                return false;

            _byPeerId.Add(peer.PeerId, peer);
            if (key.Length > 0)
                _byEndPoint.Add(key, peer);
            return true;
        }

        public bool Remove(byte peerId)
        {
            NetworkPeer peer;
            if (!_byPeerId.TryGetValue(peerId, out peer))
                return false;

            _byPeerId.Remove(peerId);
            string key = ToKey(peer.EndPoint);
            if (key.Length > 0)
                _byEndPoint.Remove(key);
            return true;
        }

        public NetworkPeer FindByPeerId(byte peerId)
        {
            NetworkPeer peer;
            return _byPeerId.TryGetValue(peerId, out peer) ? peer : null;
        }

        public NetworkPeer FindByEndPoint(IPEndPoint endPoint)
        {
            string key = ToKey(endPoint);
            if (key.Length == 0)
                return null;

            NetworkPeer peer;
            return _byEndPoint.TryGetValue(key, out peer) ? peer : null;
        }

        public NetworkPeer[] GetAll()
        {
            NetworkPeer[] peers = new NetworkPeer[_byPeerId.Count];
            _byPeerId.Values.CopyTo(peers, 0);
            return peers;
        }

        public bool TryAllocatePeerId(byte minPeerId, byte maxPeerId, out byte peerId)
        {
            for (int i = minPeerId; i <= maxPeerId; i++)
            {
                byte candidate = (byte)i;
                if (!_byPeerId.ContainsKey(candidate) && candidate != NetworkDefaults.UnassignedPeerId)
                {
                    peerId = candidate;
                    return true;
                }
            }

            peerId = NetworkDefaults.UnassignedPeerId;
            return false;
        }

        private static string ToKey(IPEndPoint endPoint)
        {
            if (endPoint == null)
                return string.Empty;
            return endPoint.Address + ":" + endPoint.Port;
        }
    }
}
