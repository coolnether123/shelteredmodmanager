using System;
using ModAPI.Networking.Sessions;

namespace ModAPI.Networking.Diagnostics
{
    public sealed class NetworkDiagnosticsSnapshot
    {
        public NetworkDiagnosticsSnapshot(
            DateTime capturedUtc,
            NetworkSessionMode mode,
            NetworkSessionState state,
            byte localPeerId,
            NetworkPeerDiagnosticsSnapshot[] peers,
            NetworkDiagnosticsEvent[] recentEvents)
        {
            CapturedUtc = capturedUtc;
            Mode = mode;
            State = state;
            LocalPeerId = localPeerId;
            Peers = peers ?? new NetworkPeerDiagnosticsSnapshot[0];
            RecentEvents = recentEvents ?? new NetworkDiagnosticsEvent[0];
        }

        public DateTime CapturedUtc { get; private set; }
        public NetworkSessionMode Mode { get; private set; }
        public NetworkSessionState State { get; private set; }
        public byte LocalPeerId { get; private set; }
        public NetworkPeerDiagnosticsSnapshot[] Peers { get; private set; }
        public NetworkDiagnosticsEvent[] RecentEvents { get; private set; }
    }
}
