using ModAPI.Networking.Connections;
using ModAPI.Networking.Diagnostics;
using ShelteredAPI.Networking.Diagnostics;
using UnityEngine;

namespace ShelteredAPI.Networking
{
    internal interface IMultiplayerDiagnosticsTab
    {
        string Title { get; }
        void Draw(MultiplayerConnectionPanelViewModel model, MultiplayerConnectionPanelState state);
    }

    internal sealed class MultiplayerSummaryDiagnosticsTab : IMultiplayerDiagnosticsTab
    {
        public string Title
        {
            get { return "Summary"; }
        }

        public void Draw(MultiplayerConnectionPanelViewModel model, MultiplayerConnectionPanelState state)
        {
            MultiplayerDiagnosticsWidgets.DrawSectionHeader("Session");
            MultiplayerDiagnosticsWidgets.DrawValue("Role", model.RoleText);
            MultiplayerDiagnosticsWidgets.DrawValue("State", model.StateText);
            MultiplayerDiagnosticsWidgets.DrawValue("Local endpoint", model.LocalEndpointText);
            MultiplayerDiagnosticsWidgets.DrawValue("Local peer ID", model.LocalPeerIdText);
            MultiplayerDiagnosticsWidgets.DrawValue("Config", model.ConfigurationSummary);
            MultiplayerDiagnosticsWidgets.DrawValue("Snapshot age", model.SnapshotAgeText);

            MultiplayerDiagnosticsWidgets.DrawSectionHeader("Systems");
            MultiplayerDiagnosticsWidgets.DrawValue("Save sync", model.SaveSyncStatus);
            MultiplayerDiagnosticsWidgets.DrawValue("Setup", model.SetupStatus);
            MultiplayerDiagnosticsWidgets.DrawOptionalError("Sync error", model.SaveSyncLastError);
            MultiplayerDiagnosticsWidgets.DrawOptionalError("Setup error", model.SetupLastError);

            MultiplayerDiagnosticsWidgets.DrawSectionHeader("Counters");
            GUILayout.BeginHorizontal();
            MultiplayerDiagnosticsWidgets.DrawMiniMetric("Peers", model.ConnectedPeerCount + "/" + model.TotalPeerCount);
            MultiplayerDiagnosticsWidgets.DrawMiniMetric("Events", model.EventCount.ToString());
            MultiplayerDiagnosticsWidgets.DrawMiniMetric("Sent", model.TotalPacketsSent + " / " + MultiplayerDiagnosticsFormatter.FormatBytes(model.TotalBytesSent));
            MultiplayerDiagnosticsWidgets.DrawMiniMetric("Received", model.TotalPacketsReceived + " / " + MultiplayerDiagnosticsFormatter.FormatBytes(model.TotalBytesReceived));
            GUILayout.EndHorizontal();

            if (!model.CanSendTestMessage)
                MultiplayerDiagnosticsWidgets.DrawHint("Test messages unlock after a client connects or a host has at least one peer.");
        }
    }

    internal sealed class MultiplayerPeerDiagnosticsTab : IMultiplayerDiagnosticsTab
    {
        public string Title
        {
            get { return "Peers"; }
        }

        public void Draw(MultiplayerConnectionPanelViewModel model, MultiplayerConnectionPanelState state)
        {
            MultiplayerDiagnosticsWidgets.DrawSectionHeader("Peer Health");

            if (model.Snapshot != null && model.Snapshot.Peers.Length > 0)
            {
                for (int i = 0; i < model.Snapshot.Peers.Length; i++)
                    DrawPeerDiagnostics(model.Snapshot.Peers[i]);
                return;
            }

            if (model.Peers.Length == 0)
            {
                MultiplayerDiagnosticsWidgets.DrawHint("No peers.");
                return;
            }

            for (int i = 0; i < model.Peers.Length; i++)
            {
                NetworkPeer peer = model.Peers[i];
                if (peer == null)
                    continue;

                string endpoint = peer.EndPoint != null ? peer.EndPoint.ToString() : "unknown endpoint";
                GUILayout.Label("#" + peer.PeerId + " " + peer.State + " " + endpoint);
            }
        }

