using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Cortex.Host.Avalonia.Composition;
using Serilog;

namespace Cortex.Host.Avalonia
{
    public partial class App : Application
    {
        private DesktopHostApplicationSession _session;
        private bool _exceptionHooksRegistered;

        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            RegisterExceptionHooks();
            _session = DesktopHostLaunchCoordinator.GetActiveSession() ??
                new DesktopSessionStartupService().Start(Environment.GetCommandLineArgs());

            var desktop = ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
            if (desktop != null)
            {
                Log.Information("Configuring classic desktop lifetime. MainWindow will be created from the active desktop host session.");
                desktop.MainWindow = _session.CreateMainWindow();
                desktop.Exit += delegate
                {
                    Log.Information("Desktop lifetime exit received. Disposing desktop host session.");
                    _session.Dispose();
                };
            }

            Log.Information("Avalonia framework initialization completed.");
            base.OnFrameworkInitializationCompleted();
        }

        private void RegisterExceptionHooks()
        {
            if (_exceptionHooksRegistered)
            {
                return;
            }

            _exceptionHooksRegistered = true;
            AppDomain.CurrentDomain.UnhandledException += delegate(object sender, UnhandledExceptionEventArgs args)
            {
                Log.Fatal(args.ExceptionObject as Exception, "Unhandled AppDomain exception. IsTerminating={IsTerminating}", args.IsTerminating);
            };
            TaskScheduler.UnobservedTaskException += delegate(object sender, UnobservedTaskExceptionEventArgs args)
            {
                Log.Error(args.Exception, "Unobserved task exception in desktop host.");
            };
            Dispatcher.UIThread.UnhandledException += delegate(object sender, DispatcherUnhandledExceptionEventArgs args)
            {
                Log.Error(args.Exception, "Unhandled UI thread exception in desktop host.");
            };
        }
    }
}
