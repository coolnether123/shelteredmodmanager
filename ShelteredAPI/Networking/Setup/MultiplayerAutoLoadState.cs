namespace ShelteredAPI.Networking.Setup
{
    internal enum MultiplayerAutoLoadState
    {
        Idle = 0,
        SetupReceived = 1,
        WaitingForMainMenu = 2,
        OpeningPlay = 3,
        WaitingForGameModeSelection = 4,
        SelectingSurvival = 5,
        WaitingForSlotSelection = 6,
        SelectingSuggestedSlot = 7,
        WaitingForLoadingScene = 8,
        WaitingForShelterScene = 9,
        Loaded = 10,
        Failed = 11,
        Cancelled = 12
    }

    internal enum MultiplayerAutoLoadActionKind
    {
        None = 0,
        PressPlay = 1,
        ChooseSurvival = 2,
        ChooseSlot = 3
    }

    internal struct MultiplayerAutoLoadClockSnapshot
    {
        public MultiplayerAutoLoadClockSnapshot(int frame, int milliseconds)
        {
            Frame = frame >= 0 ? frame : 0;
            Milliseconds = milliseconds >= 0 ? milliseconds : 0;
        }

        public readonly int Frame;
        public readonly int Milliseconds;
    }

    internal sealed class MultiplayerAutoLoadEnvironment
    {
        public MultiplayerAutoLoadEnvironment(MultiplayerAutoLoadClockSnapshot clock)
        {
            Clock = clock;
        }

        public MultiplayerAutoLoadClockSnapshot Clock;
        public bool MainMenuReady;
        public bool GameModeSelectionReady;
        public bool SlotSelectionReady;
        public bool CustomisationPanelActive;
        public bool LoadingSceneActive;
        public bool ShelterSceneActive;
        public bool SessionStarted;
        public string SceneName = string.Empty;
    }

    internal sealed class MultiplayerAutoLoadAction
    {
        private static readonly MultiplayerAutoLoadAction NoneAction =
            new MultiplayerAutoLoadAction(MultiplayerAutoLoadActionKind.None, 0, string.Empty);

        private MultiplayerAutoLoadAction(MultiplayerAutoLoadActionKind kind, int targetSlot, string detailText)
        {
            Kind = kind;
            TargetSlot = targetSlot;
            DetailText = detailText ?? string.Empty;
        }

        public readonly MultiplayerAutoLoadActionKind Kind;
        public readonly int TargetSlot;
        public readonly string DetailText;

        public bool HasAction
        {
            get { return Kind != MultiplayerAutoLoadActionKind.None; }
        }

        public static MultiplayerAutoLoadAction None()
        {
            return NoneAction;
        }

        public static MultiplayerAutoLoadAction PressPlay(string detailText)
        {
            return new MultiplayerAutoLoadAction(MultiplayerAutoLoadActionKind.PressPlay, 0, detailText);
        }

        public static MultiplayerAutoLoadAction ChooseSurvival(string detailText)
        {
            return new MultiplayerAutoLoadAction(MultiplayerAutoLoadActionKind.ChooseSurvival, 0, detailText);
        }

        public static MultiplayerAutoLoadAction ChooseSlot(int targetSlot, string detailText)
        {
            return new MultiplayerAutoLoadAction(MultiplayerAutoLoadActionKind.ChooseSlot, targetSlot, detailText);
        }
    }
}
