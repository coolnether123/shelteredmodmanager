using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Cortex.Host.Avalonia.Composition;

namespace Cortex.Host.Avalonia
{
    public partial class App : Application
    {
        private DesktopHostApplicationSession _session;

        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            _session = DesktopHostLaunchCoordinator.GetActiveSession() ??
                new DesktopSessionStartupService().Start(Environment.GetCommandLineArgs());

            var desktop = ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
            if (desktop != null)
            {
                desktop.MainWindow = _session.CreateMainWindow();
                desktop.Exit += delegate
                {
                    _session.Dispose();
                };
            }

            base.OnFrameworkInitializationCompleted();
        }
    }
}
