using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using ModAPI.Core;

namespace ShelteredAPI.Scenarios.Application.Authoring
{
    // Both publishing and local installation must use the same loaded-mod root:
    // the root that ScenarioCatalog enumerates through ModRegistry.
    internal static class ScenarioPackageModRootResolver
    {
        public static string ResolveLoadedOwnerRoot(Assembly ownerAssembly)
        {
            ModEntry owner;
            if (ownerAssembly != null
                && ModRegistry.TryGetModByAssembly(ownerAssembly, out owner)
                && owner != null
                && !string.IsNullOrEmpty(owner.RootPath))
            {
                return owner.RootPath;
            }

            List<ModEntry> loaded = ModRegistry.GetLoadedMods();
            for (int i = 0; loaded != null && i < loaded.Count; i++)
            {
                ModEntry candidate = loaded[i];
                if (candidate != null && !string.IsNullOrEmpty(candidate.RootPath)
                    && IsCatalogRoot(candidate.RootPath, loaded))
                {
                    return candidate.RootPath;
                }
            }

            throw new InvalidOperationException("No loaded mod root is available for local scenario packages.");
        }

        public static string ResolveScenariosRoot(Assembly ownerAssembly)
        {
            return Path.Combine(ResolveLoadedOwnerRoot(ownerAssembly), "Scenarios");
        }

        private static bool IsCatalogRoot(string rootPath, List<ModEntry> loaded)
        {
            for (int i = 0; loaded != null && i < loaded.Count; i++)
            {
                ModEntry candidate = loaded[i];
                if (candidate != null && string.Equals(candidate.RootPath, rootPath, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }
    }
}
