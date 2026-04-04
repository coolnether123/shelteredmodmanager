using System;
using System.Diagnostics;
using Cortex.Core.Diagnostics;

namespace Cortex.Host.Unity.Runtime
{
    public sealed class UnityExternalAvaloniaHostStartupAction : IUnityShellStartupAction
    {
        private static readonly CortexLogger Log = CortexLog.ForSource("Cortex.Host.Unity");
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
                    Log.WriteInfo(_catalog.StartupStatusMessage);
                }

                if (!string.IsNullOrEmpty(_catalog.StartupStatusMessage))
                {
                    context.SetStatusMessage(_catalog.StartupStatusMessage);
                }

                return;
            }

            Log.WriteInfo("Attempting external Avalonia host launch. SelectedHost=" +
                (_catalog.SelectedRenderHostId ?? string.Empty) +
                ", EffectiveHost=" + (_catalog.EffectiveRenderHostId ?? string.Empty) + ".");
            var result = _launcher.Launch(_catalog.AvaloniaLaunchRequest);
            Log.WriteInfo(result.Launched
                ? result.StatusMessage
                : "External Avalonia host launch did not succeed: " + result.StatusMessage);
            context.SetStatusMessage(result.StatusMessage);
        }
    }

    public sealed class UnityExternalHostLauncher
    {
        public UnityExternalHostLaunchResult Launch(UnityExternalHostLaunchRequest request)
        {
            if (request == null || !request.CanLaunch)
            {
                return new UnityExternalHostLaunchResult(false, request != null ? request.FailureReason : "External Avalonia host launch was not available.", 0);
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
                    return new UnityExternalHostLaunchResult(false, "External Avalonia host launch returned no process.", 0);
                }

                return new UnityExternalHostLaunchResult(true, request.SuccessStatusMessage, process.Id);
            }
            catch (Exception ex)
            {
                return new UnityExternalHostLaunchResult(false, "Failed to launch external Avalonia host: " + ex.Message, 0);
            }
        }
    }

    public sealed class UnityExternalHostLaunchResult
    {
        public UnityExternalHostLaunchResult(bool launched, string statusMessage, int processId)
        {
            Launched = launched;
            StatusMessage = statusMessage ?? string.Empty;
            ProcessId = processId;
        }

        public bool Launched { get; private set; }

        public string StatusMessage { get; private set; }

        public int ProcessId { get; private set; }
    }
}
