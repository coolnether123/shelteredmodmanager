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

        public static DesktopHostEnvironmentPaths Create(
            string applicationBasePath,
            string bundleRootPath,
            DesktopBundlePolicy bundlePolicy)
        {
            var effectivePolicy = bundlePolicy ?? new DesktopBundlePolicy();
            return new DesktopHostEnvironmentPaths
            {
                ApplicationBasePath = applicationBasePath ?? string.Empty,
                BundleRootPath = bundleRootPath ?? string.Empty,
                HostRuntimeRootPath = effectivePolicy.ResolveHostRuntimeRootPath(bundleRootPath),
                PortableRuntimeRootPath = effectivePolicy.ResolvePortableRuntimeRootPath(bundleRootPath),
                BundledPluginRootPath = effectivePolicy.ResolveBundledPluginRootPath(bundleRootPath),
                BundledToolRootPath = effectivePolicy.ResolveBundledToolRootPath(bundleRootPath)
            };
        }
    }
}
