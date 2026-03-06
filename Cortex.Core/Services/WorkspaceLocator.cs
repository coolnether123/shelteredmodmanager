using System.IO;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;

namespace Cortex.Core.Services
{
    public sealed class WorkspaceLocator : IWorkspaceLocator
    {
        public CortexWorkspacePaths GetWorkspace(CortexProjectDefinition project)
        {
            var paths = new CortexWorkspacePaths();
            if (project == null)
            {
                return paths;
            }

            paths.ProjectDirectory = string.IsNullOrEmpty(project.ProjectFilePath)
                ? string.Empty
                : Path.GetDirectoryName(project.ProjectFilePath);
            paths.SourceDirectory = project.SourceRootPath ?? paths.ProjectDirectory ?? string.Empty;
            paths.BuildOutputDirectory = string.IsNullOrEmpty(project.OutputAssemblyPath)
                ? string.Empty
                : Path.GetDirectoryName(project.OutputAssemblyPath);
            return paths;
        }
    }
}
