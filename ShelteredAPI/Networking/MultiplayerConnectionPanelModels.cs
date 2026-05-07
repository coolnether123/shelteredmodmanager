using ModAPI.Networking.Connections;
using ModAPI.Networking.Diagnostics;
using ModAPI.Networking.Sessions;
using UnityEngine;

namespace ShelteredAPI.Networking
{
    internal sealed class MultiplayerConnectionPanelState
    {
        public string EndpointText = "127.0.0.1:7777";
        public string PortText = "7777";
        public string MessageText = "ping";
        public Vector2 AdvancedScroll;
        public int ActiveTabIndex;
        public bool ShowSentEvents = true;
        public bool ShowReceivedEvents = true;
        public bool ShowPeerEvents = true;
        public bool ShowSessionEvents = true;
    }

    internal sealed class MultiplayerConnectionPanelViewModel
    {
        public MultiplayerConnectionTestService Service;
        public NetworkDiagnosticsSnapshot Snapshot;
        public NetworkPeer[] Peers = new NetworkPeer[0];
        public string[] DiscoveryResults = new string[0];
        public string[] ReceivedMessages = new string[0];
        public string[] LogLines = new string[0];

        public string RoleText = string.Empty;
        public string StateText = string.Empty;
        public string LocalEndpointText = string.Empty;
        public string LocalPeerIdText = string.Empty;
        public string ConfigurationSummary = string.Empty;
        public string SaveSyncStatus = string.Empty;
        public string SaveSyncLastError = string.Empty;
        public string SetupStatus = string.Empty;
        public string SetupLastError = string.Empty;
        public string LastError = string.Empty;
        public string SnapshotAgeText = string.Empty;

        public int ConnectedPeerCount;
        public int TotalPeerCount;
        public int EventCount;
        public long TotalPacketsSent;
        public long TotalPacketsReceived;
        public long TotalBytesSent;
        public long TotalBytesReceived;

        public bool IsDiscovering;
        public bool HasActiveSession;
        public bool CanSendTestMessage;
        public bool CanBeginSetup;
        public bool CanReleaseSetup;
        public NetworkSessionMode Mode;
    }
}
