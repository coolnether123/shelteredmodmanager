using System;
using System.Collections.Generic;

namespace ModAPI.Scenarios
{
    /// <summary>
    /// Neutral mod dependency declaration used by custom scenario registration.
    /// </summary>
    [Serializable]
    public sealed class ScenarioModDependency
    {
        public string modId;
        public string version;
        public string[] warnings;
    }

    /// <summary>
    /// Neutral dependency manifest produced by ModAPI scenario contracts.
    /// Game-specific assemblies translate it into their runtime save/selection format.
    /// </summary>
    [Serializable]
    public sealed class ScenarioDependencyManifestData
    {
        public string name;
        public string lastModified;
        public ScenarioModDependency[] requiredMods;
    }

    /// <summary>
    /// Converts scenario dependency declarations into a game-neutral manifest shape.
    /// </summary>
    public static class ScenarioDependencyManifest
    {
        public static ScenarioDependencyManifestData Create(string scenarioName, ScenarioModDependency[] requiredMods)
        {
            return new ScenarioDependencyManifestData
            {
                name = scenarioName ?? string.Empty,
                lastModified = DateTime.UtcNow.ToString("o"),
                requiredMods = CloneRequiredMods(requiredMods)
            };
        }

        public static ScenarioModDependency[] FromDependencyStrings(IList<string> dependencies)
        {
            if (dependencies == null || dependencies.Count == 0)
                return new ScenarioModDependency[0];

            List<ScenarioModDependency> result = new List<ScenarioModDependency>();
            for (int i = 0; i < dependencies.Count; i++)
            {
                ScenarioModDependency dependency = ParseDependency(dependencies[i]);
                if (dependency != null)
                    AddOrMerge(result, dependency);
            }

            return result.ToArray();
        }

        public static ScenarioModDependency ParseDependency(string dependency)
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

            return new ScenarioModDependency
            {
                modId = modId,
                version = TrimToNull(version),
                warnings = new string[0]
            };
        }

        public static ScenarioModDependency[] Merge(ScenarioModDependency[] first, ScenarioModDependency[] second)
        {
            List<ScenarioModDependency> merged = new List<ScenarioModDependency>();
            AppendAll(merged, first);
            AppendAll(merged, second);
            return merged.ToArray();
        }

        public static ScenarioModDependency[] CloneRequiredMods(ScenarioModDependency[] requiredMods)
        {
            if (requiredMods == null || requiredMods.Length == 0)
                return new ScenarioModDependency[0];

            List<ScenarioModDependency> result = new List<ScenarioModDependency>();
            AppendAll(result, requiredMods);
            return result.ToArray();
        }

        private static void AppendAll(List<ScenarioModDependency> target, ScenarioModDependency[] mods)
        {
            if (target == null || mods == null)
                return;

            for (int i = 0; i < mods.Length; i++)
            {
                ScenarioModDependency normalized = Normalize(mods[i]);
                if (normalized != null)
                    AddOrMerge(target, normalized);
            }
        }

        private static ScenarioModDependency Normalize(ScenarioModDependency mod)
        {
            if (mod == null)
                return null;

            string modId = TrimToNull(mod.modId);
            if (modId == null)
                return null;

            return new ScenarioModDependency
            {
                modId = modId,
                version = TrimToNull(mod.version),
                warnings = mod.warnings != null ? (string[])mod.warnings.Clone() : new string[0]
            };
        }

        private static void AddOrMerge(List<ScenarioModDependency> target, ScenarioModDependency dependency)
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
