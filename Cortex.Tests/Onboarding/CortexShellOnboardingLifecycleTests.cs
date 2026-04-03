using System.Collections.Generic;
using Cortex.Core.Models;
using Cortex.Tests.Testing;
using Xunit;
using Cortex.Services.Onboarding;
using Cortex.Shell;

namespace Cortex.Tests.Onboarding
{
    public sealed class CortexShellOnboardingLifecycleTests
    {
        [Fact]
        public void Preview_ActivatesContainers_FromCoordinatorPreview()
        {
            UnityManagedAssemblyResolver.Run(delegate
            {
                var context = new OnboardingTestContext();
                var lifecycle = new CortexShellOnboardingLifecycle();
                var activatedContainers = new List<string>();

                context.Coordinator.Open(
                    context.ShellState,
                    context.ShellState.Settings,
                    context.Registry,
                    context.LoadedModCatalog,
                    context.ProjectCatalog,
                    context.ProjectWorkspaceService,
                    false);

                var result = lifecycle.Preview(
                    context.Coordinator,
                    context.ShellState,
                    context.ViewState,
                    context.Runtime,
                    context.Registry,
                    delegate(CortexOnboardingWorkspaceApplicationResult applicationResult)
                    {
                        activatedContainers.AddRange(applicationResult.ContainersToActivate);
                    });

                Assert.True(result.WasApplied);
                Assert.Contains(CortexWorkbenchIds.EditorContainer, activatedContainers);
                Assert.Contains(CortexWorkbenchIds.ProjectsContainer, activatedContainers);
                Assert.Equal(OnboardingTestRegistryBuilder.VisualStudioThemeId, context.Runtime.ThemeState.ThemeId);
            });
        }

        [Fact]
        public void Complete_PersistsOnce_ActivatesContainers_AndUpdatesShellStatus()
        {
            UnityManagedAssemblyResolver.Run(delegate
            {
                var context = new OnboardingTestContext();
                var lifecycle = new CortexShellOnboardingLifecycle();
                var activatedContainers = new List<string>();
                var persistWorkbenchSessionCalls = 0;
                var persistWindowSettingsCalls = 0;

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

                var result = lifecycle.Complete(
                    context.Coordinator,
                    context.ShellState,
                    context.ViewState,
                    context.Runtime,
                    context.Registry,
                    context.ProjectCatalog,
                    context.ProjectWorkspaceService,
                    delegate { persistWorkbenchSessionCalls++; },
                    delegate { persistWindowSettingsCalls++; },
                    delegate(CortexOnboardingWorkspaceApplicationResult applicationResult)
                    {
                        activatedContainers.AddRange(applicationResult.ContainersToActivate);
                    });

                Assert.True(result.WasApplied);
                Assert.Equal(1, persistWorkbenchSessionCalls);
                Assert.Equal(1, persistWindowSettingsCalls);
                Assert.False(context.ShellState.Onboarding.IsActive);
                Assert.True(context.ShellState.Settings.HasCompletedOnboarding);
                Assert.Equal("Onboarding complete.", context.ShellState.StatusMessage);
                Assert.Contains(CortexWorkbenchIds.EditorContainer, activatedContainers);
                Assert.DoesNotContain(CortexWorkbenchIds.LogsContainer, activatedContainers);
            });
        }
    }
}
