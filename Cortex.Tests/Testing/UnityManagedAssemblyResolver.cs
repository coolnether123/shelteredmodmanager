using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Cortex.Tests.Testing
{
    internal static class UnityManagedAssemblyResolver
    {
        private const string UnityManagedDirEnvironmentVariable = "CORTEX_UNITY_MANAGED_DIR";
        private const string UnityEnginePathEnvironmentVariable = "CORTEX_UNITY_ENGINE_PATH";
        private static bool _registered;

        public static void Run(Action action)
        {
            EnsureRegistered();
            if (action != null)
            {
                action();
            }
        }

        public static void EnsureRegistered()
        {
            if (_registered)
            {
                return;
            }

            AppDomain.CurrentDomain.AssemblyResolve += ResolveManagedAssembly;
            _registered = true;
        }

        private static Assembly ResolveManagedAssembly(object sender, ResolveEventArgs args)
        {
            var requestedName = new AssemblyName(args.Name).Name;
            if (string.IsNullOrEmpty(requestedName))
            {
                return null;
            }

            var requestedFileName = requestedName + ".dll";
            foreach (var searchRoot in GetManagedAssemblySearchRoots())
            {
                var candidatePath = Path.Combine(searchRoot, requestedFileName);
                if (File.Exists(candidatePath))
                {
                    return Assembly.LoadFrom(candidatePath);
                }
            }

            return null;
        }

        internal static string[] GetManagedAssemblySearchRoots()
        {
            var roots = new List<string>();
            AddSearchRoot(roots, GetUnityManagedRootFromEnvironment());
            AddSearchRoot(roots, AppDomain.CurrentDomain.BaseDirectory);
            AddSearchRoot(roots, GetDirectoryNameSafe(typeof(Rect).Assembly.Location));
            return roots.ToArray();
        }

        private static string GetUnityManagedRootFromEnvironment()
        {
            var explicitUnityEnginePath = Environment.GetEnvironmentVariable(UnityEnginePathEnvironmentVariable);
            if (!string.IsNullOrEmpty(explicitUnityEnginePath))
            {
                return GetDirectoryNameSafe(explicitUnityEnginePath);
            }

            return Environment.GetEnvironmentVariable(UnityManagedDirEnvironmentVariable) ?? string.Empty;
        }

        private static string GetDirectoryNameSafe(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return string.Empty;
            }

            try
            {
                return Path.GetDirectoryName(Path.GetFullPath(path)) ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static void AddSearchRoot(ICollection<string> roots, string path)
        {
            if (roots == null || string.IsNullOrEmpty(path))
            {
                return;
            }

            string normalizedPath;
            try
            {
                normalizedPath = Path.GetFullPath(path);
            }
            catch
            {
                return;
            }

            if (!Directory.Exists(normalizedPath))
            {
                return;
            }

            if (roots.Any(root => string.Equals(root, normalizedPath, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            roots.Add(normalizedPath);
        }
    }
}
