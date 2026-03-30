using System;
using Cortex.Core.Models;
using Cortex.LanguageService.Protocol;

namespace Cortex.Services.Semantics.SignatureHelp
{
    internal sealed class EditorSignatureHelpSessionService
    {
        public bool AcceptResponse(CortexSignatureHelpInteractionState editorState, DocumentSession target, PendingLanguageSignatureHelpRequest pending, LanguageServiceSignatureHelpResponse response)
        {
            if (editorState == null || pending == null)
            {
                return false;
            }

            if (!string.Equals(editorState.RequestedKey ?? string.Empty, pending.RequestKey ?? string.Empty, StringComparison.Ordinal))
            {
                return false;
            }

            ClearRequest(editorState);
            if (response == null ||
                target == null ||
                (response.DocumentVersion > 0 && target.TextVersion > 0 && response.DocumentVersion != target.TextVersion) ||
                response.Items == null ||
                response.Items.Length == 0)
            {
                ClearActive(editorState);
                return false;
            }

            editorState.ActiveContextKey = pending.ContextKey ?? string.Empty;
            editorState.Response = response;
            return true;
        }

        public bool HasVisibleSignatureHelp(CortexSignatureHelpInteractionState editorState, DocumentSession session)
        {
            var response = editorState != null ? editorState.Response : null;
            return response != null &&
                response.Success &&
                response.Items != null &&
                response.Items.Length > 0 &&
                session != null &&
                string.Equals(response.DocumentPath ?? string.Empty, session.FilePath ?? string.Empty, StringComparison.OrdinalIgnoreCase) &&
                (response.DocumentVersion <= 0 || session.TextVersion <= 0 || response.DocumentVersion == session.TextVersion);
        }

        public void ClearActive(CortexSignatureHelpInteractionState editorState)
        {
            if (editorState == null)
            {
                return;
            }

            editorState.ActiveContextKey = string.Empty;
            editorState.Response = null;
        }

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
    }
}
