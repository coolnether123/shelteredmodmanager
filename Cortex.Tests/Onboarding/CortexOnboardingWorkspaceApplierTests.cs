using Cortex.Core.Models;
using Cortex.Tests.Testing;
using Xunit;

namespace Cortex.Tests.Onboarding
{
    public sealed class CortexOnboardingWorkspaceApplierTests
    {
        [Fact]
        public void Preview_AppliesWorkspaceShape_WithoutCommittingSettingsDefaults()
        {
            UnityManagedAssemblyResolver.Run(delegate
            {
                var context = new OnboardingTestContext();
                context.Service.SeedSelections(context.ShellState.Onboarding, context.ShellState.Settings, context.Registry);

                var selection = context.ResolveSelection();
                var result = context.WorkspaceApplier.Preview(context.ShellState, context.ViewState, context.Runtime, selection);

                Assert.True(result.WasApplied);
                Assert.Equal(CortexWorkbenchIds.ProjectsContainer, context.ShellState.Workbench.SideContainerId);
                Assert.Equal(CortexWorkbenchIds.FileExplorerContainer, context.ShellState.Workbench.SecondarySideContainerId);
                Assert.Equal(CortexWorkbenchIds.LogsContainer, context.ShellState.Workbench.PanelContainerId);
                Assert.Equal(OnboardingTestRegistryBuilder.VisualStudioThemeId, context.Runtime.ThemeState.ThemeId);
                Assert.Equal(string.Empty, context.ShellState.Onboarding.ActiveProfileId);
                Assert.False(context.ShellState.Settings.HasCompletedOnboarding);
                Assert.Equal(string.Empty, context.ShellState.Settings.DefaultOnboardingProfileId);
            });
        }

        [Fact]
        public void Apply_CommitsResolvedSelection_ToOnboardingStateAndSettings()
        {
            UnityManagedAssemblyResolver.Run(delegate
            {
                var context = new OnboardingTestContext();
                var catalog = context.BuildCatalog();
                context.Service.PrepareSession(context.ShellState.Onboarding, context.ShellState.Settings, catalog);
                context.Service.SelectProfile(context.ShellState.Onboarding, catalog, OnboardingTestRegistryBuilder.DecompilerProfileId);

                var selection = context.ResolveSelection();
                var result = context.WorkspaceApplier.Apply(context.ShellState, context.ViewState, context.Runtime, selection);

                Assert.True(result.WasApplied);
                Assert.Equal(OnboardingTestRegistryBuilder.DecompilerProfileId, context.ShellState.Onboarding.ActiveProfileId);
                Assert.Equal(OnboardingTestRegistryBuilder.DecompilerLayoutId, context.ShellState.Onboarding.ActiveLayoutPresetId);
                Assert.Equal(OnboardingTestRegistryBuilder.DecompilerThemeId, context.ShellState.Onboarding.ActiveThemeId);
                Assert.Equal(OnboardingTestRegistryBuilder.DecompilerProfileId, context.ShellState.Settings.DefaultOnboardingProfileId);
                Assert.Equal(OnboardingTestRegistryBuilder.DecompilerLayoutId, context.ShellState.Settings.DefaultOnboardingLayoutPresetId);
                Assert.Equal(OnboardingTestRegistryBuilder.DecompilerThemeId, context.ShellState.Settings.DefaultOnboardingThemeId);
                Assert.Equal(OnboardingTestRegistryBuilder.DecompilerThemeId, context.ShellState.Settings.ThemeId);
                Assert.True(context.ShellState.Settings.HasCompletedOnboarding);
                Assert.Contains(CortexWorkbenchIds.EditorContainer, result.ContainersToActivate);
                Assert.DoesNotContain(CortexWorkbenchIds.LogsContainer, result.ContainersToActivate);
            });
        }
    }
}
