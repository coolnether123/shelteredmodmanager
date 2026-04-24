using System.Collections.Generic;

namespace ShelteredAPI.Scenarios
{
    internal sealed class ScenarioAuthoringCommandService
    {
        private readonly ScenarioCommandDispatcher _dispatcher;

        public ScenarioAuthoringCommandService(
            ScenarioAuthoringCaptureService captureService,
            ScenarioSpriteSwapAuthoringService spriteSwapAuthoringService,
            ScenarioSceneSpritePlacementAuthoringService sceneSpritePlacementAuthoringService,
            ScenarioBuildPlacementAuthoringService buildPlacementAuthoringService,
            ScenarioGameplayScheduleAuthoringService gameplayScheduleAuthoringService,
            IScenarioEditorService editorService,
            ScenarioAuthoringSettingsService settingsService,
            ScenarioAuthoringLayoutService layoutService,
            ScenarioStageCoordinator stageCoordinator,
            ScenarioTimelineBuilder timelineBuilder,
            ScenarioTimelineNavigationService timelineNavigationService,
            ScenarioSelectionScopeService selectionScopeService)
        {
            _dispatcher = new ScenarioCommandDispatcher(CreateHandlers(
                captureService,
                spriteSwapAuthoringService,
                sceneSpritePlacementAuthoringService,
                buildPlacementAuthoringService,
                gameplayScheduleAuthoringService,
                editorService,
                settingsService,
                layoutService,
                timelineBuilder,
                timelineNavigationService,
                selectionScopeService));
        }

        public bool Execute(ScenarioAuthoringState state, string actionId)
        {
            if (state == null || string.IsNullOrEmpty(actionId))
                return false;

            string message;
            bool changed = _dispatcher.Dispatch(state, actionId, out message);
            if (!string.IsNullOrEmpty(message))
                state.StatusMessage = message;
            return changed;
        }

        private static IEnumerable<IScenarioCommandHandler> CreateHandlers(
            ScenarioAuthoringCaptureService captureService,
            ScenarioSpriteSwapAuthoringService spriteSwapAuthoringService,
            ScenarioSceneSpritePlacementAuthoringService sceneSpritePlacementAuthoringService,
            ScenarioBuildPlacementAuthoringService buildPlacementAuthoringService,
            ScenarioGameplayScheduleAuthoringService gameplayScheduleAuthoringService,
            IScenarioEditorService editorService,
            ScenarioAuthoringSettingsService settingsService,
            ScenarioAuthoringLayoutService layoutService,
            ScenarioTimelineBuilder timelineBuilder,
            ScenarioTimelineNavigationService timelineNavigationService,
            ScenarioSelectionScopeService selectionScopeService)
        {
            return new IScenarioCommandHandler[]
            {
                new SpriteCommandHandler(spriteSwapAuthoringService, selectionScopeService),
                new SceneSpriteCommandHandler(sceneSpritePlacementAuthoringService, selectionScopeService),
                new BuildCommandHandler(buildPlacementAuthoringService),
                new ShellCommandHandler(layoutService, settingsService),
                new TimelineCommandHandler(editorService, timelineBuilder, timelineNavigationService),
                new CaptureCommandHandler(captureService, editorService, selectionScopeService),
                new GameplayScheduleCommandHandler(gameplayScheduleAuthoringService, editorService),
                new EditorLifecycleCommandHandler(editorService),
                new SelectionCommandHandler(),
                new AssetModeCommandHandler(),
                new ToolCommandHandler(layoutService)
            };
        }
    }
}
