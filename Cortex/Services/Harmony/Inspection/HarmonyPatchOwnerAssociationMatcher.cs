using System;
using Cortex.Core.Models;

namespace Cortex.Services.Harmony.Inspection
{
    internal static class HarmonyPatchOwnerAssociationMatcher
    {
        public static bool SummaryMatchesSelectedProject(HarmonyMethodPatchSummary summary, CortexProjectDefinition selectedProject)
        {
            if (summary == null)
            {
                return false;
            }

            var entries = summary.Entries ?? new HarmonyPatchEntry[0];
            for (var i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                if (entry != null && MatchesSelectedProject(entry.OwnerAssociation, selectedProject))
                {
                    return true;
                }
            }

            return false;
        }

        public static HarmonyPatchNavigationTarget GetPreferredPatchNavigationTarget(HarmonyMethodPatchSummary summary, CortexProjectDefinition selectedProject)
        {
            if (summary == null)
            {
                return null;
            }

            var entries = summary.Entries ?? new HarmonyPatchEntry[0];
            if (selectedProject != null)
            {
                for (var i = 0; i < entries.Length; i++)
                {
                    var entry = entries[i];
                    if (entry == null ||
                        entry.NavigationTarget == null ||
                        !MatchesSelectedProject(entry.OwnerAssociation, selectedProject))
                    {
                        continue;
                    }

                    return entry.NavigationTarget;
                }
            }

            for (var i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                if (entry != null && entry.NavigationTarget != null)
                {
                    return entry.NavigationTarget;
                }
            }

            return null;
        }

        public static bool MatchesSelectedProject(HarmonyPatchOwnerAssociation association, CortexProjectDefinition selectedProject)
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

        private static bool MatchesValue(string left, string right)
        {
            return !string.IsNullOrEmpty(left) &&
                !string.IsNullOrEmpty(right) &&
                string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizePath(string path)
        {
            return string.IsNullOrEmpty(path)
                ? string.Empty
                : path.TrimEnd('\\', '/');
        }
    }
}
