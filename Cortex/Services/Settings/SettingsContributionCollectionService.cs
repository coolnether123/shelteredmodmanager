using System;
using System.Collections.Generic;
using Cortex.Core.Models;
using Cortex.Core.Services;
using Cortex.Presentation.Models;

namespace Cortex.Services.Settings
{
    internal sealed class SettingsContributionCollectionService
    {
        public List<SettingContribution> CollectVisibleContributionsForScope(
            WorkbenchPresentationSnapshot snapshot,
            string scope,
            Predicate<SettingContribution> isVisible)
        {
            var results = new List<SettingContribution>();
            if (snapshot == null || snapshot.Settings.Count == 0)
            {
                return results;
            }

            for (var i = 0; i < snapshot.Settings.Count; i++)
            {
                var contribution = snapshot.Settings[i];
                if (!ShouldRenderContribution(scope, contribution) || (isVisible != null && !isVisible(contribution)))
                {
                    continue;
                }

                results.Add(contribution);
            }

            return results;
        }

        public bool HasVisibleContributionsForScope(
            WorkbenchPresentationSnapshot snapshot,
            string scope,
            Predicate<SettingContribution> isVisible)
        {
            if (snapshot == null || snapshot.Settings.Count == 0)
            {
                return false;
            }

            for (var i = 0; i < snapshot.Settings.Count; i++)
            {
                var contribution = snapshot.Settings[i];
                if (ShouldRenderContribution(scope, contribution) && (isVisible == null || isVisible(contribution)))
                {
                    return true;
                }
            }

            return false;
        }

        public int CountVisibleContributionsForScope(
            WorkbenchPresentationSnapshot snapshot,
            string scope,
            Predicate<SettingContribution> isVisible)
        {
            var count = 0;
            if (snapshot == null || string.IsNullOrEmpty(scope))
            {
                return count;
            }

            for (var i = 0; i < snapshot.Settings.Count; i++)
            {
                var contribution = snapshot.Settings[i];
                if (ShouldRenderContribution(scope, contribution) && (isVisible == null || isVisible(contribution)))
                {
                    count++;
                }
            }

            return count;
        }

        public int CountVisibleSettingsForIds(
            WorkbenchPresentationSnapshot snapshot,
            Predicate<SettingContribution> isVisible,
            params string[] settingIds)
        {
            if (snapshot == null || settingIds == null || settingIds.Length == 0)
            {
                return 0;
            }

            var count = 0;
            for (var i = 0; i < settingIds.Length; i++)
            {
                var contribution = FindSettingContribution(snapshot, settingIds[i]);
                if (contribution != null && (isVisible == null || isVisible(contribution)))
                {
                    count++;
                }
            }

            return count;
        }

        public bool ShouldRenderContribution(string scope, SettingContribution contribution)
        {
            if (contribution == null || string.IsNullOrEmpty(contribution.SettingId) || IsThemeSetting(contribution))
            {
                return false;
            }

            if (!string.Equals(GetContributionScope(contribution), scope, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (string.Equals(scope, "Workspace", StringComparison.OrdinalIgnoreCase) && IsWorkspacePathContribution(contribution))
            {
                return false;
            }

            return true;
        }

        private static bool IsWorkspacePathContribution(SettingContribution contribution)
        {
            if (contribution == null)
            {
                return false;
            }

            return string.Equals(contribution.SettingId, CortexHostPathSettings.WorkspaceRootSettingId, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(contribution.SettingId, CortexHostPathSettings.RuntimeContentRootSettingId, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(contribution.SettingId, CortexHostPathSettings.ReferenceAssemblyRootSettingId, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(contribution.SettingId, CortexHostPathSettings.AdditionalSourceRootsSettingId, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsThemeSetting(SettingContribution contribution)
        {
            return contribution != null &&
                string.Equals(contribution.SettingId, nameof(Cortex.Core.Models.CortexSettings.ThemeId), StringComparison.OrdinalIgnoreCase);
        }

        private static string GetContributionScope(SettingContribution contribution)
        {
            return string.IsNullOrEmpty(contribution != null ? contribution.Scope : string.Empty)
                ? "General"
                : contribution.Scope;
        }

        private static SettingContribution FindSettingContribution(WorkbenchPresentationSnapshot snapshot, string settingId)
        {
            if (snapshot == null || string.IsNullOrEmpty(settingId))
            {
                return null;
            }

            for (var i = 0; i < snapshot.Settings.Count; i++)
            {
                var contribution = snapshot.Settings[i];
                if (contribution != null && string.Equals(contribution.SettingId, settingId, StringComparison.OrdinalIgnoreCase))
                {
                    return contribution;
                }
            }

            return null;
        }
    }
}