        private static void DrawPeerDiagnostics(NetworkPeerDiagnosticsSnapshot peer)
        {
            if (peer == null)
                return;

            string displayName = string.IsNullOrEmpty(peer.DisplayName) ? "unknown" : peer.DisplayName;

            GUILayout.Space(6f);
            GUILayout.Label("#" + peer.PeerId + " " + peer.State + " " + MultiplayerDiagnosticsFormatter.FormatEndpoint(peer));
            MultiplayerDiagnosticsWidgets.DrawValue("Name", displayName);
            MultiplayerDiagnosticsWidgets.DrawValue("Latency", MultiplayerDiagnosticsFormatter.FormatLatency(peer));
            MultiplayerDiagnosticsWidgets.DrawValue("Last send", MultiplayerDiagnosticsFormatter.FormatAge(peer.LastSendUtc));
            MultiplayerDiagnosticsWidgets.DrawValue("Last receive", MultiplayerDiagnosticsFormatter.FormatAge(peer.LastReceiveUtc));
            MultiplayerDiagnosticsWidgets.DrawValue("Sent", peer.PacketsSent + " packets / " + MultiplayerDiagnosticsFormatter.FormatBytes(peer.BytesSent));
            MultiplayerDiagnosticsWidgets.DrawValue("Received", peer.PacketsReceived + " packets / " + MultiplayerDiagnosticsFormatter.FormatBytes(peer.BytesReceived));
            MultiplayerDiagnosticsWidgets.DrawOptionalError("Peer error", peer.LastError);
        }
    }

    internal sealed class MultiplayerTrafficDiagnosticsTab : IMultiplayerDiagnosticsTab
    {
        public string Title
        {
            get { return "Traffic"; }
        }

        public void Draw(MultiplayerConnectionPanelViewModel model, MultiplayerConnectionPanelState state)
        {
            MultiplayerDiagnosticsWidgets.DrawSectionHeader("Traffic Totals");
            GUILayout.BeginHorizontal();
            MultiplayerDiagnosticsWidgets.DrawMiniMetric("Packets out", model.TotalPacketsSent.ToString());
            MultiplayerDiagnosticsWidgets.DrawMiniMetric("Packets in", model.TotalPacketsReceived.ToString());
            MultiplayerDiagnosticsWidgets.DrawMiniMetric("Bytes out", MultiplayerDiagnosticsFormatter.FormatBytes(model.TotalBytesSent));
            MultiplayerDiagnosticsWidgets.DrawMiniMetric("Bytes in", MultiplayerDiagnosticsFormatter.FormatBytes(model.TotalBytesReceived));
            GUILayout.EndHorizontal();

            MultiplayerDiagnosticsWidgets.DrawSectionHeader("Event Filters");
            GUILayout.BeginHorizontal();
            state.ShowSentEvents = GUILayout.Toggle(state.ShowSentEvents, "Sent", GUILayout.Width(70f));
            state.ShowReceivedEvents = GUILayout.Toggle(state.ShowReceivedEvents, "Received", GUILayout.Width(95f));
            state.ShowPeerEvents = GUILayout.Toggle(state.ShowPeerEvents, "Peers", GUILayout.Width(75f));
            state.ShowSessionEvents = GUILayout.Toggle(state.ShowSessionEvents, "Session", GUILayout.Width(90f));
            GUILayout.EndHorizontal();

            MultiplayerDiagnosticsWidgets.DrawSectionHeader("Recent Events");
            if (model.Snapshot == null || model.Snapshot.RecentEvents.Length == 0)
            {
                MultiplayerDiagnosticsWidgets.DrawHint("No packet events yet.");
                return;
            }

            int shown = 0;
            for (int i = 0; i < model.Snapshot.RecentEvents.Length; i++)
            {
                NetworkDiagnosticsEvent item = model.Snapshot.RecentEvents[i];
                if (item == null || !ShouldShowEvent(item, state))
                    continue;

                shown++;
                DrawEvent(item);
            }

            if (shown == 0)
                MultiplayerDiagnosticsWidgets.DrawHint("No events match the current filters.");
        }

        private static bool ShouldShowEvent(NetworkDiagnosticsEvent item, MultiplayerConnectionPanelState state)
        {
            if (item.Kind == NetworkDiagnosticsEventKind.PacketSent)
                return state.ShowSentEvents;
            if (item.Kind == NetworkDiagnosticsEventKind.PacketReceived)
                return state.ShowReceivedEvents;
            if (item.Kind == NetworkDiagnosticsEventKind.PeerConnected
                || item.Kind == NetworkDiagnosticsEventKind.PeerDisconnected)
                return state.ShowPeerEvents;

            return state.ShowSessionEvents;
        }

