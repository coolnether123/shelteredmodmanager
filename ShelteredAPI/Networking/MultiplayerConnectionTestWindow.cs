using System;
using ModAPI.Networking.Connections;
using ModAPI.Networking.Diagnostics;
using UnityEngine;

namespace ShelteredAPI.Networking
{
    internal sealed class MultiplayerConnectionTestWindow : MonoBehaviour
    {
        private const int WindowId = 774421;
        private const float WindowMinWidth = 620f;
        private const float WindowMinHeight = 640f;
        private const float LabelWidth = 86f;

        private MultiplayerMenuController _controller;
        private Rect _windowRect = new Rect(80f, 80f, 680f, 720f);
        private Vector2 _scroll;
        private string _endpointText = "127.0.0.1:7777";
        private string _portText = "7777";
        private string _messageText = "ping";

        public void Initialize(MultiplayerMenuController controller)
        {
            _controller = controller;
        }

        private void OnGUI()
        {
            if (_controller == null || _controller.Service == null)
                return;

            _windowRect = GUILayout.Window(WindowId, _windowRect, DrawWindow, "Sheltered Multiplayer Test");
            ClampWindowToScreen();
        }

        private void DrawWindow(int id)
        {
            MultiplayerConnectionTestService service = _controller.Service;

            GUILayout.BeginVertical();
            DrawSessionControls(service);

            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(GetScrollHeight()));
            DrawConnectionSummary(service);
            DrawPeers(service);
            DrawDiscovery(service);
            DrawReceivedMessages(service);
            DrawRecentEvents(service);
            DrawLog(service);
            GUILayout.EndScrollView();

            GUILayout.Space(4f);
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Close", GUILayout.Width(90f)))
                UnityEngine.Object.Destroy(this);
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();

            GUI.DragWindow(new Rect(0f, 0f, 10000f, 24f));
        }

        private void DrawSessionControls(MultiplayerConnectionTestService service)
        {
            GUILayout.Label("Direct IP / LAN connectivity test");

            GUILayout.BeginHorizontal();
            GUILayout.Label("Port", GUILayout.Width(LabelWidth));
            _portText = GUILayout.TextField(_portText, GUILayout.Width(90f));
            if (GUILayout.Button("Host", GUILayout.Width(90f)))
                service.StartHost(ParsePort());
            if (GUILayout.Button(service.IsDiscovering ? "Searching..." : "Find LAN", GUILayout.Width(110f)))
                service.StartLanDiscovery(ParsePort());
            GUI.enabled = service.HasActiveSession;
            if (GUILayout.Button("Stop", GUILayout.Width(90f)))
                service.Stop();
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Endpoint", GUILayout.Width(LabelWidth));
            _endpointText = GUILayout.TextField(_endpointText, GUILayout.MinWidth(260f));
            if (GUILayout.Button("Join", GUILayout.Width(90f)))
                service.Join(_endpointText);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Setup", GUILayout.Width(LabelWidth));
            GUI.enabled = service.Mode == ModAPI.Networking.Sessions.NetworkSessionMode.Host && service.HasActiveSession;
            if (GUILayout.Button("Begin Game Setup", GUILayout.Width(150f)))
                service.BeginSetup();
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Message", GUILayout.Width(LabelWidth));
            _messageText = GUILayout.TextField(_messageText, GUILayout.MinWidth(260f));
            GUI.enabled = service.CanSendTestMessage;
            if (GUILayout.Button("Send Ping", GUILayout.Width(110f)))
                service.SendTestMessage(_messageText);
            GUI.enabled = true;
            GUILayout.EndHorizontal();
        }

