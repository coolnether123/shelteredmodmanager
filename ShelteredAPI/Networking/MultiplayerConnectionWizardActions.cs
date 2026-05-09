using ModAPI.Networking.Sessions;

namespace ShelteredAPI.Networking
{
    internal enum MultiplayerConnectionWizardActionKind
    {
        None = 0,
        Host = 1,
        Join = 2,
        Stop = 3,
        DiscoverLan = 4,
        BeginSetup = 5,
        ReleaseSetup = 6,
        SendTestMessage = 7
    }

    internal sealed class MultiplayerConnectionWizardAction
    {
        private MultiplayerConnectionWizardAction(
            MultiplayerConnectionWizardActionKind kind,
            string label,
            bool enabled,
            string disabledReason)
        {
            Kind = kind;
            Label = label ?? string.Empty;
            Enabled = enabled;
            DisabledReason = disabledReason ?? string.Empty;
        }

        public MultiplayerConnectionWizardActionKind Kind { get; private set; }
        public string Label { get; private set; }
        public bool Enabled { get; private set; }
        public string DisabledReason { get; private set; }

        public static MultiplayerConnectionWizardAction Available(
            MultiplayerConnectionWizardActionKind kind,
            string label)
        {
            return new MultiplayerConnectionWizardAction(kind, label, true, string.Empty);
        }

        public static MultiplayerConnectionWizardAction Unavailable(
            MultiplayerConnectionWizardActionKind kind,
            string label,
            string disabledReason)
        {
            return new MultiplayerConnectionWizardAction(kind, label, false, disabledReason);
        }

        public static MultiplayerConnectionWizardAction FromActionState(
            MultiplayerConnectionWizardActionKind kind,
            MultiplayerConnectionActionState action)
        {
            if (action == null)
                return Unavailable(kind, "Unavailable", "Action is unavailable.");

            return action.Enabled
                ? Available(kind, action.Label)
                : Unavailable(kind, action.Label, action.DisabledReason);
        }
    }

    internal static class MultiplayerConnectionWizardActionBuilder
    {
        public static void Populate(
            MultiplayerConnectionPanelViewModel model,
            MultiplayerConnectionPanelState state)
        {
            if (model == null)
                return;

            if (model.Wizard == null)
                model.Wizard = new MultiplayerConnectionWizardModel();

            model.Wizard.PrimaryAction = BuildPrimaryAction(model);
            model.Wizard.SecondaryActions = BuildSecondaryActions(model);
        }

        private static MultiplayerConnectionWizardAction BuildPrimaryAction(
            MultiplayerConnectionPanelViewModel model)
        {
            switch (model.Wizard.CurrentSection)
            {
                case MultiplayerConnectionWizardSectionKind.Offline:
                    return model.Wizard.SelectedRole == MultiplayerConnectionWizardRole.Join
                        ? MultiplayerConnectionWizardAction.FromActionState(MultiplayerConnectionWizardActionKind.Join, model.JoinAction)
                        : MultiplayerConnectionWizardAction.FromActionState(MultiplayerConnectionWizardActionKind.Host, model.HostAction);

                case MultiplayerConnectionWizardSectionKind.Hosting:
                    return BuildHostingPrimaryAction(model);

                case MultiplayerConnectionWizardSectionKind.Joining:
                    return model.HasActiveSession
                        ? MultiplayerConnectionWizardAction.Available(MultiplayerConnectionWizardActionKind.Stop, "Cancel Join")
                        : MultiplayerConnectionWizardAction.FromActionState(MultiplayerConnectionWizardActionKind.Join, model.JoinAction);

                case MultiplayerConnectionWizardSectionKind.ConnectedClient:
                    return MultiplayerConnectionWizardAction.Unavailable(
                        MultiplayerConnectionWizardActionKind.None,
                        "Waiting for Host Setup",
                        "Only the host can begin game setup.");

                case MultiplayerConnectionWizardSectionKind.Setup:
                    return BuildSetupPrimaryAction(model);

                case MultiplayerConnectionWizardSectionKind.InGame:
                    return model.StopAction.Enabled
                        ? MultiplayerConnectionWizardAction.Available(MultiplayerConnectionWizardActionKind.Stop, "Stop Session")
                        : MultiplayerConnectionWizardAction.Unavailable(MultiplayerConnectionWizardActionKind.Stop, "Stop Session", model.StopAction.DisabledReason);

                case MultiplayerConnectionWizardSectionKind.Diagnostics:
                    return MultiplayerConnectionWizardAction.FromActionState(
                        MultiplayerConnectionWizardActionKind.SendTestMessage,
                        model.SendTestMessageAction);

                default:
                    return MultiplayerConnectionWizardAction.Unavailable(
                        MultiplayerConnectionWizardActionKind.None,
                        "Unavailable",
                        "No wizard action is available for this state.");
            }
        }

        private static MultiplayerConnectionWizardAction BuildHostingPrimaryAction(
            MultiplayerConnectionPanelViewModel model)
        {
            if (model.Mode != NetworkSessionMode.Host || !model.HasActiveSession)
            {
                return MultiplayerConnectionWizardAction.Unavailable(
                    MultiplayerConnectionWizardActionKind.BeginSetup,
                    "Begin Game Setup",
                    "Host a session before beginning setup.");
            }

            return MultiplayerConnectionWizardAction.FromActionState(
                MultiplayerConnectionWizardActionKind.BeginSetup,
                model.BeginSetupAction);
        }

