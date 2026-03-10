using System;
using System.Collections.Generic;
using System.Text;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;

namespace Cortex.Core.Services
{
    /// <summary>
    /// Default in-memory editor service for Cortex source documents.
    /// It owns mutation rules, selection semantics, undo/redo, and layout invalidation metadata.
    /// </summary>
    public sealed class EditorService : IEditorService
    {
        private const int TabSize = 4;
        private const int MinimumUndoLimit = 10;
        private const int MaximumUndoLimit = 512;

        public void EnsureDocumentState(DocumentSession session)
        {
            if (session == null)
            {
                return;
            }

            if (session.EditorState == null)
            {
                session.EditorState = new EditorDocumentState();
            }

            EnsureLineMap(session);
            NormalizeSelections(session);
            if (!session.EditorState.HasExplicitCaretPlacement && session.HighlightedLine > 0)
            {
                var targetIndex = GetCharacterIndex(session, Math.Max(0, session.HighlightedLine - 1), 0);
                SetSingleSelection(session, CreateSelection(targetIndex, targetIndex, 0));
                session.EditorState.HasExplicitCaretPlacement = true;
                session.EditorState.ScrollToCaretPending = true;
            }
        }

        public void SetUndoLimit(DocumentSession session, int undoLimit)
        {
            EnsureDocumentState(session);
            if (session == null || session.EditorState == null)
            {
                return;
            }

            session.EditorState.UndoLimit = Clamp(undoLimit, MinimumUndoLimit, MaximumUndoLimit);
            TrimHistory(session.EditorState.UndoStack, session.EditorState.UndoLimit);
            TrimHistory(session.EditorState.RedoStack, session.EditorState.UndoLimit);
        }

        public EditorLineMap GetLineMap(DocumentSession session)
        {
            EnsureDocumentState(session);
            return session != null && session.EditorState != null
                ? session.EditorState.LineMap
                : new EditorLineMap();
        }

        public EditorCaretPosition GetCaretPosition(DocumentSession session, int characterIndex)
        {
            EnsureDocumentState(session);
            var text = GetText(session);
            var index = Clamp(characterIndex, 0, text.Length);
            var map = GetLineMap(session);
            var line = FindLineIndex(map.LineStarts, index);
            return new EditorCaretPosition
            {
                Line = line,
                Column = index - map.LineStarts[line]
            };
        }

        public int GetCharacterIndex(DocumentSession session, int line, int column)
        {
            EnsureDocumentState(session);
            var map = GetLineMap(session);
            if (map.LineStarts.Length == 0)
            {
                return 0;
            }

            var targetLine = Clamp(line, 0, map.LineStarts.Length - 1);
            var lineStart = map.LineStarts[targetLine];
            var lineEnd = GetLineEndIndex(session, targetLine);
            return Clamp(lineStart + Math.Max(0, column), lineStart, lineEnd);
        }

        public int GetLineCount(DocumentSession session)
        {
            return GetLineMap(session).LineStarts.Length;
        }

        public EditorSelectionRange GetPrimarySelection(DocumentSession session)
        {
            EnsureDocumentState(session);
            return session != null && session.EditorState != null
                ? session.EditorState.PrimarySelection.Clone()
                : new EditorSelectionRange();
        }

        public EditorSelectionRange[] GetSelections(DocumentSession session)
        {
            EnsureDocumentState(session);
            if (session == null || session.EditorState == null)
            {
                return new EditorSelectionRange[0];
            }

            var result = new EditorSelectionRange[session.EditorState.Selections.Count];
            for (var i = 0; i < session.EditorState.Selections.Count; i++)
            {
                result[i] = session.EditorState.Selections[i].Clone();
            }

            return result;
        }

        public void SetCaret(DocumentSession session, int characterIndex, bool extendSelection, bool preservePreferredColumn)
        {
            EnsureDocumentState(session);
            if (session == null || session.EditorState == null)
            {
                return;
            }

            var primary = session.EditorState.PrimarySelection.Clone();
            var clamped = Clamp(characterIndex, 0, GetText(session).Length);
            primary.CaretIndex = clamped;
            if (!extendSelection)
            {
                primary.AnchorIndex = clamped;
            }

            if (!preservePreferredColumn)
            {
                primary.PreferredColumn = GetCaretPosition(session, primary.CaretIndex).Column;
            }

            SetSingleSelection(session, primary);
            session.EditorState.HasExplicitCaretPlacement = true;
            session.EditorState.ScrollToCaretPending = true;
        }

        public void SetSelection(DocumentSession session, int anchorIndex, int caretIndex)
        {
            EnsureDocumentState(session);
            if (session == null || session.EditorState == null)
            {
                return;
            }

            var textLength = GetText(session).Length;
            var clampedCaret = Clamp(caretIndex, 0, textLength);
            var selection = CreateSelection(
                Clamp(anchorIndex, 0, textLength),
                clampedCaret,
                GetCaretPosition(session, clampedCaret).Column);
            SetSingleSelection(session, selection);
            session.EditorState.HasExplicitCaretPlacement = true;
            session.EditorState.ScrollToCaretPending = true;
        }

        public void MoveCaretHorizontal(DocumentSession session, int delta, bool extendSelection)
        {
            EnsureDocumentState(session);
            if (session == null || session.EditorState == null)
            {
                return;
            }

            var selections = CloneSelections(session.EditorState.Selections);
            for (var i = 0; i < selections.Count; i++)
            {
                if (!extendSelection && selections[i].HasSelection)
                {
                    var collapseIndex = delta < 0 ? selections[i].Start : selections[i].End;
                    selections[i].AnchorIndex = collapseIndex;
                    selections[i].CaretIndex = collapseIndex;
                }
                else
                {
                    selections[i].CaretIndex = Clamp(selections[i].CaretIndex + delta, 0, GetText(session).Length);
                    if (!extendSelection)
                    {
                        selections[i].AnchorIndex = selections[i].CaretIndex;
                    }
                }

                selections[i].PreferredColumn = GetCaretPosition(session, selections[i].CaretIndex).Column;
            }

            ApplySelections(session, selections, false);
        }