        private void DrawConnectionSummary(MultiplayerConnectionTestService service)
        {
            NetworkDiagnosticsSnapshot snapshot = service.GetDiagnosticsSnapshot();

            DrawSectionHeader("Connection");
            DrawValue("Role", snapshot != null ? snapshot.Mode.ToString() : service.Mode.ToString());
            DrawValue("State", snapshot != null ? snapshot.State.ToString() : service.Status);
            DrawValue("Local", service.LocalEndpointText);
            DrawValue("Peer ID", snapshot != null ? snapshot.LocalPeerId.ToString() : "unassigned");
            DrawValue("Config", service.ConfigurationSummary);
            DrawValue("Save sync", service.SaveSyncStatus);
            DrawValue("Setup", service.SetupStatus);

            string lastError = service.LastError;
            DrawValue("Last error", string.IsNullOrEmpty(lastError) ? "none" : lastError);
            string saveSyncError = service.SaveSyncLastError;
            if (!string.IsNullOrEmpty(saveSyncError))
                DrawValue("Sync error", saveSyncError);
            string setupError = service.SetupLastError;
            if (!string.IsNullOrEmpty(setupError))
                DrawValue("Setup error", setupError);

            if (!service.CanSendTestMessage)
                DrawHint("Send is disabled until a client is connected, or until a host has at least one connected peer.");
        }

        private void DrawPeers(MultiplayerConnectionTestService service)
        {
            DrawSectionHeader("Peers");

            NetworkDiagnosticsSnapshot snapshot = service.GetDiagnosticsSnapshot();
            if (snapshot != null && snapshot.Peers.Length > 0)
            {
                for (int i = 0; i < snapshot.Peers.Length; i++)
                    DrawPeerDiagnostics(snapshot.Peers[i]);
                return;
            }

            NetworkPeer[] peers = service.GetPeers();
            if (peers.Length == 0)
            {
                GUILayout.Label("No peers.");
                return;
            }

            for (int i = 0; i < peers.Length; i++)
            {
                NetworkPeer peer = peers[i];
                if (peer == null)
                    continue;

                string endpoint = peer.EndPoint != null ? peer.EndPoint.ToString() : "unknown endpoint";
                GUILayout.Label("#" + peer.PeerId + " " + endpoint + " " + peer.State);
            }
        }

        private void DrawPeerDiagnostics(NetworkPeerDiagnosticsSnapshot peer)
        {
            if (peer == null)
                return;

            string endpoint = peer.EndPoint != null ? peer.EndPoint.ToString() : "unknown endpoint";
            string displayName = string.IsNullOrEmpty(peer.DisplayName) ? "unknown" : peer.DisplayName;

            GUILayout.Label("#" + peer.PeerId + " " + peer.State + " " + endpoint);
            DrawValue("Name", displayName);
            DrawValue("Traffic", "sent " + peer.PacketsSent + " packets / " + peer.BytesSent
                + " bytes, received " + peer.PacketsReceived + " packets / " + peer.BytesReceived + " bytes");
            DrawValue("Last send", FormatAge(peer.LastSendUtc));
            DrawValue("Last receive", FormatAge(peer.LastReceiveUtc));
            DrawValue("Latency", peer.HeartbeatLatencyMilliseconds.HasValue
                ? peer.HeartbeatLatencyMilliseconds.Value.ToString("0") + " ms"
                : "unknown");

            if (!string.IsNullOrEmpty(peer.LastError))
                DrawValue("Peer error", peer.LastError);
        }

        private void DrawDiscovery(MultiplayerConnectionTestService service)
        {
            DrawSectionHeader(service.IsDiscovering ? "LAN Discovery: Searching" : "LAN Discovery");

            string[] results = service.GetDiscoveryResults();
            if (results.Length == 0)
            {
                DrawHint("Use Find LAN, or enter an endpoint manually.");
                return;
            }

            for (int i = 0; i < results.Length; i++)
            {
                string result = results[i] ?? string.Empty;
                GUILayout.BeginHorizontal();
                GUILayout.Label(result);
                string endpoint = ExtractEndpoint(result);
                GUI.enabled = endpoint.Length > 0 && endpoint.IndexOf("No hosts", StringComparison.OrdinalIgnoreCase) < 0;
                if (GUILayout.Button("Use", GUILayout.Width(52f)))
                    _endpointText = endpoint;
                if (GUILayout.Button("Join", GUILayout.Width(56f)))
                    service.Join(endpoint);
                GUI.enabled = true;
                GUILayout.EndHorizontal();
            }
        }

        private void DrawReceivedMessages(MultiplayerConnectionTestService service)
        {
            DrawSectionHeader("Received Messages");

            string[] messages = service.GetReceivedMessages();
            if (messages.Length == 0)
            {
                GUILayout.Label("No test messages received.");
                return;
            }

            for (int i = 0; i < messages.Length; i++)
                GUILayout.Label(messages[i] ?? string.Empty);
        }

