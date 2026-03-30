using System;
using System.IO;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.Services.Editor.Commands;

namespace Cortex.Tests.Testing
{
    internal sealed class TestClipboardService : IClipboardService
    {
        private string _text;

        public TestClipboardService(string text = "")
        {
            _text = text ?? string.Empty;
        }

        public string GetText()
        {
            return _text ?? string.Empty;
        }

        public void SetText(string text)
        {
            _text = text ?? string.Empty;
        }
    }

    internal sealed class TestDocumentService : IDocumentService
    {
        public int SaveCallCount { get; private set; }

        public DocumentSession Open(string filePath)
        {
            throw new NotSupportedException();
        }

        public void Preload(string filePath)
        {
        }

        public bool Save(DocumentSession session)
        {
            SaveCallCount++;
            if (session == null || string.IsNullOrEmpty(session.FilePath))
            {
                return false;
            }

            File.WriteAllText(session.FilePath, session.Text ?? string.Empty);
            session.OriginalTextSnapshot = session.Text ?? string.Empty;
            session.IsDirty = false;
            return true;
        }

        public bool Reload(DocumentSession session)
        {
            throw new NotSupportedException();
        }

        public bool HasExternalChanges(DocumentSession session)
        {
            return false;
        }
    }

    internal sealed class TestEditorService : IEditorService
    {
        public void EnsureDocumentState(DocumentSession session)
        {
        }

        public void SetUndoLimit(DocumentSession session, int undoLimit)
        {
        }

        public EditorLineMap GetLineMap(DocumentSession session)
        {
            return session != null && session.EditorState != null
                ? session.EditorState.LineMap
                : new EditorLineMap();
        }

        public EditorCaretPosition GetCaretPosition(DocumentSession session, int characterIndex)
        {
            var safeText = session != null ? session.Text ?? string.Empty : string.Empty;
            var index = ClampIndex(safeText, characterIndex);
            var line = 0;
            var column = 0;
            for (var i = 0; i < index; i++)
            {
                if (safeText[i] == '\n')
                {
                    line++;
                    column = 0;
                }
                else if (safeText[i] != '\r')
                {
                    column++;
                }
            }

            return new EditorCaretPosition
            {
                Line = line,
                Column = column
            };
        }

        public int GetCharacterIndex(DocumentSession session, int line, int column)
        {
            var safeText = session != null ? session.Text ?? string.Empty : string.Empty;
            var currentLine = 0;
            var index = 0;
            while (index < safeText.Length && currentLine < line)
            {
                if (safeText[index] == '\n')
                {
                    currentLine++;
                }

                index++;
            }

            return ClampIndex(safeText, index + Math.Max(0, column));
        }

        public int GetLineCount(DocumentSession session)
        {
            throw new NotSupportedException();
        }

        public EditorSelectionRange GetPrimarySelection(DocumentSession session)
        {
            return session.EditorState.PrimarySelection;
        }

        public EditorSelectionRange[] GetSelections(DocumentSession session)
        {
            return session.EditorState.Selections.ToArray();
        }

        public void SetCaret(DocumentSession session, int characterIndex, bool extendSelection, bool preservePreferredColumn)
        {
            if (session == null || session.EditorState == null)
            {
                return;
            }

            var clamped = ClampIndex(session.Text, characterIndex);
            if (extendSelection)
            {
                session.EditorState.CaretIndex = clamped;
            }
            else
            {
                session.EditorState.SelectionAnchorIndex = clamped;
                session.EditorState.CaretIndex = clamped;
            }
        }

        public void SetSelection(DocumentSession session, int anchorIndex, int caretIndex)
        {
            if (session == null || session.EditorState == null)
            {
                return;
            }

            session.EditorState.SelectionAnchorIndex = ClampIndex(session.Text, anchorIndex);
            session.EditorState.CaretIndex = ClampIndex(session.Text, caretIndex);
        }

        public void MoveCaretHorizontal(DocumentSession session, int delta, bool extendSelection)
        {
            throw new NotSupportedException();
        }