        private static void DrawEvent(NetworkDiagnosticsEvent item)
        {
            string endpoint = item.EndPoint != null ? item.EndPoint.ToString() : "no endpoint";
            GUILayout.Label(MultiplayerDiagnosticsFormatter.FormatLocalTime(item.TimestampUtc)
                + " " + item.Kind
                + " peer=" + item.PeerId
                + " seq=" + item.Sequence
                + " messages=" + item.MessageCount
                + " bytes=" + item.Bytes
                + " " + endpoint
                + " " + item.Summary);
        }
    }

    internal sealed class MultiplayerLogDiagnosticsTab : IMultiplayerDiagnosticsTab
    {
        public string Title
        {
            get { return "Logs"; }
        }

        public void Draw(MultiplayerConnectionPanelViewModel model, MultiplayerConnectionPanelState state)
        {
            MultiplayerDiagnosticsWidgets.DrawSectionHeader("Received Messages");
            if (model.ReceivedMessages.Length == 0)
            {
                MultiplayerDiagnosticsWidgets.DrawHint("No test messages received.");
            }
            else
            {
                for (int i = 0; i < model.ReceivedMessages.Length; i++)
                    GUILayout.Label(model.ReceivedMessages[i] ?? string.Empty);
            }

            MultiplayerDiagnosticsWidgets.DrawSectionHeader("Service Log");
            if (model.LogLines.Length == 0)
            {
                MultiplayerDiagnosticsWidgets.DrawHint("No service log entries yet.");
                return;
            }

            for (int i = 0; i < model.LogLines.Length; i++)
                GUILayout.Label(model.LogLines[i] ?? string.Empty);
        }
    }

    internal sealed class MultiplayerMapAnchorDiagnosticsTab : IMultiplayerDiagnosticsTab
    {
        public string Title
        {
            get { return "Map Anchor"; }
        }

        public void Draw(MultiplayerConnectionPanelViewModel model, MultiplayerConnectionPanelState state)
        {
            ShelteredMultiplayerMapAnchorReport report = ShelteredMultiplayerMapAnchorDiagnostics.BuildReport();

            MultiplayerDiagnosticsWidgets.DrawSectionHeader("Active Bunker Anchor");
            MultiplayerDiagnosticsWidgets.DrawValue("MP active", report.MultiplayerActive ? "yes" : "no");
            MultiplayerDiagnosticsWidgets.DrawValue("Session", report.SessionId);
            MultiplayerDiagnosticsWidgets.DrawValue("Local player", report.LocalPlayerId.ToString());
            MultiplayerDiagnosticsWidgets.DrawValue("Bunker owner", report.ActiveBunkerOwnerId.ToString());
            MultiplayerDiagnosticsWidgets.DrawValue("Bunkers", report.BunkerCount.ToString());
            MultiplayerDiagnosticsWidgets.DrawValue("World", FormatVector(report.ActiveWorldPosition));
            MultiplayerDiagnosticsWidgets.DrawValue("Map pixels", FormatVector(report.ActiveMapPixels));
            MultiplayerDiagnosticsWidgets.DrawValue("Grid", report.GridX + ", " + report.GridY);

            MultiplayerDiagnosticsWidgets.DrawSectionHeader("Runtime Objects");
            MultiplayerDiagnosticsWidgets.DrawValue("ExplorationManager", report.HasExplorationManager ? "yes" : "no");
            MultiplayerDiagnosticsWidgets.DrawValue("ExpeditionMap", report.HasExpeditionMap ? "yes" : "no");
            MultiplayerDiagnosticsWidgets.DrawValue("Map sprite", report.HasMapSprite ? "yes" : "no");
            MultiplayerDiagnosticsWidgets.DrawValue("Shelter cell", report.ShelterCellValid ? "yes" : "no");

            MultiplayerDiagnosticsWidgets.DrawSectionHeader("Warnings");
            if (report.Warnings == null || report.Warnings.Length == 0)
            {
                MultiplayerDiagnosticsWidgets.DrawHint(report.MultiplayerActive
                    ? "No map anchor warnings."
                    : "Multiplayer is inactive.");
                return;
            }

            for (int i = 0; i < report.Warnings.Length; i++)
                MultiplayerDiagnosticsWidgets.DrawWarning(report.Warnings[i] ?? string.Empty);
        }

        private static string FormatVector(Vector2 value)
        {
            return value.x.ToString("F1") + ", " + value.y.ToString("F1");
        }

        private static string FormatVector(Vector3 value)
        {
            return value.x.ToString("F1") + ", " + value.y.ToString("F1") + ", " + value.z.ToString("F1");
        }
    }
}
