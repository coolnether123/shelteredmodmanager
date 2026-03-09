using System;
using Cortex.LanguageService.Protocol;

namespace Cortex.Core.Models
{
    public sealed class DocumentSession
    {
        public string FilePath;
        public string Text;
        public string OriginalTextSnapshot;
        public bool IsDirty;
        public int TextVersion;
        public int LastLanguageAnalysisVersion;
        public DateTime LastKnownWriteUtc;
        public DateTime LastTextMutationUtc;
        public bool HasExternalChanges;
        public int HighlightedLine;
        public LanguageServiceAnalysisResponse LanguageAnalysis;
        public DateTime LastLanguageAnalysisUtc;
    }
}
