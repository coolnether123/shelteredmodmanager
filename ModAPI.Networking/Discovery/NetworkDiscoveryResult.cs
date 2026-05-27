using System.Net;

namespace ModAPI.Networking.Discovery
{
    public sealed class NetworkDiscoveryResult
    {
        public NetworkDiscoveryResult(
            IPEndPoint endPoint,
            string applicationId,
            string sessionId,
            int peerCount,
            int maxPeers,
            string displayName)
        {
            EndPoint = endPoint;
            ApplicationId = applicationId ?? string.Empty;
            SessionId = sessionId ?? string.Empty;
            PeerCount = peerCount;
            MaxPeers = maxPeers;
            DisplayName = displayName ?? string.Empty;
        }

        public IPEndPoint EndPoint { get; private set; }
        public string ApplicationId { get; private set; }
        public string SessionId { get; private set; }
        public int PeerCount { get; private set; }
        public int MaxPeers { get; private set; }
        public string DisplayName { get; private set; }
    }
}
