using System;

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
                Program.BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
            }
            finally
            {
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
