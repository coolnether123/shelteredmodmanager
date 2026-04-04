namespace Cortex.Shell.Shared.Models
{
    public enum WorkbenchSearchScope
    {
        CurrentDocument = 0,
        AllOpenDocuments = 1,
        CurrentProject = 2,
        Workspace = 3
    }

    public sealed class EditorDocumentSummaryModel
    {
        public string FilePath { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public bool IsDirty { get; set; }
    }

    public sealed class EditorWorkbenchModel
    {
        public string ActiveDocumentPath { get; set; } = string.Empty;
        public string ActiveDocumentDisplayName { get; set; } = string.Empty;
        public string CompactPath { get; set; } = string.Empty;
        public bool UsesUnifiedSourceSurface { get; set; }
        public bool IsEditingEnabled { get; set; }
        public bool IsDirty { get; set; }
        public bool AllowSaving { get; set; }
        public int CaretLine { get; set; }
        public int CaretColumn { get; set; }
        public int LineCount { get; set; }
        public int HighlightedLine { get; set; }
        public bool HasHighlightedLine { get; set; }
        public string LanguageStatusLabel { get; set; } = string.Empty;
        public string CompletionStatusLabel { get; set; } = string.Empty;
        public System.Collections.Generic.List<EditorDocumentSummaryModel> OpenDocuments { get; set; } = new System.Collections.Generic.List<EditorDocumentSummaryModel>();
    }

    public sealed class SearchQueryModel
    {
        public string SearchText { get; set; } = string.Empty;
        public WorkbenchSearchScope Scope { get; set; }
        public bool MatchCase { get; set; }
        public bool WholeWord { get; set; }
    }

    public sealed class SearchMatchModel
    {
        public int ResultIndex { get; set; } = -1;
        public string DocumentPath { get; set; } = string.Empty;
        public string DisplayPath { get; set; } = string.Empty;
        public int LineNumber { get; set; }
        public int ColumnNumber { get; set; }
        public string LineText { get; set; } = string.Empty;
        public string PreviewText { get; set; } = string.Empty;
        public bool IsActive { get; set; }
    }

    public sealed class SearchDocumentResultModel
    {
        public string DocumentPath { get; set; } = string.Empty;
        public string DisplayPath { get; set; } = string.Empty;
        public System.Collections.Generic.List<SearchMatchModel> Matches { get; set; } = new System.Collections.Generic.List<SearchMatchModel>();
    }

    public sealed class SearchWorkbenchModel
    {
        public bool HasSemanticView { get; set; }
        public string Title { get; set; } = string.Empty;
        public string StatusMessage { get; set; } = string.Empty;
        public string ScopeCaption { get; set; } = string.Empty;
        public int TotalMatchCount { get; set; }
        public int ActiveMatchIndex { get; set; } = -1;
        public SearchQueryModel Query { get; set; } = new SearchQueryModel();
        public System.Collections.Generic.List<SearchDocumentResultModel> Documents { get; set; } = new System.Collections.Generic.List<SearchDocumentResultModel>();
    }

    public sealed class ReferenceWorkbenchModel
    {
        public bool HasDecompilerResult { get; set; }
        public bool FromCache { get; set; }
        public string StatusMessage { get; set; } = string.Empty;
        public string ResolvedTargetDisplayName { get; set; } = string.Empty;
        public string CachePath { get; set; } = string.Empty;
        public string XmlDocumentationPath { get; set; } = string.Empty;
        public string XmlDocumentationText { get; set; } = string.Empty;
        public string SourceText { get; set; } = string.Empty;
    }
}
