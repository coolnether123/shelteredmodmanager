namespace Cortex.Services.Semantics.Analysis
{
    internal sealed class DocumentLanguageAnalysisRequestState
    {
        public string RequestId;
        public int Generation;
        public string Fingerprint;
        public string DocumentPath;
        public int DocumentVersion;
        public bool IncludeDiagnostics;
        public bool IncludeClassifications;
        public bool IsPartialClassification;
        public int OldClassificationStart;
        public int OldClassificationLength;
        public int NewClassificationStart;
        public int NewClassificationLength;
    }

    internal sealed class IncrementalClassificationRange
    {
        public int RequestStart;
        public int RequestLength;
        public int OldSpanStart;
        public int OldSpanLength;
        public int NewSpanStart;
        public int NewSpanLength;
    }
}
