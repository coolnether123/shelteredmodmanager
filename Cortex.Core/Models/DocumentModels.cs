using System;

namespace Cortex.Core.Models
{
    public sealed class DocumentSession
    {
        public string FilePath;
        public string Text;
        public bool IsDirty;
        public DateTime LastKnownWriteUtc;
        public bool HasExternalChanges;
        public int HighlightedLine;
    }
}