        public void MoveCaretVertical(DocumentSession session, int deltaLines, bool extendSelection)
        {
            EnsureDocumentState(session);
            if (session == null || session.EditorState == null)
            {
                return;
            }

            var selections = CloneSelections(session.EditorState.Selections);
            for (var i = 0; i < selections.Count; i++)
            {
                var caret = GetCaretPosition(session, selections[i].CaretIndex);
                if (selections[i].PreferredColumn < 0)
                {
                    selections[i].PreferredColumn = caret.Column;
                }

                var targetLine = Clamp(caret.Line + deltaLines, 0, GetLineCount(session) - 1);
                selections[i].CaretIndex = GetCharacterIndex(session, targetLine, selections[i].PreferredColumn);
                if (!extendSelection)
                {
                    selections[i].AnchorIndex = selections[i].CaretIndex;
                }
            }

            ApplySelections(session, selections, false);
        }

        public void MoveCaretToLineBoundary(DocumentSession session, bool toLineStart, bool extendSelection)
        {
            EnsureDocumentState(session);
            if (session == null || session.EditorState == null)
            {
                return;
            }

            var selections = CloneSelections(session.EditorState.Selections);
            for (var i = 0; i < selections.Count; i++)
            {
                var caret = GetCaretPosition(session, selections[i].CaretIndex);
                selections[i].CaretIndex = toLineStart
                    ? GetCharacterIndex(session, caret.Line, 0)
                    : GetLineEndIndex(session, caret.Line);
                if (!extendSelection)
                {
                    selections[i].AnchorIndex = selections[i].CaretIndex;
                }

                selections[i].PreferredColumn = GetCaretPosition(session, selections[i].CaretIndex).Column;
            }

            ApplySelections(session, selections, false);
        }

        public void MoveCaretToDocumentBoundary(DocumentSession session, bool toDocumentStart, bool extendSelection)
        {
            EnsureDocumentState(session);
            if (session == null || session.EditorState == null)
            {
                return;
            }

            var target = toDocumentStart ? 0 : GetText(session).Length;
            var selections = CloneSelections(session.EditorState.Selections);
            for (var i = 0; i < selections.Count; i++)
            {
                selections[i].CaretIndex = target;
                if (!extendSelection)
                {
                    selections[i].AnchorIndex = target;
                }

                selections[i].PreferredColumn = GetCaretPosition(session, selections[i].CaretIndex).Column;
            }

            ApplySelections(session, selections, false);
        }

        public void SelectAll(DocumentSession session)
        {
            SetSelection(session, 0, GetText(session).Length);
        }

        public void SelectWord(DocumentSession session, int characterIndex)
        {
            EnsureDocumentState(session);
            var text = GetText(session);
            if (text.Length == 0)
            {
                SetSelection(session, 0, 0);
                return;
            }

            var index = Clamp(characterIndex, 0, text.Length - 1);
            if (char.IsWhiteSpace(text[index]))
            {
                var startWhitespace = index;
                var endWhitespace = index;
                while (startWhitespace > 0 && char.IsWhiteSpace(text[startWhitespace - 1]) && text[startWhitespace - 1] != '\r' && text[startWhitespace - 1] != '\n')
                {
                    startWhitespace--;
                }

                while (endWhitespace < text.Length && char.IsWhiteSpace(text[endWhitespace]) && text[endWhitespace] != '\r' && text[endWhitespace] != '\n')
                {
                    endWhitespace++;
                }

                SetSelection(session, startWhitespace, endWhitespace);
                return;
            }

            var start = index;
            var end = index;
            while (start > 0 && IsWordCharacter(text[start - 1]))
            {
                start--;
            }

            while (end < text.Length && IsWordCharacter(text[end]))
            {
                end++;
            }

            if (start == end)
            {
                SetSelection(session, index, Math.Min(text.Length, index + 1));
                return;
            }

            SetSelection(session, start, end);
        }

        public bool AddCaretOnAdjacentLine(DocumentSession session, int deltaLines)
        {
            if (deltaLines == 0)
            {
                return false;
            }

            EnsureDocumentState(session);
            if (session == null || session.EditorState == null)
            {
                return false;
            }

            var currentSelections = CloneSelections(session.EditorState.Selections);
            var caretIndices = new HashSet<int>();
            for (var i = 0; i < currentSelections.Count; i++)
            {
                caretIndices.Add(currentSelections[i].CaretIndex);
            }

            var added = false;
            var snapshot = CloneSelections(currentSelections);
            for (var i = 0; i < snapshot.Count; i++)
            {
                var caret = GetCaretPosition(session, snapshot[i].CaretIndex);
                var targetLine = caret.Line + deltaLines;
                if (targetLine < 0 || targetLine >= GetLineCount(session))
                {
                    continue;
                }

                var preferredColumn = snapshot[i].PreferredColumn >= 0 ? snapshot[i].PreferredColumn : caret.Column;
                var targetIndex = GetCharacterIndex(session, targetLine, preferredColumn);
                if (caretIndices.Contains(targetIndex))
                {
                    continue;
                }

                currentSelections.Add(CreateSelection(targetIndex, targetIndex, preferredColumn));
                caretIndices.Add(targetIndex);
                added = true;
            }

            if (!added)
            {
                return false;
            }

            ApplySelections(session, currentSelections, false);
            return true;
        }

        public bool ClearSecondarySelections(DocumentSession session)
        {
            EnsureDocumentState(session);
            if (session == null || session.EditorState == null || session.EditorState.Selections.Count <= 1)
            {
                return false;
            }

            SetSingleSelection(session, session.EditorState.PrimarySelection.Clone());
            return true;
        }

        public bool InsertText(DocumentSession session, string text)
        {
            if (!CanEdit(session) || string.IsNullOrEmpty(text))
            {
                return false;
            }

            return ApplySelectionEdits(session, "typing", true, delegate(EditorSelectionRange selection, string documentText)
            {
                return new PreparedSelectionEdit
                {
                    Start = selection.Start,
                    DeleteLength = selection.End - selection.Start,
                    InsertedText = text,
                    AfterSelection = CreateSelection(selection.Start + text.Length, selection.Start + text.Length, -1)
                };
            });
        }

