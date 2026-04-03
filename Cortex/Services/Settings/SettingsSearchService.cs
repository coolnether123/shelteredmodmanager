using System;
using System.Collections.Generic;

namespace Cortex.Services.Settings
{
    internal sealed class SettingsSearchService
    {
        public string NormalizeQuery(string query)
        {
            return (query ?? string.Empty).Trim().ToLowerInvariant();
        }

        public bool MatchesSearch(string normalizedQuery, string text)
        {
            if (string.IsNullOrEmpty(normalizedQuery))
            {
                return true;
            }

            var haystack = (text ?? string.Empty).ToLowerInvariant();
            var terms = normalizedQuery.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i < terms.Length; i++)
            {
                if (haystack.IndexOf(terms[i], StringComparison.Ordinal) < 0)
                {
                    return false;
                }
            }

            return true;
        }

        public List<SettingsSectionModel> GetOrderedSections(SettingsDocumentModel document)
        {
            var ordered = new List<SettingsSectionModel>();
            if (document == null)
            {
                return ordered;
            }

            for (var groupIndex = 0; groupIndex < document.Groups.Count; groupIndex++)
            {
                var group = document.Groups[groupIndex];
                for (var sectionIndex = 0; sectionIndex < group.Sections.Count; sectionIndex++)
                {
                    ordered.Add(group.Sections[sectionIndex]);
                }
            }

            return ordered;
        }

        public List<SettingsSectionModel> GetVisibleSections(
            SettingsNavigationGroupModel group,
            string normalizedQuery,
            bool showModifiedOnly,
            Func<SettingsSectionModel, int> getVisibleItemCount)
        {
            var visible = new List<SettingsSectionModel>();
            if (group == null)
            {
                return visible;
            }

            for (var i = 0; i < group.Sections.Count; i++)
            {
                if (IsSectionVisible(group.Sections[i], normalizedQuery, showModifiedOnly, getVisibleItemCount))
                {
                    visible.Add(group.Sections[i]);
                }
            }

            return visible;
        }

        public bool IsSectionVisible(
            SettingsSectionModel section,
            string normalizedQuery,
            bool showModifiedOnly,
            Func<SettingsSectionModel, int> getVisibleItemCount)
        {
            if (section == null)
            {
                return false;
            }

            var visibleItemCount = getVisibleItemCount != null ? getVisibleItemCount(section) : 0;
            if (showModifiedOnly)
            {
                return visibleItemCount > 0;
            }

            return MatchesSearch(normalizedQuery, section.SearchText) || visibleItemCount > 0;
        }

        public int CountVisibleSections(
            SettingsNavigationGroupModel group,
            string normalizedQuery,
            bool showModifiedOnly,
            Func<SettingsSectionModel, int> getVisibleItemCount)
        {
            if (group == null)
            {
                return 0;
            }

            var count = 0;
            for (var i = 0; i < group.Sections.Count; i++)
            {
                if (IsSectionVisible(group.Sections[i], normalizedQuery, showModifiedOnly, getVisibleItemCount))
                {
                    count++;
                }
            }

            return count;
        }

        public string ResolveRenderActiveSectionId(
            SettingsDocumentModel document,
            string activeSectionId,
            string normalizedQuery,
            bool showModifiedOnly,
            Func<SettingsSectionModel, int> getVisibleItemCount)
        {
            var orderedSections = GetOrderedSections(document);
            if (orderedSections.Count == 0)
            {
                return string.Empty;
            }

            for (var i = 0; i < orderedSections.Count; i++)
            {
                var section = orderedSections[i];
                if (!IsSectionVisible(section, normalizedQuery, showModifiedOnly, getVisibleItemCount))
                {
                    continue;
                }

                if (string.Equals(section.SectionId, activeSectionId, StringComparison.OrdinalIgnoreCase))
                {
                    return activeSectionId ?? string.Empty;
                }
            }

            for (var i = 0; i < orderedSections.Count; i++)
            {
                if (IsSectionVisible(orderedSections[i], normalizedQuery, showModifiedOnly, getVisibleItemCount))
                {
                    return orderedSections[i].SectionId;
                }
            }

            return string.Empty;
        }

        public string FindFirstVisibleSectionId(
            SettingsDocumentModel document,
            string normalizedQuery,
            bool showModifiedOnly,
            Func<SettingsSectionModel, int> getVisibleItemCount)
        {
            var orderedSections = GetOrderedSections(document);
            for (var i = 0; i < orderedSections.Count; i++)
            {
                if (IsSectionVisible(orderedSections[i], normalizedQuery, showModifiedOnly, getVisibleItemCount))
                {
                    return orderedSections[i].SectionId;
                }
            }

            return string.Empty;
        }

        public string BuildSearchSummary(
            SettingsDocumentModel document,
            string normalizedQuery,
            bool showModifiedOnly,
            Func<SettingsSectionModel, int> getVisibleItemCount)
        {
            if (document == null)
            {
                return "No settings available.";
            }

            var visibleSections = 0;
            var visibleItems = 0;
            var orderedSections = GetOrderedSections(document);
            for (var i = 0; i < orderedSections.Count; i++)
            {
                var section = orderedSections[i];
                if (!IsSectionVisible(section, normalizedQuery, showModifiedOnly, getVisibleItemCount))
                {
                    continue;
                }

                visibleSections++;
                visibleItems += Math.Max(1, getVisibleItemCount != null ? getVisibleItemCount(section) : 0);
            }

            if (string.IsNullOrEmpty(normalizedQuery) && !showModifiedOnly)
            {
                return visibleSections.ToString() + " sections";
            }

            if (visibleSections == 0)
            {
                return "No matching settings.";
            }

            return visibleItems.ToString() + " matching items across " + visibleSections.ToString() + " sections";
        }
    }
}
