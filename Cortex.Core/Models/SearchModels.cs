using System;
using System.Collections.Generic;

namespace Cortex.Core.Models
{
    public enum SearchScopeKind
    {
        CurrentDocument = 0,
        AllOpenDocuments = 1,
        CurrentProject = 2,
        EntireSolution = 3
    }

    public enum TextSearchWorkflowKind
    {
        Find = 0,
        References = 1,
        Rename = 2,
        CallHierarchy = 3,
        ValueSource = 4,
        UnitTests = 5
    }

    [Serializable]
    public sealed class TextSearchQuery
    {
        public string SearchText;
        public SearchScopeKind Scope;
        public bool MatchCase;
        public bool WholeWord;

        public TextSearchQuery()
        {
            SearchText = string.Empty;
            Scope = SearchScopeKind.CurrentDocument;
        }
    }

    public sealed class TextSearchDocumentInput
    {
        public string DocumentPath;
        public string DisplayPath;
        public string Text;
    }

    public sealed class TextSearchMatch
    {
        public string DocumentPath;
        public string DisplayPath;
        public int AbsoluteIndex;
        public int Length;
        public int LineNumber;
        public int ColumnNumber;
        public string LineText;
        public string PreviewText;
    }

    public sealed class TextSearchDocumentResult
    {
        public string DocumentPath;
        public string DisplayPath;
        public readonly List<TextSearchMatch> Matches = new List<TextSearchMatch>();
    }

    public sealed class TextSearchResultSet
    {
        public TextSearchQuery Query;
        public readonly List<TextSearchDocumentResult> Documents = new List<TextSearchDocumentResult>();
        public int SearchedDocumentCount;
        public int TotalMatchCount;
        public DateTime GeneratedUtc;
        public string StatusMessage;
    }

    public sealed class TextSearchReplacementResult
    {
        public int UpdatedDocumentCount;
        public int UpdatedMatchCount;
        public int PendingSaveDocumentCount;
        public int SkippedDocumentCount;
        public string StatusMessage;
    }
}