        public bool InsertPair(DocumentSession session, string openText, string closeText)
        {
            if (!CanEdit(session) || string.IsNullOrEmpty(openText))
            {
                return false;
            }

            return ApplySelectionEdits(session, "pair", false, delegate(EditorSelectionRange selection, string documentText)
            {
                var selectedText = selection.End > selection.Start
                    ? documentText.Substring(selection.Start, selection.End - selection.Start)
                    : string.Empty;
                var insertedText = selection.HasSelection
                    ? openText + selectedText + closeText
                    : openText + closeText;
                return new PreparedSelectionEdit
                {
                    Start = selection.Start,
                    DeleteLength = selection.End - selection.Start,
                    InsertedText = insertedText,
                    AfterSelection = selection.HasSelection
                        ? CreateSelection(selection.Start + openText.Length, selection.Start + openText.Length + selectedText.Length, -1)
                        : CreateSelection(selection.Start + openText.Length, selection.Start + openText.Length, -1)
                };
            });
        }

        public bool Backspace(DocumentSession session)
        {
            if (!CanEdit(session))
            {
                return false;
            }

            return ApplySelectionEdits(session, "backspace", !HasMultipleSelections(session) && !GetPrimarySelection(session).HasSelection, delegate(EditorSelectionRange selection, string documentText)
            {
                if (selection.HasSelection)
                {
                    return new PreparedSelectionEdit
                    {
                        Start = selection.Start,
                        DeleteLength = selection.End - selection.Start,
                        InsertedText = string.Empty,
                        AfterSelection = CreateSelection(selection.Start, selection.Start, -1)
                    };
                }

                if (selection.CaretIndex <= 0)
                {
                    return null;
                }

                var nextIndex = selection.CaretIndex - 1;
                return new PreparedSelectionEdit
                {
                    Start = nextIndex,
                    DeleteLength = 1,
                    InsertedText = string.Empty,
                    AfterSelection = CreateSelection(nextIndex, nextIndex, -1)
                };
            });
        }

        public bool Delete(DocumentSession session)
        {
            if (!CanEdit(session))
            {
                return false;
            }

            return ApplySelectionEdits(session, "delete", false, delegate(EditorSelectionRange selection, string documentText)
            {
                if (selection.HasSelection)
                {
                    return new PreparedSelectionEdit
                    {
                        Start = selection.Start,
                        DeleteLength = selection.End - selection.Start,
                        InsertedText = string.Empty,
                        AfterSelection = CreateSelection(selection.Start, selection.Start, -1)
                    };
                }

                if (selection.CaretIndex >= documentText.Length)
                {
                    return null;
                }

                return new PreparedSelectionEdit
                {
                    Start = selection.CaretIndex,
                    DeleteLength = 1,
                    InsertedText = string.Empty,
                    AfterSelection = CreateSelection(selection.CaretIndex, selection.CaretIndex, -1)
                };
            });
        }

        public bool InsertNewLine(DocumentSession session)
        {
            if (!CanEdit(session))
            {
                return false;
            }

            return ApplySelectionEdits(session, "newline", false, delegate(EditorSelectionRange selection, string documentText)
            {
                var caret = selection.Start;
                var line = GetCaretPosition(session, caret).Line;
                var lineStart = GetCharacterIndex(session, line, 0);
                var lineEnd = GetLineEndIndex(session, line);
                var currentLine = documentText.Substring(lineStart, lineEnd - lineStart);
                var indent = GetLeadingWhitespace(currentLine);
                var beforeChar = caret > 0 ? documentText[caret - 1] : '\0';
                var afterIndex = caret;
                while (afterIndex < documentText.Length && (documentText[afterIndex] == ' ' || documentText[afterIndex] == '\t'))
                {
                    afterIndex++;
                }

                var nextNonWhitespace = afterIndex < documentText.Length ? documentText[afterIndex] : '\0';
                var innerIndent = indent;
                if (beforeChar == '{')
                {
                    innerIndent += new string(' ', TabSize);
                }

                var inserted = beforeChar == '{' && nextNonWhitespace == '}'
                    ? "\n" + innerIndent + "\n" + indent
                    : "\n" + innerIndent;
                var caretIndex = selection.Start + 1 + innerIndent.Length;
                return new PreparedSelectionEdit
                {
                    Start = selection.Start,
                    DeleteLength = selection.End - selection.Start,
                    InsertedText = inserted,
                    AfterSelection = CreateSelection(caretIndex, caretIndex, innerIndent.Length)
                };
            });
        }

        public bool IndentSelection(DocumentSession session, bool outdent)
        {
            if (!CanEdit(session))
            {
                return false;
            }

            EnsureDocumentState(session);
            if (session == null || session.EditorState == null)
            {
                return false;
            }

            if (session.EditorState.HasMultipleSelections && !session.EditorState.HasSelection)
            {
                return ApplySelectionEdits(session, "indent", false, delegate(EditorSelectionRange selection, string documentText)
                {
                    if (outdent)
                    {
                        var caret = GetCaretPosition(session, selection.CaretIndex);
                        var lineStart = GetCharacterIndex(session, caret.Line, 0);
                        var lineEnd = GetLineEndIndex(session, caret.Line);
                        var lineText = documentText.Substring(lineStart, lineEnd - lineStart);
                        var removeLength = CountOutdent(lineText);
                        if (removeLength <= 0)
                        {
                            return null;
                        }

                        var nextIndex = Math.Max(lineStart, selection.CaretIndex - removeLength);
                        return new PreparedSelectionEdit
                        {
                            Start = lineStart,
                            DeleteLength = removeLength,
                            InsertedText = string.Empty,
                            AfterSelection = CreateSelection(nextIndex, nextIndex, -1)
                        };
                    }

                    var caretPosition = GetCaretPosition(session, selection.CaretIndex);
                    var lineStartIndex = GetCharacterIndex(session, caretPosition.Line, 0);
                    var spaces = BuildTabInsertion(selection.CaretIndex - lineStartIndex);
                    return new PreparedSelectionEdit
                    {
                        Start = selection.CaretIndex,
                        DeleteLength = 0,
                        InsertedText = spaces,
                        AfterSelection = CreateSelection(selection.CaretIndex + spaces.Length, selection.CaretIndex + spaces.Length, -1)
                    };
                });
            }

            return TransformSelectedLineBlock(session, outdent);
        }

