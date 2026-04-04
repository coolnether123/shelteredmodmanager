namespace Cortex.Shell.Shared.Models
{
    public sealed class WorkspaceProjectDefinition
    {
        public string ProjectId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string SourceRootPath { get; set; } = string.Empty;
        public string ProjectFilePath { get; set; } = string.Empty;
    }

    public sealed class WorkspaceAnalysisResult
    {
        public bool Success { get; set; }
        public string StatusMessage { get; set; } = string.Empty;
        public WorkspaceProjectDefinition Definition { get; set; }
        public System.Collections.Generic.List<string> Diagnostics { get; set; } = new System.Collections.Generic.List<string>();
    }

    public sealed class WorkspaceImportResult
    {
        public string WorkspaceRootPath { get; set; } = string.Empty;
        public int ImportedCount { get; set; }
        public string StatusMessage { get; set; } = string.Empty;
        public System.Collections.Generic.List<WorkspaceProjectDefinition> Definitions { get; set; } = new System.Collections.Generic.List<WorkspaceProjectDefinition>();
        public System.Collections.Generic.List<string> Diagnostics { get; set; } = new System.Collections.Generic.List<string>();
    }

    public sealed class WorkspaceValidationResult
    {
        public bool Success { get; set; }
        public string StatusMessage { get; set; } = string.Empty;
        public System.Collections.Generic.List<string> Lines { get; set; } = new System.Collections.Generic.List<string>();
    }

    public sealed class WorkspaceFileNode
    {
        public string Name { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
        public bool IsDirectory { get; set; }
        public System.Collections.Generic.List<WorkspaceFileNode> Children { get; set; } = new System.Collections.Generic.List<WorkspaceFileNode>();
    }
}
