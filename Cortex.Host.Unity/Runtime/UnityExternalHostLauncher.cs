using System;
using System.Diagnostics;

namespace Cortex.Host.Unity.Runtime
{
    public sealed class UnityExternalAvaloniaHostStartupAction : IUnityShellStartupAction
    {
        private readonly UnityRenderHostCatalog _catalog;
        private readonly UnityExternalHostLauncher _launcher;

        public UnityExternalAvaloniaHostStartupAction(UnityRenderHostCatalog catalog)
            : this(catalog, new UnityExternalHostLauncher())
        {
        }

        public UnityExternalAvaloniaHostStartupAction(
            UnityRenderHostCatalog catalog,
            UnityExternalHostLauncher launcher)
        {
            _catalog = catalog ?? UnityRenderHostCatalog.CreateDefault();
            _launcher = launcher ?? new UnityExternalHostLauncher();
        }

        public void OnShellStarted(UnityShellStartupContext context)
        {
            if (context == null)
            {
                return;
            }

            if (!_catalog.ShouldLaunchAvaloniaExternalHost)
            {
                if (!string.IsNullOrEmpty(_catalog.StartupStatusMessage))
                {
                    context.SetStatusMessage(_catalog.StartupStatusMessage);
                }

                return;
            }

            var result = _launcher.Launch(_catalog.AvaloniaLaunchRequest);
            context.SetStatusMessage(result.StatusMessage);
        }
    }

    public sealed class UnityExternalHostLauncher
    {
        public UnityExternalHostLaunchResult Launch(UnityExternalHostLaunchRequest request)
        {
            if (request == null || !request.CanLaunch)
            {
                return new UnityExternalHostLaunchResult(false, request != null ? request.FailureReason : "External Avalonia host launch was not available.");
            }

            try
            {
                var process = Process.Start(new ProcessStartInfo
                {
                    FileName = request.CommandPath,
                    Arguments = request.Arguments ?? string.Empty,
                    WorkingDirectory = request.WorkingDirectory ?? string.Empty,
                    UseShellExecute = false
                });

                if (process == null)
                {
                    return new UnityExternalHostLaunchResult(false, "External Avalonia host launch returned no process.");
                }

                return new UnityExternalHostLaunchResult(true, request.SuccessStatusMessage);
            }
            catch (Exception ex)
            {
                return new UnityExternalHostLaunchResult(false, "Failed to launch external Avalonia host: " + ex.Message);
            }
        }
    }

    public sealed class UnityExternalHostLaunchResult
    {
        public UnityExternalHostLaunchResult(bool launched, string statusMessage)
        {
            Launched = launched;
            StatusMessage = statusMessage ?? string.Empty;
        }

        public bool Launched { get; private set; }

        public string StatusMessage { get; private set; }
    }
}
