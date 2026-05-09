using System;
using UnityEngine;

namespace ShelteredAPI.Networking
{
    internal sealed class MultiplayerConnectionPanelRenderer
    {
        private const float CompactLabelWidth = 92f;
        private const float PrimaryButtonWidth = 190f;
        private const float SecondaryButtonWidth = 118f;
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
                new MultiplayerTimelineDiagnosticsTab(),
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
            DrawWizard(model, state);
            DrawAdvancedDiagnostics(model, state, scrollHeight);
            DrawFooter(closeAction);
            GUILayout.EndVertical();
        }

        private static void DrawWizard(
            MultiplayerConnectionPanelViewModel model,
            MultiplayerConnectionPanelState state)
        {
            MultiplayerDiagnosticsWidgets.DrawSectionHeader("Guided Multiplayer");
            DrawStepRow(model.Wizard);

            MultiplayerDiagnosticsWidgets.BeginSection();
            MultiplayerDiagnosticsWidgets.DrawSectionHeader(model.Wizard.Title);
            MultiplayerDiagnosticsWidgets.DrawHint(model.Wizard.Summary);
            MultiplayerDiagnosticsWidgets.DrawHint(model.Wizard.Detail);
            DrawLastError(model);

            switch (model.Wizard.CurrentSection)
            {
                case MultiplayerConnectionWizardSectionKind.Offline:
                    DrawOfflineSection(model, state);
                    break;

                case MultiplayerConnectionWizardSectionKind.Hosting:
                    DrawHostingSection(model, state);
                    break;

                case MultiplayerConnectionWizardSectionKind.Joining:
                    DrawJoiningSection(model, state);
                    break;

                case MultiplayerConnectionWizardSectionKind.ConnectedClient:
                    DrawConnectedClientSection(model);
                    break;

                case MultiplayerConnectionWizardSectionKind.Setup:
                    DrawSetupSection(model);
                    break;

                case MultiplayerConnectionWizardSectionKind.InGame:
                    DrawInGameSection(model);
                    break;
            }

            DrawPrimaryAction(model, state);
            DrawSecondaryActions(model, state);
            MultiplayerDiagnosticsWidgets.EndSection();
        }

        private static void DrawStepRow(MultiplayerConnectionWizardModel wizard)
        {
            if (wizard == null || wizard.Sections == null || wizard.Sections.Length == 0)
                return;

            GUILayout.BeginHorizontal();
            for (int i = 0; i < wizard.Sections.Length; i++)
            {
                MultiplayerConnectionWizardSectionModel section = wizard.Sections[i];
                if (section == null || section.IsAdvanced)
                    continue;

                string label = section.IsCurrent ? "[" + section.Title + "]" : section.Title;
                GUILayout.Label(label, GUILayout.MinWidth(78f));
            }
            GUILayout.EndHorizontal();
        }

        private static void DrawOfflineSection(
            MultiplayerConnectionPanelViewModel model,
            MultiplayerConnectionPanelState state)
        {
            DrawRoleSelector(state);
            model.Wizard = MultiplayerConnectionWizardTextBuilder.Build(model, state);
            MultiplayerConnectionWizardActionBuilder.Populate(model, state);
            if (state.SelectedRole == MultiplayerConnectionWizardRole.Join)
            {
                DrawJoinInput(model, state);
                DrawDiscoveryResults(model, state);
                return;
            }

            DrawHostInput(model, state);
            DrawEndpointCandidates(model);
        }

        private static void DrawHostingSection(
            MultiplayerConnectionPanelViewModel model,
            MultiplayerConnectionPanelState state)
        {
            MultiplayerDiagnosticsWidgets.DrawSubHeader("Session");
            DrawConnectionSummary(model);
            MultiplayerDiagnosticsWidgets.DrawValue("Listening", model.LocalEndpointText);
            DrawEndpointCandidates(model);
        }

