using Cortex.Bridge;

namespace Cortex.Host.Avalonia.Composition
{
    internal sealed class DesktopBridgeClientOptions
    {
        public string PipeName { get; set; } = DesktopBridgeProtocol.DefaultPipeName;
        public string ClientDisplayName { get; set; } = DesktopBridgeProtocol.DefaultClientDisplayName;
    }
}
