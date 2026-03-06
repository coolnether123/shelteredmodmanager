using System;

namespace Cortex.Core.Models
{
    [Serializable]
    public sealed class CortexProjectDefinition
    {
        public string ModId;
        public string SourceRootPath;
        public string ProjectFilePath;
        public string BuildCommandOverride;
        public string OutputAssemblyPath;
        public string OutputPdbPath;

        public string GetDisplayName()
        {
            return string.IsNullOrEmpty(ModId) ? (ProjectFilePath ?? "Unknown Project") : ModId;
        }
    }

    public sealed class CortexWorkspacePaths
    {
        public string ProjectDirectory;
        public string SourceDirectory;
        public string BuildOutputDirectory;
    }
}
