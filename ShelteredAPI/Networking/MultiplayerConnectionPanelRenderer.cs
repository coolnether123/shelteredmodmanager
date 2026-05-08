using System;
using UnityEngine;

namespace ShelteredAPI.Networking
{
    internal sealed class MultiplayerConnectionPanelRenderer
    {
        private const float CompactLabelWidth = 72f;
        private const float ActionButtonWidth = 112f;
        private readonly IMultiplayerDiagnosticsTab[] _tabs;
        private readonly string[] _tabLabels;

        public MultiplayerConnectionPanelRenderer()
        {
            _tabs = new IMultiplayerDiagnosticsTab[]
            {
                new MultiplayerSummaryDiagnosticsTab(),
                new MultiplayerPeerDiagnosticsTab(),
                new MultiplayerTrafficDiagnosticsTab(),
                new MultiplayerMapAnchorDiagnosticsTab(),
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
            if (model == null || state == null)
                return;

            GUILayout.BeginVertical();
            DrawStatusOverview(model);
            DrawHostSection(model, state);
            DrawJoinSection(model, state);
            DrawSetupSection(model);
            DrawAdvancedDiagnostics(model, state, scrollHeight);
            DrawFooter(closeAction);
            GUILayout.EndVertical();
        }

        private static void DrawStatusOverview(MultiplayerConnectionPanelViewModel model)
        {
            MultiplayerDiagnosticsWidgets.DrawSectionHeader("Connection");

            GUILayout.BeginHorizontal();
            MultiplayerDiagnosticsWidgets.DrawMiniMetric("Role", model.RoleText);
            MultiplayerDiagnosticsWidgets.DrawMiniMetric("State", model.StateText);
            MultiplayerDiagnosticsWidgets.DrawMiniMetric("Peers", model.ConnectedPeerCount + "/" + model.TotalPeerCount);
            MultiplayerDiagnosticsWidgets.DrawMiniMetric("Peer ID", model.LocalPeerIdText);
            GUILayout.EndHorizontal();

            MultiplayerDiagnosticsWidgets.DrawHint(model.ConnectionSummary);
            MultiplayerDiagnosticsWidgets.DrawHint(model.ConnectionDetail);

            if (!string.IsNullOrEmpty(model.LocalEndpointText))
                MultiplayerDiagnosticsWidgets.DrawValue("Local endpoint", model.LocalEndpointText);

            if (!string.IsNullOrEmpty(model.LastError))
                MultiplayerDiagnosticsWidgets.DrawWarning("Last error: " + model.LastError);
        }

        private static void DrawHostSection(
            MultiplayerConnectionPanelViewModel model,
            MultiplayerConnectionPanelState state)
        {
            MultiplayerDiagnosticsWidgets.DrawSectionHeader("Host");
            MultiplayerDiagnosticsWidgets.DrawHint("Open a UDP host. Friends can join with an endpoint like "
                + MultiplayerConnectionInputValidator.EndpointExample + ".");

            GUILayout.BeginHorizontal();
            GUILayout.Label("Port", GUILayout.Width(CompactLabelWidth));
            state.PortText = GUILayout.TextField(state.PortText, GUILayout.Width(90f));
            if (DrawActionButton(model.HostAction, ActionButtonWidth) && model.Service != null)
                model.Service.StartHost(model.PortValidation.Port);
            if (DrawActionButton(model.StopAction, 90f) && model.Service != null)
                model.Service.Stop();
            GUILayout.EndHorizontal();

            DrawValidationError(model.PortValidation.ErrorText);

            if (!string.IsNullOrEmpty(model.LanEndpointText))
                MultiplayerDiagnosticsWidgets.DrawValue("Give friends", model.LanEndpointText);
            else
                MultiplayerDiagnosticsWidgets.DrawHint("LAN address will appear here when an IPv4 adapter is available.");
        }

        private static void DrawJoinSection(
            MultiplayerConnectionPanelViewModel model,
            MultiplayerConnectionPanelState state)
        {
            MultiplayerDiagnosticsWidgets.DrawSectionHeader("Join");
            MultiplayerDiagnosticsWidgets.DrawHint("Endpoint example: " + MultiplayerConnectionInputValidator.EndpointExample + ".");

            GUILayout.BeginHorizontal();
            GUILayout.Label("Endpoint", GUILayout.Width(CompactLabelWidth));
            state.EndpointText = GUILayout.TextField(state.EndpointText, GUILayout.MinWidth(260f));
            if (DrawActionButton(model.JoinAction, ActionButtonWidth) && model.Service != null)
                model.Service.Join(model.EndpointValidation.EndpointText);
            if (DrawActionButton(model.DiscoveryAction, ActionButtonWidth) && model.Service != null)
                model.Service.StartLanDiscovery(model.PortValidation.Port);
            GUILayout.EndHorizontal();

            DrawValidationError(model.EndpointValidation.ErrorText);
            DrawEndpointSuggestions(model, state);
            DrawDiscoveryResults(model, state);
        }

        private static void DrawEndpointSuggestions(
            MultiplayerConnectionPanelViewModel model,
            MultiplayerConnectionPanelState state)
        {
            if (model.SuggestedEndpoints == null || model.SuggestedEndpoints.Length == 0)
                return;

            for (int i = 0; i < model.SuggestedEndpoints.Length; i++)
            {
                MultiplayerEndpointSuggestion suggestion = model.SuggestedEndpoints[i];
                if (suggestion == null || string.IsNullOrEmpty(suggestion.EndpointText))
                    continue;

                GUILayout.BeginHorizontal();
                GUILayout.Label(suggestion.Label, GUILayout.Width(CompactLabelWidth));
                GUILayout.Label(suggestion.EndpointText, GUILayout.MinWidth(150f));
                GUILayout.Label(suggestion.Description, GUILayout.MinWidth(180f));
                if (GUILayout.Button("Use", GUILayout.Width(52f)))
                    state.EndpointText = suggestion.EndpointText;
                GUILayout.EndHorizontal();
            }
        }

        private static void DrawDiscoveryResults(
            MultiplayerConnectionPanelViewModel model,
            MultiplayerConnectionPanelState state)
        {
            if (model.DiscoveryResults == null || model.DiscoveryResults.Length == 0)
                return;

            MultiplayerDiagnosticsWidgets.DrawSectionHeader(model.IsDiscovering ? "LAN Results: Searching" : "LAN Results");
            for (int i = 0; i < model.DiscoveryResults.Length; i++)
            {
                string result = model.DiscoveryResults[i] ?? string.Empty;
                string endpoint = MultiplayerDiagnosticsFormatter.ExtractEndpoint(result);
                bool canUse = MultiplayerDiagnosticsFormatter.HasUsableDiscoveryEndpoint(endpoint);

                GUILayout.BeginHorizontal();
                GUILayout.Label(result);
                bool previousEnabled = GUI.enabled;
                GUI.enabled = canUse;
                if (GUILayout.Button("Use", GUILayout.Width(52f)))
                    state.EndpointText = endpoint;
                if (GUILayout.Button("Join", GUILayout.Width(56f)) && model.Service != null)
                    model.Service.Join(endpoint);
                GUI.enabled = previousEnabled;
                GUILayout.EndHorizontal();
            }
        }

        private static void DrawSetupSection(MultiplayerConnectionPanelViewModel model)
        {
            MultiplayerDiagnosticsWidgets.DrawSectionHeader("Game Setup");

            GUILayout.BeginHorizontal();
            MultiplayerDiagnosticsWidgets.DrawMiniMetric("Setup", model.SetupReadiness.StatusText);
            MultiplayerDiagnosticsWidgets.DrawMiniMetric("Save sync", model.SaveSyncStatus);
            MultiplayerDiagnosticsWidgets.DrawMiniMetric("Peers", model.ConnectedPeerCount + "/" + model.TotalPeerCount);
            GUILayout.EndHorizontal();

            MultiplayerDiagnosticsWidgets.DrawHint(model.SetupReadiness.DetailText);
            MultiplayerDiagnosticsWidgets.DrawOptionalError("Save sync error", model.SaveSyncLastError);
            MultiplayerDiagnosticsWidgets.DrawOptionalError("Setup error", model.SetupLastError);

            GUILayout.BeginHorizontal();
            if (DrawActionButton(model.BeginSetupAction, 150f) && model.Service != null)
                model.Service.BeginSetup();
            if (DrawActionButton(model.ReleaseSetupAction, 140f) && model.Service != null)
                model.Service.ReleaseSetupStart();
            GUILayout.EndHorizontal();
        }

        private void DrawAdvancedDiagnostics(
            MultiplayerConnectionPanelViewModel model,
            MultiplayerConnectionPanelState state,
            float scrollHeight)
        {
            GUILayout.Space(8f);
            GUILayout.BeginHorizontal();
            MultiplayerDiagnosticsWidgets.DrawSectionHeader("Advanced Diagnostics");
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(state.ShowAdvancedDiagnostics ? "Hide" : "Show", GUILayout.Width(70f)))
                state.ShowAdvancedDiagnostics = !state.ShowAdvancedDiagnostics;
            GUILayout.EndHorizontal();

            if (!state.ShowAdvancedDiagnostics)
            {
                MultiplayerDiagnosticsWidgets.DrawHint("Packet, peer, and map diagnostics are hidden.");
                return;
            }

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
            GUILayout.Label("Message", GUILayout.Width(64f));
            state.MessageText = GUILayout.TextField(state.MessageText, GUILayout.MinWidth(160f));
            if (DrawActionButton(model.SendTestMessageAction, 110f) && model.Service != null)
                model.Service.SendTestMessage(state.MessageText);
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

        private static string[] BuildTabLabels(IMultiplayerDiagnosticsTab[] tabs)
        {
            if (tabs == null || tabs.Length == 0)
                return new string[] { "Summary" };

            string[] labels = new string[tabs.Length];
            for (int i = 0; i < tabs.Length; i++)
                labels[i] = tabs[i] != null ? tabs[i].Title : "Tab";

            return labels;
        }

        private static bool DrawActionButton(MultiplayerConnectionActionState action, float width)
        {
            if (action == null)
                return false;

            bool previousEnabled = GUI.enabled;
            GUI.enabled = action.Enabled;
            bool clicked = GUILayout.Button(
                new GUIContent(action.Label, action.Enabled ? string.Empty : action.DisabledReason),
                GUILayout.Width(width));
            GUI.enabled = previousEnabled;
            return clicked && action.Enabled;
        }

        private static void DrawValidationError(string text)
        {
            if (!string.IsNullOrEmpty(text))
                MultiplayerDiagnosticsWidgets.DrawWarning(text);
        }
    }
}