        public bool MoveSelectedLines(DocumentSession session, int deltaLines)
        {
            if (!CanEdit(session) || deltaLines == 0)
            {
                return false;
            }

            EnsureDocumentState(session);
            if (session == null || session.EditorState == null)
            {
                return false;
            }

            var block = GetSelectedLineBlock(session);
            if (deltaLines < 0 && block.StartLine <= 0)
            {
                return false;
            }

            if (deltaLines > 0 && block.EndLine >= GetLineCount(session) - 1)
            {
                return false;
            }

            var text = GetText(session);
            if (deltaLines < 0)
            {
                var previousLineStart = GetCharacterIndex(session, block.StartLine - 1, 0);
                var previousLineText = text.Substring(previousLineStart, block.StartIndex - previousLineStart);
                var blockText = text.Substring(block.StartIndex, block.EndExclusive - block.StartIndex);
                return ApplyManualEditRecord(
                    session,
                    previousLineStart,
                    block.EndExclusive - previousLineStart,
                    blockText + previousLineText,
                    OffsetSelections(session.EditorState.Selections, -previousLineText.Length),
                    "move-line",
                    false);
            }

            var nextLineStart = block.EndExclusive;
            var nextLineEnd = block.EndLine + 1 < GetLineCount(session) - 1
                ? GetCharacterIndex(session, block.EndLine + 2, 0)
                : text.Length;
            var nextLineText = text.Substring(nextLineStart, nextLineEnd - nextLineStart);
            var selectedBlockText = text.Substring(block.StartIndex, block.EndExclusive - block.StartIndex);
            return ApplyManualEditRecord(
                session,
                block.StartIndex,
                nextLineEnd - block.StartIndex,
                nextLineText + selectedBlockText,
                OffsetSelections(session.EditorState.Selections, nextLineText.Length),
                "move-line",
                false);
        }

        public bool Undo(DocumentSession session)
        {
            if (session == null || session.EditorState == null || session.EditorState.UndoStack.Count == 0)
            {
                return false;
            }

            var state = session.EditorState;
            var record = CloneRecord(state.UndoStack[state.UndoStack.Count - 1]);
            state.UndoStack.RemoveAt(state.UndoStack.Count - 1);
            ApplyUndoRecord(session, record);
            state.RedoStack.Add(record);
            TrimHistory(state.RedoStack, state.UndoLimit);
            return true;
        }

        public bool Redo(DocumentSession session)
        {
            if (session == null || session.EditorState == null || session.EditorState.RedoStack.Count == 0)
            {
                return false;
            }

            var state = session.EditorState;
            var record = CloneRecord(state.RedoStack[state.RedoStack.Count - 1]);
            state.RedoStack.RemoveAt(state.RedoStack.Count - 1);
            ApplyRedoRecord(session, record);
            state.UndoStack.Add(record);
            TrimHistory(state.UndoStack, state.UndoLimit);
            return true;
        }

        private bool TransformSelectedLineBlock(DocumentSession session, bool outdent)
        {
            var text = GetText(session);
            var block = GetSelectedLineBlock(session);
            var builder = new StringBuilder();
            var lineAdjustments = new List<int>();

            for (var line = block.StartLine; line <= block.EndLine; line++)
            {
                var lineStart = GetCharacterIndex(session, line, 0);
                var lineEnd = GetLineEndIndex(session, line);
                var rawLine = text.Substring(lineStart, lineEnd - lineStart);
                var delta = 0;
                if (outdent)
                {
                    var removeLength = CountOutdent(rawLine);
                    if (removeLength > 0)
                    {
                        rawLine = rawLine.Substring(removeLength);
                        delta = -removeLength;
                    }
                }
                else
                {
                    rawLine = new string(' ', TabSize) + rawLine;
                    delta = TabSize;
                }

                builder.Append(rawLine);
                var newlineStart = lineEnd;
                while (newlineStart < text.Length && (text[newlineStart] == '\r' || text[newlineStart] == '\n'))
                {
                    builder.Append(text[newlineStart]);
                    newlineStart++;
                }

                lineAdjustments.Add(delta);
            }

            var updatedSelections = CloneSelections(session.EditorState.Selections);
            for (var i = 0; i < updatedSelections.Count; i++)
            {
                updatedSelections[i].AnchorIndex = AdjustIndexForLineTransform(session, updatedSelections[i].AnchorIndex, block.StartLine, lineAdjustments);
                updatedSelections[i].CaretIndex = AdjustIndexForLineTransform(session, updatedSelections[i].CaretIndex, block.StartLine, lineAdjustments);
                updatedSelections[i].PreferredColumn = -1;
            }

            return ApplyManualEditRecord(
                session,
                block.StartIndex,
                block.EndExclusive - block.StartIndex,
                builder.ToString(),
                updatedSelections,
                "indent",
                false);
        }

        private bool ApplySelectionEdits(DocumentSession session, string mergeGroup, bool allowMerge, SelectionEditBuilder builder)
        {
            EnsureDocumentState(session);
            if (session == null || session.EditorState == null)
            {
                return false;
            }

            var text = GetText(session);
            var selections = CloneSelections(session.EditorState.Selections);
            var edits = new List<PreparedSelectionEdit>();
            for (var i = 0; i < selections.Count; i++)
            {
                var edit = builder != null ? builder(selections[i].Clone(), text) : null;
                if (edit == null)
                {
                    continue;
                }

                edit.Start = Clamp(edit.Start, 0, text.Length);
                edit.DeleteLength = Clamp(edit.DeleteLength, 0, text.Length - edit.Start);
                edits.Add(edit);
            }

            if (edits.Count == 0)
            {
                return false;
            }

            edits.Sort(ComparePreparedEdits);
            RemoveDuplicateEdits(edits);
            var record = BuildRecord(text, session.EditorState.Selections, edits, mergeGroup);
            ApplyRedoRecord(session, record);
            PushUndo(session, record, allowMerge && record.Changes.Count == 1 && record.BeforeSelections.Count == 1);
            session.EditorState.RedoStack.Clear();
            return true;
        }

