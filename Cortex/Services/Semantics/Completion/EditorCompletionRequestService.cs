using System;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.LanguageService.Protocol;
using Cortex.Services.Semantics.Requests;

namespace Cortex.Services.Semantics.Completion
{
    internal sealed class EditorCompletionRequestService
    {
        private readonly IEditorLanguageRequestFactory _requestFactory;
        private readonly IEditorCompletionInteractionPolicy _interactionPolicy;

        public EditorCompletionRequestService(IEditorLanguageRequestFactory requestFactory, IEditorCompletionInteractionPolicy interactionPolicy)
        {
            _requestFactory = requestFactory;
            _interactionPolicy = interactionPolicy;
        }

        public bool ShouldDispatch(CortexCompletionInteractionState editorState, bool requestInFlight)
        {
            if (requestInFlight || editorState == null)
            {
                return false;
            }

            var requestKey = editorState.RequestedKey ?? string.Empty;
            return !string.IsNullOrEmpty(requestKey) &&
                !string.Equals(requestKey, editorState.ActiveContextKey ?? string.Empty, StringComparison.Ordinal);
        }

        public LanguageServiceCompletionRequest BuildWorkerRequest(DocumentSession session, CortexSettings settings, CortexProjectDefinition project, string[] sourceRoots, CortexCompletionInteractionState editorState)
        {
            if (session == null || editorState == null)
            {
                return null;
            }

            if (project == null || string.IsNullOrEmpty(project.ProjectFilePath))
            {
                MMLog.LogOnce("Cortex.Completion.Context.NoProject", delegate
                {
                    MMLog.WriteInfo("[Cortex.Completion] Completion is running without a resolved project file. Roslyn can still suggest symbols, but results may be broader until Cortex resolves project context.");
                });
            }

            return new LanguageServiceCompletionRequest
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

        public bool QueueRequest(DocumentSession session, CortexCompletionInteractionState editorState, IEditorService editorService, bool explicitInvocation, string triggerCharacter)
        {
            if (editorState == null || session == null || session.EditorState == null || editorService == null || session.EditorState.HasMultipleSelections)
            {
                ClearPendingRequest(editorState);
                return false;
            }

            var caretIndex = Math.Max(0, session.EditorState.CaretIndex);
            var caret = editorService.GetCaretPosition(session, caretIndex);
            editorState.RequestedKey = _requestFactory.BuildCompletionRequestKey(session.FilePath, session.TextVersion, caretIndex, explicitInvocation, triggerCharacter);
            editorState.RequestedDocumentPath = session.FilePath ?? string.Empty;
            editorState.RequestedLine = caret.Line + 1;
            editorState.RequestedColumn = caret.Column + 1;
            editorState.RequestedAbsolutePosition = caretIndex;
            editorState.RequestedTriggerCharacter = triggerCharacter ?? string.Empty;
            editorState.RequestedExplicit = explicitInvocation;
            return true;
        }

        public bool ShouldTriggerCompletion(char character)
        {
            return _interactionPolicy.ShouldTriggerCompletion(character);
        }

        public bool ShouldContinueCompletion(DocumentSession session, int caretIndex)
        {
            return _interactionPolicy.ShouldContinueCompletion(session, caretIndex);
        }

        public void ClearPendingRequest(CortexCompletionInteractionState editorState)
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
