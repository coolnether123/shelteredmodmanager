namespace Cortex.Shell.Shared.Models
{
    public sealed class OnboardingState
    {
        public string SelectedProfileId { get; set; } = string.Empty;
        public string SelectedLayoutPresetId { get; set; } = string.Empty;
        public string SelectedThemeId { get; set; } = string.Empty;
        public string SelectedWorkspaceRootPath { get; set; } = string.Empty;
        public int ActiveStepIndex { get; set; }
    }

    public sealed class OnboardingStepModel
    {
        public OnboardingStepModel()
        {
        }

        public OnboardingStepModel(string stepId, string title, string description)
        {
            StepId = stepId ?? string.Empty;
            Title = title ?? string.Empty;
            Description = description ?? string.Empty;
        }

        public string StepId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    public sealed class OnboardingFlowModel
    {
        public System.Collections.Generic.List<OnboardingStepModel> Steps { get; set; } = new System.Collections.Generic.List<OnboardingStepModel>();
        public int ActiveStepIndex { get; set; }
    }

    public sealed class OnboardingResolvedSelection
    {
        public ThemeDescriptor Theme { get; set; }
        public OnboardingProfileDescriptor Profile { get; set; }
        public OnboardingLayoutDescriptor Layout { get; set; }
    }
}
