using System;
using System.Collections.Generic;
using System.IO;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;

namespace Cortex.Core.Services
{
    public sealed class ProjectWorkspaceService : IProjectWorkspaceService
    {
        private readonly ISourceLookupIndex _lookupIndex;

        public ProjectWorkspaceService(ISourceLookupIndex lookupIndex)
        {
            _lookupIndex = lookupIndex;
        }

        public ProjectWorkspaceAnalysis AnalyzeSourceRoot(string sourceRoot, string preferredModId)
        {
            var result = new ProjectWorkspaceAnalysis();
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

            RefreshRoot(normalizedRoot);
            var projectFile = FindProjectFile(normalizedRoot);
            var modId = !string.IsNullOrEmpty(preferredModId)
                ? preferredModId
                : !string.IsNullOrEmpty(projectFile)
                    ? Path.GetFileNameWithoutExtension(projectFile)
                    : Path.GetFileName(normalizedRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

            result.Definition = new CortexProjectDefinition
            {
                ModId = modId,
                SourceRootPath = normalizedRoot,
                ProjectFilePath = projectFile,
                BuildCommandOverride = string.Empty,
                OutputAssemblyPath = string.Empty,
                OutputPdbPath = string.Empty
            };

            result.Success = true;
            result.StatusMessage = "Prepared source mapping for " + result.Definition.GetDisplayName() + ".";
            result.Diagnostics.Add("Applied source folder " + normalizedRoot + " and detected mod id '" + (modId ?? string.Empty) + "'.");
            result.Diagnostics.Add(string.IsNullOrEmpty(projectFile)
                ? "No .csproj was detected under the source folder. Cortex will still browse source files."
                : "Detected project file: " + projectFile);
            return result;
        }

        public ProjectWorkspaceImportResult DiscoverWorkspaceProjects(string workspaceRoot)
        {
            var result = new ProjectWorkspaceImportResult();
            result.WorkspaceRootPath = workspaceRoot ?? string.Empty;

            if (string.IsNullOrEmpty(workspaceRoot) || !Directory.Exists(workspaceRoot))
            {
                result.StatusMessage = "Workspace root is not configured.";
                result.Diagnostics.Add(result.StatusMessage);
                return result;
            }

            RefreshRoot(workspaceRoot);
            var projectFiles = _lookupIndex != null
                ? _lookupIndex.GetProjectFiles(workspaceRoot)
                : new List<string>();

            for (var i = 0; i < projectFiles.Count; i++)
            {
                var projectFile = projectFiles[i];

                result.Definitions.Add(new CortexProjectDefinition
                {
                    ModId = Path.GetFileNameWithoutExtension(projectFile),
                    SourceRootPath = Path.GetDirectoryName(projectFile),
                    ProjectFilePath = projectFile,
                    BuildCommandOverride = string.Empty,
                    OutputAssemblyPath = string.Empty,
                    OutputPdbPath = string.Empty
                });
            }

            result.ImportedCount = result.Definitions.Count;
            result.StatusMessage = "Imported " + result.ImportedCount + " project definition(s) from workspace root.";
            result.Diagnostics.Add(result.StatusMessage);
            return result;
        }

        public ProjectValidationResult Validate(CortexProjectDefinition definition)
        {
            var result = new ProjectValidationResult();
            if (definition == null)
            {
                result.StatusMessage = "No project selected.";
                result.Lines.Add(result.StatusMessage);
                return result;
            }

            var hasModId = !string.IsNullOrEmpty(definition.ModId);
            var hasSourceRoot = !string.IsNullOrEmpty(definition.SourceRootPath) && Directory.Exists(definition.SourceRootPath);
            var hasProjectFile = !string.IsNullOrEmpty(definition.ProjectFilePath) && File.Exists(definition.ProjectFilePath);

            result.Lines.Add("Mod ID: " + (hasModId ? definition.ModId : "Missing"));
            result.Lines.Add("Source Root: " + (hasSourceRoot ? "OK" : "Missing or unreadable"));
            result.Lines.Add("Project File: " + (hasProjectFile ? "OK" : "Missing"));
            result.Lines.Add(string.IsNullOrEmpty(definition.OutputAssemblyPath) ? "Output DLL: Resolved from project file or build command." : "Output DLL: " + definition.OutputAssemblyPath);
            result.Lines.Add(string.IsNullOrEmpty(definition.OutputPdbPath) ? "Output PDB: Resolved from project file or build command." : "Output PDB: " + definition.OutputPdbPath);

            result.Success = hasModId && hasSourceRoot;
            result.StatusMessage = result.Success
                ? (hasProjectFile ? "Project mapping is ready for editor, build, and source navigation." : "Source mapping is ready, but build commands will remain unavailable until a project file is mapped.")
                : "Project mapping is incomplete.";
            result.Lines.Add(result.StatusMessage);
            return result;
        }

        public string FindLikelySourceRoot(string modRootPath)
        {
            if (string.IsNullOrEmpty(modRootPath) || !Directory.Exists(modRootPath))
            {
                return string.Empty;
            }

            var candidates = new[]
            {
                Path.Combine(modRootPath, "Source"),
                Path.Combine(modRootPath, "src"),
                Path.Combine(modRootPath, "Scripts"),
                modRootPath
            };

            for (var i = 0; i < candidates.Length; i++)
            {
                if (Directory.Exists(candidates[i]))
                {
                    return candidates[i];
                }
            }

            return string.Empty;
        }

        private string FindProjectFile(string sourceRoot)
        {
            if (string.IsNullOrEmpty(sourceRoot) || !Directory.Exists(sourceRoot))
            {
                return string.Empty;
            }

            if (_lookupIndex != null)
            {
                var projectFiles = _lookupIndex.GetProjectFiles(sourceRoot);
                for (var i = 0; i < projectFiles.Count; i++)
                {
                    return projectFiles[i];
                }

                return string.Empty;
            }

            return string.Empty;
        }

        private void RefreshRoot(string rootPath)
        {
            if (_lookupIndex != null)
            {
                _lookupIndex.RefreshRoot(rootPath);
            }
        }
    }
}
