namespace ShelteredAPI.Networking
{
    internal enum MultiplayerConnectionWizardRole
    {
        Host = 0,
        Join = 1
    }

    internal enum MultiplayerConnectionWizardSectionKind
    {
        Offline = 0,
        Hosting = 1,
        Joining = 2,
        ConnectedClient = 3,
        Setup = 4,
        InGame = 5,
        Diagnostics = 6
    }

    internal sealed class MultiplayerConnectionWizardSectionModel
    {
        public MultiplayerConnectionWizardSectionModel(
            MultiplayerConnectionWizardSectionKind kind,
            string title,
            string statusText,
            string detailText,
            bool isCurrent,
            bool isAdvanced)
        {
            Kind = kind;
            Title = title ?? string.Empty;
            StatusText = statusText ?? string.Empty;
            DetailText = detailText ?? string.Empty;
            IsCurrent = isCurrent;
            IsAdvanced = isAdvanced;
        }

        public MultiplayerConnectionWizardSectionKind Kind { get; private set; }
        public string Title { get; private set; }
        public string StatusText { get; private set; }
        public string DetailText { get; private set; }
        public bool IsCurrent { get; private set; }
        public bool IsAdvanced { get; private set; }
    }

    internal sealed class MultiplayerConnectionWizardModel
    {
        public MultiplayerConnectionWizardRole SelectedRole = MultiplayerConnectionWizardRole.Host;
        public MultiplayerConnectionWizardSectionKind CurrentSection = MultiplayerConnectionWizardSectionKind.Offline;
        public MultiplayerConnectionWizardSectionModel[] Sections = new MultiplayerConnectionWizardSectionModel[0];
        public MultiplayerConnectionWizardAction PrimaryAction =
            MultiplayerConnectionWizardAction.Unavailable(MultiplayerConnectionWizardActionKind.None, "Unavailable", "Service is unavailable.");
        public MultiplayerConnectionWizardAction[] SecondaryActions = new MultiplayerConnectionWizardAction[0];
        public string Title = "Start Multiplayer";
        public string Summary = "No multiplayer session is running.";
        public string Detail = "Host a game or join a friend by endpoint.";
    }

    internal sealed class MultiplayerAutoLoadStatusText
    {
        public string StatusText = "Idle";
        public string DetailText = "No auto-load or auto-new-save flow is pending.";
    }

    internal sealed class MultiplayerTimelineStatusText
    {
        public string StatusText = "No timeline entries";
        public string DetailText = "Timeline diagnostics are available, but no session events have been recorded yet.";
        public string[] Lines = new string[0];
    }
}
