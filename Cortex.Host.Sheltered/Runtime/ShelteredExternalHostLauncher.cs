using System;
using System.Diagnostics;
using Cortex.Host.Unity.Runtime;

namespace Cortex.Host.Sheltered.Runtime
{
    internal sealed class ShelteredExternalAvaloniaHostStartupAction : IUnityShellStartupAction
    {
        private readonly ShelteredRenderHostCatalog _catalog;
        private readonly ShelteredExternalHostLauncher _launcher;

        public ShelteredExternalAvaloniaHostStartupAction(ShelteredRenderHostCatalog catalog)
            : this(catalog, new ShelteredExternalHostLauncher())
        {
        }

        internal ShelteredExternalAvaloniaHostStartupAction(
            ShelteredRenderHostCatalog catalog,
            ShelteredExternalHostLauncher launcher)
        {
            _catalog = catalog ?? ShelteredRenderHostCatalog.CreateDefault();
            _launcher = launcher ?? new ShelteredExternalHostLauncher();
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

    internal sealed class ShelteredExternalHostLauncher
    {
        public ShelteredExternalHostLaunchResult Launch(ShelteredExternalHostLaunchRequest request)
        {
            if (request == null || !request.CanLaunch)
            {
                return new ShelteredExternalHostLaunchResult(false, request != null ? request.FailureReason : "External Avalonia host launch was not available.");
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
                    return new ShelteredExternalHostLaunchResult(false, "External Avalonia host launch returned no process.");
                }

                return new ShelteredExternalHostLaunchResult(true, request.SuccessStatusMessage);
            }
            catch (Exception ex)
            {
                return new ShelteredExternalHostLaunchResult(false, "Failed to launch external Avalonia host: " + ex.Message);
            }
        }
    }

    internal sealed class ShelteredExternalHostLaunchResult
    {
        public ShelteredExternalHostLaunchResult(bool launched, string statusMessage)
        {
            Launched = launched;
            StatusMessage = statusMessage ?? string.Empty;
        }

        public bool Launched { get; private set; }

        public string StatusMessage { get; private set; }
    }
}
