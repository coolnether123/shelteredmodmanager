using System;
using Avalonia.Controls;
using Cortex.Host.Avalonia.Logging;

namespace Cortex.Host.Avalonia.Composition
{
    internal sealed class DesktopHostApplicationSession : IDisposable
    {
        private readonly DesktopHostCompositionRoot _compositionRoot;
        private bool _disposed;

        public DesktopHostApplicationSession(DesktopHostOptions options)
        {
            Options = options ?? new DesktopHostOptions();
            _compositionRoot = new DesktopHostCompositionRoot(Options);
            DesktopHostLogging.Initialize(Options.LogFilePath);
        }

        public DesktopHostOptions Options { get; }

        public Window CreateMainWindow()
        {
            return new MainWindow
            {
                DataContext = _compositionRoot.MainWindowViewModel
            };
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
