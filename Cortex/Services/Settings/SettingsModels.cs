using System.Collections.Generic;
using Cortex.Core.Models;

namespace Cortex.Services.Settings
{
    internal enum SettingsSectionKind
    {
        WorkspaceOverview = 0,
        WorkspacePaths = 1,
        WorkspaceSettingsContributions = 2,
        WorkspaceModLinks = 3,
        WorkspaceCurrentPaths = 4,
        ContributionScope = 5,
        Onboarding = 6,
        Themes = 7,
        Keybindings = 8,
        Editors = 9,
        Actions = 10
    }

    internal sealed class SettingsDocumentModel
    {
        public readonly List<SettingsSectionModel> Sections = new List<SettingsSectionModel>();
        public readonly List<SettingsNavigationGroupModel> Groups = new List<SettingsNavigationGroupModel>();
    }

    internal sealed class SettingsNavigationGroupModel
    {
        public readonly string GroupId;
        public readonly string Title;
        public readonly int SortOrder;
        public readonly List<SettingsSectionModel> Sections = new List<SettingsSectionModel>();

        public SettingsNavigationGroupModel(string groupId, string title, int sortOrder)
        {
            GroupId = groupId ?? string.Empty;
            Title = string.IsNullOrEmpty(title) ? "General" : title;
            SortOrder = sortOrder;
        }
    }

    internal sealed class SettingsSectionModel
    {
        public readonly string SectionId;
        public readonly string GroupId;
        public readonly string GroupTitle;
        public readonly string Scope;
        public readonly string Title;
        public readonly string Description;
        public readonly string SearchText;
        public readonly int SortOrder;
        public readonly SettingsSectionKind SectionKind;
        public readonly string ContributionScope;

        public SettingsSectionModel(
            string sectionId,
            string groupId,
            string groupTitle,
            string scope,
            string title,
            string description,
            string searchText,
            int sortOrder,
            SettingsSectionKind sectionKind,
            string contributionScope)
        {
            SectionId = sectionId ?? string.Empty;
            GroupId = groupId ?? string.Empty;
            GroupTitle = groupTitle ?? string.Empty;
            Scope = scope ?? string.Empty;
            Title = title ?? string.Empty;
            Description = description ?? string.Empty;
            SearchText = searchText ?? string.Empty;
            SortOrder = sortOrder;
            SectionKind = sectionKind;
            ContributionScope = contributionScope ?? string.Empty;
        }
    }

    internal sealed class SettingsDraftState
    {
        public readonly Dictionary<string, string> TextValues = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<string, bool> ToggleValues = new Dictionary<string, bool>(System.StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<string, string> LoadedSerializedValues = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<string, SettingValidationResult> ValidationResults = new Dictionary<string, SettingValidationResult>(System.StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<string, string> LoadedModPathDrafts = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);
        public string SelectedThemeId = string.Empty;
    }
}
