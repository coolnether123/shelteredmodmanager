using System;
using ModAPI.Networking.Sessions;
using ShelteredAPI.Harmony;
using ShelteredAPI.Networking.Setup;

namespace ShelteredAPI.Networking
{
    internal static class MultiplayerConnectionWizardTextBuilder
    {
        public static MultiplayerConnectionWizardModel Build(
            MultiplayerConnectionPanelViewModel model,
            MultiplayerConnectionPanelState state)
        {
            MultiplayerConnectionWizardModel wizard = new MultiplayerConnectionWizardModel();
            wizard.SelectedRole = state != null ? state.SelectedRole : MultiplayerConnectionWizardRole.Host;
            wizard.CurrentSection = ResolveCurrentSection(model, wizard.SelectedRole);
            PopulateCurrentText(wizard, model);
            wizard.Sections = BuildSections(wizard.CurrentSection);
            return wizard;
        }

        public static MultiplayerAutoLoadStatusText BuildAutoLoadStatus()
        {
            MultiplayerAutoLoadStatusText text = new MultiplayerAutoLoadStatusText();

            try
            {
                MultiplayerAutoLoadStatus status = AutoLoadFlow.CurrentStatus;
                if (status == null)
                    return text;

                text.StatusText = status.CurrentState.ToString();
                text.DetailText = status.DetailText;
                if (!string.IsNullOrEmpty(status.ExpectedCondition))
                    text.DetailText += " Expected: " + status.ExpectedCondition + ".";
                if (status.TargetSlot > 0)
                    text.DetailText += " Target slot: " + status.TargetSlot + ".";
                if (!string.IsNullOrEmpty(status.LastAction))
                    text.DetailText += " Last action: " + status.LastAction + ".";
                if (status.RetryCount > 0)
                    text.DetailText += " Retries: " + status.RetryCount + ".";
                if (!string.IsNullOrEmpty(status.LastError))
                    text.DetailText += " Error: " + status.LastError;
            }
            catch (Exception ex)
            {
                text.StatusText = "Unavailable";
                text.DetailText = "Auto-load state could not be read: " + ex.Message;
            }

            return text;
        }

        public static MultiplayerTimelineStatusText BuildTimelineStatus()
        {
            return BuildTimelineStatus(null);
        }

        public static MultiplayerTimelineStatusText BuildTimelineStatus(string[] lines)
        {
            MultiplayerTimelineStatusText text = new MultiplayerTimelineStatusText();
            text.Lines = lines ?? new string[0];
            if (text.Lines.Length == 0)
            {
                text.StatusText = "No timeline entries";
                text.DetailText = "Timeline diagnostics are available, but no session events have been recorded yet.";
                return text;
            }

            text.StatusText = "Timeline available";
            text.DetailText = "Showing " + text.Lines.Length + " recent session timeline entr"
                + (text.Lines.Length == 1 ? "y." : "ies.");
            return text;
        }

        private static MultiplayerConnectionWizardSectionKind ResolveCurrentSection(
            MultiplayerConnectionPanelViewModel model,
            MultiplayerConnectionWizardRole selectedRole)
        {
            if (model == null || !model.HasActiveSession)
                return MultiplayerConnectionWizardSectionKind.Offline;

            if (model.SetupReadiness != null && model.SetupReadiness.Kind == MultiplayerSetupReadinessKind.Released)
                return MultiplayerConnectionWizardSectionKind.InGame;

            if (IsSetupActive(model))
                return MultiplayerConnectionWizardSectionKind.Setup;

            if (model.Mode == NetworkSessionMode.Host)
                return MultiplayerConnectionWizardSectionKind.Hosting;

            if (model.Mode == NetworkSessionMode.Client)
            {
                if (model.SessionState == NetworkSessionState.Connected)
                    return MultiplayerConnectionWizardSectionKind.ConnectedClient;

                return MultiplayerConnectionWizardSectionKind.Joining;
            }

            return MultiplayerConnectionWizardSectionKind.Offline;
        }

        private static bool IsSetupActive(MultiplayerConnectionPanelViewModel model)
        {
            if (model == null || model.SetupReadiness == null)
                return false;

            return model.SetupReadiness.Kind != MultiplayerSetupReadinessKind.NotStarted;
        }

