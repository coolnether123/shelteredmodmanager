using System;
using Cortex.Core.Models;

namespace Cortex.Services.Settings
{
    internal sealed class SettingsSessionService
    {
        public void Restore(
            SettingsSessionState sessionState,
            CortexSettings settings,
            string defaultSectionId,
            out float navigationScrollY,
            out float contentScrollY)
        {
            navigationScrollY = 0f;
            contentScrollY = 0f;
            if (sessionState == null)
            {
                return;
            }

            var effectiveSettings = settings ?? new CortexSettings();
            sessionState.SearchQuery = effectiveSettings.SettingsSearchQuery ?? string.Empty;
            sessionState.AppliedSearchQuery = effectiveSettings.SettingsSearchQuery ?? string.Empty;
            sessionState.LastNormalizedSearchQuery = string.Empty;
            sessionState.ShowModifiedOnly = effectiveSettings.SettingsShowModifiedOnly;
            sessionState.ActiveSectionId = !string.IsNullOrEmpty(effectiveSettings.SettingsActiveSectionId)
                ? effectiveSettings.SettingsActiveSectionId
                : defaultSectionId ?? string.Empty;
            sessionState.PendingSectionJumpId = string.Empty;

            navigationScrollY = effectiveSettings.SettingsNavigationScrollY > 0f
                ? effectiveSettings.SettingsNavigationScrollY
                : 0f;
            contentScrollY = effectiveSettings.SettingsContentScrollY > 0f
                ? effectiveSettings.SettingsContentScrollY
                : 0f;
        }

        public string GetNormalizedSearchQuery(SettingsSessionState sessionState, SettingsSearchService searchService)
        {
            return searchService != null
                ? searchService.NormalizeQuery(sessionState != null ? sessionState.AppliedSearchQuery : string.Empty)
                : string.Empty;
        }

        public void CommitSearchQuery(SettingsSessionState sessionState)
        {
            if (sessionState == null)
            {
                return;
            }

            sessionState.AppliedSearchQuery = (sessionState.SearchQuery ?? string.Empty).Trim();
        }

        public void HandleSearchQueryChanged(
            SettingsSessionState sessionState,
            SettingsSearchService searchService,
            SettingsDocumentModel document,
            Func<SettingsSectionModel, int> getVisibleItemCount)
        {
            if (sessionState == null || searchService == null)
            {
                return;
            }

            var normalizedQuery = GetNormalizedSearchQuery(sessionState, searchService);
            if (string.Equals(normalizedQuery, sessionState.LastNormalizedSearchQuery, StringComparison.Ordinal))
            {
                return;
            }

            sessionState.LastNormalizedSearchQuery = normalizedQuery;
            if (string.IsNullOrEmpty(normalizedQuery))
            {
                return;
            }

            var sectionId = searchService.FindFirstVisibleSectionId(
                document,
                normalizedQuery,
                sessionState.ShowModifiedOnly,
                getVisibleItemCount);
            if (!string.IsNullOrEmpty(sectionId))
            {
                RequestSection(sessionState, sectionId);
            }
        }

        public void RequestSection(SettingsSessionState sessionState, string sectionId)
        {
            if (sessionState == null || string.IsNullOrEmpty(sectionId))
            {
                return;
            }

            sessionState.ActiveSectionId = sectionId;
            sessionState.PendingSectionJumpId = sectionId;
        }

        public void Persist(SettingsSessionState sessionState, CortexSettings settings, float navigationScrollY, float contentScrollY)
        {
            if (sessionState == null || settings == null)
            {
                return;
            }

            settings.SettingsActiveSectionId = sessionState.ActiveSectionId ?? string.Empty;
            settings.SettingsNavigationScrollY = navigationScrollY;
            settings.SettingsContentScrollY = contentScrollY;
            settings.SettingsSearchQuery = sessionState.AppliedSearchQuery ?? string.Empty;
            settings.SettingsShowModifiedOnly = sessionState.ShowModifiedOnly;
        }
    }
}
