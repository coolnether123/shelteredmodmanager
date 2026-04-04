using System;
using Cortex.Shell.Shared.Models;

namespace Cortex.Shell.Shared.Services
{
    public sealed class OnboardingService
    {
        public void Seed(OnboardingState onboardingState, ShellSettings settings, WorkbenchCatalogSnapshot catalog)
        {
            if (onboardingState == null)
            {
                return;
            }

            onboardingState.SelectedWorkspaceRootPath = settings != null ? settings.WorkspaceRootPath ?? string.Empty : string.Empty;
            var selection = ResolveSelection(onboardingState, settings, catalog);
            onboardingState.SelectedProfileId = selection.Profile != null ? selection.Profile.ProfileId : string.Empty;
            onboardingState.SelectedLayoutPresetId = selection.Layout != null ? selection.Layout.LayoutPresetId : string.Empty;
            onboardingState.SelectedThemeId = selection.Theme != null ? selection.Theme.ThemeId : string.Empty;
            onboardingState.ActiveStepIndex = 0;
        }

        public OnboardingResolvedSelection ResolveSelection(OnboardingState onboardingState, ShellSettings settings, WorkbenchCatalogSnapshot catalog)
        {
            var selection = new OnboardingResolvedSelection();
            if (catalog == null)
            {
                return selection;
            }

            selection.Profile = FindProfile(catalog, FirstNonEmpty(
                onboardingState != null ? onboardingState.SelectedProfileId : string.Empty,
                settings != null ? settings.DefaultOnboardingProfileId : string.Empty));
            if (selection.Profile == null && catalog.OnboardingProfiles.Count > 0)
            {
                selection.Profile = catalog.OnboardingProfiles[0];
            }

            selection.Layout = FindLayout(catalog, FirstNonEmpty(
                onboardingState != null ? onboardingState.SelectedLayoutPresetId : string.Empty,
                selection.Profile != null ? selection.Profile.DefaultLayoutPresetId : string.Empty,
                settings != null ? settings.DefaultOnboardingLayoutPresetId : string.Empty));
            if (selection.Layout == null && catalog.OnboardingLayouts.Count > 0)
            {
                selection.Layout = catalog.OnboardingLayouts[0];
            }

            selection.Theme = FindTheme(catalog, FirstNonEmpty(
                onboardingState != null ? onboardingState.SelectedThemeId : string.Empty,
                selection.Layout != null ? selection.Layout.DefaultThemeId : string.Empty,
                selection.Profile != null ? selection.Profile.DefaultThemeId : string.Empty,
                settings != null ? settings.DefaultOnboardingThemeId : string.Empty,
                settings != null ? settings.ThemeId : string.Empty));
            if (selection.Theme == null && catalog.Themes.Count > 0)
            {
                selection.Theme = catalog.Themes[0];
            }

            return selection;
        }

        public OnboardingFlowModel BuildFlow(OnboardingState onboardingState, WorkbenchCatalogSnapshot catalog)
        {
            var flow = new OnboardingFlowModel();
            var selection = ResolveSelection(onboardingState, null, catalog);

            flow.Steps.Add(new OnboardingStepModel("profile", "Choose your starting profile", "Select the default posture Cortex should open with."));
            if (selection.Profile != null && string.Equals(selection.Profile.WorkflowKind, "modder", StringComparison.OrdinalIgnoreCase))
            {
                flow.Steps.Add(new OnboardingStepModel("workspace", "Choose your workspace root", "Point Cortex at the source workspace it should treat as editable."));
            }

            flow.Steps.Add(new OnboardingStepModel("layout", "Choose a layout style", "Pick the structural arrangement for the first desktop shell."));
            flow.Steps.Add(new OnboardingStepModel("theme", "Choose a theme", "Select the shell palette the desktop host should apply."));
            flow.ActiveStepIndex = onboardingState != null
                ? Math.Max(0, Math.Min(flow.Steps.Count - 1, onboardingState.ActiveStepIndex))
                : 0;
            return flow;
        }

        public ShellSettings Apply(OnboardingState onboardingState, WorkbenchCatalogSnapshot catalog, ShellSettings settings)
        {
            var effectiveSettings = settings ?? new ShellSettings();
            var selection = ResolveSelection(onboardingState, effectiveSettings, catalog);
            effectiveSettings.WorkspaceRootPath = onboardingState != null ? onboardingState.SelectedWorkspaceRootPath ?? string.Empty : string.Empty;
            if (string.IsNullOrEmpty(effectiveSettings.AdditionalSourceRoots))
            {
                effectiveSettings.AdditionalSourceRoots = effectiveSettings.WorkspaceRootPath;
            }

            effectiveSettings.DefaultOnboardingProfileId = selection.Profile != null ? selection.Profile.ProfileId : string.Empty;
            effectiveSettings.DefaultOnboardingLayoutPresetId = selection.Layout != null ? selection.Layout.LayoutPresetId : string.Empty;
            effectiveSettings.DefaultOnboardingThemeId = selection.Theme != null ? selection.Theme.ThemeId : string.Empty;
            effectiveSettings.ThemeId = selection.Theme != null ? selection.Theme.ThemeId : effectiveSettings.ThemeId;
            effectiveSettings.HasCompletedOnboarding = true;
            return effectiveSettings;
        }

        private static OnboardingProfileDescriptor FindProfile(WorkbenchCatalogSnapshot catalog, string profileId)
        {
            for (var i = 0; catalog != null && i < catalog.OnboardingProfiles.Count; i++)
            {
                if (string.Equals(catalog.OnboardingProfiles[i].ProfileId, profileId, StringComparison.OrdinalIgnoreCase))
                {
                    return catalog.OnboardingProfiles[i];
                }
            }

            return null;
        }

        private static OnboardingLayoutDescriptor FindLayout(WorkbenchCatalogSnapshot catalog, string layoutPresetId)
        {
            for (var i = 0; catalog != null && i < catalog.OnboardingLayouts.Count; i++)
            {
                if (string.Equals(catalog.OnboardingLayouts[i].LayoutPresetId, layoutPresetId, StringComparison.OrdinalIgnoreCase))
                {
                    return catalog.OnboardingLayouts[i];
                }
            }

            return null;
        }

        private static ThemeDescriptor FindTheme(WorkbenchCatalogSnapshot catalog, string themeId)
        {
            for (var i = 0; catalog != null && i < catalog.Themes.Count; i++)
            {
                if (string.Equals(catalog.Themes[i].ThemeId, themeId, StringComparison.OrdinalIgnoreCase))
                {
                    return catalog.Themes[i];
                }
            }

            return null;
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
    }
}
