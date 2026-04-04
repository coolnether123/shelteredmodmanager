using System;
using Avalonia.Controls;
using Cortex.Host.Avalonia.Logging;
using Serilog;

namespace Cortex.Host.Avalonia.Composition
{
    internal sealed class DesktopHostApplicationSession : IDisposable
    {
        private readonly DesktopHostCompositionRoot _compositionRoot;
        private bool _disposed;

        public DesktopHostApplicationSession(DesktopHostOptions options)
        {
            Options = options ?? new DesktopHostOptions();
            DesktopHostLogging.Initialize(Options.LogFilePath);
            _compositionRoot = new DesktopHostCompositionRoot(Options);
            Log.Information(
                "Desktop host session started. Profile={BundleProfile}, BundleRoot={BundleRoot}, Plugins={PluginSummary}, Tools={ToolSummary}, PipeName={PipeName}",
                Options.BundleProfileName,
                Options.EnvironmentPaths != null ? Options.EnvironmentPaths.BundleRootPath : string.Empty,
                Options.EnvironmentPaths != null ? Options.EnvironmentPaths.BundledPluginSummary : string.Empty,
                Options.EnvironmentPaths != null ? Options.EnvironmentPaths.BundledToolSummary : string.Empty,
                Options.BridgeClient != null ? Options.BridgeClient.PipeName : string.Empty);
        }

        public DesktopHostOptions Options { get; }

        public Window CreateMainWindow()
        {
            return new MainWindow(
                _compositionRoot.ShellViewModel,
                _compositionRoot.WorkbenchCompositionService);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _compositionRoot.Dispose();
            DesktopHostLogging.Dispose();
        }
    }
}
