using System;
using Cortex.Host.Avalonia.Bridge;
using Cortex.Host.Avalonia.ViewModels;

namespace Cortex.Host.Avalonia.Composition
{
    internal sealed class DesktopHostCompositionRoot : IDisposable
    {
        private readonly NamedPipeDesktopBridgeClient _bridgeClient;

        public DesktopHostCompositionRoot(DesktopHostOptions options)
        {
            Options = options ?? new DesktopHostOptions();
            _bridgeClient = new NamedPipeDesktopBridgeClient(Options.BridgeClient);
            MainWindowViewModel = new MainWindowViewModel(_bridgeClient);
        }

        public DesktopHostOptions Options { get; }
        public MainWindowViewModel MainWindowViewModel { get; private set; }

        public void Dispose()
        {
            _bridgeClient.Dispose();
        }
    }
}
