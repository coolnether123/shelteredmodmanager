using System;
using System.Net;
using ModAPI.Networking.Connections;

namespace ModAPI.Networking.Diagnostics
{
    public sealed class NetworkPeerDiagnosticsSnapshot
    {
        public NetworkPeerDiagnosticsSnapshot(
            byte peerId,
            IPEndPoint endPoint,
            NetworkConnectionState state,
            string displayName,
            long packetsSent,
            long packetsReceived,
            long bytesSent,
            long bytesReceived,
            string lastError,
            double? heartbeatLatencyMilliseconds,
            DateTime lastSendUtc,
            DateTime lastReceiveUtc)
        {
            PeerId = peerId;
            EndPoint = endPoint;
            State = state;
            DisplayName = displayName ?? string.Empty;
            PacketsSent = packetsSent;
            PacketsReceived = packetsReceived;
            BytesSent = bytesSent;
            BytesReceived = bytesReceived;
            LastError = lastError ?? string.Empty;
            HeartbeatLatencyMilliseconds = heartbeatLatencyMilliseconds;
            LastSendUtc = lastSendUtc;
            LastReceiveUtc = lastReceiveUtc;
        }

        public byte PeerId { get; private set; }
        public IPEndPoint EndPoint { get; private set; }
        public NetworkConnectionState State { get; private set; }
        public string DisplayName { get; private set; }
        public long PacketsSent { get; private set; }
        public long PacketsReceived { get; private set; }
        public long BytesSent { get; private set; }
        public long BytesReceived { get; private set; }
        public string LastError { get; private set; }
        public double? HeartbeatLatencyMilliseconds { get; private set; }
        public DateTime LastSendUtc { get; private set; }
        public DateTime LastReceiveUtc { get; private set; }
    }
}
