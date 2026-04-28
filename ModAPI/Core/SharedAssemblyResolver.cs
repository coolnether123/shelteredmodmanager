using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace ModAPI.Core
{
    internal static class SharedAssemblyResolver
    {
        private static readonly string[] AlwaysSharedRuntimeAssemblyNames = new[]
        {
            "ModAPI",
            "ModAPI.Core",
            "0Harmony"
        };

        internal static bool IsSharedRuntimeAssemblyName(string simpleName)
        {
            if (string.IsNullOrEmpty(simpleName))
                return false;

            for (int i = 0; i < AlwaysSharedRuntimeAssemblyNames.Length; i++)
            {
                if (string.Equals(AlwaysSharedRuntimeAssemblyNames[i], simpleName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return GetCanonicalAssemblyPath(simpleName) != null;
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

        internal static Assembly[] LoadAvailableSharedRuntimeAssemblies()
        {
            var result = new List<Assembly>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string[] directories = GetSharedRuntimeDirectories();

            for (int i = 0; i < directories.Length; i++)
            {
                string directory = directories[i];
                if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
                    continue;

                string[] files;
                try { files = Directory.GetFiles(directory, "*.dll", SearchOption.TopDirectoryOnly); }
                catch { continue; }

                for (int j = 0; j < files.Length; j++)
                {
                    string simpleName = null;
                    try { simpleName = Path.GetFileNameWithoutExtension(files[j]); }
                    catch { continue; }

                    if (string.IsNullOrEmpty(simpleName) || !seen.Add(simpleName))
                        continue;

                    if (string.Equals(simpleName, "ModAPI", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(simpleName, "ModAPI.Core", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    Assembly assembly = ResolveSharedAssembly(simpleName);
                    if (assembly != null)
                        result.Add(assembly);
                }
            }

            return result.ToArray();
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
                string[] directories = GetSharedRuntimeDirectories();
                var candidates = new List<string>();
                for (int i = 0; i < directories.Length; i++)
                {
                    if (!string.IsNullOrEmpty(directories[i]))
                        candidates.Add(Path.Combine(directories[i], simpleName + ".dll"));
                }

                for (int i = 0; i < candidates.Count; i++)
                {
                    if (File.Exists(candidates[i]))
                        return candidates[i];
                }
            }
            catch { }

            return null;
        }

        private static string[] GetSharedRuntimeDirectories()
        {
            try
            {
                string gameRoot = Directory.GetParent(Application.dataPath).FullName;
                string smmDir = Path.Combine(gameRoot, "SMM");
                return new[]
                {
                    Path.Combine(smmDir, "bin"),
                    smmDir
                };
            }
            catch
            {
                return new string[0];
            }
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
