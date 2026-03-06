namespace Cortex.Core.Models
{
    public sealed class RuntimeStackFrame
    {
        public string AssemblyPath;
        public string TypeName;
        public string MethodName;
        public int MetadataToken;
        public int IlOffset;
        public string FilePath;
        public int LineNumber;
        public int ColumnNumber;
        public string DisplayText;
    }

    public sealed class SourceNavigationTarget
    {
        public bool Success;
        public bool IsDecompiledSource;
        public string FilePath;
        public int LineNumber;
        public int ColumnNumber;
        public string StatusMessage;
    }
}
