namespace Cortex.Host.Avalonia.Composition
{
    internal sealed class DesktopHostOptions
    {
        public string BundleProfileName { get; set; } = DesktopDefaultHostProfile.BundleProfileName;
        public string DataRootPath { get; set; } = string.Empty;
        public string LogFilePath { get; set; } = string.Empty;
        public string ShellStateFilePath { get; set; } = string.Empty;
        public string DockLayoutFilePath { get; set; } = string.Empty;
        public string StartupModeSummary { get; set; } = string.Empty;
        public string LaunchToken { get; set; } = string.Empty;
        public DesktopHostEnvironmentPaths EnvironmentPaths { get; set; } = new DesktopHostEnvironmentPaths();
        public DesktopBridgeClientOptions BridgeClient { get; set; } = new DesktopBridgeClientOptions();
    }
}
