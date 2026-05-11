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
            try
            {
                DrawRenderWarning(state);
                DrawWizard(model, state);
                DrawAdvancedDiagnostics(model, state, scrollHeight);
                DrawFooter(closeAction);
            }
            finally
            {
                GUILayout.EndVertical();
            }
        }

        private static void DrawWizard(
            MultiplayerConnectionPanelViewModel model,
            MultiplayerConnectionPanelState state)
        {
            MultiplayerDiagnosticsWidgets.DrawSectionHeader("Guided Multiplayer");

            MultiplayerDiagnosticsWidgets.BeginSection();
            try
            {
                DrawMainState(model);
                DrawNextAction(model, state);
                DrawEndpointSessionInfo(model, state);
                DrawSetupAutoLoadStatus(model);
                DrawLastError(model);
            }
            finally
            {
                MultiplayerDiagnosticsWidgets.EndSection();
            }
        }

        private static void DrawMainState(MultiplayerConnectionPanelViewModel model)
        {
            MultiplayerDiagnosticsWidgets.DrawSubHeader("State");
            DrawStepRow(model.Wizard);
            MultiplayerDiagnosticsWidgets.DrawValue("Role", model.RoleText);
            MultiplayerDiagnosticsWidgets.DrawValue("State", model.StateText);
            MultiplayerDiagnosticsWidgets.DrawValue("Peers", model.ConnectedPeerCount + "/" + model.TotalPeerCount);
        }

        private static void DrawNextAction(
            MultiplayerConnectionPanelViewModel model,
            MultiplayerConnectionPanelState state)
        {
            MultiplayerDiagnosticsWidgets.DrawSubHeader("Next Action");
            MultiplayerDiagnosticsWidgets.DrawHint(model.Wizard.Title + ": " + model.Wizard.Summary);
            MultiplayerDiagnosticsWidgets.DrawHint(model.Wizard.Detail);
            DrawPrimaryAction(model, state);
            DrawSecondaryActions(model, state);
        }

        private static void DrawEndpointSessionInfo(
            MultiplayerConnectionPanelViewModel model,
            MultiplayerConnectionPanelState state)
        {
            MultiplayerDiagnosticsWidgets.DrawSubHeader("Endpoint / Session");

            switch (model.Wizard.CurrentSection)
            {
                case MultiplayerConnectionWizardSectionKind.Offline:
                    DrawOfflineSection(model, state);
                    return;

                case MultiplayerConnectionWizardSectionKind.Hosting:
                    DrawHostingSection(model, state);
                    return;

                case MultiplayerConnectionWizardSectionKind.Joining:
                    DrawJoiningSection(model, state);
                    return;

                case MultiplayerConnectionWizardSectionKind.ConnectedClient:
                    DrawConnectedClientSection(model);
                    return;

                case MultiplayerConnectionWizardSectionKind.Setup:
                    DrawConnectionSummary(model);
                    return;

                case MultiplayerConnectionWizardSectionKind.InGame:
                    DrawConnectionSummary(model);
                    return;
            }
        }

        private static void DrawSetupAutoLoadStatus(MultiplayerConnectionPanelViewModel model)
        {
            MultiplayerDiagnosticsWidgets.DrawSubHeader("Setup / Auto-load");
            MultiplayerDiagnosticsWidgets.DrawValue("Setup", model.SetupReadiness.StatusText);
            MultiplayerDiagnosticsWidgets.DrawHint(model.SetupReadiness.DetailText);
            MultiplayerDiagnosticsWidgets.DrawOptionalError("Setup error", model.SetupLastError);
            DrawAutoLoadStatus(model);
        }

        private static void DrawStepRow(MultiplayerConnectionWizardModel wizard)
        {
            if (wizard == null || wizard.Sections == null || wizard.Sections.Length == 0)
                return;

            GUILayout.BeginHorizontal();
            try
            {
                for (int i = 0; i < wizard.Sections.Length; i++)
                {
                    MultiplayerConnectionWizardSectionModel section = wizard.Sections[i];
                    if (section == null || section.IsAdvanced)
                        continue;

                    string label = section.IsCurrent ? "[" + section.Title + "]" : section.Title;
                    GUILayout.Label(label, GUILayout.MinWidth(78f));
                }
            }
            finally
            {
                GUILayout.EndHorizontal();
            }
        }

        private static void DrawOfflineSection(
            MultiplayerConnectionPanelViewModel model,
            MultiplayerConnectionPanelState state)
        {
            DrawRoleSelector(state);
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
            DrawConnectionSummary(model);
            MultiplayerDiagnosticsWidgets.DrawValue("Listening", model.LocalEndpointText);
            DrawEndpointCandidates(model);
        }

        private static void DrawJoiningSection(
            MultiplayerConnectionPanelViewModel model,
            MultiplayerConnectionPanelState state)
        {
            MultiplayerDiagnosticsWidgets.DrawValue("Target", model.EndpointValidation.EndpointText);
            MultiplayerDiagnosticsWidgets.DrawValue("State", model.StateText);
            MultiplayerDiagnosticsWidgets.DrawHint("If this stays here, confirm the host endpoint, firewall, and VPN/LAN route.");
        }

        private static void DrawConnectedClientSection(MultiplayerConnectionPanelViewModel model)
        {
            DrawConnectionSummary(model);
            MultiplayerDiagnosticsWidgets.DrawValue("Setup", model.SetupReadiness.StatusText);
            MultiplayerDiagnosticsWidgets.DrawHint(model.SetupReadiness.DetailText);
        }

        private static void DrawSetupSection(MultiplayerConnectionPanelViewModel model)
        {
            MultiplayerDiagnosticsWidgets.DrawSubHeader("Setup Gate");
            MultiplayerDiagnosticsWidgets.DrawValue("Status", model.SetupReadiness.StatusText);
            MultiplayerDiagnosticsWidgets.DrawHint(model.SetupReadiness.DetailText);
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
            MultiplayerConnectionWizardRole nextRole = next == 1
                ? MultiplayerConnectionWizardRole.Join
                : MultiplayerConnectionWizardRole.Host;
            if (state.SelectedRole != nextRole)
            {
                state.SelectedRole = nextRole;
                state.UiRevision++;
            }
        }

        private static void DrawHostInput(
            MultiplayerConnectionPanelViewModel model,
            MultiplayerConnectionPanelState state)
        {
            MultiplayerDiagnosticsWidgets.DrawSubHeader("Host Port");
            GUILayout.BeginHorizontal();
            try
            {
                GUILayout.Label("Port", GUILayout.Width(CompactLabelWidth));
                string nextPortText = GUILayout.TextField(state.PortText, GUILayout.Width(110f));
                if (!string.Equals(state.PortText, nextPortText, StringComparison.Ordinal))
                {
                    state.PortText = nextPortText;
                    state.UiRevision++;
                }
            }
            finally
            {
                GUILayout.EndHorizontal();
            }
            DrawValidationError(model.PortValidation.ErrorText);
        }

        private static void DrawJoinInput(
            MultiplayerConnectionPanelViewModel model,
            MultiplayerConnectionPanelState state)
        {
            MultiplayerDiagnosticsWidgets.DrawSubHeader("Manual Endpoint");
            GUILayout.BeginHorizontal();
            try
            {
                GUILayout.Label("Endpoint", GUILayout.Width(CompactLabelWidth));
                string nextEndpointText = GUILayout.TextField(state.EndpointText, GUILayout.MinWidth(280f));
                if (!string.Equals(state.EndpointText, nextEndpointText, StringComparison.Ordinal))
                {
                    state.EndpointText = nextEndpointText;
                    state.UiRevision++;
                }
            }
            finally
            {
                GUILayout.EndHorizontal();
            }
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
            try
            {
                string label = candidate.Recommended ? candidate.Label + " *" : candidate.Label;
                GUILayout.Label(label, GUILayout.Width(CompactLabelWidth));
                GUILayout.TextField(candidate.EndpointText, GUILayout.Width(170f));
                GUILayout.Label(candidate.Description, GUILayout.MinWidth(260f));
            }
            finally
            {
                GUILayout.EndHorizontal();
            }
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
            bool clicked = false;
            GUILayout.BeginHorizontal();
            try
            {
                GUILayout.FlexibleSpace();
                clicked = DrawWizardButton(model.Wizard.PrimaryAction, PrimaryButtonWidth);
            }
            finally
            {
                GUILayout.EndHorizontal();
            }

            if (clicked)
                QueueAction(state, model.Wizard.PrimaryAction);

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
            MultiplayerConnectionWizardAction clickedAction = null;
            GUILayout.BeginHorizontal();
            try
            {
                GUILayout.FlexibleSpace();
                for (int i = 0; i < model.Wizard.SecondaryActions.Length; i++)
                {
                    MultiplayerConnectionWizardAction action = model.Wizard.SecondaryActions[i];
                    if (DrawWizardButton(action, SecondaryButtonWidth))
                        clickedAction = action;
                }
            }
            finally
            {
                GUILayout.EndHorizontal();
            }

            if (clickedAction != null)
                QueueAction(state, clickedAction);

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
            bool canJoin = canUse && model.Service != null;
            string reason = canUse ? string.Empty : "No usable endpoint";
            bool useClicked = false;
            bool joinClicked = false;

            GUILayout.BeginHorizontal();
            try
            {
                GUILayout.Label(line, GUILayout.MinWidth(300f));
                useClicked = DrawFixedEnabledButton("Use", canUse, reason, 52f);
                joinClicked = DrawFixedEnabledButton("Join", canJoin, canUse ? "Service is unavailable." : reason, 56f);
                GUILayout.Label(reason, GUILayout.MinWidth(128f));
            }
            finally
            {
                GUILayout.EndHorizontal();
            }

            if (useClicked)
            {
                state.EndpointText = endpoint;
                state.UiRevision++;
            }
            if (joinClicked)
            {
                state.EndpointText = endpoint;
                state.UiRevision++;
                QueueAction(
                    state,
                    MultiplayerConnectionWizardAction.Available(MultiplayerConnectionWizardActionKind.Join, "Join"));
            }
        }

        private void DrawAdvancedDiagnostics(
            MultiplayerConnectionPanelViewModel model,
            MultiplayerConnectionPanelState state,
            float scrollHeight)
        {
            GUILayout.Space(8f);
            bool toggleAdvanced = false;
            GUILayout.BeginHorizontal();
            try
            {
                MultiplayerDiagnosticsWidgets.DrawSectionHeader("Advanced / Diagnostics");
                GUILayout.FlexibleSpace();
                toggleAdvanced = GUILayout.Button(state.ShowAdvancedDiagnostics ? "Hide" : "Show", GUILayout.Width(70f));
            }
            finally
            {
                GUILayout.EndHorizontal();
            }

            if (toggleAdvanced)
            {
                state.ShowAdvancedDiagnostics = !state.ShowAdvancedDiagnostics;
                state.UiRevision++;
            }

            if (!state.ShowAdvancedDiagnostics)
            {
                MultiplayerDiagnosticsWidgets.DrawHint("Packet counters, peer details, map anchors, timeline, and logs are hidden.");
                return;
            }

            DrawDiagnosticsActions(model, state);

            int selectedTab = GUILayout.Toolbar(state.ActiveTabIndex, _tabLabels);
            if (selectedTab < 0 || selectedTab >= _tabs.Length)
                selectedTab = 0;
            if (state.ActiveTabIndex != selectedTab)
            {
                state.ActiveTabIndex = selectedTab;
                state.UiRevision++;
            }

            state.AdvancedScroll = GUILayout.BeginScrollView(state.AdvancedScroll, GUILayout.Height(scrollHeight));
            try
            {
                _tabs[selectedTab].Draw(model, state);
            }
            finally
            {
                GUILayout.EndScrollView();
            }
        }

        private static void DrawDiagnosticsActions(
            MultiplayerConnectionPanelViewModel model,
            MultiplayerConnectionPanelState state)
        {
            MultiplayerDiagnosticsWidgets.BeginSection();
            MultiplayerConnectionWizardAction clickedAction = null;
            MultiplayerConnectionWizardAction action =
                MultiplayerConnectionWizardAction.FromActionState(
                    MultiplayerConnectionWizardActionKind.SendTestMessage,
                    model.SendTestMessageAction);
            GUILayout.BeginHorizontal();
            try
            {
                GUILayout.Label("Message", GUILayout.Width(CompactLabelWidth));
                string nextMessageText = GUILayout.TextField(state.MessageText, GUILayout.MinWidth(180f));
                if (!string.Equals(state.MessageText, nextMessageText, StringComparison.Ordinal))
                {
                    state.MessageText = nextMessageText;
                    state.UiRevision++;
                }
                if (DrawWizardButton(action, 112f))
                    clickedAction = action;
            }
            finally
            {
                GUILayout.EndHorizontal();
                MultiplayerDiagnosticsWidgets.EndSection();
            }

            if (clickedAction != null)
                QueueAction(state, clickedAction);
            if (!action.Enabled && !string.IsNullOrEmpty(action.DisabledReason))
                MultiplayerDiagnosticsWidgets.DrawHint("Send Ping disabled: " + action.DisabledReason);
        }

        private static void DrawFooter(Action closeAction)
        {
            GUILayout.Space(4f);
            bool clicked = false;
            GUILayout.BeginHorizontal();
            try
            {
                GUILayout.FlexibleSpace();
                clicked = GUILayout.Button("Close", GUILayout.Width(90f));
            }
            finally
            {
                GUILayout.EndHorizontal();
            }

            if (clicked && closeAction != null)
                closeAction();
        }

        private static void DrawLastError(MultiplayerConnectionPanelViewModel model)
        {
            MultiplayerDiagnosticsWidgets.DrawSubHeader("Last Error");
            MultiplayerDiagnosticsWidgets.DrawHint(string.IsNullOrEmpty(model.LastError) ? "None" : model.LastError);
        }

        private static void DrawRenderWarning(MultiplayerConnectionPanelState state)
        {
            if (state != null && !string.IsNullOrEmpty(state.LastRenderErrorText))
                MultiplayerDiagnosticsWidgets.DrawWarning("Previous render failed: " + state.LastRenderErrorText);
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

            return DrawFixedEnabledButton(action.Label, action.Enabled, action.DisabledReason, width);
        }

        private static void QueueAction(
            MultiplayerConnectionPanelState state,
            MultiplayerConnectionWizardAction action)
        {
            if (state == null || action == null || !action.Enabled)
                return;

            state.PendingActionKind = action.Kind;
        }

        private static bool DrawFixedEnabledButton(
            string label,
            bool enabled,
            string disabledReason,
            float width)
        {
            bool previousEnabled = GUI.enabled;
            GUI.enabled = enabled;
            try
            {
                bool clicked = GUILayout.Button(
                    new GUIContent(label ?? string.Empty, enabled ? string.Empty : disabledReason ?? string.Empty),
                    GUILayout.Width(width));
                return clicked && enabled;
            }
            finally
            {
                GUI.enabled = previousEnabled;
            }
        }

        private static void DrawValidationError(string text)
        {
            if (!string.IsNullOrEmpty(text))
                MultiplayerDiagnosticsWidgets.DrawWarning(text);
        }
    }
}
