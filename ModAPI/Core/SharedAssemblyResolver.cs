using System;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace ModAPI.Core
{
    internal static class SharedAssemblyResolver
    {
        private static readonly string[] SharedRuntimeAssemblyNames = new[]
        {
            "ModAPI",
            "ModAPI.Core",
            "ShelteredAPI",
            "0Harmony",
            "Cortex",
            "Cortex.Host.Unity",
            "Cortex.Platform.ModAPI"
        };

        internal static bool IsSharedRuntimeAssemblyName(string simpleName)
        {
            if (string.IsNullOrEmpty(simpleName))
                return false;

            for (int i = 0; i < SharedRuntimeAssemblyNames.Length; i++)
            {
                if (string.Equals(SharedRuntimeAssemblyNames[i], simpleName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        internal static bool ShouldSkipModAssembly(string assemblyPath)
        {
            if (string.IsNullOrEmpty(assemblyPath))
                return false;

            string simpleName = null;
            try { simpleName = Path.GetFileNameWithoutExtension(assemblyPath); }
            catch { return false; }

            return IsSharedRuntimeAssemblyName(simpleName);
        }

        internal static Assembly ResolveSharedAssembly(string simpleName)
        {
            if (string.IsNullOrEmpty(simpleName))
                return null;

            if (string.Equals(simpleName, "ModAPI", StringComparison.OrdinalIgnoreCase)
                || string.Equals(simpleName, "ModAPI.Core", StringComparison.OrdinalIgnoreCase))
            {
                return Assembly.GetExecutingAssembly();
            }

            string preferredPath = GetCanonicalAssemblyPath(simpleName);
            var loaded = FindLoadedAssembly(simpleName, preferredPath);
            if (loaded != null)
                return loaded;

            if (!string.IsNullOrEmpty(preferredPath) && File.Exists(preferredPath))
            {
                try
                {
                    var assembly = Assembly.LoadFrom(preferredPath);
                    MMLog.WriteInfo("[SharedAssemblyResolver] Loaded " + simpleName + " from shared runtime path: " + preferredPath);
                    return assembly;
                }
                catch (Exception ex)
                {
                    MMLog.WriteWarning("[SharedAssemblyResolver] Failed to load " + simpleName + " from '" + preferredPath + "': " + ex.Message);
                }
            }

            return FindLoadedAssembly(simpleName, null);
        }

        internal static string GetCanonicalAssemblyPath(string simpleName)
        {
            if (string.IsNullOrEmpty(simpleName))
                return null;

            try
            {
                string gameRoot = Directory.GetParent(Application.dataPath).FullName;
                string smmDir = Path.Combine(gameRoot, "SMM");
                string[] candidates = new[]
                {
                    Path.Combine(Path.Combine(smmDir, "bin"), simpleName + ".dll"),
                    Path.Combine(Path.Combine(Path.Combine(smmDir, "bin"), "decompiler"), simpleName + ".dll"),
                    Path.Combine(smmDir, simpleName + ".dll")
                };

                for (int i = 0; i < candidates.Length; i++)
                {
                    if (File.Exists(candidates[i]))
                        return candidates[i];
                }
            }
            catch { }

            return null;
        }

        private static Assembly FindLoadedAssembly(string simpleName, string preferredPath)
        {
            Assembly fallback = null;
            bool requirePreferredPath = !string.IsNullOrEmpty(preferredPath);

            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                var assembly = assemblies[i];
                if (assembly == null)
                    continue;

                string loadedName = null;
                try { loadedName = assembly.GetName().Name; }
                catch { continue; }

                if (!string.Equals(loadedName, simpleName, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (requirePreferredPath && PathsEqual(SafeLocation(assembly), preferredPath))
                    return assembly;

                if (fallback == null)
                    fallback = assembly;
            }

            return requirePreferredPath ? null : fallback;
        }

        private static string SafeLocation(Assembly assembly)
        {
            try { return assembly != null ? assembly.Location : null; }
            catch { return null; }
        }

        private static bool PathsEqual(string left, string right)
        {
            if (string.IsNullOrEmpty(left) || string.IsNullOrEmpty(right))
                return false;

            try
            {
                return string.Equals(
                    Path.GetFullPath(left),
                    Path.GetFullPath(right),
                    StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}
