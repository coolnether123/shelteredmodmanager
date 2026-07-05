using System.Collections.Generic;

using ShelteredAPI.Scenarios.Application.Commands;
using ShelteredAPI.Scenarios.Application.Runtime;
using ShelteredAPI.Scenarios.Application.Selection;
using ShelteredAPI.Scenarios.Application.Stages;
using ShelteredAPI.Scenarios.Application.Timeline;
using ShelteredAPI.Scenarios.Definitions;
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
            ScenarioPublishExportService publishExportService)
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
                publishExportService));
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
            ScenarioPublishExportService publishExportService)
        {
            return new IScenarioCommandHandler[]
            {
                new SpriteCommandHandler(sectionHub.SpriteSwap, selectionScopeService),
                new SceneSpriteCommandHandler(sectionHub.SceneSpritePlacement, sectionHub.BuildPlacement, selectionScopeService),
                new BuildCommandHandler(sectionHub.BuildPlacement, sectionHub.SceneSpritePlacement),
                new ShellCommandHandler(layoutService, settingsService),
                new TimelineCommandHandler(editorService, timelineBuilder, timelineNavigationService),
                new CaptureCommandHandler(captureService, editorService, selectionScopeService),
                new CharacterEditorCommandHandler(characterEditorService, editorService),
                new StoryAuthoringCommandHandler(storyAuthoringService, editorService),
                new EventAuthoringCommandHandler(eventAuthoringService, editorService),
                new GameplayScheduleCommandHandler(sectionHub.GameplaySchedule, editorService),
                new ScenarioPublishCommandHandler(publishExportService),
                new EditorLifecycleCommandHandler(editorService, sectionHub.BuildPlacement, sectionHub.SceneSpritePlacement),
                new SelectionCommandHandler(),
                new ToolCommandHandler(layoutService)
            };
        }
    }
}
