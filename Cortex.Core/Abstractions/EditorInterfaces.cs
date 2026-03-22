using Cortex.Core.Models;

namespace Cortex.Core.Abstractions
{
    /// <summary>
    /// Encapsulates editor-domain operations such as caret movement, selection,
    /// mutation, and undo/redo for an in-memory document session.
    /// </summary>
    public interface IEditorService
    {
        /// <summary>
        /// Ensures the document has initialized editor state and a current line map.
        /// </summary>
        void EnsureDocumentState(DocumentSession session);

        /// <summary>
        /// Applies the active undo-history limit to the document editor state.
        /// </summary>
        void SetUndoLimit(DocumentSession session, int undoLimit);

        /// <summary>
        /// Returns the current line map for the document text.
        /// </summary>
        EditorLineMap GetLineMap(DocumentSession session);

        /// <summary>
        /// Converts a character index into a zero-based line and column.
        /// </summary>
        EditorCaretPosition GetCaretPosition(DocumentSession session, int characterIndex);

        /// <summary>
        /// Converts a zero-based line and column into a bounded character index.
        /// </summary>
        int GetCharacterIndex(DocumentSession session, int line, int column);

        /// <summary>
        /// Returns the current logical line count.
        /// </summary>
        int GetLineCount(DocumentSession session);

        /// <summary>
        /// Returns the primary selection.
        /// </summary>
        EditorSelectionRange GetPrimarySelection(DocumentSession session);

        /// <summary>
        /// Returns all active carets and selections.
        /// </summary>
        EditorSelectionRange[] GetSelections(DocumentSession session);

        /// <summary>
        /// Moves the caret to the requested character index.
        /// </summary>
        void SetCaret(DocumentSession session, int characterIndex, bool extendSelection, bool preservePreferredColumn);

        /// <summary>
        /// Sets the selection anchor and caret explicitly.
        /// </summary>
        void SetSelection(DocumentSession session, int anchorIndex, int caretIndex);

        /// <summary>
        /// Moves the caret left or right by the requested character delta.
        /// </summary>
        void MoveCaretHorizontal(DocumentSession session, int delta, bool extendSelection);

        /// <summary>
        /// Moves the caret vertically while preserving the preferred column.
        /// </summary>
        void MoveCaretVertical(DocumentSession session, int deltaLines, bool extendSelection);

        /// <summary>
        /// Moves the caret to the start or end of the current line.
        /// </summary>
        void MoveCaretToLineBoundary(DocumentSession session, bool toLineStart, bool extendSelection);

        /// <summary>
        /// Moves the caret to the start or end of the document.
        /// </summary>
        void MoveCaretToDocumentBoundary(DocumentSession session, bool toDocumentStart, bool extendSelection);

        /// <summary>
        /// Selects the entire document.
        /// </summary>
        void SelectAll(DocumentSession session);

        /// <summary>
        /// Returns the current primary selection text.
        /// </summary>
        string GetSelectedText(DocumentSession session);

        /// <summary>
        /// Expands the selection to the word at the given character index.
        /// </summary>
        void SelectWord(DocumentSession session, int characterIndex);

        /// <summary>
        /// Adds mirrored carets one line above or below the current caret set.
        /// </summary>
        bool AddCaretOnAdjacentLine(DocumentSession session, int deltaLines);

        /// <summary>
        /// Clears any secondary carets while keeping the primary selection.
        /// </summary>
        bool ClearSecondarySelections(DocumentSession session);

        /// <summary>
        /// Inserts plain text at the current selection.
        /// </summary>
        bool InsertText(DocumentSession session, string text);

        /// <summary>
        /// Replaces the entire document text while preserving editor history semantics.
        /// </summary>
        bool SetText(DocumentSession session, string text);

        /// <summary>
        /// Inserts a matching pair around the current selection or caret.
        /// </summary>
        bool InsertPair(DocumentSession session, string openText, string closeText);

        /// <summary>
        /// Removes the selection or the character before the caret.
        /// </summary>
        bool Backspace(DocumentSession session);

        /// <summary>
        /// Removes the selection or the character at the caret.
        /// </summary>
        bool Delete(DocumentSession session);

        /// <summary>
        /// Inserts a newline and applies indentation rules.
        /// </summary>
        bool InsertNewLine(DocumentSession session);

        /// <summary>
        /// Indents or outdents the current selection.
        /// </summary>
        bool IndentSelection(DocumentSession session, bool outdent);

        /// <summary>
        /// Moves the selected or caret line block up or down.
        /// </summary>
        bool MoveSelectedLines(DocumentSession session, int deltaLines);

        /// <summary>
        /// Reverts the most recent text change.
        /// </summary>
        bool Undo(DocumentSession session);

        /// <summary>
        /// Reapplies the most recently undone text change.
        /// </summary>
        bool Redo(DocumentSession session);
    }
}
