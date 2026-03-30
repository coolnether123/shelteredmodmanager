using System;
using Cortex.Core.Abstractions;
using Cortex.Presentation.Abstractions;
using Cortex.Services.Onboarding;

namespace Cortex.Shell
{
    internal sealed class CortexShellOnboardingLifecycle
    {
        public CortexOnboardingWorkspaceApplicationResult Preview(
            CortexOnboardingCoordinator onboardingCoordinator,
            CortexShellState shellState,
            IWorkbenchRuntime workbenchRuntime,
            IContributionRegistry contributionRegistry,
            Action<CortexOnboardingWorkspaceApplicationResult> activateContainers)
        {
            if (onboardingCoordinator == null ||
                shellState == null ||
                shellState.Onboarding == null ||
                !shellState.Onboarding.IsActive ||
                workbenchRuntime == null ||
                contributionRegistry == null)
            {
                return CortexOnboardingWorkspaceApplicationResult.Empty;
            }

            var result = onboardingCoordinator.PreviewIfNeeded(shellState, workbenchRuntime, contributionRegistry);
            activateContainers?.Invoke(result);
            return result;
        }

        public CortexOnboardingWorkspaceApplicationResult Complete(
            CortexOnboardingCoordinator onboardingCoordinator,
            CortexShellState shellState,
            IWorkbenchRuntime workbenchRuntime,
            IContributionRegistry contributionRegistry,
            IProjectCatalog projectCatalog,
            IProjectWorkspaceService workspaceService,
            Action persistWorkbenchSession,
            Action persistWindowSettings,
            Action<CortexOnboardingWorkspaceApplicationResult> activateContainers)
        {
            if (onboardingCoordinator == null || workbenchRuntime == null || contributionRegistry == null)
            {
                return CortexOnboardingWorkspaceApplicationResult.Empty;
            }

            var result = onboardingCoordinator.Complete(
                shellState,
                workbenchRuntime,
                contributionRegistry,
                projectCatalog,
                workspaceService);
            if (!result.WasApplied)
            {
                return result;
            }

            persistWorkbenchSession?.Invoke();
            persistWindowSettings?.Invoke();
            activateContainers?.Invoke(result);
            if (shellState != null)
            {
                shellState.StatusMessage = "Onboarding complete.";
            }

            return result;
        }
    }
}
