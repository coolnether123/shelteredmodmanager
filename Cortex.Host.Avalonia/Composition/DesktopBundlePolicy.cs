using System;
using System.IO;

namespace Cortex.Host.Avalonia.Composition
{
    internal sealed class DesktopBundlePolicy
    {
        private static readonly string[] EnabledBundledPluginIdsValue =
        {
            "Harmony"
        };

        private static readonly string[] RequiredBundledToolIdsValue =
        {
            "roslyn",
            "tabby"
        };

        public string ProfileName
        {
            get { return DesktopDefaultHostProfile.BundleProfileName; }
        }

        public string PortableRuntimeRelativePath
        {
            get { return Path.Combine("portable", "lib"); }
        }

        public string HostRuntimeRelativePath
        {
            get { return Path.Combine("host", "lib"); }
        }

        public string BundledPluginRelativePath
        {
            get { return "plugins"; }
        }

        public string BundledToolRelativePath
        {
            get { return "tooling"; }
        }

        public string[] EnabledBundledPluginIds
        {
            get { return (string[])EnabledBundledPluginIdsValue.Clone(); }
        }

        public string[] RequiredBundledToolIds
        {
            get { return (string[])RequiredBundledToolIdsValue.Clone(); }
        }

        public string ResolvePortableRuntimeRootPath(string bundleRootPath)
        {
            return Combine(bundleRootPath, PortableRuntimeRelativePath);
        }

        public string ResolveHostRuntimeRootPath(string bundleRootPath)
        {
            return Combine(bundleRootPath, HostRuntimeRelativePath);
        }

        public string ResolveBundledPluginRootPath(string bundleRootPath)
        {
            return Combine(bundleRootPath, BundledPluginRelativePath);
        }

        public string ResolveBundledToolRootPath(string bundleRootPath)
        {
            return Combine(bundleRootPath, BundledToolRelativePath);
        }

        public string ResolveBundledPluginComponentPath(string bundleRootPath, string componentId)
        {
            return Combine(ResolveBundledPluginRootPath(bundleRootPath), componentId);
        }

        public string ResolveBundledToolComponentPath(string bundleRootPath, string componentId)
        {
            return Combine(ResolveBundledToolRootPath(bundleRootPath), componentId);
        }

        private static string Combine(string rootPath, string relativePath)
        {
            if (string.IsNullOrEmpty(rootPath) || string.IsNullOrEmpty(relativePath))
            {
                return string.Empty;
            }

            try
            {
                return Path.GetFullPath(Path.Combine(rootPath, relativePath));
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
