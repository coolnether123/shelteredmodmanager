using System.Collections.Generic;

namespace ModAPI.Internal.DebugUI
{
    internal sealed class UIDebugDiffLine
    {
        public string LeftContent;
        public string RightContent;
        public int LeftIndex;
        public int RightIndex;
        public string LeftMarker;
        public string RightMarker;
        public bool IsMatch;
    }

    internal sealed class UIDebugSourcePreviewHunk
    {
        public readonly List<string> Removed = new List<string>();
        public readonly List<string> Added = new List<string>();
        public int StartIndexBefore;
    }

    internal sealed class UIDebugSourceDiffAlignedRows
    {
        public readonly List<string> LeftLines = new List<string>();
        public readonly List<string> RightLines = new List<string>();
    }

    internal sealed class UIDebugPatchedSourcePreviewResult
    {
        public string PatchedSourceText;
        public string PatchedSourceRewrittenText;
        public int RegexReplaceCount;
        public readonly List<string> RegexSummaries = new List<string>();
    }
}
