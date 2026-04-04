namespace Cortex.Host.Avalonia.Composition
{
    internal sealed class DesktopHostOptions
    {
        public string DataRootPath { get; set; } = string.Empty;
        public string LogFilePath { get; set; } = string.Empty;
        public DesktopBridgeClientOptions BridgeClient { get; set; } = new DesktopBridgeClientOptions();
    }
}
