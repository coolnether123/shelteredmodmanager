using Cortex.Bridge;

namespace Cortex.Shell.Bridge
{
    internal sealed class RuntimeDesktopBridgeSnapshotBuilder
    {
        private readonly RuntimeDesktopBridgeSettingsFeature _settingsFeature;
        private readonly RuntimeDesktopBridgeWorkspaceFeature _workspaceFeature;
        private readonly RuntimeDesktopBridgeWorkbenchFeature _workbenchFeature;

        public RuntimeDesktopBridgeSnapshotBuilder(
            RuntimeDesktopBridgeSettingsFeature settingsFeature,
            RuntimeDesktopBridgeWorkspaceFeature workspaceFeature,
            RuntimeDesktopBridgeWorkbenchFeature workbenchFeature)
        {
            _settingsFeature = settingsFeature;
            _workspaceFeature = workspaceFeature;
            _workbenchFeature = workbenchFeature;
        }

        public WorkbenchBridgeSnapshot Build(string statusMessage)
        {
            return new WorkbenchBridgeSnapshot
            {
                WorkbenchId = "default",
                ActiveLayoutPresetId = _settingsFeature.ResolveActiveLayoutPresetId(),
                StatusMessage = statusMessage ?? string.Empty,
                RuntimeConnectionState = "connected",
                Catalog = _settingsFeature.Catalog,
                Onboarding = _settingsFeature.BuildOnboardingState(),
                OnboardingFlow = _settingsFeature.BuildOnboardingFlow(),
                ThemePreviewSummary = _settingsFeature.BuildThemePreviewSummary(),
                Settings = _settingsFeature.BuildSnapshot(),
                Workspace = _workspaceFeature.BuildSnapshot(),
                Editor = _workbenchFeature.BuildEditorSnapshot(),
                Search = _workbenchFeature.BuildSearchSnapshot(),
                Reference = _workbenchFeature.BuildReferenceSnapshot()
            };
        }
    }
}
