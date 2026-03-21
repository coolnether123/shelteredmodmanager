using Cortex.Services;
using UnityEngine;

namespace Cortex
{
    public sealed partial class CortexShell :
        ICortexShellLifecycleHost
    {
        GameObject ICortexShellLifecycleHost.ShellGameObject
        {
            get { return gameObject; }
        }

        bool ICortexShellLifecycleHost.ReloadSettingsRequested
        {
            get { return _state.ReloadSettingsRequested; }
        }

        string ICortexShellLifecycleHost.RequestedContainerId
        {
            get { return _state.Workbench.RequestedContainerId; }
            set { _state.Workbench.RequestedContainerId = value; }
        }

        int ICortexShellLifecycleHost.RequestedTabIndex
        {
            get { return _state.Workbench.RequestedTabIndex; }
            set { _state.Workbench.RequestedTabIndex = value; }
        }

        void ICortexShellLifecycleHost.InitializeSettingsAndServices() { InitializeSettingsAndServices(); }
        void ICortexShellLifecycleHost.RestoreWorkbenchSession() { RestoreWorkbenchSession(); }
        void ICortexShellLifecycleHost.InitializeWorkbenchRuntime() { InitializeWorkbenchRuntime(); }
        void ICortexShellLifecycleHost.RegisterCommandHandlers() { RegisterCommandHandlers(); }
        void ICortexShellLifecycleHost.RegisterToggleAction() { RegisterToggleAction(); }
        void ICortexShellLifecycleHost.ReleaseOverlayInputCapture() { ReleaseOverlayInputCapture(); }
        void ICortexShellLifecycleHost.ShutdownLanguageService() { ShutdownLanguageService(); }
        void ICortexShellLifecycleHost.ShutdownCompletionAugmentation() { ShutdownCompletionAugmentation(); }
        void ICortexShellLifecycleHost.DisableRuntimeLogIntegration() { DisableRuntimeLogIntegration(); }
        void ICortexShellLifecycleHost.PersistWorkbenchSession() { PersistWorkbenchSession(); }
        void ICortexShellLifecycleHost.PersistWindowSettings() { PersistWindowSettings(); }

        bool ICortexShellLifecycleHost.IsToggleActionPressed()
        {
            return (_platformModule ?? NullCortexPlatformModule.Instance).IsShellTogglePressed(ToggleActionId);
        }

        void ICortexShellLifecycleHost.ExecuteShellToggle()
        {
            ExecuteCommand("cortex.shell.toggle", null);
        }

        void ICortexShellLifecycleHost.ApplySettingsChanges() { ApplySettingsChanges(); }
        void ICortexShellLifecycleHost.UpdateLanguageService() { UpdateLanguageService(); }
        void ICortexShellLifecycleHost.ActivateContainer(string containerId) { ActivateContainer(containerId); }
        string ICortexShellLifecycleHost.MapLegacyTabIndex(int index) { return MapLegacyTabIndex(index); }
        void ICortexShellLifecycleHost.RenderVisibleShell() { RenderVisibleShell(); }
    }
}
