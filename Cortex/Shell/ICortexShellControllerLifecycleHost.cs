namespace Cortex
{
    internal interface ICortexShellControllerLifecycleHost
    {
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
        void DisableRuntimeLogIntegration();
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
}
