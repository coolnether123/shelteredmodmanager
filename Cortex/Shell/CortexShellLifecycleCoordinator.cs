using System;

namespace Cortex
{
    internal sealed class CortexShellLifecycleCoordinator
    {
        private bool _initialized;

        public void Start(ICortexShellControllerLifecycleHost host)
        {
            if (_initialized)
            {
                return;
            }

            try
            {
                host.InitializeSettingsAndServices();
                host.RestoreWorkbenchSession();
                host.InitializeWorkbenchRuntime();
                host.RegisterCommandHandlers();
                host.RegisterToggleAction();
                _initialized = true;
            }
            catch (Exception ex)
            {
                MMLog.WriteError("[Cortex] Start failed: " + ex);
                throw;
            }
        }

        public void Destroy(ICortexShellControllerLifecycleHost host)
        {
            if (!_initialized)
            {
                return;
            }

            host.ReleaseOverlayInputCapture();
            host.ShutdownLanguageService();
            host.ShutdownCompletionAugmentation();
            host.DisableRuntimeLogIntegration();
            host.PersistWorkbenchSession();
            host.PersistWindowSettings();
        }

        public void Update(ICortexShellControllerLifecycleHost host)
        {
            if (!_initialized)
            {
                return;
            }

            if (host.IsToggleActionPressed())
            {
                host.ExecuteShellToggle();
            }

            if (host.ReloadSettingsRequested)
            {
                host.ApplySettingsChanges();
            }

            host.UpdateLanguageService();

            if (!string.IsNullOrEmpty(host.RequestedContainerId))
            {
                host.ActivateContainer(host.RequestedContainerId);
                host.RequestedContainerId = string.Empty;
            }
            else if (host.RequestedTabIndex >= 0)
            {
                host.ActivateContainer(host.MapLegacyTabIndex(host.RequestedTabIndex));
                host.RequestedTabIndex = -1;
            }
        }

        public void OnGui(ICortexShellControllerLifecycleHost host)
        {
            if (!_initialized)
            {
                return;
            }

            host.RenderVisibleShell();
        }
    }
}