        private bool ApplyManualEditRecord(
            DocumentSession session,
            int start,
            int deleteLength,
            string insertedText,
            IList<EditorSelectionRange> afterSelections,
            string mergeGroup,
            bool allowMerge)
        {
            var text = GetText(session);
            var clampedStart = Clamp(start, 0, text.Length);
            var clampedDeleteLength = Clamp(deleteLength, 0, text.Length - clampedStart);
            var record = new EditorEditRecord();
            record.MergeGroup = mergeGroup ?? string.Empty;
            record.Changes.Add(new EditorTextChange
            {
                Start = clampedStart,
                RemovedText = clampedDeleteLength > 0 ? text.Substring(clampedStart, clampedDeleteLength) : string.Empty,
                InsertedText = insertedText ?? string.Empty
            });

            var beforeSelections = CloneSelections(session.EditorState.Selections);
            for (var i = 0; i < beforeSelections.Count; i++)
            {
                record.BeforeSelections.Add(beforeSelections[i]);
            }

            var clonedAfterSelections = CloneSelections(afterSelections);
            for (var i = 0; i < clonedAfterSelections.Count; i++)
            {
                record.AfterSelections.Add(clonedAfterSelections[i]);
            }

            ApplyRedoRecord(session, record);
            PushUndo(session, record, allowMerge && record.Changes.Count == 1 && record.BeforeSelections.Count == 1);
            session.EditorState.RedoStack.Clear();
            return true;
        }

        private void ApplyRedoRecord(DocumentSession session, EditorEditRecord record)
        {
            var previousText = GetText(session);
            var updatedText = ApplyChanges(previousText, record.Changes);
            UpdateSession(session, updatedText, record.AfterSelections, BuildInvalidation(previousText, updatedText, record.Changes));
        }

        private void ApplyUndoRecord(DocumentSession session, EditorEditRecord record)
        {
            var previousText = GetText(session);
            var undoChanges = BuildUndoChanges(record);
            var updatedText = ApplyChanges(previousText, undoChanges);
            UpdateSession(session, updatedText, record.BeforeSelections, BuildInvalidation(previousText, updatedText, undoChanges));
        }

        private void UpdateSession(
            DocumentSession session,
            string updatedText,
            IList<EditorSelectionRange> selections,
            EditorInvalidation invalidation)
        {
            session.Text = updatedText ?? string.Empty;
            session.TextVersion++;
            session.LastTextMutationUtc = DateTime.UtcNow;
            session.IsDirty = !string.Equals(session.Text ?? string.Empty, session.OriginalTextSnapshot ?? string.Empty, StringComparison.Ordinal);
            session.EditorState.CachedTextVersion = -1;
            session.EditorState.PendingInvalidation = invalidation ?? new EditorInvalidation();
            session.PendingLanguageInvalidation = CloneInvalidation(invalidation);
            ApplySelections(session, selections, true);
            session.EditorState.ScrollToCaretPending = true;
            session.EditorState.HasExplicitCaretPlacement = true;
        }

        private void PushUndo(DocumentSession session, EditorEditRecord record, bool allowMerge)
        {
            var undoStack = session.EditorState.UndoStack;
            if (allowMerge && undoStack.Count > 0 && CanMerge(undoStack[undoStack.Count - 1], record))
            {
                var previous = undoStack[undoStack.Count - 1];
                previous.Changes[0].InsertedText = (previous.Changes[0].InsertedText ?? string.Empty) + (record.Changes[0].InsertedText ?? string.Empty);
                previous.AfterSelections.Clear();
                for (var i = 0; i < record.AfterSelections.Count; i++)
                {
                    previous.AfterSelections.Add(record.AfterSelections[i].Clone());
                }
                return;
            }

            undoStack.Add(CloneRecord(record));
            TrimHistory(undoStack, session.EditorState.UndoLimit);
        }

        private void EnsureLineMap(DocumentSession session)
        {
            if (session == null || session.EditorState == null)
            {
                return;
            }

            if (session.EditorState.LineMap != null && session.EditorState.CachedTextVersion == session.TextVersion)
            {
                return;
            }

            var text = GetText(session);
            var starts = new List<int>();
            starts.Add(0);
            for (var i = 0; i < text.Length; i++)
            {
                var current = text[i];
                if (current == '\r')
                {
                    if (i + 1 < text.Length && text[i + 1] == '\n')
                    {
                        i++;
                    }

                    starts.Add(i + 1);
                }
                else if (current == '\n')
                {
                    starts.Add(i + 1);
                }
            }

            session.EditorState.LineMap = new EditorLineMap();
            session.EditorState.LineMap.LineStarts = starts.ToArray();
            session.EditorState.CachedTextVersion = session.TextVersion;
        }

        private void NormalizeSelections(DocumentSession session)
        {
            if (session == null || session.EditorState == null)
            {
                return;
            }

            var textLength = GetText(session).Length;
            if (session.EditorState.Selections.Count == 0)
            {
                session.EditorState.Selections.Add(new EditorSelectionRange());
            }

            for (var i = session.EditorState.Selections.Count - 1; i >= 0; i--)
            {
                var selection = session.EditorState.Selections[i];
                if (selection == null)
                {
                    session.EditorState.Selections.RemoveAt(i);
                    continue;
                }

                selection.AnchorIndex = Clamp(selection.AnchorIndex, 0, textLength);
                selection.CaretIndex = Clamp(selection.CaretIndex, 0, textLength);
            }

            if (session.EditorState.Selections.Count == 0)
            {
                session.EditorState.Selections.Add(new EditorSelectionRange());
            }
        }

