using System;
using System.Collections.Generic;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;

namespace Cortex.Services
{
    public sealed class CortexOnboardingCatalog
    {
        public readonly List<OnboardingProfileContribution> Profiles = new List<OnboardingProfileContribution>();
        public readonly List<OnboardingLayoutPresetContribution> LayoutPresets = new List<OnboardingLayoutPresetContribution>();
        public readonly List<ThemeContribution> Themes = new List<ThemeContribution>();
    }

    internal sealed class CortexOnboardingResolvedSelection
    {
        public readonly OnboardingProfileContribution Profile;
        public readonly OnboardingLayoutPresetContribution LayoutPreset;
        public readonly ThemeContribution Theme;

        public CortexOnboardingResolvedSelection(
            OnboardingProfileContribution profile,
            OnboardingLayoutPresetContribution layoutPreset,
            ThemeContribution theme)
        {
            Profile = profile;
            LayoutPreset = layoutPreset;
            Theme = theme;
        }
    }

    public sealed class CortexOnboardingService
    {
        public void SeedSelections(CortexOnboardingState onboardingState, CortexSettings settings, IContributionRegistry contributionRegistry)
        {
            var catalog = BuildCatalog(contributionRegistry);
            PrepareSession(onboardingState, settings, catalog);
            if (onboardingState == null)
            {
                return;
            }

            onboardingState.KeepFocused = false;
            onboardingState.PreviewFingerprint = string.Empty;
            onboardingState.FinishPrompt.IsVisible = false;
            onboardingState.FinishPrompt.Anchor = UnityEngine.Vector2.zero;
        }

        public CortexOnboardingCatalog BuildCatalog(IContributionRegistry contributionRegistry)
        {
            var catalog = new CortexOnboardingCatalog();
            if (contributionRegistry == null)
            {
                return catalog;
            }

            var profiles = contributionRegistry.GetOnboardingProfiles();
            for (var i = 0; i < profiles.Count; i++)
            {
                if (profiles[i] != null)
                {
                    catalog.Profiles.Add(profiles[i]);
                }
            }

            var layouts = contributionRegistry.GetOnboardingLayoutPresets();
            for (var i = 0; i < layouts.Count; i++)
            {
                if (layouts[i] != null)
                {
                    catalog.LayoutPresets.Add(layouts[i]);
                }
            }

            var themes = contributionRegistry.GetThemes();
            var visibleThemeCount = 0;
            for (var i = 0; i < themes.Count; i++)
            {
                if (themes[i] != null && themes[i].ShowInOnboarding)
                {
                    visibleThemeCount++;
                }
            }

            for (var i = 0; i < themes.Count; i++)
            {
                var theme = themes[i];
                if (theme == null)
                {
                    continue;
                }

                if (visibleThemeCount > 0 && !theme.ShowInOnboarding)
                {
                    continue;
                }

                catalog.Themes.Add(theme);
            }

            catalog.Profiles.Sort(CompareProfiles);
            catalog.LayoutPresets.Sort(CompareLayoutPresets);
            catalog.Themes.Sort(CompareThemes);
            return catalog;
        }

        internal CortexOnboardingResolvedSelection ResolveSelection(
            CortexOnboardingState onboardingState,
            CortexSettings settings,
            CortexOnboardingCatalog catalog)
        {
            if (catalog == null)
            {
                return null;
            }

            var profile = FindProfile(catalog, ResolveProfileId(onboardingState, settings, catalog)) ?? GetDefaultProfile(catalog);
            if (profile == null)
            {
                return null;
            }

            var layoutPreset = FindLayoutPreset(catalog, ResolveLayoutPresetId(onboardingState, settings, catalog, profile)) ?? GetDefaultLayoutPreset(catalog);
            if (layoutPreset == null)
            {
                return null;
            }

            var theme = FindTheme(catalog, ResolveThemeId(onboardingState, settings, catalog, profile, layoutPreset)) ?? GetDefaultTheme(catalog);
            if (theme == null)
            {
                return null;
            }

            return new CortexOnboardingResolvedSelection(profile, layoutPreset, theme);
        }

