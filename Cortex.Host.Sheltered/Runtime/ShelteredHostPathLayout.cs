using System;
using System.Collections.Generic;
using System.IO;

namespace Cortex.Host.Sheltered.Runtime
{
    /// <summary>
    /// Centralized Sheltered host path model shared by Unity host services and loader adapters.
    /// Keeps SMM/Sheltered layout assumptions inside the dedicated host layer.
    /// </summary>
    public sealed class ShelteredHostPathLayout
    {
        public const string DefaultBundleProfileId = "unity-hosted";
        public const string ExampleApplicationRootPath = @"D:\Games\Sheltered";
        public const string ExampleWorkspaceRootPath = @"D:\Projects\MyWorkspace";

        private readonly string _applicationRootPath;
        private readonly string _hostRootPath;
        private readonly string _legacyHostBinPath;
        private readonly string _cortexRootPath;
        private readonly string _bundleRootPath;
        private readonly string _portableRuntimeRootPath;
        private readonly string _hostBinPath;
        private readonly string _bundledPluginSearchRoots;
        private readonly string _configuredPluginSearchRoots;
        private readonly string _bundledToolRootPath;
        private readonly string _referenceAssemblyRootPath;
        private readonly string _runtimeContentRootPath;
        private readonly string _settingsFilePath;
        private readonly string _workbenchPersistenceFilePath;
        private readonly string _logFilePath;
        private readonly string _projectCatalogPath;
        private readonly string _decompilerCachePath;
        private readonly string _modManagerIniPath;

        private ShelteredHostPathLayout(string applicationRootPath)
        {
            _applicationRootPath = NormalizePath(applicationRootPath);
            _hostRootPath = CombinePath(_applicationRootPath, "SMM");
            _legacyHostBinPath = CombinePath(_hostRootPath, "bin");
            _cortexRootPath = CombinePath(_hostRootPath, "Cortex");
            _bundleRootPath = CombinePath(_cortexRootPath, DefaultBundleProfileId);
            _portableRuntimeRootPath = CombinePath(CombinePath(_bundleRootPath, "portable"), "lib");
            _hostBinPath = CombinePath(CombinePath(_bundleRootPath, "host"), "lib");
            _referenceAssemblyRootPath = CombinePath(CombinePath(_applicationRootPath, "Sheltered_Data"), "Managed");
            _runtimeContentRootPath = CombinePath(_hostRootPath, "mods");
            _bundledPluginSearchRoots = CombinePath(_bundleRootPath, "plugins");
            _configuredPluginSearchRoots = JoinRoots(_runtimeContentRootPath, CombinePath(_runtimeContentRootPath, "Plugins"));
            _bundledToolRootPath = CombinePath(_bundleRootPath, "tooling");
            _settingsFilePath = CombinePath(_legacyHostBinPath, "cortex_settings.json");
            _workbenchPersistenceFilePath = CombinePath(_legacyHostBinPath, "cortex_workbench.json");
            _logFilePath = CombinePath(_hostRootPath, "mod_manager.log");
            _projectCatalogPath = CombinePath(_legacyHostBinPath, "cortex_projects.json");
            _decompilerCachePath = CombinePath(_legacyHostBinPath, "cortex_cache");
            _modManagerIniPath = CombinePath(_legacyHostBinPath, "mod_manager.ini");
        }

        public string ApplicationRootPath
        {
            get { return _applicationRootPath; }
        }

        public string HostRootPath
        {
            get { return _hostRootPath; }
        }

        public string HostBinPath
        {
            get { return _hostBinPath; }
        }

        public string BundleRootPath
        {
            get { return _bundleRootPath; }
        }

        public string PortableRuntimeRootPath
        {
            get { return _portableRuntimeRootPath; }
        }

        public string BundledPluginSearchRoots
        {
            get { return _bundledPluginSearchRoots; }
        }

        public string ConfiguredPluginSearchRoots
        {
            get { return _configuredPluginSearchRoots; }
        }

        public string BundledToolRootPath
        {
            get { return _bundledToolRootPath; }
        }

