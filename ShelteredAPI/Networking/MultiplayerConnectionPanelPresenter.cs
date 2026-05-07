using System;
using ModAPI.Networking.Connections;
using ModAPI.Networking.Diagnostics;
using ModAPI.Networking.Sessions;

namespace ShelteredAPI.Networking
{
    internal sealed class MultiplayerConnectionPanelPresenter
    {
        public MultiplayerConnectionPanelViewModel Build(MultiplayerConnectionTestService service)
        {
            MultiplayerConnectionPanelViewModel model = new MultiplayerConnectionPanelViewModel();
            model.Service = service;

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

            model.RoleText = snapshot != null ? snapshot.Mode.ToString() : service.Mode.ToString();
            model.StateText = snapshot != null ? snapshot.State.ToString() : service.Status;
            model.LocalEndpointText = service.LocalEndpointText;
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
            model.EventCount = snapshot != null ? snapshot.RecentEvents.Length : 0;
            model.SnapshotAgeText = snapshot != null
                ? MultiplayerDiagnosticsFormatter.FormatAge(snapshot.CapturedUtc)
                : "no snapshot";

            PopulatePeerCounts(model, snapshot, fallbackPeers);
            PopulateTrafficTotals(model, snapshot);
            return model;
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
    }
}
