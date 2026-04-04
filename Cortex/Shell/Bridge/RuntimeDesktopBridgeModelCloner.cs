using System.Linq;
using Cortex.Shell.Shared.Models;

namespace Cortex.Shell.Bridge
{
    internal static class RuntimeDesktopBridgeModelCloner
    {
        public static ShellSettings CloneShellSettings(ShellSettings settings)
        {
            return new ShellSettings
            {
                WorkspaceRootPath = settings != null ? settings.WorkspaceRootPath ?? string.Empty : string.Empty,
                RuntimeContentRootPath = settings != null ? settings.RuntimeContentRootPath ?? string.Empty : string.Empty,
                ReferenceAssemblyRootPath = settings != null ? settings.ReferenceAssemblyRootPath ?? string.Empty : string.Empty,
                AdditionalSourceRoots = settings != null ? settings.AdditionalSourceRoots ?? string.Empty : string.Empty,
                ThemeId = settings != null ? settings.ThemeId ?? string.Empty : string.Empty,
                DefaultOnboardingProfileId = settings != null ? settings.DefaultOnboardingProfileId ?? string.Empty : string.Empty,
                DefaultOnboardingLayoutPresetId = settings != null ? settings.DefaultOnboardingLayoutPresetId ?? string.Empty : string.Empty,
                DefaultOnboardingThemeId = settings != null ? settings.DefaultOnboardingThemeId ?? string.Empty : string.Empty,
                DefaultBuildConfiguration = settings != null ? settings.DefaultBuildConfiguration ?? string.Empty : string.Empty,
                BuildTimeoutMs = settings != null ? settings.BuildTimeoutMs : 0,
                EnableFileEditing = settings != null && settings.EnableFileEditing,
                EnableFileSaving = settings != null && settings.EnableFileSaving,
                EditorUndoHistoryLimit = settings != null ? settings.EditorUndoHistoryLimit : 0,
                SettingsActiveSectionId = settings != null ? settings.SettingsActiveSectionId ?? string.Empty : string.Empty,
                SettingsSearchQuery = settings != null ? settings.SettingsSearchQuery ?? string.Empty : string.Empty,
                SettingsShowModifiedOnly = settings != null && settings.SettingsShowModifiedOnly,
                HasCompletedOnboarding = settings != null && settings.HasCompletedOnboarding
            };
        }

        public static OnboardingState CloneOnboardingState(OnboardingState onboarding)
        {
            return new OnboardingState
            {
                SelectedProfileId = onboarding != null ? onboarding.SelectedProfileId ?? string.Empty : string.Empty,
                SelectedLayoutPresetId = onboarding != null ? onboarding.SelectedLayoutPresetId ?? string.Empty : string.Empty,
                SelectedThemeId = onboarding != null ? onboarding.SelectedThemeId ?? string.Empty : string.Empty,
                SelectedWorkspaceRootPath = onboarding != null ? onboarding.SelectedWorkspaceRootPath ?? string.Empty : string.Empty,
                ActiveStepIndex = onboarding != null ? onboarding.ActiveStepIndex : 0
            };
        }

        public static OnboardingFlowModel CloneOnboardingFlow(OnboardingFlowModel flow)
        {
            var clone = new OnboardingFlowModel
            {
                ActiveStepIndex = flow != null ? flow.ActiveStepIndex : 0
            };
            if (flow != null)
            {
                foreach (var step in flow.Steps)
                {
                    clone.Steps.Add(new OnboardingStepModel
                    {
                        StepId = step != null ? step.StepId ?? string.Empty : string.Empty,
                        Title = step != null ? step.Title ?? string.Empty : string.Empty,
                        Description = step != null ? step.Description ?? string.Empty : string.Empty
                    });
                }
            }

            return clone;
        }

        public static SettingsDocumentModel CloneSettingsDocument(SettingsDocumentModel document)
        {
            var clone = new SettingsDocumentModel();
            if (document == null)
            {
                return clone;
            }

            clone.Sections.AddRange(document.Sections.Select(CloneSettingsSection));
            clone.Groups.AddRange(document.Groups.Select(CloneSettingsGroup));
            return clone;
        }

