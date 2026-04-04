using System;
using System.IO;
using Cortex.Bridge;
using Cortex.Host.Avalonia.Bridge;
using Cortex.Host.Avalonia.ViewModels;

namespace Cortex.Host.Avalonia.Composition
{
    internal sealed class DesktopHostCompositionRoot : IDisposable
    {
        private readonly NamedPipeDesktopBridgeClient _bridgeClient;

        public DesktopHostCompositionRoot(string pipeName)
        {
            var dataRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Cortex.Host.Avalonia");
            Directory.CreateDirectory(dataRoot);

            LogFilePath = Path.Combine(dataRoot, "cortex-desktop.log");
            _bridgeClient = new NamedPipeDesktopBridgeClient(string.IsNullOrEmpty(pipeName) ? DesktopBridgeProtocol.DefaultPipeName : pipeName);
            MainWindowViewModel = new MainWindowViewModel(_bridgeClient);
        }

        public string LogFilePath { get; private set; }
        public MainWindowViewModel MainWindowViewModel { get; private set; }

        public void Dispose()
        {
            _bridgeClient.Dispose();
        }
    }
}
