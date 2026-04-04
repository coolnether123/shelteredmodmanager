using System;

namespace Cortex.Shell.Shared.Models
{
    [Serializable]
    public sealed class ShellSettings
    {
        public string WorkspaceRootPath { get; set; } = string.Empty;
        public string RuntimeContentRootPath { get; set; } = string.Empty;
        public string ReferenceAssemblyRootPath { get; set; } = string.Empty;
        public string AdditionalSourceRoots { get; set; } = string.Empty;
        public string ThemeId { get; set; } = "cortex.vs-dark";
        public string DefaultOnboardingProfileId { get; set; } = "cortex.onboarding.profile.ide";
        public string DefaultOnboardingLayoutPresetId { get; set; } = "cortex.onboarding.layout.visual-studio";
        public string DefaultOnboardingThemeId { get; set; } = "cortex.vs-dark";
        public string DefaultBuildConfiguration { get; set; } = "Debug";
        public int BuildTimeoutMs { get; set; } = 300000;
        public bool EnableFileEditing { get; set; }
        public bool EnableFileSaving { get; set; }
        public int EditorUndoHistoryLimit { get; set; } = 128;
        public string SettingsActiveSectionId { get; set; } = string.Empty;
        public string SettingsSearchQuery { get; set; } = string.Empty;
        public bool SettingsShowModifiedOnly { get; set; }
        public bool HasCompletedOnboarding { get; set; }
    }
}
