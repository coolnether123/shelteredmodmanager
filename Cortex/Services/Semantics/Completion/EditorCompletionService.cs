using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.LanguageService.Protocol;
using Cortex.Services.Semantics.Requests;
using Cortex.Services.Semantics.Completion.Augmentation;

namespace Cortex.Services.Semantics.Completion
{
    internal sealed class EditorCompletionService : IEditorCompletionService
    {
        private readonly EditorCompletionRequestService _requestService;
        private readonly EditorCompletionSessionService _sessionService;

        public EditorCompletionService()
            : this(
                new EditorCompletionRequestService(new EditorLanguageRequestFactory(), new EditorCompletionInteractionPolicy()),
                new EditorCompletionSessionService(new CompletionRankingService(), new EditorCompletionInteractionPolicy()))
        {
        }

        internal EditorCompletionService(EditorCompletionRequestService requestService, EditorCompletionSessionService sessionService)
        {
            _requestService = requestService;
            _sessionService = sessionService;
        }

        public bool ShouldDispatch(CortexCompletionInteractionState editorState, bool requestInFlight) { return _requestService.ShouldDispatch(editorState, requestInFlight); }
        public LanguageServiceCompletionRequest BuildWorkerRequest(DocumentSession session, CortexSettings settings, CortexProjectDefinition project, string[] sourceRoots, CortexCompletionInteractionState editorState) { return _requestService.BuildWorkerRequest(session, settings, project, sourceRoots, editorState); }
        public bool AcceptResponse(CortexCompletionInteractionState editorState, DocumentSession target, DocumentLanguageCompletionRequestState pending, LanguageServiceCompletionResponse response) { return _sessionService.AcceptResponse(editorState, target, pending, response); }
        public bool MergeSupplementalResponse(CortexCompletionInteractionState editorState, DocumentSession target, DocumentLanguageCompletionRequestState pending, LanguageServiceCompletionResponse response) { return _sessionService.MergeSupplementalResponse(editorState, target, pending, response); }
        public bool SetInlineSuggestion(CortexCompletionInteractionState editorState, DocumentSession target, DocumentLanguageCompletionRequestState pending, LanguageServiceCompletionResponse response, string providerId) { return _sessionService.SetInlineSuggestion(editorState, target, pending, response, providerId); }
        public bool HasVisibleInlineSuggestion(CortexCompletionInteractionState editorState, DocumentSession session) { return _sessionService.HasVisibleInlineSuggestion(editorState, session); }
        public bool TryGetInlineSuggestionSuffix(CortexCompletionInteractionState editorState, DocumentSession session, out string suffixText) { return _sessionService.TryGetInlineSuggestionSuffix(editorState, session, out suffixText); }
        public bool ApplyInlineSuggestion(DocumentSession session, CortexCompletionInteractionState editorState, IEditorService editorService) { return _sessionService.ApplyInlineSuggestion(session, editorState, editorService); }
        public bool IsVisibleForSession(CortexCompletionInteractionState editorState, DocumentSession session) { return _sessionService.IsVisibleForSession(editorState, session); }
        public bool HasVisibleCompletion(CortexCompletionInteractionState editorState) { return _sessionService.HasVisibleCompletion(editorState); }
        public bool HasCompletionItems(LanguageServiceCompletionResponse response) { return _sessionService.HasCompletionItems(response); }
        public bool ShouldTriggerCompletion(char character) { return _requestService.ShouldTriggerCompletion(character); }
        public bool ShouldContinueCompletion(DocumentSession session, int caretIndex) { return _requestService.ShouldContinueCompletion(session, caretIndex); }
        public void SyncSelection(CortexCompletionInteractionState editorState) { _sessionService.SyncSelection(editorState); }
        public void MoveSelection(CortexCompletionInteractionState editorState, int delta) { _sessionService.MoveSelection(editorState, delta); }
        public bool ApplySelectedCompletion(DocumentSession session, CortexCompletionInteractionState editorState, IEditorService editorService) { return _sessionService.ApplySelectedCompletion(session, editorState, editorService); }
        public void Reset(CortexCompletionInteractionState editorState) { _sessionService.Reset(editorState); }
        public void ClearPendingRequest(CortexCompletionInteractionState editorState) { _requestService.ClearPendingRequest(editorState); }
        public void ClearPopupCompletion(CortexCompletionInteractionState editorState) { _sessionService.ClearPopupCompletion(editorState); }
        public void ClearInlineSuggestion(CortexCompletionInteractionState editorState) { _sessionService.ClearInlineSuggestion(editorState); }

        public bool QueueRequest(DocumentSession session, CortexCompletionInteractionState editorState, IEditorService editorService, bool explicitInvocation, string triggerCharacter)
        {
            var queued = _requestService.QueueRequest(session, editorState, editorService, explicitInvocation, triggerCharacter);
            if (queued && editorState != null)
            {
                editorState.ActiveContextKey = string.Empty;
                editorState.Response = null;
                editorState.PopupStateKey = string.Empty;
                editorState.SelectedIndex = -1;
            }
            else if (!queued)
            {
                Reset(editorState);
            }

            return queued;
        }
    }
}
