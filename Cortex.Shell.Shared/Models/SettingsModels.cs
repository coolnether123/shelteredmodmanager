namespace Cortex.Shell.Shared.Models
{
    public sealed class SettingsDocumentModel
    {
        public System.Collections.Generic.List<SettingsSectionModel> Sections { get; set; } = new System.Collections.Generic.List<SettingsSectionModel>();
        public System.Collections.Generic.List<SettingsNavigationGroupModel> Groups { get; set; } = new System.Collections.Generic.List<SettingsNavigationGroupModel>();
    }

    public sealed class SettingsNavigationGroupModel
    {
        public SettingsNavigationGroupModel()
        {
        }

        public SettingsNavigationGroupModel(string groupId, string title, int sortOrder)
        {
            GroupId = groupId ?? string.Empty;
            Title = title ?? string.Empty;
            SortOrder = sortOrder;
        }

        public string GroupId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public int SortOrder { get; set; }
        public System.Collections.Generic.List<SettingsSectionModel> Sections { get; set; } = new System.Collections.Generic.List<SettingsSectionModel>();
    }

    public sealed class SettingsSectionModel
    {
        public SettingsSectionModel()
        {
        }

        public SettingsSectionModel(
            string sectionId,
            string groupId,
            string groupTitle,
            string scope,
            string title,
            string description,
            string searchText,
            int sortOrder)
        {
            SectionId = sectionId ?? string.Empty;
            GroupId = groupId ?? string.Empty;
            GroupTitle = groupTitle ?? string.Empty;
            Scope = scope ?? string.Empty;
            Title = title ?? string.Empty;
            Description = description ?? string.Empty;
            SearchText = searchText ?? string.Empty;
            SortOrder = sortOrder;
        }

        public string SectionId { get; set; } = string.Empty;
        public string GroupId { get; set; } = string.Empty;
        public string GroupTitle { get; set; } = string.Empty;
        public string Scope { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string SearchText { get; set; } = string.Empty;
        public int SortOrder { get; set; }
    }

    public sealed class SettingsDraftState
    {
        public System.Collections.Generic.Dictionary<string, string> Values { get; set; } = new System.Collections.Generic.Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);
        public System.Collections.Generic.Dictionary<string, string> LoadedValues { get; set; } = new System.Collections.Generic.Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);
    }

    public sealed class SettingsSessionState
    {
        public string SearchQuery { get; set; } = string.Empty;
        public string AppliedSearchQuery { get; set; } = string.Empty;
        public string ActiveSectionId { get; set; } = string.Empty;
        public bool ShowModifiedOnly { get; set; }
    }
}
