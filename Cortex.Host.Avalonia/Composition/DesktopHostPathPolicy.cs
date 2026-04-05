using System;
using System.IO;

namespace Cortex.Host.Avalonia.Composition
{
    internal sealed class DesktopHostPathPolicy
    {
        public string ResolveDataRootPath()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Cortex.Host.Avalonia");
        }

        public string ResolveLogFilePath(string dataRootPath, string applicationBasePath, string bundleRootPath)
        {
            var preferredLogRootPath = TryResolveShelteredHostRootPath(applicationBasePath, bundleRootPath);
            if (string.IsNullOrEmpty(preferredLogRootPath))
            {
                preferredLogRootPath = dataRootPath ?? string.Empty;
            }

            return Path.Combine(preferredLogRootPath, "cortex-desktop.log");
        }

        public string ResolveShellStateFilePath(string dataRootPath)
        {
            return Path.Combine(dataRootPath ?? string.Empty, "desktop-shell-state.json");
        }

        public string ResolveDockLayoutFilePath(string dataRootPath)
        {
            return Path.Combine(dataRootPath ?? string.Empty, "desktop-dock-layout.json");
        }

        public string ResolveBundleRootPath(string applicationBasePath, string configuredBundleRootPath)
        {
            var normalizedConfiguredPath = NormalizePath(configuredBundleRootPath);
            if (!string.IsNullOrEmpty(normalizedConfiguredPath))
            {
                return normalizedConfiguredPath;
            }

            var normalizedApplicationBasePath = NormalizePath(applicationBasePath);
            var resolvedFromBundleRuntimePath = TryResolveBundleRootFromHostRuntimePath(normalizedApplicationBasePath);
            if (!string.IsNullOrEmpty(resolvedFromBundleRuntimePath))
            {
                return resolvedFromBundleRuntimePath;
            }

            var repoRoot = TryFindRepositoryRoot(normalizedApplicationBasePath);
            if (!string.IsNullOrEmpty(repoRoot))
            {
                return Path.Combine(repoRoot, "artifacts", "bundles", DesktopDefaultHostProfile.BundleProfileName);
            }

            return normalizedApplicationBasePath;
        }

        private static string TryResolveShelteredHostRootPath(string applicationBasePath, string bundleRootPath)
        {
            var resolvedFromBundleRoot = TryResolveShelteredHostRootFromBundleRoot(bundleRootPath);
            if (!string.IsNullOrEmpty(resolvedFromBundleRoot))
            {
                return resolvedFromBundleRoot;
            }

            return TryResolveShelteredHostRootFromApplicationBasePath(applicationBasePath);
        }

        private static string TryResolveShelteredHostRootFromBundleRoot(string bundleRootPath)
        {
            var normalizedBundleRootPath = NormalizePath(bundleRootPath);
            if (string.IsNullOrEmpty(normalizedBundleRootPath))
            {
                return string.Empty;
            }

            try
            {
                var current = new DirectoryInfo(normalizedBundleRootPath);
                if (current == null || !string.Equals(current.Name, "desktop-host", StringComparison.OrdinalIgnoreCase))
                {
                    return string.Empty;
                }

                var toolsDirectory = current.Parent;
                if (toolsDirectory == null || !string.Equals(toolsDirectory.Name, "tools", StringComparison.OrdinalIgnoreCase))
                {
                    return string.Empty;
                }

                var binDirectory = toolsDirectory.Parent;
                if (binDirectory == null || !string.Equals(binDirectory.Name, "bin", StringComparison.OrdinalIgnoreCase))
                {
                    return string.Empty;
                }

                var shelteredHostRoot = binDirectory.Parent;
                if (shelteredHostRoot == null || !string.Equals(shelteredHostRoot.Name, "SMM", StringComparison.OrdinalIgnoreCase))
                {
                    return string.Empty;
                }

                return shelteredHostRoot.FullName;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string TryResolveShelteredHostRootFromApplicationBasePath(string applicationBasePath)
        {
            var normalizedApplicationBasePath = NormalizePath(applicationBasePath);
            if (string.IsNullOrEmpty(normalizedApplicationBasePath))
            {
                return string.Empty;
            }

            try
            {
                var current = new DirectoryInfo(normalizedApplicationBasePath);
                while (current != null)
                {
                    if (string.Equals(current.Name, "SMM", StringComparison.OrdinalIgnoreCase))
                    {
                        return current.FullName;
                    }

                    current = current.Parent;
                }
            }
            catch
            {
            }

            return string.Empty;
        }

        private static string TryResolveBundleRootFromHostRuntimePath(string applicationBasePath)
        {
            if (string.IsNullOrEmpty(applicationBasePath))
            {
                return string.Empty;
            }

            try
            {
                var current = new DirectoryInfo(applicationBasePath);
                if (current == null || !string.Equals(current.Name, "lib", StringComparison.OrdinalIgnoreCase))
                {
                    return string.Empty;
                }

                var hostDirectory = current.Parent;
                if (hostDirectory == null || !string.Equals(hostDirectory.Name, "host", StringComparison.OrdinalIgnoreCase))
                {
                    return string.Empty;
                }

                return hostDirectory.Parent != null ? hostDirectory.Parent.FullName : string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string TryFindRepositoryRoot(string applicationBasePath)
        {
            if (string.IsNullOrEmpty(applicationBasePath))
            {
                return string.Empty;
            }

            try
            {
                var current = new DirectoryInfo(applicationBasePath);
                while (current != null)
                {
                    if (File.Exists(Path.Combine(current.FullName, "Cortex.sln")) &&
                        File.Exists(Path.Combine(current.FullName, "Directory.Build.props")))
                    {
                        return current.FullName;
                    }

                    current = current.Parent;
                }
            }
            catch
            {
            }

            return string.Empty;
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
