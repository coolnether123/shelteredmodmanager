using System.Collections.Generic;

namespace Cortex.Services.Onboarding
{
    internal enum CortexOnboardingStepKind
    {
        Profile = 0,
        Projects = 1,
        Layout = 2,
        Theme = 3
    }

    internal sealed class CortexOnboardingStepModel
    {
        public readonly string StepId;
        public readonly string Label;
        public readonly string Title;
        public readonly string Description;
        public readonly bool IsScrollable;
        public readonly CortexOnboardingStepKind StepKind;

        public CortexOnboardingStepModel(
            string stepId,
            string label,
            string title,
            string description,
            bool isScrollable,
            CortexOnboardingStepKind stepKind)
        {
            StepId = stepId ?? string.Empty;
            Label = label ?? string.Empty;
            Title = title ?? string.Empty;
            Description = description ?? string.Empty;
            IsScrollable = isScrollable;
            StepKind = stepKind;
        }
    }

    internal sealed class CortexOnboardingFlowModel
    {
        public readonly List<CortexOnboardingStepModel> Steps = new List<CortexOnboardingStepModel>();
        public int ActiveStepIndex;

        public CortexOnboardingStepModel ActiveStep
        {
            get
            {
                return ActiveStepIndex >= 0 && ActiveStepIndex < Steps.Count
                    ? Steps[ActiveStepIndex]
                    : null;
            }
        }
    }
}