        internal string BuildPreviewFingerprint(CortexOnboardingResolvedSelection selection)
        {
            if (selection == null)
            {
                return string.Empty;
            }

            return
                (selection.Profile != null ? selection.Profile.ProfileId : string.Empty) + "|" +
                (selection.LayoutPreset != null ? selection.LayoutPreset.LayoutPresetId : string.Empty) + "|" +
                (selection.Theme != null ? selection.Theme.ThemeId : string.Empty);
        }

        public IList<ThemeContribution> GetAvailableThemes(IContributionRegistry contributionRegistry)
        {
            return BuildCatalog(contributionRegistry).Themes;
        }

        public void PrepareSession(CortexOnboardingState onboardingState, CortexSettings settings, CortexOnboardingCatalog catalog)
        {
            if (onboardingState == null)
            {
                return;
            }

            onboardingState.ResetInteractionState();

            var selection = ResolveSelection(onboardingState, settings, catalog);
            if (selection == null)
            {
                return;
            }

            onboardingState.SelectedProfileId = selection.Profile.ProfileId;
            onboardingState.SelectedLayoutPresetId = selection.LayoutPreset.LayoutPresetId;
            onboardingState.SelectedThemeId = selection.Theme.ThemeId;
        }

        public void SelectProfile(CortexOnboardingState onboardingState, CortexOnboardingCatalog catalog, string profileId)
        {
            var profile = FindProfile(catalog, profileId);
            if (onboardingState == null || profile == null)
            {
                return;
            }

            onboardingState.SelectedProfileId = profile.ProfileId;
            if (!onboardingState.HasUserSelectedLayoutPreset)
            {
                onboardingState.SelectedLayoutPresetId = ResolveProfileDefaultLayoutPresetId(catalog, profile);
            }

            if (!onboardingState.HasUserSelectedTheme)
            {
                var layoutPreset = FindLayoutPreset(catalog, onboardingState.SelectedLayoutPresetId);
                onboardingState.SelectedThemeId = ResolveDefaultThemeId(catalog, profile, layoutPreset);
            }
        }

        public void SelectLayoutPreset(CortexOnboardingState onboardingState, CortexOnboardingCatalog catalog, string layoutPresetId)
        {
            var layoutPreset = FindLayoutPreset(catalog, layoutPresetId);
            if (onboardingState == null || layoutPreset == null)
            {
                return;
            }

            onboardingState.SelectedLayoutPresetId = layoutPreset.LayoutPresetId;
            onboardingState.HasUserSelectedLayoutPreset = true;
            if (!onboardingState.HasUserSelectedTheme)
            {
                var profile = FindProfile(catalog, onboardingState.SelectedProfileId);
                onboardingState.SelectedThemeId = ResolveDefaultThemeId(catalog, profile, layoutPreset);
            }
        }

        public void SelectTheme(CortexOnboardingState onboardingState, string themeId)
        {
            if (onboardingState == null || string.IsNullOrEmpty(themeId))
            {
                return;
            }

            onboardingState.SelectedThemeId = themeId;
            onboardingState.HasUserSelectedTheme = true;
        }

        public OnboardingProfileContribution FindProfile(CortexOnboardingCatalog catalog, string profileId)
        {
            if (catalog == null || string.IsNullOrEmpty(profileId))
            {
                return null;
            }

            for (var i = 0; i < catalog.Profiles.Count; i++)
            {
                if (string.Equals(catalog.Profiles[i].ProfileId, profileId, StringComparison.OrdinalIgnoreCase))
                {
                    return catalog.Profiles[i];
                }
            }

            return null;
        }

