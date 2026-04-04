namespace Cortex.Shell.Shared.Models
{
    public enum ShellSettingValueKind
    {
        String = 0,
        Integer = 1,
        Boolean = 2
    }

    public enum ShellSettingEditorKind
    {
        Text = 0,
        Path = 1,
        Choice = 2,
        Secret = 3,
        MultilineText = 4
    }

    public sealed class ThemeDescriptor
    {
        public string ThemeId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool IsDefault { get; set; }
        public bool ShowInOnboarding { get; set; } = true;
        public int SortOrder { get; set; }
        public string BackgroundColor { get; set; } = "#1E1E1E";
        public string SurfaceColor { get; set; } = "#252526";
        public string HeaderColor { get; set; } = "#2D2D30";
        public string BorderColor { get; set; } = "#3F3F46";
        public string AccentColor { get; set; } = "#007ACC";
        public string TextColor { get; set; } = "#D4D4D4";
        public string MutedTextColor { get; set; } = "#858585";
    }

    public sealed class OnboardingProfileDescriptor
    {
        public string ProfileId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string WorkflowKind { get; set; } = string.Empty;
        public string DefaultLayoutPresetId { get; set; } = string.Empty;
        public string DefaultThemeId { get; set; } = string.Empty;
        public string[] PreviewTags { get; set; } = new string[0];
        public bool IsDefault { get; set; }
        public int SortOrder { get; set; }
    }

    public sealed class OnboardingLayoutDescriptor
    {
        public string LayoutPresetId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string DefaultThemeId { get; set; } = string.Empty;
        public string PreviewPrimaryLabel { get; set; } = string.Empty;
        public string PreviewSecondaryLabel { get; set; } = string.Empty;
        public string PreviewCenterLabel { get; set; } = string.Empty;
        public string PreviewPanelLabel { get; set; } = string.Empty;
        public bool IsDefault { get; set; }
        public int SortOrder { get; set; }
    }

    public sealed class SettingChoiceDescriptor
    {
        public string Value { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    public sealed class SettingSectionDescriptor
    {
        public string SectionId { get; set; } = string.Empty;
        public string GroupId { get; set; } = string.Empty;
        public string GroupTitle { get; set; } = string.Empty;
        public string Scope { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string[] Keywords { get; set; } = new string[0];
        public int SortOrder { get; set; }
    }

    public sealed class SettingDescriptor
    {
        public string SettingId { get; set; } = string.Empty;
        public string SectionId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Scope { get; set; } = string.Empty;
        public string DefaultValue { get; set; } = string.Empty;
        public ShellSettingValueKind ValueKind { get; set; } = ShellSettingValueKind.String;
        public ShellSettingEditorKind EditorKind { get; set; } = ShellSettingEditorKind.Text;
        public string PlaceholderText { get; set; } = string.Empty;
        public string HelpText { get; set; } = string.Empty;
        public string[] Keywords { get; set; } = new string[0];
        public SettingChoiceDescriptor[] Options { get; set; } = new SettingChoiceDescriptor[0];
        public bool IsRequired { get; set; }
        public bool IsSecret { get; set; }
        public int SortOrder { get; set; }
    }

    public sealed class EditorDescriptor
    {
        public string EditorId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public int SortOrder { get; set; }
    }

    public sealed class WorkbenchCatalogSnapshot
    {
        public System.Collections.Generic.List<ThemeDescriptor> Themes { get; set; } = new System.Collections.Generic.List<ThemeDescriptor>();
        public System.Collections.Generic.List<OnboardingProfileDescriptor> OnboardingProfiles { get; set; } = new System.Collections.Generic.List<OnboardingProfileDescriptor>();
        public System.Collections.Generic.List<OnboardingLayoutDescriptor> OnboardingLayouts { get; set; } = new System.Collections.Generic.List<OnboardingLayoutDescriptor>();
        public System.Collections.Generic.List<SettingSectionDescriptor> SettingSections { get; set; } = new System.Collections.Generic.List<SettingSectionDescriptor>();
        public System.Collections.Generic.List<SettingDescriptor> Settings { get; set; } = new System.Collections.Generic.List<SettingDescriptor>();
        public System.Collections.Generic.List<EditorDescriptor> Editors { get; set; } = new System.Collections.Generic.List<EditorDescriptor>();
    }
}
