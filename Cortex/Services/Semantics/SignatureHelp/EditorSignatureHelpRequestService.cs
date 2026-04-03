using System;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.LanguageService.Protocol;
using Cortex.Services.Semantics.Requests;

namespace Cortex.Services.Semantics.SignatureHelp
{
    internal sealed class EditorSignatureHelpRequestService
    {
        private readonly IEditorLanguageRequestFactory _requestFactory;

        public EditorSignatureHelpRequestService(IEditorLanguageRequestFactory requestFactory)
        {
            _requestFactory = requestFactory;
        }

        public bool ShouldDispatch(CortexSignatureHelpInteractionState editorState, bool requestInFlight)
        {
            if (requestInFlight || editorState == null)
            {
                return false;
            }

            var requestKey = editorState.RequestedKey ?? string.Empty;
            return !string.IsNullOrEmpty(requestKey) &&
                !string.Equals(requestKey, editorState.ActiveContextKey ?? string.Empty, StringComparison.Ordinal);
        }

        public LanguageServiceSignatureHelpRequest BuildWorkerRequest(DocumentSession session, CortexSettings settings, CortexProjectDefinition project, string[] sourceRoots, CortexSignatureHelpInteractionState editorState)
        {
            if (session == null || editorState == null)
            {
                return null;
            }

            return _requestFactory.BuildSignatureHelpRequest(
                session,
                settings,
                project,
                sourceRoots,
                editorState.RequestedLine,
                editorState.RequestedColumn,
                editorState.RequestedAbsolutePosition,
                editorState.RequestedExplicit,
                editorState.RequestedTriggerCharacter);
        }

        public bool QueueRequest(DocumentSession session, CortexSignatureHelpInteractionState editorState, IEditorService editorService, bool explicitInvocation, string triggerCharacter)
        {
            if (editorState == null || session == null || session.EditorState == null || editorService == null || session.EditorState.HasMultipleSelections)
            {
                ClearRequest(editorState);
                return false;
            }

            var caretIndex = Math.Max(0, session.EditorState.CaretIndex);
            var caret = editorService.GetCaretPosition(session, caretIndex);
            editorState.RequestedKey = BuildRequestKey(session.FilePath, session.TextVersion, caretIndex, explicitInvocation, triggerCharacter);
            editorState.RequestedDocumentPath = session.FilePath ?? string.Empty;
            editorState.RequestedLine = caret.Line + 1;
            editorState.RequestedColumn = caret.Column + 1;
            editorState.RequestedAbsolutePosition = caretIndex;
            editorState.RequestedTriggerCharacter = triggerCharacter ?? string.Empty;
            editorState.RequestedExplicit = explicitInvocation;
            return true;
        }

        public bool ShouldTriggerSignatureHelp(char character) { return character == '(' || character == ','; }
        public bool ShouldDismissAfterText(char character) { return character == ')' || character == ';' || character == '{' || character == '}'; }

        public void ClearRequest(CortexSignatureHelpInteractionState editorState)
        {
            if (editorState == null)
            {
                return;
            }

            editorState.RequestedContextKey = string.Empty;
            editorState.RequestedKey = string.Empty;
            editorState.RequestedDocumentPath = string.Empty;
            editorState.RequestedLine = 0;
            editorState.RequestedColumn = 0;
            editorState.RequestedAbsolutePosition = -1;
            editorState.RequestedTriggerCharacter = string.Empty;
            editorState.RequestedExplicit = false;
        }

        private static string BuildRequestKey(string documentPath, int documentVersion, int absolutePosition, bool explicitInvocation, string triggerCharacter)
        {
            return (documentPath ?? string.Empty) + "|" + documentVersion + "|" + absolutePosition + "|" + explicitInvocation + "|" + (triggerCharacter ?? string.Empty) + "|signature";
        }
    }
}
