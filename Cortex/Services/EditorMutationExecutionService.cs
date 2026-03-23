using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.Core.Services;

namespace Cortex.Services
{
    internal sealed class EditorMutationExecutionService
    {
        private readonly IEditorService _editorService;
        private readonly EditorDocumentModeService _documentModeService;
        private readonly EditorLogicalDocumentTargetResolutionService _targetResolutionService;
        private readonly IClipboardService _clipboardService;

        public EditorMutationExecutionService()
            : this(
                new EditorService(),
                new EditorDocumentModeService(),
                new EditorLogicalDocumentTargetResolutionService(),
                new EditorClipboardService())
        {
        }

        public EditorMutationExecutionService(
            IEditorService editorService,
            EditorDocumentModeService documentModeService,
            EditorLogicalDocumentTargetResolutionService targetResolutionService,
            IClipboardService clipboardService)
        {
            _editorService = editorService ?? new EditorService();
            _documentModeService = documentModeService ?? new EditorDocumentModeService();
            _targetResolutionService = targetResolutionService ?? new EditorLogicalDocumentTargetResolutionService();
            _clipboardService = clipboardService ?? new EditorClipboardService();
        }

        public bool TryExecuteClipboardCommand(string commandId, CortexShellState state, EditorCommandTarget target, out string statusMessage)
        {
            statusMessage = string.Empty;
            if (state == null || target == null)
            {
                statusMessage = "Open a source document to use this action.";
                return false;
            }

            EditorLogicalDocumentTarget resolvedTarget;
            string reason;
            if (!_targetResolutionService.TryResolveSourceDocument(state, target, out resolvedTarget, out reason) ||
                resolvedTarget == null ||
                resolvedTarget.Session == null)
            {
                statusMessage = !string.IsNullOrEmpty(reason)
                    ? reason
                    : "Open the target source document before using this action.";
                return false;
            }

            var session = resolvedTarget.Session;
            if (!_documentModeService.IsEditingEnabled(state.Settings, session) &&
                !_documentModeService.SetEditingEnabled(state.Settings, session, true))
            {
                statusMessage = "Editing is disabled for the current document.";
                return false;
            }

            state.Documents.ActiveDocument = session;
            state.Documents.ActiveDocumentPath = session.FilePath;
            ApplyTargetSelection(session, target);

            switch (commandId ?? string.Empty)
            {
                case "cortex.editor.cut":
                    return TryCut(session, out statusMessage);
                case "cortex.editor.paste":
                    return TryPaste(session, out statusMessage);
                default:
                    statusMessage = "Unsupported editor mutation command.";
                    return false;
            }
        }

        private bool TryCut(DocumentSession session, out string statusMessage)
        {
            statusMessage = "Select text before cutting.";
            var text = _editorService.GetSelectedText(session);
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            _clipboardService.SetText(text);
            if (!_editorService.Backspace(session))
            {
                statusMessage = "Cut could not be applied to the current selection.";
                return false;
            }

            statusMessage = "Cut selection.";
            return true;
        }

        private bool TryPaste(DocumentSession session, out string statusMessage)
        {
            statusMessage = "Clipboard is empty.";
            var text = _clipboardService.GetText();
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            if (!_editorService.InsertText(session, text))
            {
                statusMessage = "Paste could not be applied at the current caret position.";
                return false;
            }

            statusMessage = "Pasted clipboard contents.";
            return true;
        }

        private void ApplyTargetSelection(DocumentSession session, EditorCommandTarget target)
        {
            if (session == null || target == null)
            {
                return;
            }

            if (target.HasSelection && target.SelectionStart >= 0 && target.SelectionEnd >= 0)
            {
                _editorService.SetSelection(session, target.SelectionStart, target.SelectionEnd);
                return;
            }

            var caretIndex = target.CaretIndex >= 0
                ? target.CaretIndex
                : target.AbsolutePosition >= 0
                    ? target.AbsolutePosition
                    : 0;
            _editorService.SetCaret(session, caretIndex, false, false);
        }
    }
}
