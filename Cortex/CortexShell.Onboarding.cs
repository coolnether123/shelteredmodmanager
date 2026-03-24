using Cortex.Core.Models;
using Cortex.Presentation.Abstractions;
using Cortex.Services;
using UnityEngine;

namespace Cortex
{
    public sealed partial class CortexShellController
    {
        private void OpenOnboarding() => OpenOnboarding(true);

        private void OpenOnboarding(bool reopenedByUser)
        {
            if (_workbenchRuntime == null || _workbenchRuntime.ContributionRegistry == null) return;

            _onboardingCoordinator.Open(
                _state,
                _state.Settings,
                _workbenchRuntime.ContributionRegistry,
                _loadedModCatalog,
                _projectCatalog,
                _projectWorkspaceService,
                reopenedByUser);
            _sessionCoordinator.Visible = true;
            _openMenuGroup = string.Empty;
        }

        private bool IsOnboardingActive() => _state.Onboarding?.IsActive ?? false;

        private void PreviewOnboardingSelections()
        {
            _onboardingLifecycle.Preview(
                _onboardingCoordinator,
                _state,
                _workbenchRuntime,
                _workbenchRuntime?.ContributionRegistry,
                ActivateOnboardingContainers);
        }

        private void CompleteOnboarding()
        {
            _onboardingLifecycle.Complete(
                _onboardingCoordinator,
                _state,
                _workbenchRuntime,
                _workbenchRuntime?.ContributionRegistry,
                _projectCatalog,
                _projectWorkspaceService,
                PersistWorkbenchSession,
                PersistWindowSettings,
                ActivateOnboardingContainers);
        }

        private void ActivateOnboardingContainers(CortexOnboardingWorkspaceApplicationResult result)
        {
            if (result?.ContainersToActivate == null) return;
            foreach (var containerId in result.ContainersToActivate) ActivateContainer(containerId);
        }

        private static void DrawOnboardingBlock(Rect rect, Color color)
        {
            if (Event.current?.type != EventType.Repaint) return;
            var prev = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = prev;
        }
    }
}
