using System;
using ModAPI.Core;
using UnityEngine;

namespace Cortex.Shell
{
    internal interface ICortexShellLifecycleHost
    {
        GameObject ShellGameObject { get; }
        bool ReloadSettingsRequested { get; }
        string RequestedContainerId { get; set; }
        int RequestedTabIndex { get; set; }

        void InitializeSettingsAndServices();
        void RestoreWorkbenchSession();
        void InitializeWorkbenchRuntime();
        void RegisterCommandHandlers();
        void RegisterToggleAction();
        void ReleaseOverlayInputCapture();
        void ShutdownLanguageService();
        void ShutdownCompletionAugmentation();
        void DisableMmLogRuntimeIntegration();
        void PersistWorkbenchSession();
        void PersistWindowSettings();
        bool IsToggleActionPressed();
        void ExecuteShellToggle();
        void ApplySettingsChanges();
        void UpdateLanguageService();
        void ActivateContainer(string containerId);
        string MapLegacyTabIndex(int index);
        void RenderVisibleShell();
    }

    internal sealed class CortexShellLifecycleCoordinator
    {
        public void Awake(ICortexShellLifecycleHost host)
        {
            try
            {
                host.ShellGameObject.name = "Cortex.Shell";
                UnityEngine.Object.DontDestroyOnLoad(host.ShellGameObject);
                host.InitializeSettingsAndServices();
                host.RestoreWorkbenchSession();
                host.InitializeWorkbenchRuntime();
                host.RegisterCommandHandlers();
                host.RegisterToggleAction();
            }
            catch (Exception ex)
            {
                MMLog.WriteError("[Cortex] Awake failed: " + ex);
                throw;
            }
        }

        public void Destroy(ICortexShellLifecycleHost host)
        {
            host.ReleaseOverlayInputCapture();
            host.ShutdownLanguageService();
            host.ShutdownCompletionAugmentation();
            host.DisableMmLogRuntimeIntegration();
            host.PersistWorkbenchSession();
            host.PersistWindowSettings();
        }

        public void Update(ICortexShellLifecycleHost host)
        {
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

        public void OnGui(ICortexShellLifecycleHost host)
        {
            host.RenderVisibleShell();
        }
    }
}
