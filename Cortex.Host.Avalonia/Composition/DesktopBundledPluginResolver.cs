using System;
using System.Collections.Generic;
using System.IO;

namespace Cortex.Host.Avalonia.Composition
{
    internal sealed class DesktopBundledPluginResolver
    {
        public string ResolveBundledSearchRoots(DesktopHostEnvironmentPaths environmentPaths, DesktopBundlePolicy bundlePolicy)
        {
            var roots = ResolveBundledComponentRoots(environmentPaths, bundlePolicy);
            return roots.Count > 0 ? string.Join(";", roots.ToArray()) : string.Empty;
        }

        public string BuildSummary(DesktopHostEnvironmentPaths environmentPaths, DesktopBundlePolicy bundlePolicy)
        {
            var effectivePolicy = bundlePolicy ?? new DesktopBundlePolicy();
            var summaries = new List<string>();
            var pluginIds = effectivePolicy.EnabledBundledPluginIds;
            for (var i = 0; i < pluginIds.Length; i++)
            {
                var pluginId = pluginIds[i];
                var componentPath = effectivePolicy.ResolveBundledPluginComponentPath(
                    environmentPaths != null ? environmentPaths.BundleRootPath : string.Empty,
                    pluginId);
                summaries.Add(pluginId + ":" + (Directory.Exists(componentPath) ? "bundled" : "not-built"));
            }

            return summaries.Count > 0 ? string.Join(", ", summaries.ToArray()) : "No bundled plugins.";
        }

        private static IList<string> ResolveBundledComponentRoots(DesktopHostEnvironmentPaths environmentPaths, DesktopBundlePolicy bundlePolicy)
        {
            var roots = new List<string>();
            var effectivePolicy = bundlePolicy ?? new DesktopBundlePolicy();
            var bundleRootPath = environmentPaths != null ? environmentPaths.BundleRootPath : string.Empty;
            var pluginIds = effectivePolicy.EnabledBundledPluginIds;
            for (var i = 0; i < pluginIds.Length; i++)
            {
                var componentRootPath = effectivePolicy.ResolveBundledPluginComponentPath(bundleRootPath, pluginIds[i]);
                if (string.IsNullOrEmpty(componentRootPath) || !Directory.Exists(componentRootPath))
                {
                    continue;
                }

                if (!roots.Contains(componentRootPath, StringComparer.OrdinalIgnoreCase))
                {
                    roots.Add(componentRootPath);
                }
            }

            return roots;
        }
    }
}
