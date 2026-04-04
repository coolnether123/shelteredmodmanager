using Cortex.Shell.Shared.Models;

namespace Cortex.Shell.Shared.Services
{
    public static class WorkbenchCatalogFactory
    {
        public static WorkbenchCatalogSnapshot CreateDefaultCatalog()
        {
            var catalog = new WorkbenchCatalogSnapshot();

            catalog.Themes.Add(new ThemeDescriptor
            {
                ThemeId = "cortex.vs-dark",
                DisplayName = "VS Dark",
                Description = "Default dark desktop palette.",
                IsDefault = true,
                SortOrder = 0,
                BackgroundColor = "#181A21",
                SurfaceColor = "#22252F",
                HeaderColor = "#262A35",
                BorderColor = "#3A4050",
                AccentColor = "#4DA3FF",
                TextColor = "#E6E8EE",
                MutedTextColor = "#9EA7B8"
            });
            catalog.Themes.Add(new ThemeDescriptor
            {
                ThemeId = "cortex.vs-light",
                DisplayName = "VS Light",
                Description = "Light desktop palette.",
                SortOrder = 10,
                BackgroundColor = "#F4F6FB",
                SurfaceColor = "#FFFFFF",
                HeaderColor = "#E5EAF5",
                BorderColor = "#C5CEDD",
                AccentColor = "#0A70E8",
                TextColor = "#1D2433",
                MutedTextColor = "#5B667D"
            });
            catalog.Themes.Add(new ThemeDescriptor
            {
                ThemeId = "cortex.steel",
                DisplayName = "Steel",
                Description = "Cool blue-gray desktop palette.",
                SortOrder = 20,
                BackgroundColor = "#10151C",
                SurfaceColor = "#18212B",
                HeaderColor = "#1E2935",
                BorderColor = "#35506B",
                AccentColor = "#53C5B9",
                TextColor = "#E8F1F7",
                MutedTextColor = "#8CA3B5"
            });

            catalog.OnboardingProfiles.Add(new OnboardingProfileDescriptor
            {
                ProfileId = "cortex.onboarding.profile.ide",
                DisplayName = "IDE",
                Description = "Balanced settings, workspace, and file-preview posture.",
                WorkflowKind = "standard",
                DefaultLayoutPresetId = "cortex.onboarding.layout.visual-studio",
                DefaultThemeId = "cortex.vs-dark",
                PreviewTags = new[] { "editor", "workspace", "settings" },
                IsDefault = true,
                SortOrder = 0
            });
            catalog.OnboardingProfiles.Add(new OnboardingProfileDescriptor
            {
                ProfileId = "cortex.onboarding.profile.modder",
                DisplayName = "Modder",
                Description = "Workspace-first posture for source roots and project discovery.",
                WorkflowKind = "modder",
                DefaultLayoutPresetId = "cortex.onboarding.layout.focus",
                DefaultThemeId = "cortex.steel",
                PreviewTags = new[] { "workspace", "projects", "files" },
                SortOrder = 10
            });

            catalog.OnboardingLayouts.Add(new OnboardingLayoutDescriptor
            {
                LayoutPresetId = "cortex.onboarding.layout.visual-studio",
                DisplayName = "Workbench",
                Description = "Balanced desktop workbench layout.",
                DefaultThemeId = "cortex.vs-dark",
                PreviewPrimaryLabel = "Projects",
                PreviewSecondaryLabel = "Settings",
                PreviewCenterLabel = "Editor",
                PreviewPanelLabel = "Status",
                IsDefault = true,
                SortOrder = 0
            });
            catalog.OnboardingLayouts.Add(new OnboardingLayoutDescriptor
            {
                LayoutPresetId = "cortex.onboarding.layout.focus",
                DisplayName = "Focus",
                Description = "Workspace tree and preview first.",
                DefaultThemeId = "cortex.steel",
                PreviewPrimaryLabel = "Tree",
                PreviewSecondaryLabel = "Preview",
                PreviewCenterLabel = "Workspace",
                PreviewPanelLabel = "Log",
                SortOrder = 10
            });

            catalog.SettingSections.Add(new SettingSectionDescriptor
            {
                SectionId = "settings.workspace.paths",
                GroupId = "workspace",
                GroupTitle = "Workspace",
                Scope = "Workspace",
                Title = "Workspace Paths",
                Description = "Configure the active workspace and reference roots.",
                Keywords = new[] { "workspace", "paths", "roots", "runtime", "reference" },
                SortOrder = 0
            });
            catalog.SettingSections.Add(new SettingSectionDescriptor
            {
                SectionId = "settings.onboarding.defaults",
                GroupId = "shell",
                GroupTitle = "Shell",
                Scope = "Onboarding",
                Title = "Onboarding Defaults",
                Description = "Default onboarding profile, layout, and theme.",
                Keywords = new[] { "onboarding", "profile", "layout", "theme" },
                SortOrder = 10
            });
            catalog.SettingSections.Add(new SettingSectionDescriptor
            {
                SectionId = "settings.appearance.theme",
                GroupId = "appearance",
                GroupTitle = "Appearance",
                Scope = "Appearance",
                Title = "Theme",
                Description = "Choose the active desktop theme.",
                Keywords = new[] { "theme", "appearance", "colors" },
                SortOrder = 20
            });
            catalog.SettingSections.Add(new SettingSectionDescriptor
            {
                SectionId = "settings.editor.behavior",
                GroupId = "editor",
                GroupTitle = "Editor",
                Scope = "Editing",
                Title = "Editor Behavior",
                Description = "Editability and undo defaults for the desktop host.",
                Keywords = new[] { "editor", "undo", "save", "editing" },
                SortOrder = 30
            });
            catalog.SettingSections.Add(new SettingSectionDescriptor
            {
                SectionId = "settings.build.defaults",
                GroupId = "tooling",
                GroupTitle = "Tooling",
                Scope = "Build",
                Title = "Build Defaults",
                Description = "Default build configuration and timeout.",
                Keywords = new[] { "build", "timeout", "configuration" },
                SortOrder = 40
            });

            catalog.Settings.Add(new SettingDescriptor
            {
                SettingId = nameof(ShellSettings.WorkspaceRootPath),
                SectionId = "settings.workspace.paths",
                DisplayName = "Workspace Root",
                Description = "Root folder used for project discovery and file browsing.",
                Scope = "Workspace",
                EditorKind = ShellSettingEditorKind.Path,
                PlaceholderText = @"D:\Projects\Workspace",
                Keywords = new[] { "workspace", "root", "source" },
                SortOrder = 0
            });
            catalog.Settings.Add(new SettingDescriptor
            {
                SettingId = nameof(ShellSettings.RuntimeContentRootPath),
                SectionId = "settings.workspace.paths",
                DisplayName = "Runtime Content Root",
                Description = "Optional runtime content folder for future host assets.",
                Scope = "Workspace",
                EditorKind = ShellSettingEditorKind.Path,
                PlaceholderText = @"D:\RuntimeContent",
                Keywords = new[] { "runtime", "content" },
                SortOrder = 10
            });
            catalog.Settings.Add(new SettingDescriptor
            {
                SettingId = nameof(ShellSettings.ReferenceAssemblyRootPath),
                SectionId = "settings.workspace.paths",
                DisplayName = "Reference Assembly Root",
                Description = "Optional assembly root for future metadata navigation.",
                Scope = "Workspace",
                EditorKind = ShellSettingEditorKind.Path,
                PlaceholderText = @"D:\Managed",
                Keywords = new[] { "assemblies", "references" },
                SortOrder = 20
            });
            catalog.Settings.Add(new SettingDescriptor
            {
                SettingId = nameof(ShellSettings.ThemeId),
                SectionId = "settings.appearance.theme",
                DisplayName = "Theme",
                Description = "Active desktop theme identifier.",
                Scope = "Appearance",
                EditorKind = ShellSettingEditorKind.Choice,
                Options = new[]
                {
                    new SettingChoiceDescriptor { Value = "cortex.vs-dark", DisplayName = "VS Dark" },
                    new SettingChoiceDescriptor { Value = "cortex.vs-light", DisplayName = "VS Light" },
                    new SettingChoiceDescriptor { Value = "cortex.steel", DisplayName = "Steel" }
                },
                SortOrder = 0
            });
            catalog.Settings.Add(new SettingDescriptor
            {
                SettingId = nameof(ShellSettings.DefaultOnboardingProfileId),
                SectionId = "settings.onboarding.defaults",
                DisplayName = "Default Profile",
                Description = "Profile selected when onboarding starts.",
                Scope = "Onboarding",
                EditorKind = ShellSettingEditorKind.Choice,
                Options = new[]
                {
                    new SettingChoiceDescriptor { Value = "cortex.onboarding.profile.ide", DisplayName = "IDE" },
                    new SettingChoiceDescriptor { Value = "cortex.onboarding.profile.modder", DisplayName = "Modder" }
                },
                SortOrder = 0
            });
            catalog.Settings.Add(new SettingDescriptor
            {
                SettingId = nameof(ShellSettings.DefaultOnboardingLayoutPresetId),
                SectionId = "settings.onboarding.defaults",
                DisplayName = "Default Layout",
                Description = "Layout selected when onboarding starts.",
                Scope = "Onboarding",
                EditorKind = ShellSettingEditorKind.Choice,
                Options = new[]
                {
                    new SettingChoiceDescriptor { Value = "cortex.onboarding.layout.visual-studio", DisplayName = "Workbench" },
                    new SettingChoiceDescriptor { Value = "cortex.onboarding.layout.focus", DisplayName = "Focus" }
                },
                SortOrder = 10
            });
            catalog.Settings.Add(new SettingDescriptor
            {
                SettingId = nameof(ShellSettings.DefaultOnboardingThemeId),
                SectionId = "settings.onboarding.defaults",
                DisplayName = "Default Theme",
                Description = "Theme selected when onboarding starts.",
                Scope = "Onboarding",
                EditorKind = ShellSettingEditorKind.Choice,
                Options = new[]
                {
                    new SettingChoiceDescriptor { Value = "cortex.vs-dark", DisplayName = "VS Dark" },
                    new SettingChoiceDescriptor { Value = "cortex.vs-light", DisplayName = "VS Light" },
                    new SettingChoiceDescriptor { Value = "cortex.steel", DisplayName = "Steel" }
                },
                SortOrder = 20
            });
            catalog.Settings.Add(new SettingDescriptor
            {
                SettingId = nameof(ShellSettings.EnableFileEditing),
                SectionId = "settings.editor.behavior",
                DisplayName = "Enable File Editing",
                Description = "Allow writable editor operations in future surfaces.",
                Scope = "Editing",
                ValueKind = ShellSettingValueKind.Boolean,
                EditorKind = ShellSettingEditorKind.Choice,
                Options = new[]
                {
                    new SettingChoiceDescriptor { Value = "false", DisplayName = "Off" },
                    new SettingChoiceDescriptor { Value = "true", DisplayName = "On" }
                },
                DefaultValue = "false",
                SortOrder = 0
            });
            catalog.Settings.Add(new SettingDescriptor
            {
                SettingId = nameof(ShellSettings.EnableFileSaving),
                SectionId = "settings.editor.behavior",
                DisplayName = "Enable File Saving",
                Description = "Allow save operations from future writable editor surfaces.",
                Scope = "Editing",
                ValueKind = ShellSettingValueKind.Boolean,
                EditorKind = ShellSettingEditorKind.Choice,
                Options = new[]
                {
                    new SettingChoiceDescriptor { Value = "false", DisplayName = "Off" },
                    new SettingChoiceDescriptor { Value = "true", DisplayName = "On" }
                },
                DefaultValue = "false",
                SortOrder = 10
            });
            catalog.Settings.Add(new SettingDescriptor
            {
                SettingId = nameof(ShellSettings.EditorUndoHistoryLimit),
                SectionId = "settings.editor.behavior",
                DisplayName = "Undo History Limit",
                Description = "Maximum undo history depth for future editor sessions.",
                Scope = "Editing",
                ValueKind = ShellSettingValueKind.Integer,
                DefaultValue = "128",
                SortOrder = 20
            });
            catalog.Settings.Add(new SettingDescriptor
            {
                SettingId = nameof(ShellSettings.DefaultBuildConfiguration),
                SectionId = "settings.build.defaults",
                DisplayName = "Default Build Configuration",
                Description = "Default configuration used by future build commands.",
                Scope = "Build",
                EditorKind = ShellSettingEditorKind.Choice,
                Options = new[]
                {
                    new SettingChoiceDescriptor { Value = "Debug", DisplayName = "Debug" },
                    new SettingChoiceDescriptor { Value = "Release", DisplayName = "Release" }
                },
                DefaultValue = "Debug",
                SortOrder = 0
            });
            catalog.Settings.Add(new SettingDescriptor
            {
                SettingId = nameof(ShellSettings.BuildTimeoutMs),
                SectionId = "settings.build.defaults",
                DisplayName = "Build Timeout (ms)",
                Description = "Maximum execution time budget for future build actions.",
                Scope = "Build",
                ValueKind = ShellSettingValueKind.Integer,
                DefaultValue = "300000",
                SortOrder = 10
            });

            catalog.Editors.Add(new EditorDescriptor
            {
                EditorId = "cortex.editor.text",
                DisplayName = "Text Preview",
                ContentType = "text/plain",
                SortOrder = 0
            });

            return catalog;
        }
    }
}
