using System;
using UnityEngine;

namespace ShelteredAPI.Networking
{
    internal sealed class MultiplayerConnectionPanelRenderer
    {
        private const float CompactLabelWidth = 72f;
        private readonly IMultiplayerDiagnosticsTab[] _tabs;
        private readonly string[] _tabLabels;

        public MultiplayerConnectionPanelRenderer()
        {
            _tabs = new IMultiplayerDiagnosticsTab[]
            {
                new MultiplayerSummaryDiagnosticsTab(),
                new MultiplayerPeerDiagnosticsTab(),
                new MultiplayerTrafficDiagnosticsTab(),
                new MultiplayerLogDiagnosticsTab()
            };
            _tabLabels = BuildTabLabels(_tabs);
        }

        public void Draw(
            MultiplayerConnectionPanelViewModel model,
            MultiplayerConnectionPanelState state,
            float scrollHeight,
            Action closeAction)
        {
            GUILayout.BeginVertical();
            DrawSimpleConnectionPanel(model, state);
            DrawAdvancedDiagnostics(model, state, scrollHeight);
            DrawFooter(closeAction);
            GUILayout.EndVertical();
        }

        private static void DrawSimpleConnectionPanel(
            MultiplayerConnectionPanelViewModel model,
            MultiplayerConnectionPanelState state)
        {
            MultiplayerDiagnosticsWidgets.DrawSectionHeader("Connect / Host");
            DrawStatusStrip(model);

            GUILayout.BeginHorizontal();
            GUILayout.Label("Port", GUILayout.Width(CompactLabelWidth));
            state.PortText = GUILayout.TextField(state.PortText, GUILayout.Width(90f));
            if (GUILayout.Button("Host", GUILayout.Width(90f)))
                model.Service.StartHost(ParsePort(state.PortText));

            bool previousEnabled = GUI.enabled;
            GUI.enabled = model.HasActiveSession;
            if (GUILayout.Button("Stop", GUILayout.Width(90f)))
                model.Service.Stop();
            GUI.enabled = previousEnabled;
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Endpoint", GUILayout.Width(CompactLabelWidth));
            state.EndpointText = GUILayout.TextField(state.EndpointText, GUILayout.MinWidth(260f));
            if (GUILayout.Button("Join", GUILayout.Width(90f)))
                model.Service.Join(state.EndpointText);
            GUILayout.EndHorizontal();
        }

        private static void DrawStatusStrip(MultiplayerConnectionPanelViewModel model)
        {
            GUILayout.BeginHorizontal();
            MultiplayerDiagnosticsWidgets.DrawMiniMetric("Role", model.RoleText);
            MultiplayerDiagnosticsWidgets.DrawMiniMetric("State", model.StateText);
            MultiplayerDiagnosticsWidgets.DrawMiniMetric("Peers", model.ConnectedPeerCount + "/" + model.TotalPeerCount);
            MultiplayerDiagnosticsWidgets.DrawMiniMetric("Local", model.LocalEndpointText);
            GUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(model.LastError))
                MultiplayerDiagnosticsWidgets.DrawWarning("Last error: " + model.LastError);
        }

        private void DrawAdvancedDiagnostics(
            MultiplayerConnectionPanelViewModel model,
            MultiplayerConnectionPanelState state,
            float scrollHeight)
        {
            GUILayout.Space(8f);
            MultiplayerDiagnosticsWidgets.DrawSectionHeader("Advanced Diagnostics");
            DrawDiagnosticsActions(model, state);

            int selectedTab = GUILayout.Toolbar(state.ActiveTabIndex, _tabLabels);
            if (selectedTab < 0 || selectedTab >= _tabs.Length)
                selectedTab = 0;
            state.ActiveTabIndex = selectedTab;

            state.AdvancedScroll = GUILayout.BeginScrollView(state.AdvancedScroll, GUILayout.Height(scrollHeight));
            _tabs[selectedTab].Draw(model, state);
            GUILayout.EndScrollView();
        }

        private static void DrawDiagnosticsActions(
            MultiplayerConnectionPanelViewModel model,
            MultiplayerConnectionPanelState state)
        {
            GUILayout.BeginHorizontal();
            bool previousEnabled = GUI.enabled;

            GUI.enabled = model.CanBeginSetup;
            if (GUILayout.Button("Begin Game Setup", GUILayout.Width(150f)))
                model.Service.BeginSetup();

            GUI.enabled = model.CanReleaseSetup;
            if (GUILayout.Button("Everyone Loaded", GUILayout.Width(140f)))
                model.Service.ReleaseSetupStart();

            GUI.enabled = true;
            if (GUILayout.Button(model.IsDiscovering ? "Searching LAN..." : "Find LAN", GUILayout.Width(120f)))
                model.Service.StartLanDiscovery(ParsePort(state.PortText));

            GUILayout.Label("Message", GUILayout.Width(64f));
            state.MessageText = GUILayout.TextField(state.MessageText, GUILayout.MinWidth(160f));
            GUI.enabled = model.CanSendTestMessage;
            if (GUILayout.Button("Send Ping", GUILayout.Width(110f)))
                model.Service.SendTestMessage(state.MessageText);

            GUI.enabled = previousEnabled;
            GUILayout.EndHorizontal();
        }

        private static void DrawFooter(Action closeAction)
        {
            GUILayout.Space(4f);
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Close", GUILayout.Width(90f)) && closeAction != null)
                closeAction();
            GUILayout.EndHorizontal();
        }

        private static int ParsePort(string portText)
        {
            if (string.IsNullOrEmpty(portText))
                return MultiplayerConnectionTestService.DefaultPort;

            int port;
            if (!int.TryParse(portText, out port))
                return -1;

            return port;
        }

        private static string[] BuildTabLabels(IMultiplayerDiagnosticsTab[] tabs)
        {
            if (tabs == null || tabs.Length == 0)
                return new string[] { "Summary" };

            string[] labels = new string[tabs.Length];
            for (int i = 0; i < tabs.Length; i++)
                labels[i] = tabs[i] != null ? tabs[i].Title : "Tab";

            return labels;
        }
    }
}
