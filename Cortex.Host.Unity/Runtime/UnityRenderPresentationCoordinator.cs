using Cortex;
using Cortex.Core.Abstractions;
using Cortex.Core.Diagnostics;
using Cortex.Core.Models;
using Cortex.Presentation.Abstractions;
using Cortex.Rendering;
using Cortex.Rendering.RuntimeUi;
using Cortex.Shell.Unity.Imgui;
using System.Diagnostics;
using System;

namespace Cortex.Host.Unity.Runtime
{
    internal interface IUnityRenderPresentationHost
    {
        CortexSettings Settings { get; }
        void ApplyStatusMessage(string statusMessage);
        bool ApplyRuntimeUiFactory(IWorkbenchRuntimeUiFactory runtimeUiFactory);
        void RequestExternalHostShutdown();
        void RegisterOrUpdateStatusItem(StatusItemContribution contribution);
        void RegisterOrUpdateSettingContribution(SettingContribution contribution);
    }

    internal sealed class UnityRenderPresentationCoordinator
    {
        private static readonly CortexLogger Log = CortexLog.ForSource("Cortex.RenderHost");
        private readonly UnityRenderHostCatalogBuilder _catalogBuilder;
        private readonly IWorkbenchFrameContext _frameContext;
        private readonly UnityExternalHostLauncher _externalHostLauncher;

        private string _lastSelectedRenderHostId = string.Empty;
        private string _lastEffectiveRenderHostId = string.Empty;
        private UnityExternalHostSession _externalHostSession;
        private string _suppressedLaunchSignature = string.Empty;
        private string _lastCatalogLogSummary = string.Empty;
        private string _lastSuppressedLogSignature = string.Empty;

        public UnityRenderPresentationCoordinator(
            IWorkbenchFrameContext frameContext,
            UnityRenderHostCatalogBuilder catalogBuilder,
            UnityExternalHostLauncher externalHostLauncher)
        {
            _frameContext = frameContext ?? NullWorkbenchFrameContext.Instance;
            _catalogBuilder = catalogBuilder ?? new UnityRenderHostCatalogBuilder();
            _externalHostLauncher = externalHostLauncher ?? new UnityExternalHostLauncher();
        }

