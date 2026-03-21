using Cortex.Core.Models;
using Cortex.Tests.Testing;
using Xunit;

namespace Cortex.Tests.Onboarding
{
    public sealed class CortexOnboardingCoordinatorTests
    {
        [Fact]
        public void PreviewIfNeeded_SkipsReapplying_WhenFingerprintHasNotChanged()
        {
            UnityManagedAssemblyResolver.Run(delegate
            {
                var context = new OnboardingTestContext();
                context.Coordinator.Open(
                    context.ShellState,
                    context.ShellState.Settings,
                    context.Registry,
                    context.LoadedModCatalog,
                    context.ProjectCatalog,
                    context.ProjectWorkspaceService,
                    false);

                var firstResult = context.Coordinator.PreviewIfNeeded(context.ShellState, context.Runtime, context.Registry);
                var secondResult = context.Coordinator.PreviewIfNeeded(context.ShellState, context.Runtime, context.Registry);

                Assert.True(firstResult.WasApplied);
                Assert.False(secondResult.WasApplied);
            });
        }

        [Fact]
        public void Complete_AppliesSelection_AndLeavesPersistenceAsSeparateConcern()
        {
            UnityManagedAssemblyResolver.Run(delegate
            {
                var context = new OnboardingTestContext();
                context.Coordinator.Open(
                    context.ShellState,
                    context.ShellState.Settings,
                    context.Registry,
                    context.LoadedModCatalog,
                    context.ProjectCatalog,
                    context.ProjectWorkspaceService,
                    true);
                context.ShellState.Onboarding.SelectedProfileId = OnboardingTestRegistryBuilder.DecompilerProfileId;
                context.ShellState.Onboarding.SelectedLayoutPresetId = OnboardingTestRegistryBuilder.DecompilerLayoutId;
                context.ShellState.Onboarding.SelectedThemeId = OnboardingTestRegistryBuilder.DecompilerThemeId;

                var result = context.Coordinator.Complete(
                    context.ShellState,
                    context.Runtime,
                    context.Registry,
                    context.ProjectCatalog,
                    context.ProjectWorkspaceService);
                var persisted = new PersistedWorkbenchState();
                context.Coordinator.PersistToPersistence(context.ShellState.Onboarding, persisted);

                Assert.True(result.WasApplied);
                Assert.False(context.ShellState.Onboarding.IsActive);
                Assert.Equal(OnboardingTestRegistryBuilder.DecompilerProfileId, persisted.ActiveOnboardingProfileId);
                Assert.Equal(OnboardingTestRegistryBuilder.DecompilerLayoutId, persisted.ActiveOnboardingLayoutPresetId);
                Assert.Equal(OnboardingTestRegistryBuilder.DecompilerThemeId, persisted.ActiveOnboardingThemeId);
            });
        }

        [Fact]
        public void Complete_LinksOwnedModProjects_ForModderProfile()
        {
            UnityManagedAssemblyResolver.Run(delegate
            {
                var context = new OnboardingTestContext();
                context.Coordinator.Open(
                    context.ShellState,
                    context.ShellState.Settings,
                    context.Registry,
                    context.LoadedModCatalog,
                    context.ProjectCatalog,
                    context.ProjectWorkspaceService,
                    false);
                context.ShellState.Onboarding.SelectedProfileId = OnboardingTestRegistryBuilder.ModderProfileId;
                context.ShellState.Onboarding.SelectedWorkspaceRootPath = context.WorkspaceRootPath;
                context.ShellState.Onboarding.ModProjectDrafts[0].IsOwnedByUser = true;
                context.ShellState.Onboarding.ModProjectDrafts[0].SourceRootPath = context.ModSourceRootPath;

                var result = context.Coordinator.Complete(
                    context.ShellState,
                    context.Runtime,
                    context.Registry,
                    context.ProjectCatalog,
                    context.ProjectWorkspaceService);

                var project = context.ProjectCatalog.GetProject("TestMod");
                Assert.True(result.WasApplied);
                Assert.NotNull(project);
                Assert.Equal(context.ModSourceRootPath, project.SourceRootPath);
                Assert.Equal(context.WorkspaceRootPath, context.ShellState.Settings.WorkspaceRootPath);
            });
        }
    }
}
