namespace Cortex.Shell
{
    internal sealed class PendingLanguageHoverRequest
    {
        public string RequestId;
        public int Generation;
        public string DocumentPath;
        public int DocumentVersion;
        public string HoverKey;
    }

    internal sealed class PendingLanguageDefinitionRequest
    {
        public string RequestId;
        public int Generation;
        public string DocumentPath;
        public int DocumentVersion;
        public string TokenText;
    }
}
