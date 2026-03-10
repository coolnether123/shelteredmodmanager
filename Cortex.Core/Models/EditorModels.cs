using System.Collections.Generic;

namespace Cortex.Core.Models
{
    /// <summary>
    /// Zero-based logical caret position.
    /// </summary>
    public sealed class EditorCaretPosition
    {
        public int Line;
        public int Column;
    }

    /// <summary>
    /// Represents one caret or selection range.
    /// </summary>
    public sealed class EditorSelectionRange
    {
        public int AnchorIndex;
        public int CaretIndex;
        public int PreferredColumn;

        public EditorSelectionRange()
        {
            PreferredColumn = -1;
        }

        public bool HasSelection
        {
            get { return AnchorIndex != CaretIndex; }
        }

        public int Start
        {
            get { return AnchorIndex < CaretIndex ? AnchorIndex : CaretIndex; }
        }

        public int End
        {
            get { return AnchorIndex > CaretIndex ? AnchorIndex : CaretIndex; }
        }

        public EditorSelectionRange Clone()
        {
            return new EditorSelectionRange
            {
                AnchorIndex = AnchorIndex,
                CaretIndex = CaretIndex,
                PreferredColumn = PreferredColumn
            };
        }
    }

    /// <summary>
    /// Maps logical lines to character indices for fast caret and selection queries.
    /// </summary>
    public sealed class EditorLineMap
    {
        public int[] LineStarts;

        public EditorLineMap()
        {
            LineStarts = new[] { 0 };
        }
    }

    /// <summary>
    /// Describes the text region that needs layout refresh after a mutation.
    /// </summary>
    public sealed class EditorInvalidation
    {
        public int Start;
        public int OldLength;
        public int NewLength;
        public int StartLine;
        public int EndLine;
        public int PreviousContextStart;
        public int PreviousContextLength;
        public int CurrentContextStart;
        public int CurrentContextLength;
        public bool CanUseIncrementalLanguageAnalysis;
    }

    /// <summary>
    /// Stores one text replacement inside an edit record.
    /// </summary>
    public sealed class EditorTextChange
    {
        public int Start;
        public string RemovedText;
        public string InsertedText;
    }

    /// <summary>
    /// Stores one reversible editor action, including all text changes and selection state.
    /// </summary>
    public sealed class EditorEditRecord
    {
        public readonly List<EditorTextChange> Changes = new List<EditorTextChange>();
        public readonly List<EditorSelectionRange> BeforeSelections = new List<EditorSelectionRange>();
        public readonly List<EditorSelectionRange> AfterSelections = new List<EditorSelectionRange>();
        public string MergeGroup = string.Empty;
    }

    /// <summary>
    /// Holds editor-only state for a single open document.
    /// This is intentionally separate from the broader document session model.
    /// </summary>
    public sealed class EditorDocumentState
    {
        public bool ScrollToCaretPending;
        public bool HasExplicitCaretPlacement;
        public int CachedTextVersion;
        public int UndoLimit;
        public EditorLineMap LineMap;
        public EditorInvalidation PendingInvalidation;
        public readonly List<EditorSelectionRange> Selections;
        public readonly List<EditorEditRecord> UndoStack;
        public readonly List<EditorEditRecord> RedoStack;

        public EditorDocumentState()
        {
            CachedTextVersion = -1;
            UndoLimit = 128;
            LineMap = new EditorLineMap();
            PendingInvalidation = new EditorInvalidation();
            Selections = new List<EditorSelectionRange>();
            Selections.Add(new EditorSelectionRange());
            UndoStack = new List<EditorEditRecord>();
            RedoStack = new List<EditorEditRecord>();
        }

        public EditorSelectionRange PrimarySelection
        {
            get
            {
                if (Selections.Count == 0)
                {
                    Selections.Add(new EditorSelectionRange());
                }

                return Selections[0];
            }
        }

        public int CaretIndex
        {
            get { return PrimarySelection.CaretIndex; }
            set { PrimarySelection.CaretIndex = value; }
        }

        public int SelectionAnchorIndex
        {
            get { return PrimarySelection.AnchorIndex; }
            set { PrimarySelection.AnchorIndex = value; }
        }

        public int PreferredColumn
        {
            get { return PrimarySelection.PreferredColumn; }
            set { PrimarySelection.PreferredColumn = value; }
        }

        public bool HasSelection
        {
            get
            {
                for (var i = 0; i < Selections.Count; i++)
                {
                    if (Selections[i].HasSelection)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public bool HasMultipleSelections
        {
            get { return Selections.Count > 1; }
        }

        public int SelectionStart
        {
            get { return PrimarySelection.Start; }
        }

        public int SelectionEnd
        {
            get { return PrimarySelection.End; }
        }
    }
}