        private void ApplySelections(DocumentSession session, IList<EditorSelectionRange> selections, bool preservePreferredColumn)
        {
            if (session == null || session.EditorState == null)
            {
                return;
            }

            session.EditorState.Selections.Clear();
            if (selections != null)
            {
                for (var i = 0; i < selections.Count; i++)
                {
                    var selection = selections[i] != null ? selections[i].Clone() : new EditorSelectionRange();
                    if (!preservePreferredColumn || selection.PreferredColumn < 0)
                    {
                        selection.PreferredColumn = GetCaretPosition(session, selection.CaretIndex).Column;
                    }

                    session.EditorState.Selections.Add(selection);
                }
            }

            if (session.EditorState.Selections.Count == 0)
            {
                session.EditorState.Selections.Add(new EditorSelectionRange());
            }

            NormalizeSelections(session);
        }

        private void SetSingleSelection(DocumentSession session, EditorSelectionRange selection)
        {
            var selections = new List<EditorSelectionRange>();
            selections.Add(selection != null ? selection.Clone() : new EditorSelectionRange());
            ApplySelections(session, selections, false);
        }

        private SelectedLineBlock GetSelectedLineBlock(DocumentSession session)
        {
            var selections = GetSelections(session);
            var startLine = int.MaxValue;
            var endLine = 0;
            for (var i = 0; i < selections.Length; i++)
            {
                var selection = selections[i];
                var selectionStartLine = GetCaretPosition(session, selection.Start).Line;
                var endReference = selection.End > selection.Start ? selection.End - 1 : selection.End;
                var selectionEndLine = GetCaretPosition(session, endReference).Line;
                startLine = Math.Min(startLine, selectionStartLine);
                endLine = Math.Max(endLine, selectionEndLine);
            }

            if (startLine == int.MaxValue)
            {
                startLine = 0;
            }

            return new SelectedLineBlock
            {
                StartLine = startLine,
                EndLine = endLine,
                StartIndex = GetCharacterIndex(session, startLine, 0),
                EndExclusive = endLine + 1 < GetLineCount(session)
                    ? GetCharacterIndex(session, endLine + 1, 0)
                    : GetText(session).Length
            };
        }

        private int AdjustIndexForLineTransform(DocumentSession session, int index, int startLine, IList<int> lineAdjustments)
        {
            var line = GetCaretPosition(session, index).Line;
            if (line < startLine)
            {
                return index;
            }

            var delta = 0;
            for (var i = 0; i <= line - startLine && i < lineAdjustments.Count; i++)
            {
                delta += lineAdjustments[i];
            }

            return Math.Max(0, index + delta);
        }

        private EditorInvalidation BuildInvalidation(string previousText, string updatedText, IList<EditorTextChange> changes)
        {
            if (changes == null || changes.Count == 0)
            {
                return new EditorInvalidation();
            }

            var start = changes[0].Start;
            var oldLength = 0;
            var newLength = 0;
            for (var i = 0; i < changes.Count; i++)
            {
                start = Math.Min(start, changes[i].Start);
                oldLength += changes[i].RemovedText != null ? changes[i].RemovedText.Length : 0;
                newLength += changes[i].InsertedText != null ? changes[i].InsertedText.Length : 0;
            }

            var safePreviousText = previousText ?? string.Empty;
            var safeUpdatedText = updatedText ?? string.Empty;
            var previousStartPosition = GetCaretPosition(safePreviousText, start);
            var previousEndPosition = GetCaretPosition(safePreviousText, Clamp(start + oldLength, 0, safePreviousText.Length));
            var updatedEndPosition = GetCaretPosition(safeUpdatedText, Clamp(start + newLength, 0, safeUpdatedText.Length));
            var previousContextStart = GetExpandedLineStart(safePreviousText, start, 1);
            var previousContextEnd = GetExpandedLineEndExclusive(
                safePreviousText,
                GetLineRangeFocusPosition(safePreviousText, start, oldLength),
                1);
            var currentContextStart = GetExpandedLineStart(safeUpdatedText, start, 1);
            var currentContextEnd = GetExpandedLineEndExclusive(
                safeUpdatedText,
                GetLineRangeFocusPosition(safeUpdatedText, start, newLength),
                1);
            return new EditorInvalidation
            {
                Start = start,
                OldLength = oldLength,
                NewLength = newLength,
                StartLine = previousStartPosition.Line,
                EndLine = updatedEndPosition.Line,
                PreviousContextStart = previousContextStart,
                PreviousContextLength = Math.Max(0, previousContextEnd - previousContextStart),
                CurrentContextStart = currentContextStart,
                CurrentContextLength = Math.Max(0, currentContextEnd - currentContextStart),
                CanUseIncrementalLanguageAnalysis =
                    previousStartPosition.Line == previousEndPosition.Line &&
                    previousStartPosition.Line == updatedEndPosition.Line
            };
        }

        private static EditorInvalidation CloneInvalidation(EditorInvalidation invalidation)
        {
            if (invalidation == null)
            {
                return new EditorInvalidation();
            }

            return new EditorInvalidation
            {
                Start = invalidation.Start,
                OldLength = invalidation.OldLength,
                NewLength = invalidation.NewLength,
                StartLine = invalidation.StartLine,
                EndLine = invalidation.EndLine,
                PreviousContextStart = invalidation.PreviousContextStart,
                PreviousContextLength = invalidation.PreviousContextLength,
                CurrentContextStart = invalidation.CurrentContextStart,
                CurrentContextLength = invalidation.CurrentContextLength,
                CanUseIncrementalLanguageAnalysis = invalidation.CanUseIncrementalLanguageAnalysis
            };
        }

        private static int GetLineRangeFocusPosition(string text, int start, int length)
        {
            var safeText = text ?? string.Empty;
            if (safeText.Length == 0)
            {
                return 0;
            }

            var end = Clamp(start + Math.Max(0, length), 0, safeText.Length);
            if (end > start && end > 0)
            {
                return end - 1;
            }

            return Clamp(start, 0, safeText.Length - 1);
        }

        private static int GetExpandedLineStart(string text, int position, int linesBefore)
        {
            var safeText = text ?? string.Empty;
            var lineStart = FindLineStart(safeText, Clamp(position, 0, safeText.Length));
            for (var i = 0; i < linesBefore && lineStart > 0; i++)
            {
                lineStart = FindLineStart(safeText, Math.Max(0, lineStart - 1));
            }

            return lineStart;
        }

