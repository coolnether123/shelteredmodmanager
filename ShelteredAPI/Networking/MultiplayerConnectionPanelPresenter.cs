using System.Collections.Generic;
using ModAPI.Networking.Connections;
using ModAPI.Networking.Diagnostics;
using ModAPI.Networking.Sessions;

namespace ShelteredAPI.Networking
{
    internal sealed class MultiplayerConnectionPanelPresenter
    {
        public MultiplayerConnectionPanelViewModel Build(
            MultiplayerConnectionTestService service,
            MultiplayerConnectionPanelState state)
        {
            MultiplayerConnectionPanelViewModel model = new MultiplayerConnectionPanelViewModel();
            model.Service = service;
            PopulateInputState(model, service, state);

            if (service == null)
                return model;

            NetworkDiagnosticsSnapshot snapshot = service.GetDiagnosticsSnapshot();
            NetworkPeer[] fallbackPeers = service.GetPeers();

            model.Snapshot = snapshot;
            model.Peers = fallbackPeers ?? new NetworkPeer[0];
            model.DiscoveryResults = service.GetDiscoveryResults();
            model.ReceivedMessages = service.GetReceivedMessages();
            model.LogLines = service.GetLogTail();
            model.Mode = service.Mode;

            model.LocalEndpointText = service.LocalEndpointText;
            model.LanEndpointText = model.PortValidation.IsValid
                ? service.GetLanEndpointText(model.PortValidation.Port)
                : string.Empty;
            model.LocalPeerIdText = snapshot != null ? snapshot.LocalPeerId.ToString() : "unassigned";
            model.ConfigurationSummary = service.ConfigurationSummary;
            model.SaveSyncStatus = service.SaveSyncStatus;
            model.SaveSyncLastError = service.SaveSyncLastError;
            model.SetupStatus = service.SetupStatus;
            model.SetupLastError = service.SetupLastError;
            model.LastError = service.LastError;
            model.IsDiscovering = service.IsDiscovering;
            model.HasActiveSession = service.HasActiveSession;
            model.CanSendTestMessage = service.CanSendTestMessage;
            model.CanBeginSetup = service.Mode == NetworkSessionMode.Host && service.HasActiveSession;
            model.CanReleaseSetup = service.CanReleaseSetup;
            model.EventCount = snapshot != null ? snapshot.RecentEvents.Length : 0;
            model.SnapshotAgeText = snapshot != null
                ? MultiplayerDiagnosticsFormatter.FormatAge(snapshot.CapturedUtc)
                : "no snapshot";

            PopulatePeerCounts(model, snapshot, fallbackPeers);
            PopulateTrafficTotals(model, snapshot);
            PopulateStatusText(model, snapshot, service);
            PopulateActions(model, service);
            model.SuggestedEndpoints = BuildEndpointSuggestions(service, model.PortValidation);
            return model;
        }

        private static void PopulateInputState(
            MultiplayerConnectionPanelViewModel model,
            MultiplayerConnectionTestService service,
            MultiplayerConnectionPanelState state)
        {
            string portText = state != null ? state.PortText : string.Empty;
            string endpointText = state != null ? state.EndpointText : string.Empty;

            model.PortValidation = MultiplayerConnectionInputValidator.ValidatePortText(portText);
            int endpointDefaultPort = model.PortValidation.IsValid
                ? model.PortValidation.Port
                : MultiplayerConnectionTestService.DefaultPort;
            model.EndpointValidation = MultiplayerConnectionInputValidator.ValidateEndpointText(
                endpointText,
                endpointDefaultPort);

            if (service == null)
            {
                model.HostAction = MultiplayerConnectionActionState.Unavailable("Host", "Service is unavailable.");
                model.JoinAction = MultiplayerConnectionActionState.Unavailable("Join", "Service is unavailable.");
                model.DiscoveryAction = MultiplayerConnectionActionState.Unavailable("Find LAN", "Service is unavailable.");
            }
        }

        private static void PopulatePeerCounts(
            MultiplayerConnectionPanelViewModel model,
            NetworkDiagnosticsSnapshot snapshot,
            NetworkPeer[] fallbackPeers)
        {
            if (snapshot != null)
            {
                model.TotalPeerCount = snapshot.Peers.Length;
                for (int i = 0; i < snapshot.Peers.Length; i++)
                {
                    NetworkPeerDiagnosticsSnapshot peer = snapshot.Peers[i];
                    if (peer != null && peer.State == NetworkConnectionState.Connected)
                        model.ConnectedPeerCount++;
                }

                return;
            }

            if (fallbackPeers == null)
                return;

            model.TotalPeerCount = fallbackPeers.Length;
            for (int i = 0; i < fallbackPeers.Length; i++)
            {
                NetworkPeer peer = fallbackPeers[i];
                if (peer != null && peer.State == NetworkConnectionState.Connected)
                    model.ConnectedPeerCount++;
            }
        }

