using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.LanguageService.Protocol;

namespace Cortex.Services.Semantics.Completion
{
    internal interface IEditorCompletionService
    {
        bool ShouldDispatch(CortexCompletionInteractionState editorState, bool requestInFlight);
        LanguageServiceCompletionRequest BuildWorkerRequest(DocumentSession session, CortexSettings settings, CortexProjectDefinition project, string[] sourceRoots, CortexCompletionInteractionState editorState);
        bool QueueRequest(DocumentSession session, CortexCompletionInteractionState editorState, IEditorService editorService, bool explicitInvocation, string triggerCharacter);
        bool AcceptResponse(CortexCompletionInteractionState editorState, DocumentSession target, DocumentLanguageCompletionRequestState pending, LanguageServiceCompletionResponse response);
        bool MergeSupplementalResponse(CortexCompletionInteractionState editorState, DocumentSession target, DocumentLanguageCompletionRequestState pending, LanguageServiceCompletionResponse response);
        bool SetInlineSuggestion(CortexCompletionInteractionState editorState, DocumentSession target, DocumentLanguageCompletionRequestState pending, LanguageServiceCompletionResponse response, string providerId);
        bool HasVisibleInlineSuggestion(CortexCompletionInteractionState editorState, DocumentSession session);
        bool TryGetInlineSuggestionSuffix(CortexCompletionInteractionState editorState, DocumentSession session, out string suffixText);
        bool ApplyInlineSuggestion(DocumentSession session, CortexCompletionInteractionState editorState, IEditorService editorService);
        bool IsVisibleForSession(CortexCompletionInteractionState editorState, DocumentSession session);
        bool HasVisibleCompletion(CortexCompletionInteractionState editorState);
        bool HasCompletionItems(LanguageServiceCompletionResponse response);
        bool ShouldTriggerCompletion(char character);
        bool ShouldContinueCompletion(DocumentSession session, int caretIndex);
        void SyncSelection(CortexCompletionInteractionState editorState);
        void MoveSelection(CortexCompletionInteractionState editorState, int delta);
        bool ApplySelectedCompletion(DocumentSession session, CortexCompletionInteractionState editorState, IEditorService editorService);
        void Reset(CortexCompletionInteractionState editorState);
        void ClearPendingRequest(CortexCompletionInteractionState editorState);
        void ClearPopupCompletion(CortexCompletionInteractionState editorState);
        void ClearInlineSuggestion(CortexCompletionInteractionState editorState);
    }
}