        public void MoveCaretVertical(DocumentSession session, int deltaLines, bool extendSelection)
        {
            throw new NotSupportedException();
        }

        public void MoveCaretToLineBoundary(DocumentSession session, bool toLineStart, bool extendSelection)
        {
            throw new NotSupportedException();
        }

        public void MoveCaretToDocumentBoundary(DocumentSession session, bool toDocumentStart, bool extendSelection)
        {
            throw new NotSupportedException();
        }

        public void SelectAll(DocumentSession session)
        {
            throw new NotSupportedException();
        }

        public string GetSelectedText(DocumentSession session)
        {
            if (session == null || session.EditorState == null || !session.EditorState.HasSelection)
            {
                return string.Empty;
            }

            var start = session.EditorState.SelectionStart;
            var length = session.EditorState.SelectionEnd - start;
            return length > 0
                ? (session.Text ?? string.Empty).Substring(start, length)
                : string.Empty;
        }

        public void SelectWord(DocumentSession session, int characterIndex)
        {
            throw new NotSupportedException();
        }

        public bool AddCaretOnAdjacentLine(DocumentSession session, int deltaLines)
        {
            throw new NotSupportedException();
        }

        public bool ClearSecondarySelections(DocumentSession session)
        {
            throw new NotSupportedException();
        }

        public bool InsertText(DocumentSession session, string text)
        {
            if (session == null || session.EditorState == null)
            {
                return false;
            }

            var safeText = session.Text ?? string.Empty;
            var insertText = text ?? string.Empty;
            var selectionStart = session.EditorState.SelectionStart;
            var selectionLength = session.EditorState.SelectionEnd - selectionStart;
            session.Text = safeText.Substring(0, selectionStart) +
                insertText +
                safeText.Substring(selectionStart + selectionLength);
            session.EditorState.SelectionAnchorIndex = selectionStart + insertText.Length;
            session.EditorState.CaretIndex = selectionStart + insertText.Length;
            return true;
        }

        public bool SetText(DocumentSession session, string text)
        {
            if (session == null)
            {
                return false;
            }

            session.Text = text ?? string.Empty;
            return true;
        }

        public bool InsertPair(DocumentSession session, string openText, string closeText)
        {
            throw new NotSupportedException();
        }

        public bool Backspace(DocumentSession session)
        {
            if (session == null || session.EditorState == null)
            {
                return false;
            }

            if (session.EditorState.HasSelection)
            {
                var start = session.EditorState.SelectionStart;
                var length = session.EditorState.SelectionEnd - start;
                session.Text = (session.Text ?? string.Empty).Remove(start, length);
                session.EditorState.SelectionAnchorIndex = start;
                session.EditorState.CaretIndex = start;
                return true;
            }

            if (session.EditorState.CaretIndex <= 0)
            {
                return false;
            }

            var removeAt = session.EditorState.CaretIndex - 1;
            session.Text = (session.Text ?? string.Empty).Remove(removeAt, 1);
            session.EditorState.SelectionAnchorIndex = removeAt;
            session.EditorState.CaretIndex = removeAt;
            return true;
        }

        public bool Delete(DocumentSession session)
        {
            throw new NotSupportedException();
        }

        public bool InsertNewLine(DocumentSession session)
        {
            throw new NotSupportedException();
        }

        public bool IndentSelection(DocumentSession session, bool outdent)
        {
            throw new NotSupportedException();
        }

        public bool MoveSelectedLines(DocumentSession session, int deltaLines)
        {
            throw new NotSupportedException();
        }

        public bool Undo(DocumentSession session)
        {
            throw new NotSupportedException();
        }

        public bool Redo(DocumentSession session)
        {
            throw new NotSupportedException();
        }

        private static int ClampIndex(string text, int index)
        {
            var safeText = text ?? string.Empty;
            return Math.Max(0, Math.Min(index, safeText.Length));
        }
    }
}
