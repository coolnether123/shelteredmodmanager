using System.Collections.Generic;

using ShelteredAPI.Scenarios.Application.Commands;
using ShelteredAPI.Scenarios.Application.Authoring.Tutorial;
using ShelteredAPI.Scenarios.Application.Runtime;
using ShelteredAPI.Scenarios.Application.Selection;
using ShelteredAPI.Scenarios.Application.Stages;
using ShelteredAPI.Scenarios.Application.Timeline;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Infrastructure.Assets;
using ShelteredAPI.Scenarios.Presentation.Authoring.Shell;
namespace ShelteredAPI.Scenarios.Application.Authoring{
    internal sealed class ScenarioAuthoringCommandService
    {
        private readonly ScenarioCommandDispatcher _dispatcher;

        public ScenarioAuthoringCommandService(
            ScenarioAuthoringCaptureService captureService,
            IScenarioAuthoringSectionHub sectionHub,
            IScenarioEditorService editorService,
            ScenarioAuthoringSettingsService settingsService,
            ScenarioAuthoringLayoutService layoutService,
            ScenarioStageCoordinator stageCoordinator,
            ScenarioTimelineBuilder timelineBuilder,
            ScenarioTimelineNavigationService timelineNavigationService,
            ScenarioSelectionScopeService selectionScopeService,
            ScenarioCharacterEditorAuthoringService characterEditorService,
            ScenarioStoryAuthoringService storyAuthoringService,
            ScenarioEventAuthoringService eventAuthoringService,
            ScenarioPublishExportService publishExportService,
            ScenarioAuthoringBaseModeReloadService baseModeReloadService,
            ScenarioAuthoringTutorialService tutorialService,
            ScenarioAuthoringSetupStateService setupStateService,
            ScenarioWeatherEffectSpriteCatalogService weatherEffectSpriteCatalog)
        {
            _dispatcher = new ScenarioCommandDispatcher(CreateHandlers(
                captureService,
                sectionHub,
                editorService,
                settingsService,
                layoutService,
                timelineBuilder,
                timelineNavigationService,
                selectionScopeService,
                characterEditorService,
                storyAuthoringService,
                eventAuthoringService,
                publishExportService,
                baseModeReloadService,
                tutorialService,
                setupStateService,
                weatherEffectSpriteCatalog));
        }

        public bool Execute(ScenarioAuthoringState state, string actionId)
        {
            return ExecuteWithResult(state, actionId).Result;
        }

        public ScenarioAuthoringActionExecutionResult ExecuteWithResult(ScenarioAuthoringState state, string actionId)
        {
            if (state == null || string.IsNullOrEmpty(actionId))
                return ScenarioAuthoringActionExecutionResult.Unavailable(actionId, "Scenario authoring is not active.");

            string beforeStatus = state.StatusMessage;
            ScenarioCommandDispatchResult dispatch = _dispatcher.DispatchDetailed(state, actionId);
            if (!string.IsNullOrEmpty(dispatch.Message))
                state.StatusMessage = dispatch.Message;

            string afterStatus = state.StatusMessage;
            if (dispatch.Result)
                return ScenarioAuthoringActionExecutionResult.Success(actionId, true, afterStatus);

            string reason = !string.IsNullOrEmpty(dispatch.Message)
                ? dispatch.Message
                : (!string.Equals(beforeStatus, afterStatus) && !string.IsNullOrEmpty(afterStatus)
                    ? afterStatus
                    : null);
            if (string.IsNullOrEmpty(reason))
                reason = dispatch.Handled ? "Action was handled but made no change." : "Action was not handled.";

            return ScenarioAuthoringActionExecutionResult.Failure(actionId, reason, afterStatus);
        }

        private static IEnumerable<IScenarioCommandHandler> CreateHandlers(
            ScenarioAuthoringCaptureService captureService,
            IScenarioAuthoringSectionHub sectionHub,
            IScenarioEditorService editorService,
            ScenarioAuthoringSettingsService settingsService,
            ScenarioAuthoringLayoutService layoutService,
            ScenarioTimelineBuilder timelineBuilder,
            ScenarioTimelineNavigationService timelineNavigationService,
            ScenarioSelectionScopeService selectionScopeService,
            ScenarioCharacterEditorAuthoringService characterEditorService,
            ScenarioStoryAuthoringService storyAuthoringService,
            ScenarioEventAuthoringService eventAuthoringService,
            ScenarioPublishExportService publishExportService,
            ScenarioAuthoringBaseModeReloadService baseModeReloadService,
            ScenarioAuthoringTutorialService tutorialService,
            ScenarioAuthoringSetupStateService setupStateService,
            ScenarioWeatherEffectSpriteCatalogService weatherEffectSpriteCatalog)
        {
            return new IScenarioCommandHandler[]
            {
                new AssetBrowserCommandHandler(sectionHub.BuildPlacement, sectionHub.SceneSpritePlacement, sectionHub.SpriteSwap, layoutService, weatherEffectSpriteCatalog),
                new SpriteCommandHandler(sectionHub.SpriteSwap, selectionScopeService, layoutService, sectionHub.BuildPlacement),
                new SceneSpriteCommandHandler(sectionHub.SceneSpritePlacement, sectionHub.BuildPlacement, selectionScopeService),
                new BuildCommandHandler(sectionHub.BuildPlacement, sectionHub.SceneSpritePlacement),
                new ScenarioHelpCommandHandler(tutorialService, layoutService, setupStateService),
                new ShellCommandHandler(layoutService, settingsService),
                new TutorialCommandHandler(tutorialService, editorService, layoutService),
                new TimelineCommandHandler(editorService, timelineBuilder, timelineNavigationService),
                new CaptureCommandHandler(captureService, editorService, selectionScopeService),
                new CharacterEditorCommandHandler(characterEditorService, editorService),
                new ScenarioStoryFocusedEditorCommandHandler(storyAuthoringService, editorService),
                new StoryAuthoringCommandHandler(storyAuthoringService, editorService),
                new EditorLifecycleCommandHandler(editorService, sectionHub.BuildPlacement, sectionHub.SceneSpritePlacement, baseModeReloadService),
                new EventAuthoringCommandHandler(eventAuthoringService, editorService),
                new GameplayScheduleCommandHandler(sectionHub.GameplaySchedule, editorService),
                new ScenarioWinLossCommandHandler(editorService),
                new ScenarioPublishCommandHandler(publishExportService),
                new SelectionCommandHandler(weatherEffectSpriteCatalog),
                new ToolCommandHandler(layoutService)
            };
        }
    }
}
