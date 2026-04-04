using System;
using System.Collections.Generic;
using System.IO;

namespace Cortex.Host.Avalonia.Composition
{
    internal sealed class DesktopHostEnvironmentPaths
    {
        public string ApplicationBasePath { get; set; } = string.Empty;
        public string BundleRootPath { get; set; } = string.Empty;
        public string HostRuntimeRootPath { get; set; } = string.Empty;
        public string PortableRuntimeRootPath { get; set; } = string.Empty;
        public string BundledPluginRootPath { get; set; } = string.Empty;
        public string BundledToolRootPath { get; set; } = string.Empty;
        public string BundledPluginSearchRoots { get; set; } = string.Empty;
        public string BundledPluginSummary { get; set; } = string.Empty;
        public string BundledToolSummary { get; set; } = string.Empty;

        public static DesktopHostEnvironmentPaths Create(
            string applicationBasePath,
            string bundleRootPath,
            DesktopBundlePolicy bundlePolicy)
        {
            var effectivePolicy = bundlePolicy ?? new DesktopBundlePolicy();
            var resolvedBundleRootPath = bundleRootPath ?? string.Empty;
            var bundledPluginRootPath = effectivePolicy.ResolveBundledPluginRootPath(resolvedBundleRootPath);
            var bundledToolRootPath = effectivePolicy.ResolveBundledToolRootPath(resolvedBundleRootPath);
            return new DesktopHostEnvironmentPaths
            {
                ApplicationBasePath = applicationBasePath ?? string.Empty,
                BundleRootPath = resolvedBundleRootPath,
                HostRuntimeRootPath = effectivePolicy.ResolveHostRuntimeRootPath(resolvedBundleRootPath),
                PortableRuntimeRootPath = effectivePolicy.ResolvePortableRuntimeRootPath(resolvedBundleRootPath),
                BundledPluginRootPath = bundledPluginRootPath,
                BundledToolRootPath = bundledToolRootPath,
                BundledPluginSearchRoots = BuildPluginSearchRoots(effectivePolicy, resolvedBundleRootPath),
                BundledPluginSummary = BuildPluginSummary(effectivePolicy, resolvedBundleRootPath),
                BundledToolSummary = BuildToolSummary(effectivePolicy, resolvedBundleRootPath)
            };
        }

        private static string BuildPluginSearchRoots(DesktopBundlePolicy bundlePolicy, string bundleRootPath)
        {
            var pluginIds = bundlePolicy.EnabledBundledPluginIds;
            var roots = new List<string>();
            for (var i = 0; i < pluginIds.Length; i++)
            {
                var componentRootPath = bundlePolicy.ResolveBundledPluginComponentPath(bundleRootPath, pluginIds[i]);
                if (string.IsNullOrEmpty(componentRootPath) || !Directory.Exists(componentRootPath))
                {
                    continue;
                }

                if (!ContainsPath(roots, componentRootPath))
                {
                    roots.Add(componentRootPath);
                }
            }

            return roots.Count > 0 ? string.Join(";", roots.ToArray()) : string.Empty;
        }

        private static string BuildPluginSummary(DesktopBundlePolicy bundlePolicy, string bundleRootPath)
        {
            return BuildComponentSummary(
                bundlePolicy.EnabledBundledPluginIds,
                delegate(string componentId)
                {
                    return bundlePolicy.ResolveBundledPluginComponentPath(bundleRootPath, componentId);
                },
                "No bundled plugins.");
        }

        private static string BuildToolSummary(DesktopBundlePolicy bundlePolicy, string bundleRootPath)
        {
            return BuildComponentSummary(
                bundlePolicy.RequiredBundledToolIds,
                delegate(string componentId)
                {
                    return bundlePolicy.ResolveBundledToolComponentPath(bundleRootPath, componentId);
                },
                "No bundled tools.");
        }

        private static string BuildComponentSummary(string[] componentIds, Func<string, string> resolveComponentRootPath, string emptySummary)
        {
            var summaries = new List<string>();
            for (var i = 0; componentIds != null && i < componentIds.Length; i++)
            {
                var componentId = componentIds[i] ?? string.Empty;
                var componentRootPath = resolveComponentRootPath != null
                    ? resolveComponentRootPath(componentId)
                    : string.Empty;
                summaries.Add(componentId + ":" + (Directory.Exists(componentRootPath) ? "bundled" : "not-built"));
            }

            return summaries.Count > 0 ? string.Join(", ", summaries.ToArray()) : emptySummary;
        }

        private static bool ContainsPath(IList<string> paths, string candidatePath)
        {
            for (var i = 0; paths != null && i < paths.Count; i++)
            {
                if (string.Equals(paths[i], candidatePath, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
