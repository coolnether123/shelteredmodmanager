namespace ShelteredAPI.Scenarios
{
    internal interface IScenarioAuthoringSectionHub
    {
        ScenarioSpriteSwapAuthoringService SpriteSwap { get; }
        ScenarioSceneSpritePlacementAuthoringService SceneSpritePlacement { get; }
        ScenarioBuildPlacementAuthoringService BuildPlacement { get; }
        ScenarioGameplayScheduleAuthoringService GameplaySchedule { get; }
        bool ShouldSuppressSelection { get; }

        bool Update(ScenarioAuthoringContext context, out string statusMessage);
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
        private bool _shouldSuppressSelection;

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

        public bool ShouldSuppressSelection
        {
            get { return _shouldSuppressSelection; }
        }

        public bool Update(ScenarioAuthoringContext context, out string statusMessage)
        {
            bool changed = false;
            statusMessage = null;
            ScenarioAuthoringState state = context != null ? context.State : null;
            ScenarioEditorSession editorSession = context != null ? context.EditorSession : null;
            if (_buildPlacement.HasActivePlacement && _sceneSpritePlacement.HasActivePlacement)
            {
                _sceneSpritePlacement.Reset();
                statusMessage = "Scene sprite placement cancelled because another placement tool is active.";
                changed = true;
            }

            _shouldSuppressSelection = _buildPlacement.HasActivePlacement || _sceneSpritePlacement.HasActivePlacement;

            string buildPlacementMessage;
            if (_buildPlacement.Update(state, editorSession, out buildPlacementMessage))
            {
                changed = true;
                if (!string.IsNullOrEmpty(buildPlacementMessage))
                    statusMessage = buildPlacementMessage;
            }

            string sceneSpritePlacementMessage;
            if (_sceneSpritePlacement.Update(state, editorSession, out sceneSpritePlacementMessage))
            {
                changed = true;
                if (!string.IsNullOrEmpty(sceneSpritePlacementMessage))
                    statusMessage = sceneSpritePlacementMessage;
            }

            _shouldSuppressSelection = _shouldSuppressSelection || _buildPlacement.HasActivePlacement || _sceneSpritePlacement.HasActivePlacement;

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
            _sceneSpritePlacement.Reset();
            _spriteSwap.ResetTransientState(true);
            _shouldSuppressSelection = false;
        }

        public void RefreshAuthoringArtifacts()
        {
            _spriteSwap.Invalidate();
            _sceneSpritePlacement.Invalidate();
        }
    }
}