        public string ReferenceAssemblyRootPath
        {
            get { return _referenceAssemblyRootPath; }
        }

        public string RuntimeContentRootPath
        {
            get { return _runtimeContentRootPath; }
        }

        public string SettingsFilePath
        {
            get { return _settingsFilePath; }
        }

        public string WorkbenchPersistenceFilePath
        {
            get { return _workbenchPersistenceFilePath; }
        }

        public string LogFilePath
        {
            get { return _logFilePath; }
        }

        public string ProjectCatalogPath
        {
            get { return _projectCatalogPath; }
        }

        public string DecompilerCachePath
        {
            get { return _decompilerCachePath; }
        }

        public string ModManagerIniPath
        {
            get { return _modManagerIniPath; }
        }

        public string GetBundledToolPath(string componentId, string fileName)
        {
            return CombinePath(CombinePath(_bundledToolRootPath, componentId), fileName);
        }

        public string GetLegacyDecompilerToolPath(string fileName)
        {
            return CombinePath(CombinePath(_bundledToolRootPath, "decompiler"), fileName);
        }

        public static ShelteredHostPathLayout FromApplicationRoot(string applicationRootPath)
        {
            return new ShelteredHostPathLayout(applicationRootPath);
        }

        public static ShelteredHostPathLayout FromUnityApplicationDataPath(string applicationDataPath)
        {
            var normalizedDataPath = NormalizePath(applicationDataPath);
            if (string.IsNullOrEmpty(normalizedDataPath))
            {
                return new ShelteredHostPathLayout(string.Empty);
            }

            var parent = Directory.GetParent(normalizedDataPath);
            return new ShelteredHostPathLayout(parent != null ? parent.FullName : string.Empty);
        }

        public static ShelteredHostPathLayout FromCurrentDirectory()
        {
            return new ShelteredHostPathLayout(Directory.GetCurrentDirectory());
        }

        public static ShelteredHostPathLayout CreateIllustrativeLayout()
        {
            return new ShelteredHostPathLayout(ExampleApplicationRootPath);
        }

        public static string BuildBundledToolPath(string hostBinPath, string componentId, string fileName)
        {
            var root = NormalizePath(hostBinPath);
            if (string.Equals(GetLeafName(root), "lib", StringComparison.OrdinalIgnoreCase))
            {
                var hostDirectory = Directory.GetParent(root);
                if (hostDirectory != null && string.Equals(GetLeafName(hostDirectory.FullName), "host", StringComparison.OrdinalIgnoreCase))
                {
                    var bundleRoot = Directory.GetParent(hostDirectory.FullName);
                    root = bundleRoot != null ? bundleRoot.FullName : root;
                    return CombinePath(CombinePath(CombinePath(root, "tooling"), componentId), fileName);
                }
            }

            if (string.Equals(GetLeafName(root), "runtime", StringComparison.OrdinalIgnoreCase))
            {
                var runtimeDirectory = Directory.GetParent(root);
                root = runtimeDirectory != null ? runtimeDirectory.FullName : root;
                return CombinePath(CombinePath(CombinePath(root, "tools"), componentId), fileName);
            }

            return CombinePath(CombinePath(CombinePath(root, "tooling"), componentId), fileName);
        }

        public static string BuildLegacyDecompilerToolPath(string hostBinPath, string fileName)
        {
            var root = NormalizePath(hostBinPath);
            if (string.Equals(GetLeafName(root), "lib", StringComparison.OrdinalIgnoreCase))
            {
                var hostDirectory = Directory.GetParent(root);
                if (hostDirectory != null && string.Equals(GetLeafName(hostDirectory.FullName), "host", StringComparison.OrdinalIgnoreCase))
                {
                    var bundleRoot = Directory.GetParent(hostDirectory.FullName);
                    root = bundleRoot != null ? bundleRoot.FullName : root;
                    return CombinePath(CombinePath(CombinePath(root, "tooling"), "decompiler"), fileName);
                }
            }

            return CombinePath(CombinePath(CombinePath(hostBinPath, "tools"), "decompiler"), fileName);
        }

