using System.Collections.Generic;

namespace Cortex.Core.Models
{
    public sealed class LoadedModInfo
    {
        public string ModId;
        public string DisplayName;
        public string RootPath;
    }

    public sealed class ProjectWorkspaceAnalysis
    {
        public bool Success;
        public CortexProjectDefinition Definition;
        public string StatusMessage;
        public readonly List<string> Diagnostics = new List<string>();
    }

    public sealed class ProjectWorkspaceImportResult
    {
        public string WorkspaceRootPath;
        public int ImportedCount;
        public readonly List<CortexProjectDefinition> Definitions = new List<CortexProjectDefinition>();
        public readonly List<string> Diagnostics = new List<string>();
        public string StatusMessage;
    }

    public sealed class ProjectValidationResult
    {
        public bool Success;
        public readonly List<string> Lines = new List<string>();
        public string StatusMessage;
    }

    public enum WorkspaceTreeKind
    {
        ProjectSource = 0,
        DecompiledCache = 1
    }

    public sealed class WorkspaceTreeNode
    {
        public string Name;
        public string FullPath;
        public string RelativePath;
        public bool IsDirectory;
        public readonly List<WorkspaceTreeNode> Children = new List<WorkspaceTreeNode>();
    }

    public sealed class SourceLocationMatch
    {
        public bool Success;
        public string SourceKind;
        public string RawPath;
        public string ResolvedPath;
        public int LineNumber;
        public int ColumnNumber;
        public string StatusMessage;
    }

    public sealed class ReferenceAssemblyDescriptor
    {
        public string DisplayName;
        public string AssemblyPath;
    }

    public sealed class ReferenceTypeDescriptor
    {
        public string DisplayName;
        public string FullName;
        public string AssemblyPath;
        public int MetadataToken;
    }

    public sealed class ReferenceMemberDescriptor
    {
        public string DisplayName;
        public string AssemblyPath;
        public string DeclaringTypeName;
        public int MetadataToken;
    }

    public sealed class RuntimeToolStatus
    {
        public string ToolId;
        public string DisplayName;
        public string Description;
        public string ShortcutHint;
        public bool IsAvailable;
        public bool IsActive;
        public string UnavailableReason;
    }
}
