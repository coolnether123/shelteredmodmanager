using System;
using System.Collections.Generic;
using Cortex.Shell.Shared.Models;

namespace Cortex.Shell.Shared.Services
{
    public sealed class SettingsSearchService
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

        public IList<SettingsSectionModel> GetVisibleSections(SettingsDocumentModel document, string normalizedQuery)
        {
            var results = new List<SettingsSectionModel>();
            if (document == null)
            {
                return results;
            }

            for (var i = 0; i < document.Sections.Count; i++)
            {
                if (MatchesSearch(normalizedQuery, document.Sections[i].SearchText))
                {
                    results.Add(document.Sections[i]);
                }
            }

            return results;
        }

        public string ResolveActiveSectionId(SettingsDocumentModel document, string requestedSectionId, string normalizedQuery)
        {
            var visibleSections = GetVisibleSections(document, normalizedQuery);
            if (visibleSections.Count == 0)
            {
                return string.Empty;
            }

            for (var i = 0; i < visibleSections.Count; i++)
            {
                if (string.Equals(visibleSections[i].SectionId, requestedSectionId, StringComparison.OrdinalIgnoreCase))
                {
                    return visibleSections[i].SectionId;
                }
            }

            return visibleSections[0].SectionId;
        }
    }
}