        private void DrawRecentEvents(MultiplayerConnectionTestService service)
        {
            NetworkDiagnosticsSnapshot snapshot = service.GetDiagnosticsSnapshot();
            DrawSectionHeader("Network Events");

            if (snapshot == null || snapshot.RecentEvents.Length == 0)
            {
                GUILayout.Label("No packet events yet.");
                return;
            }

            int first = Math.Max(0, snapshot.RecentEvents.Length - 12);
            for (int i = first; i < snapshot.RecentEvents.Length; i++)
            {
                NetworkDiagnosticsEvent item = snapshot.RecentEvents[i];
                if (item == null)
                    continue;

                string endpoint = item.EndPoint != null ? item.EndPoint.ToString() : "no endpoint";
                GUILayout.Label(FormatLocalTime(item.TimestampUtc) + " " + item.Kind + " peer "
                    + item.PeerId + " " + endpoint + " seq=" + item.Sequence + " bytes="
                    + item.Bytes + " " + item.Summary);
            }
        }

        private void DrawLog(MultiplayerConnectionTestService service)
        {
            DrawSectionHeader("Service Log");

            string[] lines = service.GetLogTail();
            if (lines.Length == 0)
            {
                GUILayout.Label("No service log entries yet.");
                return;
            }

            for (int i = 0; i < lines.Length; i++)
                GUILayout.Label(lines[i] ?? string.Empty);
        }

        private static void DrawSectionHeader(string text)
        {
            GUILayout.Space(10f);
            GUILayout.Label(text);
        }

        private static void DrawValue(string label, string value)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label + ":", GUILayout.Width(LabelWidth));
            GUILayout.Label(value ?? string.Empty);
            GUILayout.EndHorizontal();
        }

        private static void DrawHint(string text)
        {
            GUILayout.Label(text ?? string.Empty);
        }

        private int ParsePort()
        {
            if (string.IsNullOrEmpty(_portText))
                return MultiplayerConnectionTestService.DefaultPort;

            int port;
            if (!int.TryParse(_portText, out port))
                return -1;

            return port;
        }

        private static string ExtractEndpoint(string discoveryLine)
        {
            if (string.IsNullOrEmpty(discoveryLine))
                return string.Empty;

            int separator = discoveryLine.IndexOf('|');
            if (separator < 0)
                return discoveryLine.Trim();

            return discoveryLine.Substring(0, separator).Trim();
        }

        private static string FormatAge(DateTime utc)
        {
            if (utc == DateTime.MinValue)
                return "never";

            double seconds = (DateTime.UtcNow - utc).TotalSeconds;
            if (seconds < 0)
                seconds = 0;
            if (seconds < 1)
                return "now";
            if (seconds < 60)
                return seconds.ToString("0.0") + "s ago";

            return (seconds / 60.0).ToString("0.0") + "m ago";
        }

        private static string FormatLocalTime(DateTime utc)
        {
            try
            {
                return utc.ToLocalTime().ToString("HH:mm:ss");
            }
            catch
            {
                return "unknown";
            }
        }

        private float GetScrollHeight()
        {
            float available = _windowRect.height - 180f;
            if (available < 320f)
                return 320f;
            return available;
        }

        private void ClampWindowToScreen()
        {
            if (_windowRect.width < WindowMinWidth)
                _windowRect.width = WindowMinWidth;
            if (_windowRect.height < WindowMinHeight)
                _windowRect.height = WindowMinHeight;

            if (_windowRect.width > Screen.width)
                _windowRect.width = Screen.width;
            if (_windowRect.height > Screen.height)
                _windowRect.height = Screen.height;

            if (_windowRect.x < 0f)
                _windowRect.x = 0f;
            if (_windowRect.y < 0f)
                _windowRect.y = 0f;
            if (_windowRect.xMax > Screen.width)
                _windowRect.x = Screen.width - _windowRect.width;
            if (_windowRect.yMax > Screen.height)
                _windowRect.y = Screen.height - _windowRect.height;
        }
    }
}