        private static int GetExpandedLineEndExclusive(string text, int position, int linesAfter)
        {
            var safeText = text ?? string.Empty;
            var lineEnd = FindLineEndExclusive(safeText, Clamp(position, 0, safeText.Length));
            for (var i = 0; i < linesAfter && lineEnd < safeText.Length; i++)
            {
                lineEnd = FindLineEndExclusive(safeText, lineEnd);
            }

            return lineEnd;
        }

        private static int FindLineStart(string text, int position)
        {
            var safeText = text ?? string.Empty;
            var index = Clamp(position, 0, safeText.Length);
            while (index > 0)
            {
                var previous = safeText[index - 1];
                if (previous == '\n' || previous == '\r')
                {
                    break;
                }

                index--;
            }

            return index;
        }

        private static int FindLineEndExclusive(string text, int position)
        {
            var safeText = text ?? string.Empty;
            var index = Clamp(position, 0, safeText.Length);
            while (index < safeText.Length)
            {
                if (safeText[index] == '\r')
                {
                    index++;
                    if (index < safeText.Length && safeText[index] == '\n')
                    {
                        index++;
                    }

                    return index;
                }

                if (safeText[index] == '\n')
                {
                    return index + 1;
                }

                index++;
            }

            return safeText.Length;
        }

        private static EditorCaretPosition GetCaretPosition(string text, int characterIndex)
        {
            var safeText = text ?? string.Empty;
            var index = Clamp(characterIndex, 0, safeText.Length);
            var line = 0;
            var lineStart = 0;
            for (var i = 0; i < index; i++)
            {
                if (safeText[i] == '\r')
                {
                    if (i + 1 < safeText.Length && safeText[i + 1] == '\n')
                    {
                        i++;
                    }

                    line++;
                    lineStart = i + 1;
                }
                else if (safeText[i] == '\n')
                {
                    line++;
                    lineStart = i + 1;
                }
            }

            return new EditorCaretPosition
            {
                Line = line,
                Column = index - lineStart
            };
        }

        private static EditorEditRecord BuildRecord(
            string text,
            IList<EditorSelectionRange> currentSelections,
            IList<PreparedSelectionEdit> edits,
            string mergeGroup)
        {
            var record = new EditorEditRecord();
            record.MergeGroup = mergeGroup ?? string.Empty;

            var beforeSelections = CloneSelections(currentSelections);
            for (var i = 0; i < beforeSelections.Count; i++)
            {
                record.BeforeSelections.Add(beforeSelections[i]);
            }

            for (var i = 0; i < edits.Count; i++)
            {
                record.Changes.Add(new EditorTextChange
                {
                    Start = edits[i].Start,
                    RemovedText = edits[i].DeleteLength > 0 ? text.Substring(edits[i].Start, edits[i].DeleteLength) : string.Empty,
                    InsertedText = edits[i].InsertedText ?? string.Empty
                });
            }

            var afterSelections = BuildAfterSelections(edits);
            for (var i = 0; i < afterSelections.Count; i++)
            {
                record.AfterSelections.Add(afterSelections[i]);
            }

            return record;
        }

        private static List<EditorSelectionRange> BuildAfterSelections(IList<PreparedSelectionEdit> edits)
        {
            var result = new List<EditorSelectionRange>();
            for (var i = 0; i < edits.Count; i++)
            {
                var shift = 0;
                for (var j = 0; j < edits.Count; j++)
                {
                    if (edits[j].Start >= edits[i].Start)
                    {
                        break;
                    }

                    shift += edits[j].Delta;
                }

                var selection = edits[i].AfterSelection != null ? edits[i].AfterSelection.Clone() : new EditorSelectionRange();
                selection.AnchorIndex += shift;
                selection.CaretIndex += shift;
                result.Add(selection);
            }

            return result;
        }

        private static List<EditorTextChange> BuildUndoChanges(EditorEditRecord record)
        {
            var result = new List<EditorTextChange>();
            var shift = 0;
            for (var i = 0; i < record.Changes.Count; i++)
            {
                var change = record.Changes[i];
                result.Add(new EditorTextChange
                {
                    Start = change.Start + shift,
                    RemovedText = change.InsertedText ?? string.Empty,
                    InsertedText = change.RemovedText ?? string.Empty
                });

                shift += (change.InsertedText != null ? change.InsertedText.Length : 0) -
                    (change.RemovedText != null ? change.RemovedText.Length : 0);
            }

            return result;
        }

        private static string ApplyChanges(string text, IList<EditorTextChange> changes)
        {
            var updated = text ?? string.Empty;
            var ordered = new List<EditorTextChange>();
            for (var i = 0; i < changes.Count; i++)
            {
                ordered.Add(changes[i]);
            }

            ordered.Sort(delegate(EditorTextChange left, EditorTextChange right)
            {
                if (left.Start != right.Start)
                {
                    return right.Start.CompareTo(left.Start);
                }

                return (right.RemovedText != null ? right.RemovedText.Length : 0)
                    .CompareTo(left.RemovedText != null ? left.RemovedText.Length : 0);
            });

            for (var i = 0; i < ordered.Count; i++)
            {
                var removeLength = ordered[i].RemovedText != null ? ordered[i].RemovedText.Length : 0;
                updated = updated.Substring(0, ordered[i].Start) +
                    (ordered[i].InsertedText ?? string.Empty) +
                    updated.Substring(ordered[i].Start + removeLength);
            }

            return updated;
        }

        private static void RemoveDuplicateEdits(List<PreparedSelectionEdit> edits)
        {
            for (var index = edits.Count - 1; index > 0; index--)
            {
                if (edits[index].Start == edits[index - 1].Start &&
                    edits[index].DeleteLength == edits[index - 1].DeleteLength)
                {
                    edits.RemoveAt(index);
                }
            }
        }

        private static int ComparePreparedEdits(PreparedSelectionEdit left, PreparedSelectionEdit right)
        {
            if (left.Start != right.Start)
            {
                return left.Start.CompareTo(right.Start);
            }

            return left.DeleteLength.CompareTo(right.DeleteLength);
        }

