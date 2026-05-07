namespace ShelteredAPI.Networking
{
    internal sealed class ShelteredMultiplayerSetupSettingsApplicator : IShelteredMultiplayerSessionLifecycleHandler
    {
        private static readonly ShelteredMultiplayerSetupSettingsApplicator _instance =
            new ShelteredMultiplayerSetupSettingsApplicator();

        public static ShelteredMultiplayerSetupSettingsApplicator Instance
        {
            get { return _instance; }
        }

        public void Handle(ShelteredMultiplayerLifecycleEvent lifecycleEvent)
        {
            if (lifecycleEvent == null || lifecycleEvent.Context == null)
                return;

            if (lifecycleEvent.Kind != ShelteredMultiplayerLifecycleEventKind.SetupReceived)
                return;

            ShelteredMultiplayerSetupSettings setup = lifecycleEvent.Context.SetupSettings;
            DifficultyManager.StoreMenuDifficultySettings(
                setup.RainDifficulty,
                setup.ResourceDifficulty,
                setup.BreachDifficulty,
                setup.FactionDifficulty,
                setup.MoodDifficulty,
                setup.MapSize,
                setup.Fog);
        }
    }
}
