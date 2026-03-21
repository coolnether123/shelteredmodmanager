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
                context.Coordinator.Open(context.ShellState, context.ShellState.Settings, context.Registry, false);

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
                context.Coordinator.Open(context.ShellState, context.ShellState.Settings, context.Registry, true);
                context.ShellState.Onboarding.SelectedProfileId = OnboardingTestRegistryBuilder.DecompilerProfileId;
                context.ShellState.Onboarding.SelectedLayoutPresetId = OnboardingTestRegistryBuilder.DecompilerLayoutId;
                context.ShellState.Onboarding.SelectedThemeId = OnboardingTestRegistryBuilder.DecompilerThemeId;

                var result = context.Coordinator.Complete(context.ShellState, context.Runtime, context.Registry);
                var persisted = new PersistedWorkbenchState();
                context.Coordinator.PersistToPersistence(context.ShellState.Onboarding, persisted);

                Assert.True(result.WasApplied);
                Assert.False(context.ShellState.Onboarding.IsActive);
                Assert.Equal(OnboardingTestRegistryBuilder.DecompilerProfileId, persisted.ActiveOnboardingProfileId);
                Assert.Equal(OnboardingTestRegistryBuilder.DecompilerLayoutId, persisted.ActiveOnboardingLayoutPresetId);
                Assert.Equal(OnboardingTestRegistryBuilder.DecompilerThemeId, persisted.ActiveOnboardingThemeId);
            });
        }
    }
}