        private static void DrawJoiningSection(
            MultiplayerConnectionPanelViewModel model,
            MultiplayerConnectionPanelState state)
        {
            MultiplayerDiagnosticsWidgets.DrawSubHeader("Endpoint");
            MultiplayerDiagnosticsWidgets.DrawValue("Target", model.EndpointValidation.EndpointText);
            MultiplayerDiagnosticsWidgets.DrawValue("State", model.StateText);
            MultiplayerDiagnosticsWidgets.DrawHint("If this stays here, confirm the host endpoint, firewall, and VPN/LAN route.");
        }

        private static void DrawConnectedClientSection(MultiplayerConnectionPanelViewModel model)
        {
            MultiplayerDiagnosticsWidgets.DrawSubHeader("Connection");
            DrawConnectionSummary(model);
            MultiplayerDiagnosticsWidgets.DrawValue("Setup", model.SetupReadiness.StatusText);
            MultiplayerDiagnosticsWidgets.DrawHint(model.SetupReadiness.DetailText);
        }

        private static void DrawSetupSection(MultiplayerConnectionPanelViewModel model)
        {
            MultiplayerDiagnosticsWidgets.DrawSubHeader("Setup Gate");
            MultiplayerDiagnosticsWidgets.DrawValue("Status", model.SetupReadiness.StatusText);
            MultiplayerDiagnosticsWidgets.DrawHint(model.SetupReadiness.DetailText);
            MultiplayerDiagnosticsWidgets.DrawValue("Save sync", model.SaveSyncStatus);
            MultiplayerDiagnosticsWidgets.DrawOptionalError("Save sync error", model.SaveSyncLastError);
            MultiplayerDiagnosticsWidgets.DrawOptionalError("Setup error", model.SetupLastError);
            DrawAutoLoadStatus(model);
        }

        private static void DrawInGameSection(MultiplayerConnectionPanelViewModel model)
        {
            MultiplayerDiagnosticsWidgets.DrawSubHeader("Released State");
            MultiplayerDiagnosticsWidgets.DrawValue("Setup", model.SetupReadiness.StatusText);
            MultiplayerDiagnosticsWidgets.DrawHint(model.SetupReadiness.DetailText);
            DrawConnectionSummary(model);
            DrawAutoLoadStatus(model);
        }

        private static void DrawRoleSelector(MultiplayerConnectionPanelState state)
        {
            MultiplayerDiagnosticsWidgets.DrawSubHeader("Path");
            int selected = state.SelectedRole == MultiplayerConnectionWizardRole.Join ? 1 : 0;
            int next = GUILayout.Toolbar(selected, new string[] { "Host", "Join" });
            state.SelectedRole = next == 1
                ? MultiplayerConnectionWizardRole.Join
                : MultiplayerConnectionWizardRole.Host;
        }

        private static void DrawHostInput(
            MultiplayerConnectionPanelViewModel model,
            MultiplayerConnectionPanelState state)
        {
            MultiplayerDiagnosticsWidgets.DrawSubHeader("Host Port");
            GUILayout.BeginHorizontal();
            GUILayout.Label("Port", GUILayout.Width(CompactLabelWidth));
            state.PortText = GUILayout.TextField(state.PortText, GUILayout.Width(110f));
            GUILayout.EndHorizontal();
            DrawValidationError(model.PortValidation.ErrorText);
        }

        private static void DrawJoinInput(
            MultiplayerConnectionPanelViewModel model,
            MultiplayerConnectionPanelState state)
        {
            MultiplayerDiagnosticsWidgets.DrawSubHeader("Manual Endpoint");
            GUILayout.BeginHorizontal();
            GUILayout.Label("Endpoint", GUILayout.Width(CompactLabelWidth));
            state.EndpointText = GUILayout.TextField(state.EndpointText, GUILayout.MinWidth(280f));
            GUILayout.EndHorizontal();
            DrawValidationError(model.EndpointValidation.ErrorText);
            MultiplayerDiagnosticsWidgets.DrawHint("Example: " + MultiplayerConnectionInputValidator.EndpointExample + ".");
        }

