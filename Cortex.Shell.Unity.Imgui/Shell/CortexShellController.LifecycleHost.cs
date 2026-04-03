using Cortex.Shell;

namespace Cortex
{
    public sealed partial class CortexShellController :
        ICortexShellControllerLifecycleHost
    {
        bool ICortexShellControllerLifecycleHost.ReloadSettingsRequested
        {
            get { return _state.ReloadSettingsRequested; }
        }

        string ICortexShellControllerLifecycleHost.RequestedContainerId
        {
            get { return _state.Workbench.RequestedContainerId; }
            set { _state.Workbench.RequestedContainerId = value; }
        }

        int ICortexShellControllerLifecycleHost.RequestedTabIndex
        {
            get { return _state.Workbench.RequestedTabIndex; }
            set { _state.Workbench.RequestedTabIndex = value; }
        }

        void ICortexShellControllerLifecycleHost.InitializeSettingsAndServices() { InitializeSettingsAndServices(); }
        void ICortexShellControllerLifecycleHost.RestoreWorkbenchSession() { RestoreWorkbenchSession(); }
        void ICortexShellControllerLifecycleHost.InitializeWorkbenchRuntime() { InitializeWorkbenchRuntime(); }
        void ICortexShellControllerLifecycleHost.RegisterCommandHandlers() { RegisterCommandHandlers(); }
        void ICortexShellControllerLifecycleHost.RegisterToggleAction() { RegisterToggleAction(); }
        void ICortexShellControllerLifecycleHost.ReleaseOverlayInputCapture() { ReleaseOverlayInputCapture(); }
        void ICortexShellControllerLifecycleHost.ShutdownLanguageService() { ShutdownLanguageService(); }
        void ICortexShellControllerLifecycleHost.ShutdownCompletionAugmentation() { ShutdownCompletionAugmentation(); }
        void ICortexShellControllerLifecycleHost.DisableRuntimeLogIntegration() { DisableRuntimeLogIntegration(); }
        void ICortexShellControllerLifecycleHost.PersistWorkbenchSession() { PersistWorkbenchSession(); }
        void ICortexShellControllerLifecycleHost.PersistWindowSettings() { PersistWindowSettings(); }

        bool ICortexShellControllerLifecycleHost.IsToggleActionPressed()
        {
            return (_platformModule ?? NullCortexPlatformModule.Instance).IsShellTogglePressed(ToggleActionId);
        }

        void ICortexShellControllerLifecycleHost.ExecuteShellToggle()
        {
            ExecuteCommand("cortex.shell.toggle", null);
        }

        void ICortexShellControllerLifecycleHost.ApplySettingsChanges() { ApplySettingsChanges(); }
        void ICortexShellControllerLifecycleHost.UpdateLanguageService() { UpdateLanguageService(); }
        void ICortexShellControllerLifecycleHost.ActivateContainer(string containerId) { ActivateContainer(containerId); }
        string ICortexShellControllerLifecycleHost.MapLegacyTabIndex(int index) { return MapLegacyTabIndex(index); }
        void ICortexShellControllerLifecycleHost.RenderVisibleShell() { RenderVisibleShell(); }
    }
}
