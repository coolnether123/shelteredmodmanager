using Cortex.Core.Models;

namespace Cortex
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

    internal sealed class PendingLanguageSignatureHelpRequest
    {
        public string RequestId;
        public int Generation;
        public string RequestKey;
        public string DocumentPath;
        public int DocumentVersion;
        public int AbsolutePosition;
    }

    internal sealed class PendingSemanticOperationRequest
    {
        public string RequestId;
        public int Generation;
        public SemanticRequestKind Kind;
        public string RequestKey;
        public string DocumentPath;
        public int DocumentVersion;
        public string SymbolText;
        public string NewName;
    }

    internal sealed class PendingMethodInspectorCallHierarchyRequest
    {
        public string RequestId;
        public int Generation;
        public string RequestKey;
        public string TargetKey;
        public string DocumentPath;
        public int DocumentVersion;
    }
}
