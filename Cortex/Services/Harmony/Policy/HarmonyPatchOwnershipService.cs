using System;
using System.IO;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;

namespace Cortex.Services.Harmony.Policy
{
    internal sealed class HarmonyPatchOwnershipService
    {
        public HarmonyPatchOwnerAssociation Resolve(string ownerId, string patchAssemblyPath, ILoadedModCatalog loadedModCatalog, IProjectCatalog projectCatalog)
        {
            var association = new HarmonyPatchOwnerAssociation
            {
                OwnerId = ownerId ?? string.Empty,
                DisplayName = ownerId ?? string.Empty,
                AssemblyPath = patchAssemblyPath ?? string.Empty
            };

            var modMatch = ResolveLoadedMod(ownerId, patchAssemblyPath, loadedModCatalog);
            if (modMatch != null)
            {
                association.LoadedModId = modMatch.ModId ?? string.Empty;
                association.LoadedModRootPath = modMatch.RootPath ?? string.Empty;
                if (string.IsNullOrEmpty(association.DisplayName) || string.Equals(association.DisplayName, ownerId ?? string.Empty, StringComparison.Ordinal))
                {
                    association.DisplayName = !string.IsNullOrEmpty(modMatch.DisplayName)
                        ? modMatch.DisplayName
                        : modMatch.ModId ?? string.Empty;
                }

                association.MatchReason = string.IsNullOrEmpty(ownerId)
                    ? "Matched patch assembly to loaded mod."
                    : "Matched Harmony owner to loaded mod.";
            }

            var projectMatch = ResolveProject(ownerId, patchAssemblyPath, projectCatalog, association.LoadedModId);
            if (projectMatch != null)
            {
                association.ProjectModId = projectMatch.ModId ?? string.Empty;
                association.ProjectSourceRootPath = projectMatch.SourceRootPath ?? string.Empty;
                if (string.IsNullOrEmpty(association.DisplayName) || string.Equals(association.DisplayName, ownerId ?? string.Empty, StringComparison.Ordinal))
                {
                    association.DisplayName = projectMatch.GetDisplayName();
                }

                if (string.IsNullOrEmpty(association.MatchReason))
                {
                    association.MatchReason = string.IsNullOrEmpty(ownerId)
                        ? "Matched patch assembly to project output."
                        : "Matched Harmony owner to project.";
                }
            }

            association.HasMatch =
                !string.IsNullOrEmpty(association.LoadedModId) ||
                !string.IsNullOrEmpty(association.ProjectModId);
            if (string.IsNullOrEmpty(association.DisplayName))
            {
                association.DisplayName = !string.IsNullOrEmpty(ownerId)
                    ? ownerId
                    : (!string.IsNullOrEmpty(patchAssemblyPath) ? Path.GetFileNameWithoutExtension(patchAssemblyPath) ?? string.Empty : string.Empty);
            }

            if (!association.HasMatch && string.IsNullOrEmpty(association.MatchReason))
            {
                association.MatchReason = "No mod or project association could be resolved.";
            }

            return association;
        }

        public CortexProjectDefinition ResolveProjectForAssembly(string assemblyPath, IProjectCatalog projectCatalog)
        {
            return ResolveProject(string.Empty, assemblyPath, projectCatalog, string.Empty);
        }

        public LoadedModInfo ResolveLoadedModForAssembly(string assemblyPath, ILoadedModCatalog loadedModCatalog)
        {
            return ResolveLoadedMod(string.Empty, assemblyPath, loadedModCatalog);
        }

        private static LoadedModInfo ResolveLoadedMod(string ownerId, string patchAssemblyPath, ILoadedModCatalog loadedModCatalog)
        {
            if (loadedModCatalog == null)
            {
                return null;
            }

            if (!string.IsNullOrEmpty(ownerId))
            {
                var direct = loadedModCatalog.GetMod(ownerId);
                if (direct != null)
                {
                    return direct;
                }
            }

            var mods = loadedModCatalog.GetLoadedMods();
            for (var i = 0; i < mods.Count; i++)
            {
                var mod = mods[i];
                if (mod == null)
                {
                    continue;
                }

                if (IsMatch(ownerId, mod.ModId) ||
                    IsMatch(ownerId, mod.DisplayName) ||
                    PathStartsWith(patchAssemblyPath, mod.RootPath))
                {
                    return mod;
                }
            }

            return null;
        }

        private static CortexProjectDefinition ResolveProject(string ownerId, string patchAssemblyPath, IProjectCatalog projectCatalog, string preferredModId)
        {
            if (projectCatalog == null)
            {
                return null;
            }

            if (!string.IsNullOrEmpty(preferredModId))
            {
                var direct = projectCatalog.GetProject(preferredModId);
                if (direct != null)
                {
                    return direct;
                }
            }

            if (!string.IsNullOrEmpty(ownerId))
            {
                var directOwner = projectCatalog.GetProject(ownerId);
                if (directOwner != null)
                {
                    return directOwner;
                }
            }

            var projects = projectCatalog.GetProjects();
            for (var i = 0; i < projects.Count; i++)
            {
                var project = projects[i];
                if (project == null)
                {
                    continue;
                }

                if (IsMatch(ownerId, project.ModId) ||
                    IsMatch(ownerId, project.GetDisplayName()) ||
                    IsMatch(patchAssemblyPath, project.OutputAssemblyPath))
                {
                    return project;
                }
            }

            return null;
        }

        private static bool IsMatch(string ownerId, string candidate)
        {
            if (string.IsNullOrEmpty(ownerId) || string.IsNullOrEmpty(candidate))
            {
                return false;
            }

            if (string.Equals(ownerId, candidate, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return ownerId.IndexOf(candidate, StringComparison.OrdinalIgnoreCase) >= 0 ||
                candidate.IndexOf(ownerId, StringComparison.OrdinalIgnoreCase) >= 0;
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
    }
}
