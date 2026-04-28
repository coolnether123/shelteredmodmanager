using System;
using System.Collections.Generic;
using Cortex.Contracts.Integration;

namespace Cortex.Host.Sheltered.Runtime
{
    public sealed class ShelteredProductAdapter : ICortexProductAdapter
    {
        public const string ProductIdValue = "sheltered";
        public const string AdapterIdValue = "sheltered.unity-hosted";
        public const string HostIdValue = "cortex.host.unity.sheltered";
        public const string LaunchProfileIdValue = "unity-hosted";

        public static readonly ShelteredProductAdapter Instance = new ShelteredProductAdapter();

        private static readonly ICortexPathPolicy SharedPathPolicy = new ShelteredPathPolicy();
        private static readonly ICortexReferenceAssemblyProvider SharedReferenceAssemblyProvider = new ShelteredReferenceAssemblyProvider();
        private static readonly ICortexPluginRootProvider SharedPluginRootProvider = new ShelteredPluginRootProvider();

        public string AdapterId
        {
            get { return AdapterIdValue; }
        }

        public ICortexPathPolicy PathPolicy
        {
            get { return SharedPathPolicy; }
        }

        public ICortexReferenceAssemblyProvider ReferenceAssemblyProvider
        {
            get { return SharedReferenceAssemblyProvider; }
        }

        public ICortexPluginRootProvider PluginRootProvider
        {
            get { return SharedPluginRootProvider; }
        }

        public bool CanHandle(ICortexHostLaunchContext launchContext, ICortexRuntimeEnvironment runtimeEnvironment)
        {
            if (launchContext == null)
            {
                return true;
            }

            return string.IsNullOrEmpty(launchContext.AdapterId) ||
                string.Equals(launchContext.AdapterId, AdapterIdValue, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(launchContext.ProductId, ProductIdValue, StringComparison.OrdinalIgnoreCase);
        }

        public static CortexHostLaunchContext CreateLaunchContext(ICortexRuntimeEnvironment runtimeEnvironment)
        {
            return new CortexHostLaunchContext
            {
                ProductId = ProductIdValue,
                AdapterId = AdapterIdValue,
                HostId = HostIdValue,
                LaunchProfileId = LaunchProfileIdValue,
                LaunchRootPath = runtimeEnvironment != null ? runtimeEnvironment.ApplicationRootPath : string.Empty,
                Arguments = new string[0]
            };
        }

        private sealed class ShelteredPathPolicy : ICortexPathPolicy
        {
            public CortexPathPolicyResult Resolve(ICortexRuntimeEnvironment runtimeEnvironment, ICortexHostLaunchContext launchContext)
            {
                return new CortexPathPolicyResult
                {
                    WorkspaceRootPath = runtimeEnvironment != null ? runtimeEnvironment.RuntimeContentRootPath : string.Empty,
                    RuntimeContentRootPath = runtimeEnvironment != null ? runtimeEnvironment.RuntimeContentRootPath : string.Empty,
                    AdditionalSourceRoots = runtimeEnvironment != null ? runtimeEnvironment.RuntimeContentRootPath : string.Empty,
                    SettingsFilePath = runtimeEnvironment != null ? runtimeEnvironment.SettingsFilePath : string.Empty,
                    WorkbenchPersistenceFilePath = runtimeEnvironment != null ? runtimeEnvironment.WorkbenchPersistenceFilePath : string.Empty,
                    LogFilePath = runtimeEnvironment != null ? runtimeEnvironment.LogFilePath : string.Empty,
                    ProjectCatalogPath = runtimeEnvironment != null ? runtimeEnvironment.ProjectCatalogPath : string.Empty,
                    DecompilerCachePath = runtimeEnvironment != null ? runtimeEnvironment.DecompilerCachePath : string.Empty,
                    HostBinPath = runtimeEnvironment != null ? runtimeEnvironment.HostBinPath : string.Empty,
                    BundledToolRootPath = runtimeEnvironment != null ? runtimeEnvironment.BundledToolRootPath : string.Empty
                };
            }
        }

        private sealed class ShelteredReferenceAssemblyProvider : ICortexReferenceAssemblyProvider
        {
            public string[] GetReferenceAssemblyRoots(
                ICortexRuntimeEnvironment runtimeEnvironment,
                ICortexHostLaunchContext launchContext,
                CortexPathPolicyResult pathPolicyResult)
            {
                return SplitRoots(runtimeEnvironment != null ? runtimeEnvironment.ReferenceAssemblyRootPath : string.Empty);
            }
        }

        private sealed class ShelteredPluginRootProvider : ICortexPluginRootProvider
        {
            public string[] GetBundledPluginRoots(
                ICortexRuntimeEnvironment runtimeEnvironment,
                ICortexHostLaunchContext launchContext,
                CortexPathPolicyResult pathPolicyResult)
            {
                return SplitRoots(runtimeEnvironment != null ? runtimeEnvironment.BundledPluginSearchRoots : string.Empty);
            }

            public string[] GetConfiguredPluginRoots(
                ICortexRuntimeEnvironment runtimeEnvironment,
                ICortexHostLaunchContext launchContext,
                CortexPathPolicyResult pathPolicyResult)
            {
                return SplitRoots(runtimeEnvironment != null ? runtimeEnvironment.ConfiguredPluginSearchRoots : string.Empty);
            }
        }

        private static string[] SplitRoots(string roots)
        {
            if (string.IsNullOrEmpty(roots))
            {
                return new string[0];
            }

            var parts = roots.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            var normalized = new List<string>();
            for (var i = 0; i < parts.Length; i++)
            {
                var root = NormalizePath(parts[i]);
                if (string.IsNullOrEmpty(root))
                {
                    continue;
                }

                var isDuplicate = false;
                for (var j = 0; j < normalized.Count; j++)
                {
                    if (string.Equals(normalized[j], root, StringComparison.OrdinalIgnoreCase))
                    {
                        isDuplicate = true;
                        break;
                    }
                }

                if (!isDuplicate)
                {
                    normalized.Add(root);
                }
            }

            return normalized.ToArray();
        }

        private static string NormalizePath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return string.Empty;
            }

            try
            {
                return System.IO.Path.GetFullPath(path.Trim());
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
