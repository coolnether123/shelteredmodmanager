using System;
using System.Collections.Generic;
using Cortex.Shell.Shared.Models;

namespace Cortex.Shell.Shared.Services
{
    public sealed class SettingsDocumentBuilder
    {
        public SettingsDocumentModel BuildDocument(WorkbenchCatalogSnapshot catalog)
        {
            var document = new SettingsDocumentModel();
            if (catalog == null)
            {
                return document;
            }

            for (var i = 0; i < catalog.SettingSections.Count; i++)
            {
                var section = catalog.SettingSections[i];
                if (section == null)
                {
                    continue;
                }

                var searchText = section.Title + " " + section.Description + " " + string.Join(" ", section.Keywords ?? new string[0]);
                for (var settingIndex = 0; settingIndex < catalog.Settings.Count; settingIndex++)
                {
                    var setting = catalog.Settings[settingIndex];
                    if (setting == null || !string.Equals(setting.SectionId, section.SectionId, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    searchText += " " + setting.DisplayName + " " + setting.Description + " " + setting.HelpText + " " + string.Join(" ", setting.Keywords ?? new string[0]);
                }

                document.Sections.Add(new SettingsSectionModel(
                    section.SectionId,
                    section.GroupId,
                    section.GroupTitle,
                    section.Scope,
                    section.Title,
                    section.Description,
                    searchText.Trim(),
                    section.SortOrder));
            }

            document.Sections.Sort(CompareSections);
            BuildGroups(document);
            return document;
        }

        private static void BuildGroups(SettingsDocumentModel document)
        {
            var groups = new Dictionary<string, SettingsNavigationGroupModel>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < document.Sections.Count; i++)
            {
                var section = document.Sections[i];
                SettingsNavigationGroupModel group;
                if (!groups.TryGetValue(section.GroupId, out group))
                {
                    group = new SettingsNavigationGroupModel(section.GroupId, section.GroupTitle, section.SortOrder);
                    groups[section.GroupId] = group;
                    document.Groups.Add(group);
                }

                group.Sections.Add(section);
            }

            document.Groups.Sort(delegate(SettingsNavigationGroupModel left, SettingsNavigationGroupModel right)
            {
                var order = left.SortOrder.CompareTo(right.SortOrder);
                return order != 0
                    ? order
                    : string.Compare(left.Title, right.Title, StringComparison.OrdinalIgnoreCase);
            });
        }

        private static int CompareSections(SettingsSectionModel left, SettingsSectionModel right)
        {
            var groupOrder = string.Compare(left.GroupId, right.GroupId, StringComparison.OrdinalIgnoreCase);
            if (groupOrder != 0)
            {
                return groupOrder;
            }

            var order = left.SortOrder.CompareTo(right.SortOrder);
            return order != 0
                ? order
                : string.Compare(left.Title, right.Title, StringComparison.OrdinalIgnoreCase);
        }
    }
}