        private static void DrawEndpointCandidates(MultiplayerConnectionPanelViewModel model)
        {
            MultiplayerDiagnosticsWidgets.DrawSubHeader("Endpoints to Share");
            if (model.EndpointCandidates == null || model.EndpointCandidates.Length == 0)
            {
                MultiplayerDiagnosticsWidgets.DrawHint("No local endpoint candidates are available yet.");
            }
            else
            {
                for (int i = 0; i < model.EndpointCandidates.Length; i++)
                    DrawEndpointCandidate(model.EndpointCandidates[i]);
            }

            if (!string.IsNullOrEmpty(model.EndpointCandidateStatus))
                MultiplayerDiagnosticsWidgets.DrawHint(model.EndpointCandidateStatus);
        }

        private static void DrawEndpointCandidate(MultiplayerEndpointCandidate candidate)
        {
            if (candidate == null || string.IsNullOrEmpty(candidate.EndpointText))
                return;

            GUILayout.BeginHorizontal();
            string label = candidate.Recommended ? candidate.Label + " *" : candidate.Label;
            GUILayout.Label(label, GUILayout.Width(CompactLabelWidth));
            GUILayout.TextField(candidate.EndpointText, GUILayout.Width(170f));
            GUILayout.Label(candidate.Description, GUILayout.MinWidth(260f));
            GUILayout.EndHorizontal();
        }

        private static void DrawConnectionSummary(MultiplayerConnectionPanelViewModel model)
        {
            MultiplayerDiagnosticsWidgets.DrawValue("Role", model.RoleText);
            MultiplayerDiagnosticsWidgets.DrawValue("State", model.StateText);
            MultiplayerDiagnosticsWidgets.DrawValue("Peers", model.ConnectedPeerCount + "/" + model.TotalPeerCount);
            MultiplayerDiagnosticsWidgets.DrawValue("Peer ID", model.LocalPeerIdText);
        }

        private static void DrawAutoLoadStatus(MultiplayerConnectionPanelViewModel model)
        {
            if (model.AutoLoadDisplayStatus == null)
                return;

            MultiplayerDiagnosticsWidgets.DrawSubHeader("Auto-load");
            MultiplayerDiagnosticsWidgets.DrawValue("State", model.AutoLoadDisplayStatus.StatusText);
            MultiplayerDiagnosticsWidgets.DrawHint(model.AutoLoadDisplayStatus.DetailText);
        }

        private static void DrawPrimaryAction(
            MultiplayerConnectionPanelViewModel model,
            MultiplayerConnectionPanelState state)
        {
            GUILayout.Space(10f);
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (DrawWizardButton(model.Wizard.PrimaryAction, PrimaryButtonWidth))
                MultiplayerConnectionWizardActionInvoker.Invoke(model.Wizard.PrimaryAction, model, state);
            GUILayout.EndHorizontal();

            if (model.Wizard.PrimaryAction != null
                && !model.Wizard.PrimaryAction.Enabled
                && !string.IsNullOrEmpty(model.Wizard.PrimaryAction.DisabledReason))
            {
                MultiplayerDiagnosticsWidgets.DrawHint("Why disabled: " + model.Wizard.PrimaryAction.DisabledReason);
            }
        }

        private static void DrawSecondaryActions(
            MultiplayerConnectionPanelViewModel model,
            MultiplayerConnectionPanelState state)
        {
            if (model.Wizard.SecondaryActions == null || model.Wizard.SecondaryActions.Length == 0)
                return;

            GUILayout.Space(6f);
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            for (int i = 0; i < model.Wizard.SecondaryActions.Length; i++)
            {
                MultiplayerConnectionWizardAction action = model.Wizard.SecondaryActions[i];
                if (DrawWizardButton(action, SecondaryButtonWidth))
                    MultiplayerConnectionWizardActionInvoker.Invoke(action, model, state);
            }
            GUILayout.EndHorizontal();

            for (int i = 0; i < model.Wizard.SecondaryActions.Length; i++)
            {
                MultiplayerConnectionWizardAction action = model.Wizard.SecondaryActions[i];
                if (action != null && !action.Enabled && !string.IsNullOrEmpty(action.DisabledReason))
                    MultiplayerDiagnosticsWidgets.DrawHint(action.Label + " disabled: " + action.DisabledReason);
            }
        }

