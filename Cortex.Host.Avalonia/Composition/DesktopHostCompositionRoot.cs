using System;
using Cortex.Host.Avalonia.Bridge;
using Cortex.Host.Avalonia.Services;
using Cortex.Host.Avalonia.ViewModels;
using Serilog;

namespace Cortex.Host.Avalonia.Composition
{
    internal sealed class DesktopHostCompositionRoot : IDisposable
    {
        private readonly NamedPipeDesktopBridgeClient _bridgeClient;

        public DesktopHostCompositionRoot(DesktopHostOptions options)
        {
            Options = options ?? new DesktopHostOptions();
            Log.Information(
                "Building desktop host composition root. PipeName={PipeName}, LaunchTokenPresent={LaunchTokenPresent}, ShellStatePath={ShellStatePath}, DockLayoutPath={DockLayoutPath}",
                Options.BridgeClient != null ? Options.BridgeClient.PipeName : string.Empty,
                !string.IsNullOrEmpty(Options.LaunchToken),
                Options.ShellStateFilePath ?? string.Empty,
                Options.DockLayoutFilePath ?? string.Empty);
            _bridgeClient = new NamedPipeDesktopBridgeClient(Options.BridgeClient, Options.LaunchToken);
            WorkbenchViewModel = new MainWindowViewModel(_bridgeClient, Options);
            ShellViewModel = new DesktopShellViewModel(WorkbenchViewModel);
            OverlayWindowManager = new DesktopOverlayWindowManager(_bridgeClient, WorkbenchViewModel, ShellViewModel);

            var surfaceRegistry = new DesktopWorkbenchSurfaceRegistry();
            var shellStateStore = new DesktopShellStateStore(Options.ShellStateFilePath);
            var dockLayoutPersistence = new DesktopDockLayoutPersistenceService(Options.DockLayoutFilePath);

            WorkbenchCompositionService = new DesktopWorkbenchCompositionService(
                surfaceRegistry,
                shellStateStore,
                dockLayoutPersistence);
            ShellViewModel.ApplyShellState(
                WorkbenchCompositionService.CurrentShellState,
                WorkbenchCompositionService.SurfaceRegistry.Definitions);
            Log.Information(
                "Desktop host composition root ready. SurfaceDefinitionCount={SurfaceDefinitionCount}",
                WorkbenchCompositionService.SurfaceRegistry.Definitions != null ? WorkbenchCompositionService.SurfaceRegistry.Definitions.Count : 0);
        }

        public DesktopHostOptions Options { get; }
        public MainWindowViewModel WorkbenchViewModel { get; }
        public DesktopShellViewModel ShellViewModel { get; }
        public DesktopOverlayWindowManager OverlayWindowManager { get; }
        public DesktopWorkbenchCompositionService WorkbenchCompositionService { get; }

        public void Dispose()
        {
            Log.Information("Disposing desktop host composition root.");
            OverlayWindowManager.Dispose();
            _bridgeClient.Dispose();
        }
    }
}
