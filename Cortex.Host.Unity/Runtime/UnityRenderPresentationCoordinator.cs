using Cortex;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.Rendering.RuntimeUi;
using Cortex.Shell.Unity.Imgui;
using System.Diagnostics;

namespace Cortex.Host.Unity.Runtime
{
    internal interface IUnityRenderPresentationHost
    {
        CortexSettings Settings { get; }
        void ApplyStatusMessage(string statusMessage);
        bool ApplyRuntimeUiFactory(IWorkbenchRuntimeUiFactory runtimeUiFactory);
        void SetShellVisible(bool visible);
        void RegisterOrUpdateStatusItem(StatusItemContribution contribution);
        void RegisterOrUpdateSettingContribution(SettingContribution contribution);
    }

    internal sealed class UnityRenderPresentationCoordinator
    {
        private readonly UnityRenderHostCatalogBuilder _catalogBuilder;
        private readonly IWorkbenchFrameContext _frameContext;
        private readonly UnityExternalHostLauncher _externalHostLauncher;

        private string _lastSelectedRenderHostId = string.Empty;
        private string _lastEffectiveRenderHostId = string.Empty;
        private string _lastExternalLaunchSignature = string.Empty;
        private int _externalHostProcessId;
        private bool _autoHiddenShellForExternalPresentation;

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
            if (!selectedChanged && !effectiveChanged)
            {
                return;
            }

            host.ApplyRuntimeUiFactory(ResolveRuntimeUiFactory(catalog));

            if (!string.IsNullOrEmpty(catalog.StartupStatusMessage))
            {
                host.ApplyStatusMessage(catalog.StartupStatusMessage);
            }

            if (catalog.ShouldLaunchAvaloniaExternalHost)
            {
                var launchSignature = BuildLaunchSignature(catalog.AvaloniaLaunchRequest);
                if (!string.Equals(_lastExternalLaunchSignature, launchSignature, System.StringComparison.OrdinalIgnoreCase) ||
                    !IsTrackedExternalHostAlive())
                {
                    var launchResult = _externalHostLauncher.Launch(catalog.AvaloniaLaunchRequest);
                    host.ApplyStatusMessage(launchResult.StatusMessage);
                    if (launchResult.Launched)
                    {
                        _lastExternalLaunchSignature = launchSignature;
                        _externalHostProcessId = launchResult.ProcessId;
                        host.SetShellVisible(false);
                        _autoHiddenShellForExternalPresentation = true;
                    }
                }
            }
            else if (_autoHiddenShellForExternalPresentation)
            {
                TryStopTrackedExternalHost();
                _lastExternalLaunchSignature = string.Empty;
                host.SetShellVisible(true);
                _autoHiddenShellForExternalPresentation = false;
            }
            else if (!catalog.ShouldLaunchAvaloniaExternalHost)
            {
                TryStopTrackedExternalHost();
                _lastExternalLaunchSignature = string.Empty;
            }

            _lastSelectedRenderHostId = catalog.SelectedRenderHostId ?? string.Empty;
            _lastEffectiveRenderHostId = catalog.EffectiveRenderHostId ?? string.Empty;
        }

        private IWorkbenchRuntimeUiFactory ResolveRuntimeUiFactory(UnityRenderHostCatalog catalog)
        {
            if (catalog != null &&
                string.Equals(catalog.EffectiveRenderHostId, UnityRenderHostSettings.AvaloniaExternalRenderHostId, System.StringComparison.OrdinalIgnoreCase))
            {
                return ImguiWorkbenchRuntimeUiComposition.CreateRuntimeUiFactory(_frameContext);
            }

            return UnityWorkbenchRuntimeUiFactorySelector.Select(catalog, _frameContext);
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

        private void TryStopTrackedExternalHost()
        {
            if (_externalHostProcessId <= 0)
            {
                return;
            }

            try
            {
                var process = Process.GetProcessById(_externalHostProcessId);
                if (process != null && !process.HasExited)
                {
                    process.Kill();
                }
            }
            catch
            {
            }
            finally
            {
                _externalHostProcessId = 0;
            }
        }

        private bool IsTrackedExternalHostAlive()
        {
            if (_externalHostProcessId <= 0)
            {
                return false;
            }

            try
            {
                var process = Process.GetProcessById(_externalHostProcessId);
                return process != null && !process.HasExited;
            }
            catch
            {
                return false;
            }
        }
    }
}
