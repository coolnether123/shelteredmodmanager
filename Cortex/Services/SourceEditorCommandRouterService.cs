using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.Core.Services;

namespace Cortex.Services
{
    internal sealed class SourceEditorCommandRouterService
    {
        private readonly IEditorService _editorService = new EditorService();

        public bool TryExecuteLocal(DocumentSession session, string commandId, bool extendSelection, bool editingEnabled, out bool handled)
        {
            if (TryExecuteSharedCommand(session, commandId, extendSelection, out handled))
            {
                return true;
            }

            if (editingEnabled && TryExecuteEditingCommand(session, commandId, out handled))
            {
                return true;
            }

            if (!editingEnabled && IsEditingCommand(commandId))
            {
                handled = false;
                return true;
            }

            handled = false;
            return false;
        }

        public bool IsEditingCommand(string commandId)
        {
            switch (commandId ?? string.Empty)
            {
                case "edit.complete":
                case "edit.undo":
                case "edit.redo":
                case "edit.backspace":
                case "edit.delete":
                case "edit.indent":
                case "edit.outdent":
                case "edit.newline":
                case "multi.above":
                case "multi.below":
                case "multi.clear":
                case "move.line.up":
                case "move.line.down":
                    return true;
                default:
                    return false;
            }
        }

        private bool TryExecuteSharedCommand(DocumentSession session, string commandId, bool extendSelection, out bool handled)
        {
            handled = true;
            switch (commandId ?? string.Empty)
            {
                case "select.all":
                    _editorService.SelectAll(session);
                    return true;
                case "caret.left":
                    _editorService.MoveCaretHorizontal(session, -1, extendSelection);
                    return true;
                case "caret.right":
                    _editorService.MoveCaretHorizontal(session, 1, extendSelection);
                    return true;
                case "caret.up":
                    _editorService.MoveCaretVertical(session, -1, extendSelection);
                    return true;
                case "caret.down":
                    _editorService.MoveCaretVertical(session, 1, extendSelection);
                    return true;
                case "caret.line.start":
                    _editorService.MoveCaretToLineBoundary(session, true, extendSelection);
                    return true;
                case "caret.line.end":
                    _editorService.MoveCaretToLineBoundary(session, false, extendSelection);
                    return true;
                case "caret.document.start":
                    _editorService.MoveCaretToDocumentBoundary(session, true, extendSelection);
                    return true;
                case "caret.document.end":
                    _editorService.MoveCaretToDocumentBoundary(session, false, extendSelection);
                    return true;
                case "caret.page.up":
                    _editorService.MoveCaretVertical(session, -16, extendSelection);
                    return true;
                case "caret.page.down":
                    _editorService.MoveCaretVertical(session, 16, extendSelection);
                    return true;
                default:
                    handled = false;
                    return false;
            }
        }

        private bool TryExecuteEditingCommand(DocumentSession session, string commandId, out bool handled)
        {
            handled = true;
            switch (commandId ?? string.Empty)
            {
                case "edit.undo":
                    handled = _editorService.Undo(session);
                    return true;
                case "edit.redo":
                    handled = _editorService.Redo(session);
                    return true;
                case "edit.backspace":
                    handled = _editorService.Backspace(session);
                    return true;
                case "edit.delete":
                    handled = _editorService.Delete(session);
                    return true;
                case "edit.indent":
                    handled = _editorService.IndentSelection(session, false);
                    return true;
                case "edit.outdent":
                    handled = _editorService.IndentSelection(session, true);
                    return true;
                case "edit.newline":
                    handled = _editorService.InsertNewLine(session);
                    return true;
                case "multi.above":
                    handled = _editorService.AddCaretOnAdjacentLine(session, -1);
                    return true;
                case "multi.below":
                    handled = _editorService.AddCaretOnAdjacentLine(session, 1);
                    return true;
                case "multi.clear":
                    handled = _editorService.ClearSecondarySelections(session);
                    return true;
                case "move.line.up":
                    handled = _editorService.MoveSelectedLines(session, -1);
                    return true;
                case "move.line.down":
                    handled = _editorService.MoveSelectedLines(session, 1);
                    return true;
                default:
                    handled = false;
                    return false;
            }
        }
    }
}
