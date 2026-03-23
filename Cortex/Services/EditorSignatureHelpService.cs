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
        public bool ShouldDispatch(CortexEditorInteractionState editorState, bool requestInFlight)
        {
            if (requestInFlight || editorState == null)
            {
                return false;
            }

            var requestKey = editorState.RequestedSignatureHelpKey ?? string.Empty;
            return !string.IsNullOrEmpty(requestKey) &&
                !string.Equals(requestKey, editorState.ActiveSignatureHelpKey ?? string.Empty, StringComparison.Ordinal);
        }

        public LanguageServiceSignatureHelpRequest BuildWorkerRequest(
            DocumentSession session,
            CortexSettings settings,
            CortexProjectDefinition project,
            string[] sourceRoots,
            CortexEditorInteractionState editorState)
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
                Line = editorState.RequestedSignatureHelpLine,
                Column = editorState.RequestedSignatureHelpColumn,
                AbsolutePosition = editorState.RequestedSignatureHelpAbsolutePosition,
                ExplicitInvocation = editorState.RequestedSignatureHelpExplicit,
                TriggerCharacter = editorState.RequestedSignatureHelpTriggerCharacter ?? string.Empty
            };
        }

        public bool QueueRequest(
            DocumentSession session,
            CortexEditorInteractionState editorState,
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
            editorState.RequestedSignatureHelpKey = BuildRequestKey(session.FilePath, session.TextVersion, caretIndex, explicitInvocation, triggerCharacter);
            editorState.RequestedSignatureHelpDocumentPath = session.FilePath ?? string.Empty;
            editorState.RequestedSignatureHelpLine = caret.Line + 1;
            editorState.RequestedSignatureHelpColumn = caret.Column + 1;
            editorState.RequestedSignatureHelpAbsolutePosition = caretIndex;
            editorState.RequestedSignatureHelpTriggerCharacter = triggerCharacter ?? string.Empty;
            editorState.RequestedSignatureHelpExplicit = explicitInvocation;
            editorState.ActiveSignatureHelpKey = string.Empty;
            editorState.ActiveSignatureHelpResponse = null;
            return true;
        }

        public bool AcceptResponse(
            CortexEditorInteractionState editorState,
            DocumentSession target,
            PendingLanguageSignatureHelpRequest pending,
            LanguageServiceSignatureHelpResponse response)
        {
            if (editorState == null || pending == null)
            {
                return false;
            }

            if (!string.Equals(editorState.RequestedSignatureHelpKey ?? string.Empty, pending.RequestKey ?? string.Empty, StringComparison.Ordinal))
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

            editorState.ActiveSignatureHelpKey = pending.RequestKey ?? string.Empty;
            editorState.ActiveSignatureHelpResponse = response;
            return true;
        }

        public bool HasVisibleSignatureHelp(CortexEditorInteractionState editorState, DocumentSession session)
        {
            var response = editorState != null ? editorState.ActiveSignatureHelpResponse : null;
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

        public void Reset(CortexEditorInteractionState editorState)
        {
            ClearRequest(editorState);
            ClearActive(editorState);
        }

        public void ClearActive(CortexEditorInteractionState editorState)
        {
            if (editorState == null)
            {
                return;
            }

            editorState.ActiveSignatureHelpKey = string.Empty;
            editorState.ActiveSignatureHelpResponse = null;
        }

        private static void ClearRequest(CortexEditorInteractionState editorState)
        {
            if (editorState == null)
            {
                return;
            }

            editorState.RequestedSignatureHelpKey = string.Empty;
            editorState.RequestedSignatureHelpDocumentPath = string.Empty;
            editorState.RequestedSignatureHelpLine = 0;
            editorState.RequestedSignatureHelpColumn = 0;
            editorState.RequestedSignatureHelpAbsolutePosition = -1;
            editorState.RequestedSignatureHelpTriggerCharacter = string.Empty;
            editorState.RequestedSignatureHelpExplicit = false;
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
