using ModAPI.Networking.Connections;
using ModAPI.Networking.Diagnostics;
using ModAPI.Networking.Sessions;
using ShelteredAPI.Networking.Diagnostics;
using ShelteredAPI.Networking.Setup;
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
        public MultiplayerConnectionWizardRole SelectedRole = MultiplayerConnectionWizardRole.Host;
        public bool ShowAdvancedDiagnostics;
        public bool ShowSentEvents = true;
        public bool ShowReceivedEvents = true;
        public bool ShowPeerEvents = true;
        public bool ShowSessionEvents = true;
        public string LastRenderErrorText = string.Empty;
        public int UiRevision;
        public MultiplayerConnectionWizardActionKind PendingActionKind = MultiplayerConnectionWizardActionKind.None;
        public bool PendingCloseRequested;
    }

    internal sealed class MultiplayerConnectionPanelViewModel
    {
        public MultiplayerConnectionTestService Service;
        public NetworkDiagnosticsSnapshot Snapshot;
        public NetworkPeer[] Peers = new NetworkPeer[0];
        public string[] DiscoveryResults = new string[0];
        public string[] ReceivedMessages = new string[0];
        public string[] LogLines = new string[0];
        public string[] TimelineLines = new string[0];
        public MultiplayerEndpointCandidate[] EndpointCandidates = new MultiplayerEndpointCandidate[0];
        public MultiplayerPortValidationResult PortValidation = MultiplayerPortValidationResult.Valid(MultiplayerConnectionTestService.DefaultPort);
        public MultiplayerEndpointValidationResult EndpointValidation = MultiplayerEndpointValidationResult.Invalid(string.Empty);
        public MultiplayerConnectionActionState HostAction = MultiplayerConnectionActionState.Unavailable("Host", "Service is unavailable.");
        public MultiplayerConnectionActionState JoinAction = MultiplayerConnectionActionState.Unavailable("Join", "Service is unavailable.");
        public MultiplayerConnectionActionState StopAction = MultiplayerConnectionActionState.Unavailable("Stop", "No active session.");
        public MultiplayerConnectionActionState DiscoveryAction = MultiplayerConnectionActionState.Unavailable("Find LAN", "Service is unavailable.");
        public MultiplayerConnectionActionState SendTestMessageAction = MultiplayerConnectionActionState.Unavailable("Send Ping", "No connected peer.");
        public MultiplayerConnectionActionState BeginSetupAction = MultiplayerConnectionActionState.Unavailable("Begin Game Setup", "Host a session first.");
        public MultiplayerConnectionActionState ReleaseSetupAction = MultiplayerConnectionActionState.Unavailable("Everyone Loaded", "Setup is not ready.");
        public MultiplayerSetupReadinessText SetupReadiness = new MultiplayerSetupReadinessText();
        public MultiplayerConnectionWizardModel Wizard = new MultiplayerConnectionWizardModel();
        public MultiplayerAutoLoadStatusText AutoLoadDisplayStatus = new MultiplayerAutoLoadStatusText();
        public MultiplayerTimelineStatusText TimelineStatus = new MultiplayerTimelineStatusText();
        public ShelteredMultiplayerMapAnchorReport MapAnchorReport;

        public string RoleText = string.Empty;
        public string StateText = string.Empty;
        public string ConnectionSummary = string.Empty;
        public string ConnectionDetail = string.Empty;
        public string LocalEndpointText = string.Empty;
        public string LanEndpointText = string.Empty;
        public string EndpointCandidateStatus = string.Empty;
        public string DiscoveryFallbackText = string.Empty;
        public string LocalPeerIdText = string.Empty;
        public string ConfigurationSummary = string.Empty;
        public string SetupStatus = string.Empty;
        public string SetupLastError = string.Empty;
        public MultiplayerAutoLoadStatus AutoLoadFlowStatus;
        public string AutoLoadFlowStatusText = string.Empty;
        public string AutoLoadFlowLastError = string.Empty;
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
        public NetworkSessionState SessionState;
    }
}