        private static MultiplayerConnectionWizardAction BuildSetupPrimaryAction(
            MultiplayerConnectionPanelViewModel model)
        {
            if (model.Mode == NetworkSessionMode.Host)
            {
                if (model.SetupReadiness.Kind == MultiplayerSetupReadinessKind.EveryoneLoaded)
                {
                    return MultiplayerConnectionWizardAction.FromActionState(
                        MultiplayerConnectionWizardActionKind.ReleaseSetup,
                        model.ReleaseSetupAction);
                }

                return MultiplayerConnectionWizardAction.Unavailable(
                    MultiplayerConnectionWizardActionKind.ReleaseSetup,
                    "Everyone Loaded",
                    BuildReleaseDisabledReason(model));
            }

            if (model.Mode == NetworkSessionMode.Client)
            {
                string label = model.SetupReadiness.Kind == MultiplayerSetupReadinessKind.Loading
                    ? "Client Loading"
                    : "Waiting for Host";
                return MultiplayerConnectionWizardAction.Unavailable(
                    MultiplayerConnectionWizardActionKind.None,
                    label,
                    BuildClientSetupDisabledReason(model));
            }

            return MultiplayerConnectionWizardAction.Unavailable(
                MultiplayerConnectionWizardActionKind.None,
                "Setup Active",
                "The active setup role is not known.");
        }

        private static string BuildReleaseDisabledReason(MultiplayerConnectionPanelViewModel model)
        {
            if (!model.HasActiveSession)
                return "No active host session.";
            if (model.SetupReadiness.Kind == MultiplayerSetupReadinessKind.NotStarted)
                return "Begin setup before releasing world start.";
            if (model.SetupReadiness.Kind == MultiplayerSetupReadinessKind.Loading)
                return "The host save is still loading.";
            if (model.SetupReadiness.Kind == MultiplayerSetupReadinessKind.Waiting)
                return model.SetupReadiness.DetailText;
            if (model.SetupReadiness.Kind == MultiplayerSetupReadinessKind.Error)
                return model.SetupReadiness.DetailText;

            return "Wait until the host and all expected clients are loaded.";
        }

        private static string BuildClientSetupDisabledReason(MultiplayerConnectionPanelViewModel model)
        {
            if (model.SetupReadiness.Kind == MultiplayerSetupReadinessKind.Loading)
                return "Finish the local setup load, then wait for the host release.";
            if (model.SetupReadiness.Kind == MultiplayerSetupReadinessKind.EveryoneLoaded)
                return "All players are loaded; waiting for the host to release world start.";
            if (model.SetupReadiness.Kind == MultiplayerSetupReadinessKind.Error)
                return model.SetupReadiness.DetailText;

            return "Only the host can release world start.";
        }

        private static MultiplayerConnectionWizardAction[] BuildSecondaryActions(
            MultiplayerConnectionPanelViewModel model)
        {
            if (model.Wizard.CurrentSection == MultiplayerConnectionWizardSectionKind.Offline
                && model.Wizard.SelectedRole == MultiplayerConnectionWizardRole.Join)
            {
                return new MultiplayerConnectionWizardAction[]
                {
                    MultiplayerConnectionWizardAction.FromActionState(
                        MultiplayerConnectionWizardActionKind.DiscoverLan,
                        model.DiscoveryAction)
                };
            }

            if (model.HasActiveSession
                && model.Wizard.CurrentSection != MultiplayerConnectionWizardSectionKind.Joining
                && model.Wizard.CurrentSection != MultiplayerConnectionWizardSectionKind.InGame)
            {
                return new MultiplayerConnectionWizardAction[]
                {
                    MultiplayerConnectionWizardAction.FromActionState(
                        MultiplayerConnectionWizardActionKind.Stop,
                        model.StopAction)
                };
            }

            return new MultiplayerConnectionWizardAction[0];
        }
    }

    internal static class MultiplayerConnectionWizardActionInvoker
    {
        public static bool Invoke(
            MultiplayerConnectionWizardAction action,
            MultiplayerConnectionPanelViewModel model,
            MultiplayerConnectionPanelState state)
        {
            if (action == null || model == null || state == null || model.Service == null || !action.Enabled)
                return false;

            switch (action.Kind)
            {
                case MultiplayerConnectionWizardActionKind.Host:
                    model.Service.StartHost(model.PortValidation.Port);
                    return true;

                case MultiplayerConnectionWizardActionKind.Join:
                    model.Service.Join(model.EndpointValidation.EndpointText);
                    return true;

                case MultiplayerConnectionWizardActionKind.Stop:
                    model.Service.Stop();
                    return true;

                case MultiplayerConnectionWizardActionKind.DiscoverLan:
                    model.Service.StartLanDiscovery(model.PortValidation.Port);
                    return true;

                case MultiplayerConnectionWizardActionKind.BeginSetup:
                    model.Service.BeginSetup();
                    return true;

                case MultiplayerConnectionWizardActionKind.ReleaseSetup:
                    model.Service.ReleaseSetupStart();
                    return true;

                case MultiplayerConnectionWizardActionKind.SendTestMessage:
                    model.Service.SendTestMessage(state.MessageText);
                    return true;

                default:
                    return false;
            }
        }
    }
}