        public OnboardingLayoutPresetContribution FindLayoutPreset(CortexOnboardingCatalog catalog, string layoutPresetId)
        {
            if (catalog == null || string.IsNullOrEmpty(layoutPresetId))
            {
                return null;
            }

            for (var i = 0; i < catalog.LayoutPresets.Count; i++)
            {
                if (string.Equals(catalog.LayoutPresets[i].LayoutPresetId, layoutPresetId, StringComparison.OrdinalIgnoreCase))
                {
                    return catalog.LayoutPresets[i];
                }
            }

            return null;
        }

        public ThemeContribution FindTheme(CortexOnboardingCatalog catalog, string themeId)
        {
            if (catalog == null || string.IsNullOrEmpty(themeId))
            {
                return null;
            }

            for (var i = 0; i < catalog.Themes.Count; i++)
            {
                if (string.Equals(catalog.Themes[i].ThemeId, themeId, StringComparison.OrdinalIgnoreCase))
                {
                    return catalog.Themes[i];
                }
            }

            return null;
        }

        private static string ResolveProfileId(CortexOnboardingState onboardingState, CortexSettings settings, CortexOnboardingCatalog catalog)
        {
            var profileId = FirstNonEmpty(
                onboardingState != null ? onboardingState.SelectedProfileId : string.Empty,
                onboardingState != null ? onboardingState.ActiveProfileId : string.Empty,
                settings != null ? settings.DefaultOnboardingProfileId : string.Empty);
            if (!string.IsNullOrEmpty(profileId))
            {
                return profileId;
            }

            var profile = GetDefaultProfile(catalog);
            return profile != null ? profile.ProfileId : string.Empty;
        }

        private static string ResolveLayoutPresetId(
            CortexOnboardingState onboardingState,
            CortexSettings settings,
            CortexOnboardingCatalog catalog,
            OnboardingProfileContribution profile)
        {
            var layoutPresetId = FirstNonEmpty(
                onboardingState != null ? onboardingState.SelectedLayoutPresetId : string.Empty,
                onboardingState != null ? onboardingState.ActiveLayoutPresetId : string.Empty,
                settings != null ? settings.DefaultOnboardingLayoutPresetId : string.Empty,
                profile != null ? profile.DefaultLayoutPresetId : string.Empty);
            if (!string.IsNullOrEmpty(layoutPresetId))
            {
                return layoutPresetId;
            }

            var layoutPreset = GetDefaultLayoutPreset(catalog);
            return layoutPreset != null ? layoutPreset.LayoutPresetId : string.Empty;
        }

        private static string ResolveThemeId(
            CortexOnboardingState onboardingState,
            CortexSettings settings,
            CortexOnboardingCatalog catalog,
            OnboardingProfileContribution profile,
            OnboardingLayoutPresetContribution layoutPreset)
        {
            var themeId = FirstNonEmpty(
                onboardingState != null ? onboardingState.SelectedThemeId : string.Empty,
                onboardingState != null ? onboardingState.ActiveThemeId : string.Empty,
                settings != null ? settings.DefaultOnboardingThemeId : string.Empty,
                settings != null ? settings.ThemeId : string.Empty,
                layoutPreset != null ? layoutPreset.DefaultThemeId : string.Empty,
                profile != null ? profile.DefaultThemeId : string.Empty);
            if (!string.IsNullOrEmpty(themeId))
            {
                return themeId;
            }

            var theme = GetDefaultTheme(catalog);
            return theme != null ? theme.ThemeId : string.Empty;
        }

        private static string ResolveProfileDefaultLayoutPresetId(
            CortexOnboardingCatalog catalog,
            OnboardingProfileContribution profile)
        {
            var layoutPresetId = profile != null ? profile.DefaultLayoutPresetId : string.Empty;
            if (!string.IsNullOrEmpty(layoutPresetId))
            {
                return layoutPresetId;
            }

            var layoutPreset = GetDefaultLayoutPreset(catalog);
            return layoutPreset != null ? layoutPreset.LayoutPresetId : string.Empty;
        }

