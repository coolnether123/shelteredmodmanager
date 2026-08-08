using System.Collections.Generic;

using ShelteredAPI.Scenarios.Definitions;
using ShelteredScenarioEditor.Application.Authoring;
using ShelteredScenarioEditor.Application.Runtime;
using ShelteredScenarioEditor.Presentation.Authoring.Shell;

namespace ShelteredScenarioEditor.Application.Commands
{
    internal sealed class TypedStoryAuthoringCommandHandler : IScenarioCommandHandler
    {
        private readonly ScenarioStoryAuthoringService _service;
        private readonly IScenarioEditorService _editorService;
        private readonly ScenarioAuthoringRendererInteractionState _rendererInteraction;

        internal TypedStoryAuthoringCommandHandler(ScenarioStoryAuthoringService service, IScenarioEditorService editorService, ScenarioAuthoringRendererInteractionState rendererInteraction)
        {
            _service = service;
            _editorService = editorService;
            _rendererInteraction = rendererInteraction;
        }

        public bool CanHandle(ScenarioAuthoringCommand command) { return command is StoryAuthoringCommand; }

        public ScenarioCommandDispatchResult Handle(ScenarioAuthoringState state, ScenarioAuthoringCommand command)
        {
            StoryAuthoringCommand story = command as StoryAuthoringCommand;
            string reason = null;
            if (story == null || !story.ValidateStructure(out reason)) return Result(false, reason ?? "Story command is invalid.");
            if (_service == null || _editorService == null) return Result(false, "Story editing is unavailable.");
            ScenarioEditorSession session = _editorService.CurrentSession;
            ScenarioDefinition definition = session != null ? session.WorkingDefinition : null;
            ScenarioNpcDefinition selectedCharacter = ResolveSelectedCharacter(definition);
            ScenarioConversationDefinition selectedConversation = ResolveSelectedConversation(definition);
            string message;
            bool changed = _service.TryHandleCommand(session, story, out message);
            if (changed)
            {
                if (story.Kind == StoryAuthoringCommandKind.AddStage
                    || story.Kind == StoryAuthoringCommandKind.AddRoutedStage
                    || story.Kind == StoryAuthoringCommandKind.AddUnansweredStage)
                {
                    List<ScenarioFlowStageDefinition> stages = definition != null && definition.ScenarioFlow != null ? definition.ScenarioFlow.Stages : null;
                    if (stages != null && stages.Count > 0)
                    {
                        ScenarioStoryFocusedEditorActions.SelectStageDocument(definition, stages.Count - 1, _rendererInteraction);
                        state.FocusedEditorIndex = -1;
                        state.FocusedEditorIsNew = false;
                        state.TimelineSelectedEntryId = ScenarioStoryFocusedEditorActions.FocusedEntryId(stages.Count - 1);
                    }
                }
                ReconcileCharacterSelection(definition, story, selectedCharacter);
                ReconcileConversationSelection(definition, story, selectedConversation);
            }
            return Result(changed, message);
        }

        private ScenarioNpcDefinition ResolveSelectedCharacter(ScenarioDefinition definition)
        {
            string selected = _rendererInteraction.GetWorkspaceSelection(ScenarioStoryFocusedEditorActions.WorkspaceId, ScenarioStoryFocusedEditorActions.CharactersSubtabId);
            int index;
            return ScenarioStoryFocusedEditorActions.TryResolveCharacterEntity(definition, selected, out index) && definition != null && definition.ScenarioCharacters != null ? definition.ScenarioCharacters[index] : null;
        }

        private ScenarioConversationDefinition ResolveSelectedConversation(ScenarioDefinition definition)
        {
            string selected = _rendererInteraction.GetWorkspaceSelection(ScenarioStoryFocusedEditorActions.WorkspaceId, ScenarioStoryFocusedEditorActions.ConversationsSubtabId);
            int index;
            return ScenarioStoryFocusedEditorActions.TryResolveConversationEntity(definition, selected, out index) && definition != null && definition.Conversations != null ? definition.Conversations.Conversations[index] : null;
        }

        private void ReconcileCharacterSelection(ScenarioDefinition definition, StoryAuthoringCommand command, ScenarioNpcDefinition selected)
        {
            List<ScenarioNpcDefinition> characters = definition != null ? definition.ScenarioCharacters : null;
            if (characters == null) return;
            if (command.Kind == StoryAuthoringCommandKind.AddStoryCharacter && characters.Count > 0) { ScenarioStoryFocusedEditorActions.SelectCharacterDocument(definition, characters.Count - 1, _rendererInteraction); return; }
            if (command.Kind != StoryAuthoringCommandKind.DeleteStoryCharacter || selected == null) return;
            for (int i = 0; i < characters.Count; i++) if (object.ReferenceEquals(characters[i], selected)) { ScenarioStoryFocusedEditorActions.SelectCharacterDocument(definition, i, _rendererInteraction); return; }
            _rendererInteraction.SetWorkspaceSelection(ScenarioStoryFocusedEditorActions.WorkspaceId, ScenarioStoryFocusedEditorActions.CharactersSubtabId, null);
        }

        private void ReconcileConversationSelection(ScenarioDefinition definition, StoryAuthoringCommand command, ScenarioConversationDefinition selected)
        {
            List<ScenarioConversationDefinition> conversations = definition != null && definition.Conversations != null ? definition.Conversations.Conversations : null;
            if (conversations == null) return;
            if (command.Kind == StoryAuthoringCommandKind.AddConversation && conversations.Count > 0) { ScenarioStoryFocusedEditorActions.SelectConversationDocument(definition, conversations.Count - 1, _rendererInteraction); return; }
            if (command.Kind == StoryAuthoringCommandKind.DuplicateConversation && command.PrimaryIndex + 1 < conversations.Count) { ScenarioStoryFocusedEditorActions.SelectConversationDocument(definition, command.PrimaryIndex + 1, _rendererInteraction); return; }
            if (command.Kind != StoryAuthoringCommandKind.DeleteConversation || selected == null) return;
            for (int i = 0; i < conversations.Count; i++) if (object.ReferenceEquals(conversations[i], selected)) { ScenarioStoryFocusedEditorActions.SelectConversationDocument(definition, i, _rendererInteraction); return; }
            _rendererInteraction.SetWorkspaceSelection(ScenarioStoryFocusedEditorActions.WorkspaceId, ScenarioStoryFocusedEditorActions.ConversationsSubtabId, null);
        }

        private static ScenarioCommandDispatchResult Result(bool changed, string message) { return new ScenarioCommandDispatchResult { Handled = true, Changed = changed, Message = message }; }
    }
}
