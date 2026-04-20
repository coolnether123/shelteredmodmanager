using System;
using System.Collections.Generic;
using ModAPI.Saves;

namespace ModAPI.Scenarios
{
    /// <summary>
    /// Converts scenario dependency declarations into the same manifest shape used by save verification.
    /// </summary>
    public static class ScenarioDependencyManifest
    {
        public static SlotManifest Create(string scenarioName, LoadedModInfo[] requiredMods)
        {
            return new SlotManifest
            {
                family_name = scenarioName ?? string.Empty,
                lastModified = DateTime.UtcNow.ToString("o"),
                lastLoadedMods = CloneRequiredMods(requiredMods)
            };
        }

        public static LoadedModInfo[] FromDependencyStrings(IList<string> dependencies)
        {
            if (dependencies == null || dependencies.Count == 0)
                return new LoadedModInfo[0];

            List<LoadedModInfo> result = new List<LoadedModInfo>();
            for (int i = 0; i < dependencies.Count; i++)
            {
                LoadedModInfo dependency = ParseDependency(dependencies[i]);
                if (dependency != null)
                    AddOrMerge(result, dependency);
            }

            return result.ToArray();
        }

        public static LoadedModInfo ParseDependency(string dependency)
        {
            string raw = TrimToNull(dependency);
            if (raw == null)
                return null;

            string modId = raw;
            string version = null;
            int separator = raw.IndexOf('@');
            if (separator < 0)
                separator = raw.IndexOf('|');

            if (separator > 0)
            {
                modId = raw.Substring(0, separator);
                version = raw.Substring(separator + 1);
            }

            modId = TrimToNull(modId);
            if (modId == null)
                return null;

            return new LoadedModInfo
            {
                modId = modId,
                version = TrimToNull(version),
                warnings = new string[0]
            };
        }

        public static LoadedModInfo[] Merge(LoadedModInfo[] first, LoadedModInfo[] second)
        {
            List<LoadedModInfo> merged = new List<LoadedModInfo>();
            AppendAll(merged, first);
            AppendAll(merged, second);
            return merged.ToArray();
        }

        public static LoadedModInfo[] CloneRequiredMods(LoadedModInfo[] requiredMods)
        {
            if (requiredMods == null || requiredMods.Length == 0)
                return new LoadedModInfo[0];

            List<LoadedModInfo> result = new List<LoadedModInfo>();
            AppendAll(result, requiredMods);
            return result.ToArray();
        }

        private static void AppendAll(List<LoadedModInfo> target, LoadedModInfo[] mods)
        {
            if (target == null || mods == null)
                return;

            for (int i = 0; i < mods.Length; i++)
            {
                LoadedModInfo normalized = Normalize(mods[i]);
                if (normalized != null)
                    AddOrMerge(target, normalized);
            }
        }

        private static LoadedModInfo Normalize(LoadedModInfo mod)
        {
            if (mod == null)
                return null;

            string modId = TrimToNull(mod.modId);
            if (modId == null)
                return null;

            return new LoadedModInfo
            {
                modId = modId,
                version = TrimToNull(mod.version),
                warnings = mod.warnings != null ? (string[])mod.warnings.Clone() : new string[0]
            };
        }

        private static void AddOrMerge(List<LoadedModInfo> target, LoadedModInfo dependency)
        {
            for (int i = 0; i < target.Count; i++)
            {
                if (!string.Equals(target[i].modId, dependency.modId, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (string.IsNullOrEmpty(target[i].version) && !string.IsNullOrEmpty(dependency.version))
                    target[i].version = dependency.version;
                return;
            }

            target.Add(dependency);
        }

        private static string TrimToNull(string value)
        {
            if (string.IsNullOrEmpty(value))
                return null;

            string trimmed = value.Trim();
            return trimmed.Length == 0 ? null : trimmed;
        }
    }
}
