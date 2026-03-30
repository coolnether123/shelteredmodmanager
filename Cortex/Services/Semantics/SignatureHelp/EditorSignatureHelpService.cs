using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.LanguageService.Protocol;
using Cortex.Services.Semantics.Requests;

namespace Cortex.Services.Semantics.SignatureHelp
{
    internal sealed class EditorSignatureHelpService : IEditorSignatureHelpService
    {
        private readonly EditorSignatureHelpRequestService _requestService;
        private readonly EditorSignatureHelpSessionService _sessionService;

        public EditorSignatureHelpService()
            : this(new EditorSignatureHelpRequestService(new EditorLanguageRequestFactory()), new EditorSignatureHelpSessionService())
        {
        }

        internal EditorSignatureHelpService(EditorSignatureHelpRequestService requestService, EditorSignatureHelpSessionService sessionService)
        {
            _requestService = requestService;
            _sessionService = sessionService;
        }

        public bool ShouldDispatch(CortexSignatureHelpInteractionState editorState, bool requestInFlight) { return _requestService.ShouldDispatch(editorState, requestInFlight); }
        public LanguageServiceSignatureHelpRequest BuildWorkerRequest(DocumentSession session, CortexSettings settings, CortexProjectDefinition project, string[] sourceRoots, CortexSignatureHelpInteractionState editorState) { return _requestService.BuildWorkerRequest(session, settings, project, sourceRoots, editorState); }
        public bool AcceptResponse(CortexSignatureHelpInteractionState editorState, DocumentSession target, PendingLanguageSignatureHelpRequest pending, LanguageServiceSignatureHelpResponse response) { return _sessionService.AcceptResponse(editorState, target, pending, response); }
        public bool HasVisibleSignatureHelp(CortexSignatureHelpInteractionState editorState, DocumentSession session) { return _sessionService.HasVisibleSignatureHelp(editorState, session); }
        public bool ShouldTriggerSignatureHelp(char character) { return _requestService.ShouldTriggerSignatureHelp(character); }
        public bool ShouldDismissAfterText(char character) { return _requestService.ShouldDismissAfterText(character); }
        public void ClearActive(CortexSignatureHelpInteractionState editorState) { _sessionService.ClearActive(editorState); }

        public bool QueueRequest(DocumentSession session, CortexSignatureHelpInteractionState editorState, IEditorService editorService, bool explicitInvocation, string triggerCharacter)
        {
            var queued = _requestService.QueueRequest(session, editorState, editorService, explicitInvocation, triggerCharacter);
            if (queued)
            {
                _sessionService.ClearActive(editorState);
            }
            else
            {
                Reset(editorState);
            }

            return queued;
        }

        public void Reset(CortexSignatureHelpInteractionState editorState)
        {
            _requestService.ClearRequest(editorState);
            _sessionService.ClearActive(editorState);
        }
    }
}
