using System;
using Cortex.Core.Models;

namespace Cortex.Services.Onboarding
{
    internal sealed class CortexOnboardingFlowService
    {
        public CortexOnboardingFlowModel BuildFlow(
            CortexOnboardingState onboardingState,
            CortexOnboardingCatalog catalog,
            CortexOnboardingService onboardingService)
        {
            var flow = new CortexOnboardingFlowModel();
            var selectedProfile = ResolveSelectedProfile(onboardingState, catalog, onboardingService);

            flow.Steps.Add(new CortexOnboardingStepModel(
                "profile",
                "Profile",
                "Choose your starting profile",
                "Pick the default posture Cortex should start from.",
                false,
                CortexOnboardingStepKind.Profile));

            if (selectedProfile != null && selectedProfile.WorkflowKind == OnboardingProfileWorkflowKind.Modder)
            {
                flow.Steps.Add(new CortexOnboardingStepModel(
                    "projects",
                    "Projects",
                    "Link active content to source roots",
                    "Mark which active content items you maintain, then point Cortex at the source roots it should treat as editable worktrees.",
                    true,
                    CortexOnboardingStepKind.Projects));
            }

            flow.Steps.Add(new CortexOnboardingStepModel(
                "layout",
                "Layout",
                "Choose a layout style",
                "Select the shell arrangement you want Cortex to apply after onboarding.",
                false,
                CortexOnboardingStepKind.Layout));

            flow.Steps.Add(new CortexOnboardingStepModel(
                "theme",
                "Theme",
                "Choose a theme",
                "Keep it simple for v1 with built-in themes. Custom colors can layer on top later.",
                true,
                CortexOnboardingStepKind.Theme));

            flow.ActiveStepIndex = ClampStepIndex(onboardingState != null ? onboardingState.ActiveStepIndex : 0, flow.Steps.Count);
            return flow;
        }

        public void ClampActiveStepIndex(CortexOnboardingState onboardingState, CortexOnboardingFlowModel flow)
        {
            if (onboardingState == null)
            {
                return;
            }

            onboardingState.ActiveStepIndex = flow != null
                ? flow.ActiveStepIndex
                : 0;
        }

        public bool CanMovePrevious(CortexOnboardingFlowModel flow)
        {
            return flow != null && flow.ActiveStepIndex > 0;
        }

        public bool CanMoveNext(CortexOnboardingFlowModel flow)
        {
            return flow != null && flow.ActiveStepIndex < flow.Steps.Count - 1;
        }

        public bool IsFinalStep(CortexOnboardingFlowModel flow)
        {
            return flow != null && flow.Steps.Count > 0 && flow.ActiveStepIndex >= flow.Steps.Count - 1;
        }

        public void MovePrevious(CortexOnboardingState onboardingState, CortexOnboardingFlowModel flow)
        {
            if (onboardingState == null || flow == null)
            {
                return;
            }

            onboardingState.ActiveStepIndex = ClampStepIndex(flow.ActiveStepIndex - 1, flow.Steps.Count);
        }

        public void MoveNext(CortexOnboardingState onboardingState, CortexOnboardingFlowModel flow)
        {
            if (onboardingState == null || flow == null)
            {
                return;
            }

            onboardingState.ActiveStepIndex = ClampStepIndex(flow.ActiveStepIndex + 1, flow.Steps.Count);
        }

        private static int ClampStepIndex(int stepIndex, int totalSteps)
        {
            if (totalSteps <= 0)
            {
                return 0;
            }

            return Math.Max(0, Math.Min(totalSteps - 1, stepIndex));
        }

        private static OnboardingProfileContribution ResolveSelectedProfile(
            CortexOnboardingState onboardingState,
            CortexOnboardingCatalog catalog,
            CortexOnboardingService onboardingService)
        {
            if (catalog == null || onboardingService == null)
            {
                return null;
            }

            var profile = onboardingService.FindProfile(catalog, onboardingState != null ? onboardingState.SelectedProfileId : string.Empty);
            if (profile != null)
            {
                return profile;
            }

            return catalog.Profiles.Count > 0 ? catalog.Profiles[0] : null;
        }
    }
}