        public static IList<string> EnumerateHostBinCandidates(string baseDirectory)
        {
            var candidates = new List<string>();
            var normalizedBaseDirectory = NormalizePath(baseDirectory);
            AddCandidate(candidates, normalizedBaseDirectory);

            if (string.IsNullOrEmpty(normalizedBaseDirectory))
            {
                return candidates;
            }

            var normalizedLeaf = GetLeafName(normalizedBaseDirectory);
            var parent = Directory.GetParent(normalizedBaseDirectory);

            if (string.Equals(normalizedLeaf, "decompiler", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalizedLeaf, "plugins", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalizedLeaf, "tools", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalizedLeaf, "tooling", StringComparison.OrdinalIgnoreCase))
            {
                AddCandidate(candidates, parent != null ? parent.FullName : string.Empty);
            }
            else if (parent != null &&
                (string.Equals(GetLeafName(parent.FullName), "tools", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(GetLeafName(parent.FullName), "tooling", StringComparison.OrdinalIgnoreCase)))
            {
                var grandParent = Directory.GetParent(parent.FullName);
                AddCandidate(candidates, grandParent != null ? grandParent.FullName : string.Empty);
            }
            else if (string.Equals(normalizedLeaf, "lib", StringComparison.OrdinalIgnoreCase) &&
                parent != null &&
                string.Equals(GetLeafName(parent.FullName), "host", StringComparison.OrdinalIgnoreCase))
            {
                AddCandidate(candidates, normalizedBaseDirectory);
            }

            if (!string.Equals(normalizedLeaf, "bin", StringComparison.OrdinalIgnoreCase))
            {
                AddCandidate(candidates, CombinePath(normalizedBaseDirectory, "bin"));
            }

            AddCandidate(candidates, CombinePath(CombinePath(CombinePath(CombinePath(normalizedBaseDirectory, "Cortex"), DefaultBundleProfileId), "host"), "lib"));
            AddCandidate(candidates, CombinePath(CombinePath(CombinePath(CombinePath(CombinePath(normalizedBaseDirectory, "SMM"), "Cortex"), DefaultBundleProfileId), "host"), "lib"));
            AddCandidate(candidates, CombinePath(CombinePath(normalizedBaseDirectory, "Cortex"), "runtime"));
            AddCandidate(candidates, CombinePath(CombinePath(CombinePath(normalizedBaseDirectory, "SMM"), "Cortex"), "runtime"));
            AddCandidate(candidates, CombinePath(CombinePath(normalizedBaseDirectory, "SMM"), "bin"));
            return candidates;
        }

        private static void AddCandidate(IList<string> candidates, string path)
        {
            var normalized = NormalizePath(path);
            if (string.IsNullOrEmpty(normalized))
            {
                return;
            }

            for (var i = 0; i < candidates.Count; i++)
            {
                if (string.Equals(candidates[i], normalized, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }

            candidates.Add(normalized);
        }

        private static string JoinRoots(params string[] roots)
        {
            var normalizedRoots = new List<string>();
            for (var i = 0; roots != null && i < roots.Length; i++)
            {
                var normalizedRoot = NormalizePath(roots[i]);
                if (string.IsNullOrEmpty(normalizedRoot))
                {
                    continue;
                }

                var duplicate = false;
                for (var j = 0; j < normalizedRoots.Count; j++)
                {
                    if (string.Equals(normalizedRoots[j], normalizedRoot, StringComparison.OrdinalIgnoreCase))
                    {
                        duplicate = true;
                        break;
                    }
                }

                if (!duplicate)
                {
                    normalizedRoots.Add(normalizedRoot);
                }
            }

            return normalizedRoots.Count > 0 ? string.Join(";", normalizedRoots.ToArray()) : string.Empty;
        }

        private static string CombinePath(string root, string child)
        {
            return string.IsNullOrEmpty(root) || string.IsNullOrEmpty(child)
                ? string.Empty
                : Path.Combine(root, child);
        }

        private static string GetLeafName(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return string.Empty;
            }

            return Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)) ?? string.Empty;
        }

        private static string NormalizePath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return string.Empty;
            }

            try
            {
                return Path.GetFullPath(path.Trim());
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
