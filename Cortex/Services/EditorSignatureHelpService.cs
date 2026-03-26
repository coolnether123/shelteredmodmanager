using System;
using Cortex;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.LanguageService.Protocol;
using UnityEngine;

namespace Cortex.Services
{
    internal sealed class EditorSignatureHelpService
    {
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

        public LanguageServiceSignatureHelpRequest BuildWorkerRequest(
            DocumentSession session,
            CortexSettings settings,
            CortexProjectDefinition project,
            string[] sourceRoots,
            CortexSignatureHelpInteractionState editorState)
        {
            if (session == null || editorState == null)
            {
                return null;
            }

            return new LanguageServiceSignatureHelpRequest
            {
                DocumentPath = session.FilePath ?? string.Empty,
                ProjectFilePath = project != null ? project.ProjectFilePath : string.Empty,
                WorkspaceRootPath = settings != null ? settings.WorkspaceRootPath : string.Empty,
                SourceRoots = sourceRoots ?? new string[0],
                DocumentText = session.Text ?? string.Empty,
                DocumentVersion = session.TextVersion,
                Line = editorState.RequestedLine,
                Column = editorState.RequestedColumn,
                AbsolutePosition = editorState.RequestedAbsolutePosition,
                ExplicitInvocation = editorState.RequestedExplicit,
                TriggerCharacter = editorState.RequestedTriggerCharacter ?? string.Empty
            };
        }

        public bool QueueRequest(
            DocumentSession session,
            CortexSignatureHelpInteractionState editorState,
            IEditorService editorService,
            bool explicitInvocation,
            string triggerCharacter)
        {
            if (editorState == null || session == null || session.EditorState == null || editorService == null || session.EditorState.HasMultipleSelections)
            {
                Reset(editorState);
                return false;
            }

            var caretIndex = Mathf.Max(0, session.EditorState.CaretIndex);
            var caret = editorService.GetCaretPosition(session, caretIndex);
            editorState.RequestedKey = BuildRequestKey(session.FilePath, session.TextVersion, caretIndex, explicitInvocation, triggerCharacter);
            editorState.RequestedDocumentPath = session.FilePath ?? string.Empty;
            editorState.RequestedLine = caret.Line + 1;
            editorState.RequestedColumn = caret.Column + 1;
            editorState.RequestedAbsolutePosition = caretIndex;
            editorState.RequestedTriggerCharacter = triggerCharacter ?? string.Empty;
            editorState.RequestedExplicit = explicitInvocation;
            editorState.ActiveContextKey = string.Empty;
            editorState.Response = null;
            return true;
        }

        public bool AcceptResponse(
            CortexSignatureHelpInteractionState editorState,
            DocumentSession target,
            PendingLanguageSignatureHelpRequest pending,
            LanguageServiceSignatureHelpResponse response)
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
                (response.DocumentVersion <= 0 ||
                    session.TextVersion <= 0 ||
                    response.DocumentVersion == session.TextVersion);
        }

        public bool ShouldTriggerSignatureHelp(char character)
        {
            return character == '(' || character == ',';
        }

        public bool ShouldDismissAfterText(char character)
        {
            return character == ')' || character == ';' || character == '{' || character == '}';
        }

        public void Reset(CortexSignatureHelpInteractionState editorState)
        {
            ClearRequest(editorState);
            ClearActive(editorState);
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

        private static void ClearRequest(CortexSignatureHelpInteractionState editorState)
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
            return (documentPath ?? string.Empty) +
                "|" + documentVersion +
                "|" + absolutePosition +
                "|" + explicitInvocation +
                "|" + (triggerCharacter ?? string.Empty) +
                "|signature";
        }
    }
}