        private static void PopulateCurrentText(
            MultiplayerConnectionWizardModel wizard,
            MultiplayerConnectionPanelViewModel model)
        {
            switch (wizard.CurrentSection)
            {
                case MultiplayerConnectionWizardSectionKind.Offline:
                    PopulateOfflineText(wizard);
                    return;

                case MultiplayerConnectionWizardSectionKind.Hosting:
                    wizard.Title = "Hosting";
                    wizard.Summary = "Your session is open. Share an endpoint, then begin setup when clients are ready.";
                    wizard.Detail = model.ConnectedPeerCount == 0
                        ? "No clients are connected yet."
                        : model.ConnectedPeerCount + " client peer(s) connected.";
                    return;

                case MultiplayerConnectionWizardSectionKind.Joining:
                    wizard.Title = "Joining";
                    wizard.Summary = "Connecting to the host endpoint.";
                    wizard.Detail = "Waiting for the host handshake to complete. Cancel if the endpoint or VPN/LAN route is wrong.";
                    return;

                case MultiplayerConnectionWizardSectionKind.ConnectedClient:
                    wizard.Title = "Connected Client";
                    wizard.Summary = "Connected to the host.";
                    wizard.Detail = "Wait for the host to begin game setup. Keep this panel open for setup status.";
                    return;

                case MultiplayerConnectionWizardSectionKind.Setup:
                    wizard.Title = "Game Setup";
                    wizard.Summary = model.SetupReadiness != null ? model.SetupReadiness.StatusText : "Setup active";
                    wizard.Detail = model.SetupReadiness != null ? model.SetupReadiness.DetailText : model.SetupStatus;
                    return;

                case MultiplayerConnectionWizardSectionKind.InGame:
                    wizard.Title = "In Game";
                    wizard.Summary = "World start has been released.";
                    wizard.Detail = "The multiplayer setup gate is released. Diagnostics remain available below if needed.";
                    return;

                case MultiplayerConnectionWizardSectionKind.Diagnostics:
                    wizard.Title = "Diagnostics";
                    wizard.Summary = "Advanced packet and runtime diagnostics.";
                    wizard.Detail = "Use this only when troubleshooting networking behavior.";
                    return;

                default:
                    wizard.Title = "Multiplayer";
                    wizard.Summary = model != null ? model.ConnectionSummary : string.Empty;
                    wizard.Detail = model != null ? model.ConnectionDetail : string.Empty;
                    return;
            }
        }

        private static void PopulateOfflineText(MultiplayerConnectionWizardModel wizard)
        {
            wizard.Title = "Start Multiplayer";
            if (wizard.SelectedRole == MultiplayerConnectionWizardRole.Host)
            {
                wizard.Summary = "Host a session for other players.";
                wizard.Detail = "Choose the UDP port, start hosting, then share one of the listed endpoints.";
                return;
            }

            wizard.Summary = "Join a host by endpoint.";
            wizard.Detail = "Manual endpoint entry is the primary join path. LAN discovery can help, but VPNs often require manual entry.";
        }

        private static MultiplayerConnectionWizardSectionModel[] BuildSections(
            MultiplayerConnectionWizardSectionKind current)
        {
            return new MultiplayerConnectionWizardSectionModel[]
            {
                BuildSection(MultiplayerConnectionWizardSectionKind.Offline, "Offline", "Start here", "Host or join.", current, false),
                BuildSection(MultiplayerConnectionWizardSectionKind.Hosting, "Hosting", "Host open", "Share endpoints.", current, false),
                BuildSection(MultiplayerConnectionWizardSectionKind.Joining, "Joining", "Client route", "Connect to host.", current, false),
                BuildSection(MultiplayerConnectionWizardSectionKind.ConnectedClient, "Connected", "Client ready", "Wait for setup.", current, false),
                BuildSection(MultiplayerConnectionWizardSectionKind.Setup, "Setup", "Load gate", "Players load saves.", current, false),
                BuildSection(MultiplayerConnectionWizardSectionKind.InGame, "In Game", "Released", "World start released.", current, false),
                BuildSection(MultiplayerConnectionWizardSectionKind.Diagnostics, "Diagnostics", "Advanced", "Hidden unless opened.", current, true)
            };
        }

        private static MultiplayerConnectionWizardSectionModel BuildSection(
            MultiplayerConnectionWizardSectionKind kind,
            string title,
            string status,
            string detail,
            MultiplayerConnectionWizardSectionKind current,
            bool isAdvanced)
        {
            return new MultiplayerConnectionWizardSectionModel(
                kind,
                title,
                kind == current ? "Current" : status,
                detail,
                kind == current,
                isAdvanced);
        }
    }
}
