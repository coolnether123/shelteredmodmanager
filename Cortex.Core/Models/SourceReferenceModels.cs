namespace Cortex.Core.Models
{
    public enum DecompilerEntityKind
    {
        Method = 0,
        Type = 1
    }

    public sealed class DecompilerRequest
    {
        public string AssemblyPath;
        public int MetadataToken;
        public bool IgnoreCache;
        public DecompilerEntityKind EntityKind;
        public string CacheRelativePathStem;
    }

    public sealed class DecompilerResponse
    {
        public string SourceText;
        public string MapText;
        public string CachePath;
        public string MapPath;
        public string XmlDocumentationPath;
        public string XmlDocumentationText;
        public string ResolvedMemberDisplayName;
        public bool FromCache;
        public string StatusMessage;
    }
}