        private static string ResolveDefaultThemeId(
            CortexOnboardingCatalog catalog,
            OnboardingProfileContribution profile,
            OnboardingLayoutPresetContribution layoutPreset)
        {
            var themeId = FirstNonEmpty(
                layoutPreset != null ? layoutPreset.DefaultThemeId : string.Empty,
                profile != null ? profile.DefaultThemeId : string.Empty);
            if (!string.IsNullOrEmpty(themeId))
            {
                return themeId;
            }

            var theme = GetDefaultTheme(catalog);
            return theme != null ? theme.ThemeId : string.Empty;
        }

        private static OnboardingProfileContribution GetDefaultProfile(CortexOnboardingCatalog catalog)
        {
            if (catalog == null || catalog.Profiles.Count == 0)
            {
                return null;
            }

            for (var i = 0; i < catalog.Profiles.Count; i++)
            {
                if (catalog.Profiles[i].IsDefault)
                {
                    return catalog.Profiles[i];
                }
            }

            return catalog.Profiles[0];
        }

        private static OnboardingLayoutPresetContribution GetDefaultLayoutPreset(CortexOnboardingCatalog catalog)
        {
            if (catalog == null || catalog.LayoutPresets.Count == 0)
            {
                return null;
            }

            for (var i = 0; i < catalog.LayoutPresets.Count; i++)
            {
                if (catalog.LayoutPresets[i].IsDefault)
                {
                    return catalog.LayoutPresets[i];
                }
            }

            return catalog.LayoutPresets[0];
        }

        private static ThemeContribution GetDefaultTheme(CortexOnboardingCatalog catalog)
        {
            if (catalog == null || catalog.Themes.Count == 0)
            {
                return null;
            }

            for (var i = 0; i < catalog.Themes.Count; i++)
            {
                if (catalog.Themes[i].IsDefault)
                {
                    return catalog.Themes[i];
                }
            }

            return catalog.Themes[0];
        }

        private static string FirstNonEmpty(params string[] values)
        {
            if (values == null)
            {
                return string.Empty;
            }

            for (var i = 0; i < values.Length; i++)
            {
                if (!string.IsNullOrEmpty(values[i]))
                {
                    return values[i];
                }
            }

            return string.Empty;
        }

        private static int CompareProfiles(OnboardingProfileContribution left, OnboardingProfileContribution right)
        {
            if (left == null && right == null)
            {
                return 0;
            }

            if (left == null)
            {
                return 1;
            }

            if (right == null)
            {
                return -1;
            }

            var order = left.SortOrder.CompareTo(right.SortOrder);
            if (order != 0)
            {
                return order;
            }

            return string.Compare(left.DisplayName, right.DisplayName, StringComparison.OrdinalIgnoreCase);
        }

        private static int CompareLayoutPresets(OnboardingLayoutPresetContribution left, OnboardingLayoutPresetContribution right)
        {
            if (left == null && right == null)
            {
                return 0;
            }

            if (left == null)
            {
                return 1;
            }

            if (right == null)
            {
                return -1;
            }

            var order = left.SortOrder.CompareTo(right.SortOrder);
            if (order != 0)
            {
                return order;
            }

            return string.Compare(left.DisplayName, right.DisplayName, StringComparison.OrdinalIgnoreCase);
        }

        private static int CompareThemes(ThemeContribution left, ThemeContribution right)
        {
            if (left == null && right == null)
            {
                return 0;
            }

            if (left == null)
            {
                return 1;
            }

            if (right == null)
            {
                return -1;
            }

            var order = left.SortOrder.CompareTo(right.SortOrder);
            if (order != 0)
            {
                return order;
            }

            return string.Compare(left.DisplayName, right.DisplayName, StringComparison.OrdinalIgnoreCase);
        }
    }
}
