using System;
using System.IO;
using Cortex.Shell.Shared.Models;

namespace Cortex.Shell.Shared.Services
{
    public sealed class ProjectWorkspaceService
    {
        public WorkspaceAnalysisResult AnalyzeSourceRoot(string sourceRoot, string preferredProjectId)
        {
            var result = new WorkspaceAnalysisResult();
            if (string.IsNullOrEmpty(sourceRoot))
            {
                result.StatusMessage = "Set a source folder first.";
                result.Diagnostics.Add(result.StatusMessage);
                return result;
            }

            string normalizedRoot;
            try
            {
                normalizedRoot = Path.GetFullPath(sourceRoot.Trim());
            }
            catch (Exception ex)
            {
                result.StatusMessage = "Invalid source folder: " + ex.Message;
                result.Diagnostics.Add(result.StatusMessage);
                return result;
            }

            if (!Directory.Exists(normalizedRoot))
            {
                result.StatusMessage = "Source folder not found: " + normalizedRoot;
                result.Diagnostics.Add(result.StatusMessage);
                return result;
            }

            var projectFile = FindProjectFile(normalizedRoot);
            var projectId = !string.IsNullOrEmpty(preferredProjectId)
                ? preferredProjectId
                : !string.IsNullOrEmpty(projectFile)
                    ? Path.GetFileNameWithoutExtension(projectFile)
                    : Path.GetFileName(normalizedRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

            result.Success = true;
            result.Definition = new WorkspaceProjectDefinition
            {
                ProjectId = projectId,
                DisplayName = projectId,
                SourceRootPath = normalizedRoot,
                ProjectFilePath = projectFile
            };
            result.StatusMessage = "Prepared source mapping for " + result.Definition.DisplayName + ".";
            result.Diagnostics.Add(result.StatusMessage);
            return result;
        }

        public WorkspaceImportResult DiscoverWorkspaceProjects(string workspaceRoot)
        {
            var result = new WorkspaceImportResult();
            result.WorkspaceRootPath = workspaceRoot ?? string.Empty;
            if (string.IsNullOrEmpty(workspaceRoot) || !Directory.Exists(workspaceRoot))
            {
                result.StatusMessage = "Workspace root is not configured.";
                result.Diagnostics.Add(result.StatusMessage);
                return result;
            }

            var projectFiles = Directory.GetFiles(workspaceRoot, "*.csproj", SearchOption.AllDirectories);
            Array.Sort(projectFiles, StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < projectFiles.Length; i++)
            {
                if (IsBuildArtifact(projectFiles[i]))
                {
                    continue;
                }

                result.Definitions.Add(new WorkspaceProjectDefinition
                {
                    ProjectId = Path.GetFileNameWithoutExtension(projectFiles[i]),
                    DisplayName = Path.GetFileNameWithoutExtension(projectFiles[i]),
                    SourceRootPath = Path.GetDirectoryName(projectFiles[i]) ?? string.Empty,
                    ProjectFilePath = projectFiles[i]
                });
            }

            result.ImportedCount = result.Definitions.Count;
            result.StatusMessage = "Imported " + result.ImportedCount + " project definition(s) from workspace root.";
            result.Diagnostics.Add(result.StatusMessage);
            return result;
        }

        public WorkspaceValidationResult Validate(WorkspaceProjectDefinition definition)
        {
            var result = new WorkspaceValidationResult();
            if (definition == null)
            {
                result.StatusMessage = "No project selected.";
                result.Lines.Add(result.StatusMessage);
                return result;
            }

            var hasProjectId = !string.IsNullOrEmpty(definition.ProjectId);
            var hasSourceRoot = !string.IsNullOrEmpty(definition.SourceRootPath) && Directory.Exists(definition.SourceRootPath);
            var hasProjectFile = !string.IsNullOrEmpty(definition.ProjectFilePath) && File.Exists(definition.ProjectFilePath);

            result.Lines.Add("Project ID: " + (hasProjectId ? definition.ProjectId : "Missing"));
            result.Lines.Add("Source Root: " + (hasSourceRoot ? "OK" : "Missing or unreadable"));
            result.Lines.Add("Project File: " + (hasProjectFile ? "OK" : "Missing"));
            result.Success = hasProjectId && hasSourceRoot;
            result.StatusMessage = result.Success
                ? "Project mapping is ready for browsing and file preview."
                : "Project mapping is incomplete.";
            result.Lines.Add(result.StatusMessage);
            return result;
        }

        public WorkspaceFileNode BuildWorkspaceTree(string rootPath)
        {
            if (string.IsNullOrEmpty(rootPath) || !Directory.Exists(rootPath))
            {
                return null;
            }

            return BuildDirectoryNode(new DirectoryInfo(rootPath));
        }

        public string ReadFilePreview(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                return string.Empty;
            }

            return File.ReadAllText(filePath);
        }

        private static WorkspaceFileNode BuildDirectoryNode(DirectoryInfo directory)
        {
            var node = new WorkspaceFileNode
            {
                Name = directory != null ? directory.Name : string.Empty,
                FullPath = directory != null ? directory.FullName : string.Empty,
                IsDirectory = true
            };
            if (directory == null)
            {
                return node;
            }

            DirectoryInfo[] directories;
            try { directories = directory.GetDirectories(); }
            catch { directories = new DirectoryInfo[0]; }

            Array.Sort(directories, delegate(DirectoryInfo left, DirectoryInfo right)
            {
                return string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);
            });

            for (var i = 0; i < directories.Length; i++)
            {
                if (!ShouldSkipDirectory(directories[i].Name))
                {
                    node.Children.Add(BuildDirectoryNode(directories[i]));
                }
            }

            FileInfo[] files;
            try { files = directory.GetFiles(); }
            catch { files = new FileInfo[0]; }

            Array.Sort(files, delegate(FileInfo left, FileInfo right)
            {
                return string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);
            });

            for (var i = 0; i < files.Length; i++)
            {
                if (!ShouldIncludeFile(files[i].Extension))
                {
                    continue;
                }

                node.Children.Add(new WorkspaceFileNode
                {
                    Name = files[i].Name,
                    FullPath = files[i].FullName,
                    IsDirectory = false
                });
            }

            return node;
        }

        private static string FindProjectFile(string sourceRoot)
        {
            var projectFiles = Directory.GetFiles(sourceRoot, "*.csproj", SearchOption.AllDirectories);
            Array.Sort(projectFiles, StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < projectFiles.Length; i++)
            {
                if (!IsBuildArtifact(projectFiles[i]))
                {
                    return projectFiles[i];
                }
            }

            return string.Empty;
        }

        private static bool ShouldIncludeFile(string extension)
        {
            return string.Equals(extension, ".cs", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(extension, ".json", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(extension, ".xml", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(extension, ".txt", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(extension, ".md", StringComparison.OrdinalIgnoreCase);
        }

        private static bool ShouldSkipDirectory(string name)
        {
            return string.Equals(name, ".git", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "bin", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "obj", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsBuildArtifact(string path)
        {
            return path.IndexOf("\\bin\\", StringComparison.OrdinalIgnoreCase) >= 0 ||
                path.IndexOf("\\obj\\", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
