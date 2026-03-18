using ModAPI.InputActions;
using UnityEngine;

namespace Cortex
{
    public sealed partial class CortexShell :
        Shell.ICortexShellLifecycleHost
    {
        GameObject Shell.ICortexShellLifecycleHost.ShellGameObject
        {
            get { return gameObject; }
        }

        bool Shell.ICortexShellLifecycleHost.ReloadSettingsRequested
        {
            get { return _state.ReloadSettingsRequested; }
        }

        string Shell.ICortexShellLifecycleHost.RequestedContainerId
        {
            get { return _state.Workbench.RequestedContainerId; }
            set { _state.Workbench.RequestedContainerId = value; }
        }

        int Shell.ICortexShellLifecycleHost.RequestedTabIndex
        {
            get { return _state.Workbench.RequestedTabIndex; }
            set { _state.Workbench.RequestedTabIndex = value; }
        }

        void Shell.ICortexShellLifecycleHost.InitializeSettingsAndServices() { InitializeSettingsAndServices(); }
        void Shell.ICortexShellLifecycleHost.RestoreWorkbenchSession() { RestoreWorkbenchSession(); }
        void Shell.ICortexShellLifecycleHost.InitializeWorkbenchRuntime() { InitializeWorkbenchRuntime(); }
        void Shell.ICortexShellLifecycleHost.RegisterCommandHandlers() { RegisterCommandHandlers(); }
        void Shell.ICortexShellLifecycleHost.RegisterToggleAction() { RegisterToggleAction(); }
        void Shell.ICortexShellLifecycleHost.ReleaseOverlayInputCapture() { ReleaseOverlayInputCapture(); }
        void Shell.ICortexShellLifecycleHost.ShutdownLanguageService() { ShutdownLanguageService(); }
        void Shell.ICortexShellLifecycleHost.ShutdownCompletionAugmentation() { ShutdownCompletionAugmentation(); }
        void Shell.ICortexShellLifecycleHost.DisableMmLogRuntimeIntegration() { DisableMmLogRuntimeIntegration(); }
        void Shell.ICortexShellLifecycleHost.PersistWorkbenchSession() { PersistWorkbenchSession(); }
        void Shell.ICortexShellLifecycleHost.PersistWindowSettings() { PersistWindowSettings(); }

        bool Shell.ICortexShellLifecycleHost.IsToggleActionPressed()
        {
            return InputActionRegistry.IsDown(ToggleActionId);
        }

        void Shell.ICortexShellLifecycleHost.ExecuteShellToggle()
        {
            ExecuteCommand("cortex.shell.toggle", null);
        }

        void Shell.ICortexShellLifecycleHost.ApplySettingsChanges() { ApplySettingsChanges(); }
        void Shell.ICortexShellLifecycleHost.UpdateLanguageService() { UpdateLanguageService(); }
        void Shell.ICortexShellLifecycleHost.ActivateContainer(string containerId) { ActivateContainer(containerId); }
        string Shell.ICortexShellLifecycleHost.MapLegacyTabIndex(int index) { return MapLegacyTabIndex(index); }
        void Shell.ICortexShellLifecycleHost.RenderVisibleShell() { RenderVisibleShell(); }
    }
}