        public void Synchronize(IUnityRenderPresentationHost host, ICortexHostEnvironment environment)
        {
            if (host == null)
            {
                return;
            }

            var selectedRenderHostId = UnityRenderHostSettings.ReadSelectedRenderHostId(host.Settings);
            var catalog = _catalogBuilder.Build(environment, selectedRenderHostId);

            host.RegisterOrUpdateSettingContribution(UnityRenderHostSettingContributions.CreateContribution(catalog, "Appearance", 10));
            host.RegisterOrUpdateStatusItem(new StatusItemContribution
            {
                ItemId = "cortex.status.renderer",
                Text = catalog.StatusSummary,
                ToolTip = "Active Cortex host and render mode.",
                CommandId = string.Empty,
                Severity = "Info",
                Alignment = StatusItemAlignment.Right,
                Priority = 100
            });

            var selectedChanged = !string.Equals(_lastSelectedRenderHostId, catalog.SelectedRenderHostId, System.StringComparison.OrdinalIgnoreCase);
            var effectiveChanged = !string.Equals(_lastEffectiveRenderHostId, catalog.EffectiveRenderHostId, System.StringComparison.OrdinalIgnoreCase);
            var launchSignature = BuildLaunchSignature(catalog.AvaloniaLaunchRequest);
            LogCatalogDecision(catalog);
            if (catalog.ShouldLaunchAvaloniaExternalHost &&
                _externalHostSession != null &&
                !IsTrackedExternalHostAlive())
            {
                Log.WriteWarning("Tracked external overlay host is no longer alive. Falling back to IMGUI.");
                host.ApplyStatusMessage("External overlay host exited. Falling back to in-process presentation.");
                host.ApplyRuntimeUiFactory(ImguiWorkbenchRuntimeUiComposition.CreateRuntimeUiFactory(_frameContext));
                _suppressedLaunchSignature = launchSignature;
                _lastSuppressedLogSignature = launchSignature;
                _externalHostSession = null;
                _lastSelectedRenderHostId = catalog.SelectedRenderHostId ?? string.Empty;
                _lastEffectiveRenderHostId = catalog.EffectiveRenderHostId ?? string.Empty;
                return;
            }

            if (!selectedChanged && !effectiveChanged && !ShouldRelaunchExternalHost(catalog, launchSignature))
            {
                return;
            }

            host.ApplyRuntimeUiFactory(ResolveRuntimeUiFactory(catalog, launchSignature));

            if (!string.IsNullOrEmpty(catalog.StartupStatusMessage))
            {
                host.ApplyStatusMessage(catalog.StartupStatusMessage);
            }

            if (catalog.ShouldLaunchAvaloniaExternalHost)
            {
                if (string.Equals(_suppressedLaunchSignature, launchSignature, StringComparison.OrdinalIgnoreCase))
                {
                    if (!string.Equals(_lastSuppressedLogSignature, launchSignature, StringComparison.OrdinalIgnoreCase))
                    {
                        Log.WriteWarning("External overlay host launch is suppressed for the current launch signature after a previous failure or crash.");
                        _lastSuppressedLogSignature = launchSignature;
                    }

                    host.ApplyStatusMessage("External overlay host is currently suppressed after a failed launch or crash. Switch away and back to retry.");
                }
                else if (ShouldRelaunchExternalHost(catalog, launchSignature))
                {
                    TryStopTrackedExternalHost(host);
                    var request = CloneRequest(catalog.AvaloniaLaunchRequest, Guid.NewGuid().ToString("N"));
                    Log.WriteInfo(
                        "Launching external overlay host. Command=" + (request.CommandPath ?? string.Empty) +
                        ", Arguments=" + (request.Arguments ?? string.Empty) +
                        ", WorkingDirectory=" + (request.WorkingDirectory ?? string.Empty) + ".");
                    var launchResult = _externalHostLauncher.Launch(request);
                    host.ApplyStatusMessage(launchResult.StatusMessage);
                    if (launchResult.Launched)
                    {
                        Log.WriteInfo(
                            "External overlay host launched successfully. ProcessId=" + launchResult.ProcessId +
                            ", LaunchToken=" + (launchResult.LaunchToken ?? string.Empty) + ".");
                        _externalHostSession = new UnityExternalHostSession(
                            launchSignature,
                            launchResult.LaunchToken,
                            launchResult.ProcessId,
                            launchResult.ProcessStartTimeUtc);
                        _suppressedLaunchSignature = string.Empty;
                        _lastSuppressedLogSignature = string.Empty;
                    }
                    else
                    {
                        Log.WriteWarning("External overlay host launch failed. Status=" + (launchResult.StatusMessage ?? string.Empty) + ".");
                        _suppressedLaunchSignature = launchSignature;
                        _lastSuppressedLogSignature = string.Empty;
                        host.ApplyRuntimeUiFactory(ImguiWorkbenchRuntimeUiComposition.CreateRuntimeUiFactory(_frameContext));
                    }
                }
            }
            else
            {
                _suppressedLaunchSignature = string.Empty;
                _lastSuppressedLogSignature = string.Empty;
                TryStopTrackedExternalHost(host);
            }

            _lastSelectedRenderHostId = catalog.SelectedRenderHostId ?? string.Empty;
            _lastEffectiveRenderHostId = catalog.EffectiveRenderHostId ?? string.Empty;
        }

        public void Shutdown(IUnityRenderPresentationHost host)
        {
            if (host == null)
            {
                return;
            }

            TryStopTrackedExternalHost(host);
        }

        private IWorkbenchRuntimeUiFactory ResolveRuntimeUiFactory(UnityRenderHostCatalog catalog, string launchSignature)
        {
            if (catalog != null &&
                string.Equals(catalog.EffectiveRenderHostId, UnityRenderHostSettings.AvaloniaExternalRenderHostId, System.StringComparison.OrdinalIgnoreCase) &&
                string.Equals(_suppressedLaunchSignature, launchSignature, StringComparison.OrdinalIgnoreCase))
            {
                return ImguiWorkbenchRuntimeUiComposition.CreateRuntimeUiFactory(_frameContext);
            }

            return UnityWorkbenchRuntimeUiFactorySelector.Select(catalog, _frameContext);
        }

        private bool ShouldRelaunchExternalHost(UnityRenderHostCatalog catalog, string launchSignature)
        {
            if (catalog == null || !catalog.ShouldLaunchAvaloniaExternalHost)
            {
                return false;
            }

            return _externalHostSession == null ||
                !IsTrackedExternalHostAlive() ||
                !string.Equals(_externalHostSession.LaunchSignature, launchSignature, StringComparison.OrdinalIgnoreCase);
        }