        private static void PopulateTrafficTotals(
            MultiplayerConnectionPanelViewModel model,
            NetworkDiagnosticsSnapshot snapshot)
        {
            if (snapshot == null)
                return;

            for (int i = 0; i < snapshot.Peers.Length; i++)
            {
                NetworkPeerDiagnosticsSnapshot peer = snapshot.Peers[i];
                if (peer == null)
                    continue;

                model.TotalPacketsSent += peer.PacketsSent;
                model.TotalPacketsReceived += peer.PacketsReceived;
                model.TotalBytesSent += peer.BytesSent;
                model.TotalBytesReceived += peer.BytesReceived;
            }
        }

        private static void PopulateStatusText(
            MultiplayerConnectionPanelViewModel model,
            NetworkDiagnosticsSnapshot snapshot,
            MultiplayerConnectionTestService service)
        {
            NetworkSessionState state = snapshot != null ? snapshot.State : service.SessionState;
            NetworkSessionMode mode = snapshot != null ? snapshot.Mode : service.Mode;
            MultiplayerConnectionStatusText status = MultiplayerConnectionStatusTextBuilder.Build(
                mode,
                state,
                service.HasActiveSession,
                model.ConnectedPeerCount,
                model.TotalPeerCount);

            model.RoleText = status.RoleText;
            model.StateText = status.StateText;
            model.ConnectionSummary = status.SummaryText;
            model.ConnectionDetail = status.DetailText;
            model.SetupReadiness = MultiplayerSetupReadinessTextBuilder.Build(
                model.SetupStatus,
                model.SetupLastError,
                mode,
                service.HasActiveSession,
                service.CanReleaseSetup,
                model.ConnectedPeerCount);
        }

        private static void PopulateActions(
            MultiplayerConnectionPanelViewModel model,
            MultiplayerConnectionTestService service)
        {
            model.HostAction = model.PortValidation.IsValid
                ? MultiplayerConnectionActionState.Available(service.HasActiveSession ? "Restart Host" : "Host")
                : MultiplayerConnectionActionState.Unavailable("Host", model.PortValidation.ErrorText);

            model.JoinAction = model.EndpointValidation.IsValid
                ? MultiplayerConnectionActionState.Available(service.HasActiveSession ? "Reconnect" : "Join")
                : MultiplayerConnectionActionState.Unavailable("Join", model.EndpointValidation.ErrorText);

            model.StopAction = service.HasActiveSession
                ? MultiplayerConnectionActionState.Available("Stop")
                : MultiplayerConnectionActionState.Unavailable("Stop", "No active session.");

            if (!model.PortValidation.IsValid)
                model.DiscoveryAction = MultiplayerConnectionActionState.Unavailable("Find LAN", model.PortValidation.ErrorText);
            else if (service.IsDiscovering)
                model.DiscoveryAction = MultiplayerConnectionActionState.Unavailable("Searching LAN...", "Discovery is already running.");
            else
                model.DiscoveryAction = MultiplayerConnectionActionState.Available("Find LAN");

            model.SendTestMessageAction = service.CanSendTestMessage
                ? MultiplayerConnectionActionState.Available("Send Ping")
                : MultiplayerConnectionActionState.Unavailable("Send Ping", "Connect to a peer before sending test messages.");

            model.BeginSetupAction = model.CanBeginSetup
                ? MultiplayerConnectionActionState.Available("Begin Game Setup")
                : MultiplayerConnectionActionState.Unavailable("Begin Game Setup", "Only an active host session can begin setup.");

            model.ReleaseSetupAction = model.CanReleaseSetup
                ? MultiplayerConnectionActionState.Available("Everyone Loaded")
                : MultiplayerConnectionActionState.Unavailable("Everyone Loaded", "Setup is not ready to release.");
        }

        private static MultiplayerEndpointSuggestion[] BuildEndpointSuggestions(
            MultiplayerConnectionTestService service,
            MultiplayerPortValidationResult port)
        {
            if (service == null || port == null || !port.IsValid)
                return new MultiplayerEndpointSuggestion[0];

            List<MultiplayerEndpointSuggestion> suggestions = new List<MultiplayerEndpointSuggestion>();
            string lanEndpoint = service.GetLanEndpointText(port.Port);
            if (!string.IsNullOrEmpty(lanEndpoint))
            {
                suggestions.Add(new MultiplayerEndpointSuggestion(
                    "LAN",
                    lanEndpoint,
                    "Give this endpoint to friends on the same LAN or VPN."));
            }

            suggestions.Add(new MultiplayerEndpointSuggestion(
                "Local test",
                "127.0.0.1:" + port.Port,
                "Use this for a second local client instance."));

            return suggestions.ToArray();
        }
    }
}
