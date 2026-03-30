using System;
using System.IO;
using Cortex.Core.Models;
using Cortex.Plugins.Abstractions;

namespace Cortex.Plugin.Harmony
{
    internal sealed partial class HarmonyWorkflowController
    {
        private static bool ShouldIncludeSummary(HarmonyMethodPatchSummary summary, ExplorerFilterRuntimeContext context)
        {
            if (summary == null)
            {
                return false;
            }

            if (context == null || !context.RestrictToSelectedProject || context.SelectedProject == null)
            {
                return true;
            }

            return SummaryMatchesSelectedProject(summary, context.SelectedProject);
        }

        private static bool SummaryMatchesSelectedProject(HarmonyMethodPatchSummary summary, CortexProjectDefinition selectedProject)
        {
            if (summary == null || selectedProject == null)
            {
                return false;
            }

            var entries = summary.Entries ?? new HarmonyPatchEntry[0];
            for (var i = 0; i < entries.Length; i++)
            {
                if (MatchesSelectedProject(entries[i] != null ? entries[i].OwnerAssociation : null, selectedProject))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool MatchesSelectedProject(HarmonyPatchOwnerAssociation association, CortexProjectDefinition selectedProject)
        {
            if (association == null || selectedProject == null)
            {
                return false;
            }

            var selectedModId = selectedProject.ModId ?? string.Empty;
            var selectedSourceRoot = NormalizePath(selectedProject.SourceRootPath);
            return MatchesValue(association.ProjectModId, selectedModId) ||
                MatchesValue(association.LoadedModId, selectedModId) ||
                MatchesValue(NormalizePath(association.ProjectSourceRootPath), selectedSourceRoot);
        }

        private static HarmonyPatchOwnerAssociation ResolveOwnerAssociation(IWorkbenchModuleRuntime runtime, HarmonyPatchEntry entry)
        {
            if (entry == null)
            {
                return null;
            }

            var patchAssemblyPath = !string.IsNullOrEmpty(entry.AssemblyPath)
                ? entry.AssemblyPath
                : (entry.NavigationTarget != null ? entry.NavigationTarget.AssemblyPath ?? string.Empty : string.Empty);
            var association = new HarmonyPatchOwnerAssociation
            {
                OwnerId = entry.OwnerId ?? string.Empty,
                DisplayName = !string.IsNullOrEmpty(entry.OwnerDisplayName) ? entry.OwnerDisplayName : entry.OwnerId ?? string.Empty,
                AssemblyPath = patchAssemblyPath ?? string.Empty
            };

            var loadedMod = ResolveLoadedMod(runtime, entry.OwnerId, patchAssemblyPath);
            if (loadedMod != null)
            {
                association.LoadedModId = loadedMod.ModId ?? string.Empty;
                association.LoadedModRootPath = loadedMod.RootPath ?? string.Empty;
                if (string.IsNullOrEmpty(association.DisplayName) ||
                    string.Equals(association.DisplayName, entry.OwnerId ?? string.Empty, StringComparison.OrdinalIgnoreCase))
                {
                    association.DisplayName = !string.IsNullOrEmpty(loadedMod.DisplayName)
                        ? loadedMod.DisplayName
                        : loadedMod.ModId ?? string.Empty;
                }
            }

            var project = ResolveProject(runtime, entry.OwnerId, patchAssemblyPath, association.LoadedModId);
            if (project != null)
            {
                association.ProjectModId = project.ModId ?? string.Empty;
                association.ProjectSourceRootPath = project.SourceRootPath ?? string.Empty;
                if (string.IsNullOrEmpty(association.DisplayName) ||
                    string.Equals(association.DisplayName, entry.OwnerId ?? string.Empty, StringComparison.OrdinalIgnoreCase))
                {
                    association.DisplayName = project.GetDisplayName();
                }
            }

            association.HasMatch = !string.IsNullOrEmpty(association.LoadedModId) || !string.IsNullOrEmpty(association.ProjectModId);
            return association;
        }

        private static bool IsDecompilerPath(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                return false;
            }

            try
            {
                var fullPath = Path.GetFullPath(filePath);
                return fullPath.IndexOf(Path.DirectorySeparatorChar + "cortex_cache" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    fullPath.IndexOf(Path.AltDirectorySeparatorChar + "cortex_cache" + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) >= 0;
            }
            catch
            {
                return false;
            }
        }

        private static bool PathStartsWith(string path, string rootPath)
        {
            if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(rootPath))
            {
                return false;
            }

            try
            {
                var normalizedPath = Path.GetFullPath(path);
                var normalizedRoot = Path.GetFullPath(rootPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
                return normalizedPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private static bool PathsEqual(string left, string right)
        {
            if (string.IsNullOrEmpty(left) || string.IsNullOrEmpty(right))
            {
                return false;
            }

            try
            {
                return string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private static LoadedModInfo ResolveLoadedMod(IWorkbenchModuleRuntime runtime, string ownerId, string patchAssemblyPath)
        {
            var mods = runtime != null && runtime.Projects != null ? runtime.Projects.GetLoadedMods() : null;
            if (mods == null)
            {
                return null;
            }

            for (var i = 0; i < mods.Count; i++)
            {
                var mod = mods[i];
                if (mod == null)
                {
                    continue;
                }

                if (MatchesValue(ownerId, mod.ModId) ||
                    MatchesValue(ownerId, mod.DisplayName) ||
                    PathStartsWith(patchAssemblyPath, mod.RootPath))
                {
                    return mod;
                }
            }

            return null;
        }

        private static CortexProjectDefinition ResolveProject(IWorkbenchModuleRuntime runtime, string ownerId, string patchAssemblyPath, string preferredModId)
        {
            var projects = runtime != null && runtime.Projects != null ? runtime.Projects.GetProjects() : null;
            if (projects == null)
            {
                return null;
            }

            if (!string.IsNullOrEmpty(preferredModId))
            {
                for (var i = 0; i < projects.Count; i++)
                {
                    var preferred = projects[i];
                    if (preferred != null && MatchesValue(preferred.ModId, preferredModId))
                    {
                        return preferred;
                    }
                }
            }

            for (var i = 0; i < projects.Count; i++)
            {
                var project = projects[i];
                if (project == null)
                {
                    continue;
                }

                if (MatchesValue(ownerId, project.ModId) ||
                    MatchesValue(ownerId, project.GetDisplayName()) ||
                    PathsEqual(patchAssemblyPath, project.OutputAssemblyPath))
                {
                    return project;
                }
            }

            return null;
        }

        private static bool MatchesValue(string left, string right)
        {
            if (string.IsNullOrEmpty(left) || string.IsNullOrEmpty(right))
            {
                return false;
            }

            return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizePath(string path)
        {
            return string.IsNullOrEmpty(path)
                ? string.Empty
                : path.TrimEnd('\\', '/');
        }
    }
}
