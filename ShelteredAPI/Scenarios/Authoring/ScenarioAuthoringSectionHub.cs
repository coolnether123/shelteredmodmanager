namespace ShelteredAPI.Scenarios
{
    internal interface IScenarioAuthoringSectionHub
    {
        ScenarioSpriteSwapAuthoringService SpriteSwap { get; }
        ScenarioSceneSpritePlacementAuthoringService SceneSpritePlacement { get; }
        ScenarioBuildPlacementAuthoringService BuildPlacement { get; }
        ScenarioGameplayScheduleAuthoringService GameplaySchedule { get; }

        bool Update(ScenarioAuthoringState state, ScenarioEditorSession editorSession, out string statusMessage);
        bool SynchronizeAfterAction(ScenarioAuthoringState state, out string statusMessage);
        void ResetInteractiveSubsystems();
        void RefreshAuthoringArtifacts();
    }

    internal sealed class ScenarioAuthoringSectionHub : IScenarioAuthoringSectionHub
    {
        private readonly ScenarioSpriteSwapAuthoringService _spriteSwap;
        private readonly ScenarioSceneSpritePlacementAuthoringService _sceneSpritePlacement;
        private readonly ScenarioBuildPlacementAuthoringService _buildPlacement;
        private readonly ScenarioGameplayScheduleAuthoringService _gameplaySchedule;

        public ScenarioAuthoringSectionHub(
            ScenarioSpriteSwapAuthoringService spriteSwap,
            ScenarioSceneSpritePlacementAuthoringService sceneSpritePlacement,
            ScenarioBuildPlacementAuthoringService buildPlacement,
            ScenarioGameplayScheduleAuthoringService gameplaySchedule)
        {
            _spriteSwap = spriteSwap;
            _sceneSpritePlacement = sceneSpritePlacement;
            _buildPlacement = buildPlacement;
            _gameplaySchedule = gameplaySchedule;
        }

        public ScenarioSpriteSwapAuthoringService SpriteSwap
        {
            get { return _spriteSwap; }
        }

        public ScenarioSceneSpritePlacementAuthoringService SceneSpritePlacement
        {
            get { return _sceneSpritePlacement; }
        }

        public ScenarioBuildPlacementAuthoringService BuildPlacement
        {
            get { return _buildPlacement; }
        }

        public ScenarioGameplayScheduleAuthoringService GameplaySchedule
        {
            get { return _gameplaySchedule; }
        }

        public bool Update(ScenarioAuthoringState state, ScenarioEditorSession editorSession, out string statusMessage)
        {
            bool changed = false;
            statusMessage = null;

            string buildPlacementMessage;
            if (_buildPlacement.Update(state, editorSession, out buildPlacementMessage))
            {
                changed = true;
                if (!string.IsNullOrEmpty(buildPlacementMessage))
                    statusMessage = buildPlacementMessage;
            }

            string pickerMessage;
            if (_spriteSwap.SynchronizePicker(state, out pickerMessage))
            {
                changed = true;
                if (!string.IsNullOrEmpty(pickerMessage))
                    statusMessage = pickerMessage;
            }

            return changed;
        }

        public bool SynchronizeAfterAction(ScenarioAuthoringState state, out string statusMessage)
        {
            return _spriteSwap.SynchronizePicker(state, out statusMessage);
        }

        public void ResetInteractiveSubsystems()
        {
            _buildPlacement.Reset();
            _spriteSwap.ResetTransientState(true);
        }

        public void RefreshAuthoringArtifacts()
        {
            _spriteSwap.Invalidate();
            _sceneSpritePlacement.Invalidate();
        }
    }
}
