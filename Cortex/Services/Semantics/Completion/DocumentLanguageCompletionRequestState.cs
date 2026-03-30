namespace Cortex.Services.Semantics.Completion
{
    internal sealed class DocumentLanguageCompletionRequestState
    {
        public string RequestId;
        public int Generation;
        public string RequestKey;
        public string ContextKey;
        public string DocumentPath;
        public int DocumentVersion;
        public int AbsolutePosition;
    }
}
