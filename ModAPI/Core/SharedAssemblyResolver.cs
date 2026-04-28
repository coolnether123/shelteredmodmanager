using System;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace ModAPI.Core
{
    internal static class SharedAssemblyResolver
    {
        private const string DefaultCortexBundleProfileId = "unity-hosted";

        private static readonly string[] SharedRuntimeAssemblyNames = new[]
        {
            "ModAPI",
            "ModAPI.Core",
            "ShelteredAPI",
            "0Harmony",
            "GameModding.Shared"
        };

        internal static bool IsSharedRuntimeAssemblyName(string simpleName)
        {
            if (string.IsNullOrEmpty(simpleName))
                return false;

            if (IsCortexAssemblyName(simpleName))
                return true;

            for (int i = 0; i < SharedRuntimeAssemblyNames.Length; i++)
            {
                if (string.Equals(SharedRuntimeAssemblyNames[i], simpleName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        internal static bool IsCortexAssemblyName(string simpleName)
        {
            return !string.IsNullOrEmpty(simpleName) &&
                (string.Equals(simpleName, "Cortex", StringComparison.OrdinalIgnoreCase) ||
                 simpleName.StartsWith("Cortex.", StringComparison.OrdinalIgnoreCase));
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

        internal static string GetSmmRootPath()
        {
            try
            {
                var dataPath = Application.dataPath;
                if (!string.IsNullOrEmpty(dataPath))
                {
                    var gameRoot = Directory.GetParent(dataPath);
                    if (gameRoot != null)
                    {
                        return Path.Combine(gameRoot.FullName, "SMM");
                    }
                }
            }
            catch
            {
            }

            try
            {
                var assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                if (string.IsNullOrEmpty(assemblyDir))
                    return string.Empty;

                if (string.Equals(Path.GetFileName(assemblyDir), "bin", StringComparison.OrdinalIgnoreCase))
                {
                    var parent = Directory.GetParent(assemblyDir);
                    return parent != null ? parent.FullName : assemblyDir;
                }

                return assemblyDir;
            }
            catch
            {
                return string.Empty;
            }
        }

        internal static string GetCortexRootPath()
        {
            var smmRoot = GetSmmRootPath();
            return string.IsNullOrEmpty(smmRoot) ? string.Empty : Path.Combine(smmRoot, "Cortex");
        }

        internal static string GetCortexBundleRootPath()
        {
            var cortexRoot = GetCortexRootPath();
            return string.IsNullOrEmpty(cortexRoot)
                ? string.Empty
                : Path.Combine(cortexRoot, DefaultCortexBundleProfileId);
        }

        internal static string GetCortexRuntimePath()
        {
            var bundleRoot = GetCortexBundleRootPath();
            var hostRuntimeRoot = string.IsNullOrEmpty(bundleRoot)
                ? string.Empty
                : Path.Combine(Path.Combine(bundleRoot, "host"), "lib");
            if (Directory.Exists(hostRuntimeRoot))
                return hostRuntimeRoot;

            var cortexRoot = GetCortexRootPath();
            return string.IsNullOrEmpty(cortexRoot) ? string.Empty : Path.Combine(cortexRoot, "runtime");
        }

        internal static string GetCortexPortableRuntimePath()
        {
            var bundleRoot = GetCortexBundleRootPath();
            if (string.IsNullOrEmpty(bundleRoot))
                return string.Empty;

            return Path.Combine(Path.Combine(bundleRoot, "portable"), "lib");
        }

        internal static string GetCortexToolRootPath()
        {
            var bundleRoot = GetCortexBundleRootPath();
            var toolingRoot = string.IsNullOrEmpty(bundleRoot) ? string.Empty : Path.Combine(bundleRoot, "tooling");
            if (Directory.Exists(toolingRoot))
                return toolingRoot;

            var cortexRoot = GetCortexRootPath();
            return string.IsNullOrEmpty(cortexRoot) ? string.Empty : Path.Combine(cortexRoot, "tools");
        }

        internal static string GetCortexPluginRootPath()
        {
            var bundleRoot = GetCortexBundleRootPath();
            var pluginRoot = string.IsNullOrEmpty(bundleRoot) ? string.Empty : Path.Combine(bundleRoot, "plugins");
            if (Directory.Exists(pluginRoot))
                return pluginRoot;

            var cortexRoot = GetCortexRootPath();
            return string.IsNullOrEmpty(cortexRoot) ? string.Empty : Path.Combine(cortexRoot, "plugins");
        }

        internal static string GetCortexManifestPath()
        {
            var bundleRoot = GetCortexBundleRootPath();
            return string.IsNullOrEmpty(bundleRoot)
                ? string.Empty
                : Path.Combine(Path.Combine(bundleRoot, "manifest"), "cortex.bundle.manifest.json");
        }

        internal static string GetCortexToolPath(string componentId, string fileName)
        {
            var toolRoot = GetCortexToolRootPath();
            if (string.IsNullOrEmpty(toolRoot) || string.IsNullOrEmpty(componentId) || string.IsNullOrEmpty(fileName))
                return string.Empty;

            var preferredPath = Path.Combine(Path.Combine(toolRoot, componentId), fileName);
            if (File.Exists(preferredPath))
                return preferredPath;

            var cortexRoot = GetCortexRootPath();
            if (!string.IsNullOrEmpty(cortexRoot))
            {
                var legacyPath = Path.Combine(Path.Combine(Path.Combine(cortexRoot, "tools"), componentId), fileName);
                if (File.Exists(legacyPath))
                    return legacyPath;
            }

            return preferredPath;
        }

        internal static string GetCanonicalAssemblyPath(string simpleName)
        {
            if (string.IsNullOrEmpty(simpleName))
                return null;

            var candidates = GetCanonicalAssemblyCandidates(simpleName);
            for (int i = 0; i < candidates.Length; i++)
            {
                if (!string.IsNullOrEmpty(candidates[i]) && File.Exists(candidates[i]))
                    return candidates[i];
            }

            return null;
        }

        private static string[] GetCanonicalAssemblyCandidates(string simpleName)
        {
            var smmRoot = GetSmmRootPath();
            var smmBin = string.IsNullOrEmpty(smmRoot) ? string.Empty : Path.Combine(smmRoot, "bin");
            var cortexRuntime = GetCortexRuntimePath();
            var cortexPortableRuntime = GetCortexPortableRuntimePath();
            var fileName = simpleName + ".dll";

            if (IsCortexAssemblyName(simpleName))
            {
                return new[]
                {
                    Path.Combine(cortexRuntime, fileName),
                    Path.Combine(cortexPortableRuntime, fileName),
                    Path.Combine(Path.Combine(GetCortexRootPath(), "runtime"), fileName),
                    Path.Combine(smmBin, fileName),
                    Path.Combine(smmRoot, fileName)
                };
            }

            if (string.Equals(simpleName, "GameModding.Shared", StringComparison.OrdinalIgnoreCase))
            {
                return new[]
                {
                    Path.Combine(smmBin, fileName),
                    Path.Combine(cortexRuntime, fileName),
                    Path.Combine(cortexPortableRuntime, fileName)
                };
            }

            return new[]
            {
                Path.Combine(smmBin, fileName),
                Path.Combine(smmRoot, fileName)
            };
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
