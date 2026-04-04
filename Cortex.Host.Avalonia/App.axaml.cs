using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Cortex.Bridge;
using Cortex.Host.Avalonia.Composition;
using Cortex.Host.Avalonia.Logging;

namespace Cortex.Host.Avalonia
{
    public partial class App : Application
    {
        private DesktopHostCompositionRoot _compositionRoot;

        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            _compositionRoot = new DesktopHostCompositionRoot(ResolvePipeName(Environment.GetCommandLineArgs()));
            DesktopHostLogging.Initialize(_compositionRoot.LogFilePath);

            var desktop = ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
            if (desktop != null)
            {
                desktop.MainWindow = new MainWindow
                {
                    DataContext = _compositionRoot.MainWindowViewModel
                };
                desktop.Exit += delegate
                {
                    _compositionRoot.Dispose();
                    DesktopHostLogging.Dispose();
                };
            }

            base.OnFrameworkInitializationCompleted();
        }

        private static string ResolvePipeName(string[] args)
        {
            if (args != null)
            {
                for (var i = 0; i < args.Length - 1; i++)
                {
                    if (string.Equals(args[i], "--pipe-name", StringComparison.OrdinalIgnoreCase))
                    {
                        return args[i + 1] ?? DesktopBridgeProtocol.DefaultPipeName;
                    }
                }
            }

            var configuredPipeName = Environment.GetEnvironmentVariable(DesktopBridgeProtocol.PipeNameEnvironmentVariable);
            return string.IsNullOrEmpty(configuredPipeName)
                ? DesktopBridgeProtocol.DefaultPipeName
                : configuredPipeName;
        }
    }
}