        public static SettingsNavigationGroupModel CloneSettingsGroup(SettingsNavigationGroupModel group)
        {
            var clone = new SettingsNavigationGroupModel
            {
                GroupId = group != null ? group.GroupId ?? string.Empty : string.Empty,
                Title = group != null ? group.Title ?? string.Empty : string.Empty,
                SortOrder = group != null ? group.SortOrder : 0
            };
            if (group != null)
            {
                clone.Sections.AddRange(group.Sections.Select(CloneSettingsSection));
            }

            return clone;
        }

        public static SettingsSectionModel CloneSettingsSection(SettingsSectionModel section)
        {
            return new SettingsSectionModel
            {
                SectionId = section != null ? section.SectionId ?? string.Empty : string.Empty,
                GroupId = section != null ? section.GroupId ?? string.Empty : string.Empty,
                GroupTitle = section != null ? section.GroupTitle ?? string.Empty : string.Empty,
                Scope = section != null ? section.Scope ?? string.Empty : string.Empty,
                Title = section != null ? section.Title ?? string.Empty : string.Empty,
                Description = section != null ? section.Description ?? string.Empty : string.Empty,
                SearchText = section != null ? section.SearchText ?? string.Empty : string.Empty,
                SortOrder = section != null ? section.SortOrder : 0
            };
        }

        public static SettingDescriptor CloneSettingDescriptor(SettingDescriptor setting)
        {
            if (setting == null)
            {
                return new SettingDescriptor();
            }

            return new SettingDescriptor
            {
                SettingId = setting.SettingId ?? string.Empty,
                SectionId = setting.SectionId ?? string.Empty,
                DisplayName = setting.DisplayName ?? string.Empty,
                Description = setting.Description ?? string.Empty,
                Scope = setting.Scope ?? string.Empty,
                DefaultValue = setting.DefaultValue ?? string.Empty,
                ValueKind = setting.ValueKind,
                EditorKind = setting.EditorKind,
                PlaceholderText = setting.PlaceholderText ?? string.Empty,
                HelpText = setting.HelpText ?? string.Empty,
                Keywords = setting.Keywords != null ? setting.Keywords.ToArray() : new string[0],
                Options = setting.Options != null
                    ? setting.Options.Select(option => new SettingChoiceDescriptor
                    {
                        Value = option != null ? option.Value ?? string.Empty : string.Empty,
                        DisplayName = option != null ? option.DisplayName ?? string.Empty : string.Empty,
                        Description = option != null ? option.Description ?? string.Empty : string.Empty
                    }).ToArray()
                    : new SettingChoiceDescriptor[0],
                IsRequired = setting.IsRequired,
                IsSecret = setting.IsSecret,
                SortOrder = setting.SortOrder
            };
        }

        public static WorkspaceProjectDefinition CloneProjectDefinition(WorkspaceProjectDefinition definition)
        {
            return new WorkspaceProjectDefinition
            {
                ProjectId = definition != null ? definition.ProjectId ?? string.Empty : string.Empty,
                DisplayName = definition != null ? definition.DisplayName ?? string.Empty : string.Empty,
                SourceRootPath = definition != null ? definition.SourceRootPath ?? string.Empty : string.Empty,
                ProjectFilePath = definition != null ? definition.ProjectFilePath ?? string.Empty : string.Empty
            };
        }

        public static WorkspaceFileNode CloneWorkspaceFileNode(WorkspaceFileNode node)
        {
            if (node == null)
            {
                return null;
            }

            var clone = new WorkspaceFileNode
            {
                Name = node.Name ?? string.Empty,
                FullPath = node.FullPath ?? string.Empty,
                IsDirectory = node.IsDirectory
            };
            foreach (var child in node.Children)
            {
                clone.Children.Add(CloneWorkspaceFileNode(child));
            }

            return clone;
        }
    }
}
