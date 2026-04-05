using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Serilog;

namespace Cortex.Host.Avalonia.Composition
{
    internal sealed class DesktopHostLaunchCoordinator
    {
        private static DesktopHostApplicationSession _activeSession;
        private readonly DesktopSessionStartupService _startupService;

        public DesktopHostLaunchCoordinator()
            : this(new DesktopSessionStartupService())
        {
        }

        public DesktopHostLaunchCoordinator(DesktopSessionStartupService startupService)
        {
            _startupService = startupService ?? new DesktopSessionStartupService();
        }

        public void Run(string[] args)
        {
            var session = _startupService.Start(args);
            _activeSession = session;

            try
            {
                Log.Information("Starting Avalonia desktop host lifetime. ArgsCount={ArgsCount}", args != null ? args.Length : 0);
                Program.BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
            }
            finally
            {
                Log.Information("Avalonia desktop host lifetime exited.");
                if (ReferenceEquals(_activeSession, session))
                {
                    _activeSession = null;
                }
            }
        }

        public static DesktopHostApplicationSession GetActiveSession()
        {
            return _activeSession;
        }
    }
}
