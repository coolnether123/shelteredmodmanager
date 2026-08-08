using ShelteredScenarioEditor.Application.Authoring;
using ShelteredScenarioEditor.Application.Runtime;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredScenarioEditor.Presentation.Authoring.Shell;
using ShelteredScenarioEditor.Presentation.Authoring.Windows;

namespace ShelteredScenarioEditor.Application.Commands
{
    internal sealed class ScenarioStoryFocusedEditorCommandHandler : IScenarioCommandHandler
    {
        private readonly ScenarioAuthoringLayoutService _layoutService;
        private readonly ScenarioAuthoringRendererInteractionState _rendererInteraction;
        private readonly IScenarioEditorService _editorService;

        public ScenarioStoryFocusedEditorCommandHandler(
            IScenarioEditorService editorService,
            ScenarioAuthoringLayoutService layoutService,
            ScenarioAuthoringRendererInteractionState rendererInteraction)
        {
            _editorService = editorService;
            _layoutService = layoutService;
            _rendererInteraction = rendererInteraction;
        }

        public bool CanHandle(ScenarioAuthoringCommand command)
        {
            return command is FocusedStoryCommand;
        }

        public ScenarioCommandDispatchResult Handle(ScenarioAuthoringState state, ScenarioAuthoringCommand command)
        {
            FocusedStoryCommand focused = command as FocusedStoryCommand;
            string reason = null;
            if (state == null || focused == null || !focused.ValidateStructure(out reason))
                return Result(false, reason ?? "Focused Story navigation is unavailable.");

            ScenarioEditorSession session = _editorService != null ? _editorService.CurrentSession : null;
            ScenarioDefinition definition = session != null ? session.WorkingDefinition : null;
            if (definition == null
                || definition.ScenarioFlow == null
                || definition.ScenarioFlow.Stages == null
                || focused.StageIndex >= definition.ScenarioFlow.Stages.Count)
            {
                return Result(false, "Story stage no longer exists.");
            }

            if (_layoutService != null)
                _layoutService.SetWindowOpen(state, ScenarioAuthoringWindowIds.Quests, true);
            ScenarioStoryFocusedEditorActions.SelectStageDocument(definition, focused.StageIndex, _rendererInteraction);
            state.FocusedEditorIndex = -1;
            state.FocusedEditorIsNew = false;
            state.TimelineSelectedEntryId = ScenarioStoryFocusedEditorActions.FocusedEntryId(focused.StageIndex);
            return Result(true, "Story stage selected.");
        }

        private static ScenarioCommandDispatchResult Result(bool changed, string message)
        {
            return new ScenarioCommandDispatchResult
            {
                Handled = true,
                Changed = changed,
                Message = message
            };
        }
    }
}