        private static void DrawDiscoveryResults(
            MultiplayerConnectionPanelViewModel model,
            MultiplayerConnectionPanelState state)
        {
            if (model.IsDiscovering)
                MultiplayerDiagnosticsWidgets.DrawHint("Searching LAN for hosts...");

            if (model.DiscoveryResults != null && model.DiscoveryResults.Length > 0)
            {
                MultiplayerDiagnosticsWidgets.DrawSubHeader("LAN Discovery Results");
                for (int i = 0; i < model.DiscoveryResults.Length; i++)
                    DrawDiscoveryResult(model, state, model.DiscoveryResults[i]);
            }

            if (!string.IsNullOrEmpty(model.DiscoveryFallbackText))
                MultiplayerDiagnosticsWidgets.DrawHint(model.DiscoveryFallbackText);
        }

        private static void DrawDiscoveryResult(
            MultiplayerConnectionPanelViewModel model,
            MultiplayerConnectionPanelState state,
            string result)
        {
            string line = result ?? string.Empty;
            string endpoint = MultiplayerDiagnosticsFormatter.ExtractEndpoint(line);
            bool canUse = MultiplayerDiagnosticsFormatter.HasUsableDiscoveryEndpoint(endpoint);

            GUILayout.BeginHorizontal();
            GUILayout.Label(line, GUILayout.MinWidth(300f));
            bool previousEnabled = GUI.enabled;
            GUI.enabled = canUse;
            if (GUILayout.Button("Use", GUILayout.Width(52f)))
                state.EndpointText = endpoint;
            if (GUILayout.Button("Join", GUILayout.Width(56f)) && model.Service != null)
                model.Service.Join(endpoint);
            GUI.enabled = previousEnabled;
            GUILayout.EndHorizontal();
        }

        private void DrawAdvancedDiagnostics(
            MultiplayerConnectionPanelViewModel model,
            MultiplayerConnectionPanelState state,
            float scrollHeight)
        {
            GUILayout.Space(8f);
            GUILayout.BeginHorizontal();
            MultiplayerDiagnosticsWidgets.DrawSectionHeader("Advanced / Diagnostics");
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(state.ShowAdvancedDiagnostics ? "Hide" : "Show", GUILayout.Width(70f)))
                state.ShowAdvancedDiagnostics = !state.ShowAdvancedDiagnostics;
            GUILayout.EndHorizontal();

            if (!state.ShowAdvancedDiagnostics)
            {
                MultiplayerDiagnosticsWidgets.DrawHint("Packet counters, peer details, map anchors, timeline, and logs are hidden.");
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
            MultiplayerDiagnosticsWidgets.BeginSection();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Message", GUILayout.Width(CompactLabelWidth));
            state.MessageText = GUILayout.TextField(state.MessageText, GUILayout.MinWidth(180f));
            MultiplayerConnectionWizardAction action =
                MultiplayerConnectionWizardAction.FromActionState(
                    MultiplayerConnectionWizardActionKind.SendTestMessage,
                    model.SendTestMessageAction);
            if (DrawWizardButton(action, 112f))
                MultiplayerConnectionWizardActionInvoker.Invoke(action, model, state);
            GUILayout.EndHorizontal();
            if (!action.Enabled && !string.IsNullOrEmpty(action.DisabledReason))
                MultiplayerDiagnosticsWidgets.DrawHint("Send Ping disabled: " + action.DisabledReason);
            MultiplayerDiagnosticsWidgets.EndSection();
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

        private static void DrawLastError(MultiplayerConnectionPanelViewModel model)
        {
            if (!string.IsNullOrEmpty(model.LastError))
                MultiplayerDiagnosticsWidgets.DrawWarning("Last error: " + model.LastError);
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

        private static bool DrawWizardButton(MultiplayerConnectionWizardAction action, float width)
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
