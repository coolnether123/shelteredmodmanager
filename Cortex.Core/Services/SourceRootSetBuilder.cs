using System;
using System.Collections.Generic;
using System.IO;
using Cortex.Core.Models;

namespace Cortex.Core.Services
{
    [Flags]
    public enum SourceRootFlags
    {
        None = 0,
        ProjectSource = 1,
        ProjectDirectory = 2,
        Workspace = 4,
        RuntimeContent = 8,
        Additional = 16,
        ReferenceAssemblies = 32
    }

    public static class SourceRootSetBuilder
    {
        public const SourceRootFlags DefaultNavigationRoots =
            SourceRootFlags.ProjectSource |
            SourceRootFlags.ProjectDirectory |
            SourceRootFlags.Workspace |
            SourceRootFlags.RuntimeContent |
            SourceRootFlags.Additional;

        public const SourceRootFlags LanguageServiceRoots =
            DefaultNavigationRoots |
            SourceRootFlags.ReferenceAssemblies;

        public static List<string> Build(CortexProjectDefinition project, CortexSettings settings, SourceRootFlags flags)
        {
            var roots = new List<string>();

            if ((flags & SourceRootFlags.ProjectSource) == SourceRootFlags.ProjectSource)
            {
                AddRoot(roots, project != null ? project.SourceRootPath : string.Empty);
            }

            if ((flags & SourceRootFlags.ProjectDirectory) == SourceRootFlags.ProjectDirectory)
            {
                AddRoot(roots, GetProjectDirectoryPath(project));
            }

            if ((flags & SourceRootFlags.Workspace) == SourceRootFlags.Workspace)
            {
                AddRoot(roots, settings != null ? settings.WorkspaceRootPath : string.Empty);
            }

            if ((flags & SourceRootFlags.RuntimeContent) == SourceRootFlags.RuntimeContent)
            {
                AddRoot(roots, settings != null ? settings.RuntimeContentRootPath : string.Empty);
            }

            if ((flags & SourceRootFlags.ReferenceAssemblies) == SourceRootFlags.ReferenceAssemblies)
            {
                AddRoot(roots, settings != null ? settings.ReferenceAssemblyRootPath : string.Empty);
            }

            if ((flags & SourceRootFlags.Additional) == SourceRootFlags.Additional)
            {
                AddAdditionalRoots(roots, settings != null ? settings.AdditionalSourceRoots : string.Empty);
            }

            return roots;
        }

        public static string BuildCacheKey(CortexProjectDefinition project, CortexSettings settings, SourceRootFlags flags)
        {
            return (int)flags + "|" +
                (project != null ? project.SourceRootPath ?? string.Empty : string.Empty) + "|" +
                (project != null ? project.ProjectFilePath ?? string.Empty : string.Empty) + "|" +
                (settings != null ? settings.WorkspaceRootPath ?? string.Empty : string.Empty) + "|" +
                (settings != null ? settings.RuntimeContentRootPath ?? string.Empty : string.Empty) + "|" +
                (settings != null ? settings.ReferenceAssemblyRootPath ?? string.Empty : string.Empty) + "|" +
                (settings != null ? settings.AdditionalSourceRoots ?? string.Empty : string.Empty);
        }

        private static void AddAdditionalRoots(ICollection<string> roots, string rawRoots)
        {
            if (roots == null || string.IsNullOrEmpty(rawRoots))
            {
                return;
            }

            var segments = rawRoots.Split(new[] { ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i < segments.Length; i++)
            {
                AddRoot(roots, segments[i]);
            }
        }

        private static void AddRoot(ICollection<string> roots, string rawPath)
        {
            if (roots == null || string.IsNullOrEmpty(rawPath))
            {
                return;
            }

            try
            {
                var fullPath = Path.GetFullPath(rawPath.Trim());
                if ((Directory.Exists(fullPath) || File.Exists(fullPath)) && !ContainsPath(roots, fullPath))
                {
                    roots.Add(fullPath);
                }
            }
            catch
            {
            }
        }

        private static string GetProjectDirectoryPath(CortexProjectDefinition project)
        {
            if (project == null || string.IsNullOrEmpty(project.ProjectFilePath))
            {
                return string.Empty;
            }

            try
            {
                return Path.GetDirectoryName(project.ProjectFilePath) ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static bool ContainsPath(IEnumerable<string> roots, string candidate)
        {
            if (roots == null || string.IsNullOrEmpty(candidate))
            {
                return false;
            }

            foreach (var root in roots)
            {
                if (string.Equals(root, candidate, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
