using System.Collections.Generic;
using System;
using Cortex.LanguageService.Protocol;

namespace Cortex.Core.Models
{
    /// <summary>
    /// Identifies the role of an open document so the shell can enforce the correct
    /// editing, saving, and presentation rules.
    /// </summary>
    public enum DocumentKind
    {
        Unknown,
        SourceCode,
        DecompiledCode,
        Text,
        Log
    }

    /// <summary>
    /// Represents the in-memory state for an open document tab.
    /// Disk writes are deferred until an explicit save operation succeeds.
    /// </summary>
    public sealed class DocumentLanguageAnalysisSnapshot
    {
        public string TextSnapshot;
        public LanguageServiceAnalysisResponse Analysis;
        public bool HasClassifications;
        public bool HasDiagnostics;
        public DateTime CachedUtc;
    }

    public sealed class DocumentSession
    {
        public DocumentSession()
        {
            FilePath = string.Empty;
            Text = string.Empty;
            OriginalTextSnapshot = string.Empty;
            LanguageAnalysis = new LanguageServiceAnalysisResponse();
            EditorState = new EditorDocumentState();
            PendingLanguageInvalidation = new EditorInvalidation();
            LanguageAnalysisHistory = new List<DocumentLanguageAnalysisSnapshot>();
        }

        public string FilePath;
        public DocumentKind Kind;
        public bool IsReadOnly;
        public string Text;
        public string OriginalTextSnapshot;
        public bool IsDirty;
        public int TextVersion;
        public int LastLanguageAnalysisVersion;
        public int LastLanguageClassificationVersion;
        public int LastLanguageDiagnosticVersion;
        public int LastLanguageCacheRestoreVersion;
        public DateTime LastKnownWriteUtc;
        public DateTime LastTextMutationUtc;
        public bool HasExternalChanges;
        public int HighlightedLine;
        public LanguageServiceAnalysisResponse LanguageAnalysis;
        public DateTime LastLanguageAnalysisUtc;
        public EditorDocumentState EditorState;
        public EditorInvalidation PendingLanguageInvalidation;
        public readonly List<DocumentLanguageAnalysisSnapshot> LanguageAnalysisHistory;

        /// <summary>
        /// True when the document is a writable source file and can route through the
        /// editor mutation pipeline.
        /// </summary>
        public bool SupportsEditing
        {
            get { return Kind == DocumentKind.SourceCode && !IsReadOnly; }
        }

        /// <summary>
        /// True when Cortex is allowed to persist the current in-memory snapshot back
        /// to disk.
        /// </summary>
        public bool SupportsSaving
        {
            get { return !IsReadOnly && Kind != DocumentKind.DecompiledCode; }
        }
    }
}
