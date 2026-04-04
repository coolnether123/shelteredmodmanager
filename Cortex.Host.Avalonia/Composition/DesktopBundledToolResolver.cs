using System;
using System.Collections.Generic;
using System.IO;

namespace Cortex.Host.Avalonia.Composition
{
    internal sealed class DesktopBundledToolResolver
    {
        public string ResolveComponentRootPath(DesktopHostEnvironmentPaths environmentPaths, DesktopBundlePolicy bundlePolicy, string componentId)
        {
            var effectivePolicy = bundlePolicy ?? new DesktopBundlePolicy();
            var bundleRootPath = environmentPaths != null ? environmentPaths.BundleRootPath : string.Empty;
            return effectivePolicy.ResolveBundledToolComponentPath(bundleRootPath, componentId ?? string.Empty);
        }

        public string BuildSummary(DesktopHostEnvironmentPaths environmentPaths, DesktopBundlePolicy bundlePolicy)
        {
            var effectivePolicy = bundlePolicy ?? new DesktopBundlePolicy();
            var toolIds = effectivePolicy.RequiredBundledToolIds;
            var summaries = new List<string>();
            for (var i = 0; i < toolIds.Length; i++)
            {
                var toolId = toolIds[i];
                var componentRootPath = ResolveComponentRootPath(environmentPaths, effectivePolicy, toolId);
                summaries.Add(toolId + ":" + (Directory.Exists(componentRootPath) ? "bundled" : "not-built"));
            }

            return summaries.Count > 0 ? string.Join(", ", summaries.ToArray()) : "No bundled tools.";
        }
    }
}