        private static string BuildLaunchSignature(UnityExternalHostLaunchRequest request)
        {
            if (request == null)
            {
                return string.Empty;
            }

            return
                (request.CommandPath ?? string.Empty) + "|" +
                (request.Arguments ?? string.Empty) + "|" +
                (request.WorkingDirectory ?? string.Empty);
        }

        private static UnityExternalHostLaunchRequest CloneRequest(UnityExternalHostLaunchRequest request, string launchToken)
        {
            if (request == null)
            {
                return new UnityExternalHostLaunchRequest();
            }

            return new UnityExternalHostLaunchRequest
            {
                CommandPath = request.CommandPath,
                Arguments = request.Arguments,
                WorkingDirectory = request.WorkingDirectory,
                SuccessStatusMessage = request.SuccessStatusMessage,
                FailureReason = request.FailureReason,
                LaunchToken = launchToken ?? string.Empty
            };
        }

        private void TryStopTrackedExternalHost(IUnityRenderPresentationHost host)
        {
            if (_externalHostSession == null)
            {
                return;
            }

            try
            {
                Log.WriteInfo(
                    "Stopping tracked external overlay host. ProcessId=" + _externalHostSession.ProcessId +
                    ", LaunchToken=" + (_externalHostSession.LaunchToken ?? string.Empty) + ".");
                host.RequestExternalHostShutdown();
                var process = Process.GetProcessById(_externalHostSession.ProcessId);
                if (process != null && !process.HasExited)
                {
                    process.WaitForExit(1500);
                    if (!process.HasExited)
                    {
                        Log.WriteWarning("External overlay host did not exit after graceful shutdown request. Killing process " + _externalHostSession.ProcessId + ".");
                        process.Kill();
                    }
                }
            }
            catch
            {
            }
            finally
            {
                _externalHostSession = null;
            }
        }

        private bool IsTrackedExternalHostAlive()
        {
            if (_externalHostSession == null || _externalHostSession.ProcessId <= 0)
            {
                return false;
            }

            try
            {
                var process = Process.GetProcessById(_externalHostSession.ProcessId);
                if (process == null || process.HasExited)
                {
                    return false;
                }

                return process.StartTime.ToUniversalTime() == _externalHostSession.ProcessStartTimeUtc;
            }
            catch
            {
                return false;
            }
        }

        private void LogCatalogDecision(UnityRenderHostCatalog catalog)
        {
            var summary = BuildCatalogLogSummary(catalog);
            if (string.Equals(_lastCatalogLogSummary, summary, StringComparison.Ordinal))
            {
                return;
            }

            _lastCatalogLogSummary = summary;
            if (catalog != null &&
                string.Equals(catalog.SelectedRenderHostId, UnityRenderHostSettings.AvaloniaExternalRenderHostId, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(catalog.EffectiveRenderHostId, UnityRenderHostSettings.AvaloniaExternalRenderHostId, StringComparison.OrdinalIgnoreCase))
            {
                Log.WriteWarning(summary);
                return;
            }

            Log.WriteInfo(summary);
        }

        private static string BuildCatalogLogSummary(UnityRenderHostCatalog catalog)
        {
            if (catalog == null)
            {
                return "Render host catalog was null. Falling back to IMGUI.";
            }

            var request = catalog.AvaloniaLaunchRequest ?? new UnityExternalHostLaunchRequest();
            return
                "Presentation decision. Selected=" + (catalog.SelectedRenderHostId ?? string.Empty) +
                ", Effective=" + (catalog.EffectiveRenderHostId ?? string.Empty) +
                ", Launchable=" + request.CanLaunch +
                ", ShouldLaunchExternal=" + catalog.ShouldLaunchAvaloniaExternalHost +
                ", Command=" + (request.CommandPath ?? string.Empty) +
                ", WorkingDirectory=" + (request.WorkingDirectory ?? string.Empty) +
                ", FailureReason=" + (request.FailureReason ?? string.Empty) + ".";
        }

        private sealed class UnityExternalHostSession
        {
            public UnityExternalHostSession(string launchSignature, string launchToken, int processId, DateTime processStartTimeUtc)
            {
                LaunchSignature = launchSignature ?? string.Empty;
                LaunchToken = launchToken ?? string.Empty;
                ProcessId = processId;
                ProcessStartTimeUtc = processStartTimeUtc;
            }

            public string LaunchSignature { get; private set; }
            public string LaunchToken { get; private set; }
            public int ProcessId { get; private set; }
            public DateTime ProcessStartTimeUtc { get; private set; }
        }
    }
}
