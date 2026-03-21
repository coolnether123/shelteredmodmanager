using Cortex.Tests.Testing;
using Xunit;

namespace Cortex.Tests.Onboarding
{
    public sealed class CortexOnboardingServiceTests
    {
        [Fact]
        public void SeedSelections_UsesRegistryDefaults_WhenSettingsAreEmpty()
        {
            UnityManagedAssemblyResolver.Run(delegate
            {
                var context = new OnboardingTestContext();

                context.Service.SeedSelections(context.ShellState.Onboarding, context.ShellState.Settings, context.Registry);

                Assert.Equal(OnboardingTestRegistryBuilder.IdeProfileId, context.ShellState.Onboarding.SelectedProfileId);
                Assert.Equal(OnboardingTestRegistryBuilder.VisualStudioLayoutId, context.ShellState.Onboarding.SelectedLayoutPresetId);
                Assert.Equal(OnboardingTestRegistryBuilder.VisualStudioThemeId, context.ShellState.Onboarding.SelectedThemeId);
                Assert.False(context.ShellState.Onboarding.KeepFocused);
                Assert.Equal(0, context.ShellState.Onboarding.ActiveStepIndex);
            });
        }

        [Fact]
        public void SelectProfile_UsesProfileDefaults_UntilUserOverridesLayoutAndTheme()
        {
            UnityManagedAssemblyResolver.Run(delegate
            {
                var context = new OnboardingTestContext();
                var catalog = context.BuildCatalog();
                context.Service.PrepareSession(context.ShellState.Onboarding, context.ShellState.Settings, catalog);

                context.Service.SelectProfile(context.ShellState.Onboarding, catalog, OnboardingTestRegistryBuilder.DecompilerProfileId);

                Assert.Equal(OnboardingTestRegistryBuilder.DecompilerLayoutId, context.ShellState.Onboarding.SelectedLayoutPresetId);
                Assert.Equal(OnboardingTestRegistryBuilder.DecompilerThemeId, context.ShellState.Onboarding.SelectedThemeId);

                context.Service.SelectLayoutPreset(context.ShellState.Onboarding, catalog, OnboardingTestRegistryBuilder.VisualStudioLayoutId);
                context.Service.SelectTheme(context.ShellState.Onboarding, OnboardingTestRegistryBuilder.AccentThemeId);
                context.Service.SelectProfile(context.ShellState.Onboarding, catalog, OnboardingTestRegistryBuilder.IdeProfileId);

                Assert.Equal(OnboardingTestRegistryBuilder.VisualStudioLayoutId, context.ShellState.Onboarding.SelectedLayoutPresetId);
                Assert.Equal(OnboardingTestRegistryBuilder.AccentThemeId, context.ShellState.Onboarding.SelectedThemeId);
            });
        }

        [Fact]
        public void ResolveSelection_PrefersPersistedSettingsBeforeContributionFallbacks()
        {
            UnityManagedAssemblyResolver.Run(delegate
            {
                var context = new OnboardingTestContext();
                context.ShellState.Settings.DefaultOnboardingProfileId = OnboardingTestRegistryBuilder.DecompilerProfileId;
                context.ShellState.Settings.DefaultOnboardingLayoutPresetId = OnboardingTestRegistryBuilder.DecompilerLayoutId;
                context.ShellState.Settings.DefaultOnboardingThemeId = OnboardingTestRegistryBuilder.AccentThemeId;

                var selection = context.Service.ResolveSelection(context.ShellState.Onboarding, context.ShellState.Settings, context.BuildCatalog());

                Assert.NotNull(selection);
                Assert.Equal(OnboardingTestRegistryBuilder.DecompilerProfileId, selection.Profile.ProfileId);
                Assert.Equal(OnboardingTestRegistryBuilder.DecompilerLayoutId, selection.LayoutPreset.LayoutPresetId);
                Assert.Equal(OnboardingTestRegistryBuilder.AccentThemeId, selection.Theme.ThemeId);
            });
        }
    }
}
