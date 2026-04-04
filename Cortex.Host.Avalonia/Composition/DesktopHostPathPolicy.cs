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

        public string ResolveLogFilePath(string dataRootPath)
        {
            return Path.Combine(dataRootPath ?? string.Empty, "cortex-desktop.log");
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
