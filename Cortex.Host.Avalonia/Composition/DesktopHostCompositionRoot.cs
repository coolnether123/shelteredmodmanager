using System;
using Cortex.Host.Avalonia.Bridge;
using Cortex.Host.Avalonia.Services;
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
            WorkbenchViewModel = new MainWindowViewModel(_bridgeClient, Options);

            var surfaceRegistry = new DesktopWorkbenchSurfaceRegistry();
            var shellStateStore = new DesktopShellStateStore(Options.ShellStateFilePath);
            var dockLayoutPersistence = new DesktopDockLayoutPersistenceService(Options.DockLayoutFilePath);

            WorkbenchCompositionService = new DesktopWorkbenchCompositionService(
                surfaceRegistry,
                shellStateStore,
                dockLayoutPersistence);
            ShellViewModel = new DesktopShellViewModel(WorkbenchViewModel);
            ShellViewModel.ApplyShellState(
                WorkbenchCompositionService.CurrentShellState,
                WorkbenchCompositionService.SurfaceRegistry.Definitions);
        }

        public DesktopHostOptions Options { get; }
        public MainWindowViewModel WorkbenchViewModel { get; }
        public DesktopShellViewModel ShellViewModel { get; }
        public DesktopWorkbenchCompositionService WorkbenchCompositionService { get; }

        public void Dispose()
        {
            _bridgeClient.Dispose();
        }
    }
}