        private static List<EditorSelectionRange> CloneSelections(IList<EditorSelectionRange> selections)
        {
            var result = new List<EditorSelectionRange>();
            if (selections == null)
            {
                return result;
            }

            for (var i = 0; i < selections.Count; i++)
            {
                result.Add(selections[i] != null ? selections[i].Clone() : new EditorSelectionRange());
            }

            return result;
        }

        private static List<EditorSelectionRange> OffsetSelections(IList<EditorSelectionRange> selections, int delta)
        {
            var result = CloneSelections(selections);
            for (var i = 0; i < result.Count; i++)
            {
                result[i].AnchorIndex += delta;
                result[i].CaretIndex += delta;
                result[i].PreferredColumn = -1;
            }

            return result;
        }

        private static EditorEditRecord CloneRecord(EditorEditRecord record)
        {
            var clone = new EditorEditRecord();
            if (record == null)
            {
                return clone;
            }

            clone.MergeGroup = record.MergeGroup ?? string.Empty;
            for (var i = 0; i < record.Changes.Count; i++)
            {
                clone.Changes.Add(new EditorTextChange
                {
                    Start = record.Changes[i].Start,
                    RemovedText = record.Changes[i].RemovedText,
                    InsertedText = record.Changes[i].InsertedText
                });
            }

            for (var i = 0; i < record.BeforeSelections.Count; i++)
            {
                clone.BeforeSelections.Add(record.BeforeSelections[i].Clone());
            }

            for (var i = 0; i < record.AfterSelections.Count; i++)
            {
                clone.AfterSelections.Add(record.AfterSelections[i].Clone());
            }

            return clone;
        }

        private static EditorSelectionRange CreateSelection(int anchorIndex, int caretIndex, int preferredColumn)
        {
            return new EditorSelectionRange
            {
                AnchorIndex = anchorIndex,
                CaretIndex = caretIndex,
                PreferredColumn = preferredColumn
            };
        }

        private int GetLineEndIndex(DocumentSession session, int line)
        {
            var map = GetLineMap(session);
            if (line < 0 || line >= map.LineStarts.Length)
            {
                return 0;
            }

            var text = GetText(session);
            var nextLineStart = line + 1 < map.LineStarts.Length ? map.LineStarts[line + 1] : text.Length;
            var end = nextLineStart;
            while (end > map.LineStarts[line] && (text[end - 1] == '\r' || text[end - 1] == '\n'))
            {
                end--;
            }

            return end;
        }

        private static int FindLineIndex(int[] lineStarts, int index)
        {
            var low = 0;
            var high = lineStarts.Length - 1;
            while (low <= high)
            {
                var mid = low + ((high - low) / 2);
                var midValue = lineStarts[mid];
                if (midValue == index)
                {
                    return mid;
                }

                if (midValue < index)
                {
                    low = mid + 1;
                }
                else
                {
                    high = mid - 1;
                }
            }

            return Math.Max(0, high);
        }

        private static bool CanMerge(EditorEditRecord previous, EditorEditRecord next)
        {
            return previous != null &&
                next != null &&
                previous.Changes.Count == 1 &&
                next.Changes.Count == 1 &&
                previous.BeforeSelections.Count == 1 &&
                next.BeforeSelections.Count == 1 &&
                !string.IsNullOrEmpty(previous.MergeGroup) &&
                string.Equals(previous.MergeGroup, next.MergeGroup, StringComparison.Ordinal) &&
                string.IsNullOrEmpty(previous.Changes[0].RemovedText) &&
                string.IsNullOrEmpty(next.Changes[0].RemovedText) &&
                previous.Changes[0].Start + (previous.Changes[0].InsertedText != null ? previous.Changes[0].InsertedText.Length : 0) == next.Changes[0].Start;
        }

        private static void TrimHistory<T>(List<T> stack, int limit)
        {
            while (stack != null && stack.Count > limit)
            {
                stack.RemoveAt(0);
            }
        }

        private static int Clamp(int value, int minimum, int maximum)
        {
            return value < minimum ? minimum : (value > maximum ? maximum : value);
        }

        private static bool IsWordCharacter(char value)
        {
            return char.IsLetterOrDigit(value) || value == '_' || value == '@';
        }

        private static bool CanEdit(DocumentSession session)
        {
            return session != null && session.SupportsEditing;
        }

        private static bool HasMultipleSelections(DocumentSession session)
        {
            return session != null && session.EditorState != null && session.EditorState.HasMultipleSelections;
        }

        private static string GetText(DocumentSession session)
        {
            return session != null ? session.Text ?? string.Empty : string.Empty;
        }

        private static string GetLeadingWhitespace(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            var index = 0;
            while (index < text.Length && (text[index] == ' ' || text[index] == '\t'))
            {
                index++;
            }

            return index > 0 ? text.Substring(0, index) : string.Empty;
        }

        private static int CountOutdent(string lineText)
        {
            if (string.IsNullOrEmpty(lineText))
            {
                return 0;
            }

            var count = 0;
            while (count < lineText.Length && count < TabSize && lineText[count] == ' ')
            {
                count++;
            }

            if (count == 0 && lineText[0] == '\t')
            {
                return 1;
            }

            return count;
        }

        private static string BuildTabInsertion(int column)
        {
            var remaining = TabSize - (Math.Max(0, column) % TabSize);
            return new string(' ', remaining == 0 ? TabSize : remaining);
        }

        private sealed class PreparedSelectionEdit
        {
            public int Start;
            public int DeleteLength;
            public string InsertedText = string.Empty;
            public EditorSelectionRange AfterSelection;

            public int Delta
            {
                get
                {
                    return (InsertedText != null ? InsertedText.Length : 0) - DeleteLength;
                }
            }
        }

        private sealed class SelectedLineBlock
        {
            public int StartLine;
            public int EndLine;
            public int StartIndex;
            public int EndExclusive;
        }

        private delegate PreparedSelectionEdit SelectionEditBuilder(EditorSelectionRange selection, string text);
    }
}
