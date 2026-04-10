namespace Cortex.Shell.Shared.Models
{
    public sealed class RendererClassifiedTextSpan
    {
        public int Start { get; set; }
        public int Length { get; set; }
        public string Classification { get; set; } = string.Empty;
        public string SemanticTokenType { get; set; } = string.Empty;
    }

    public sealed class RendererDocumentContentModel
    {
        public string FilePath { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string CompactPath { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public bool IsReadOnly { get; set; }
        public bool IsDirty { get; set; }
        public int TextVersion { get; set; }
        public int HighlightedLine { get; set; }
        public bool HasHighlightedLine { get; set; }
        public int CaretLine { get; set; }
        public int CaretColumn { get; set; }
        public int LineCount { get; set; }
        public string LanguageStatusLabel { get; set; } = string.Empty;
        public string CompletionStatusLabel { get; set; } = string.Empty;
        public RendererClassifiedTextSpan[] Classifications { get; set; } = new RendererClassifiedTextSpan[0];
    }
}
