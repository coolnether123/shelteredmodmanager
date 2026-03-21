using System;

namespace Cortex.Core.Models
{
    public enum OnboardingProfileWorkflowKind
    {
        Standard = 0,
        Modder = 1
    }

    [Serializable]
    public sealed class OnboardingProfileContribution
    {
        public string ProfileId;
        public string DisplayName;
        public string Description;
        public string DefaultLayoutPresetId;
        public string DefaultThemeId;
        public string[] PreviewTags;
        public string[] Keywords;
        public OnboardingProfileWorkflowKind WorkflowKind;
        public bool IsDefault;
        public int SortOrder;

        public OnboardingProfileContribution()
        {
            ProfileId = string.Empty;
            DisplayName = string.Empty;
            Description = string.Empty;
            DefaultLayoutPresetId = string.Empty;
            DefaultThemeId = string.Empty;
            PreviewTags = new string[0];
            Keywords = new string[0];
            WorkflowKind = OnboardingProfileWorkflowKind.Standard;
            IsDefault = false;
            SortOrder = 0;
        }
    }

    [Serializable]
    public sealed class OnboardingContainerHostAssignment
    {
        public string ContainerId;
        public WorkbenchHostLocation HostLocation;

        public OnboardingContainerHostAssignment()
        {
            ContainerId = string.Empty;
            HostLocation = WorkbenchHostLocation.PrimarySideHost;
        }
    }

    [Serializable]
    public sealed class OnboardingLayoutPresetContribution
    {
        public string LayoutPresetId;
        public string DisplayName;
        public string Description;
        public string DefaultThemeId;
        public string DefaultFocusedContainerId;
        public string DefaultPrimarySideContainerId;
        public string DefaultSecondarySideContainerId;
        public string DefaultPanelContainerId;
        public string DefaultEditorContainerId;
        public string PreviewPrimaryLabel;
        public string PreviewSecondaryLabel;
        public string PreviewCenterLabel;
        public string PreviewPanelLabel;
        public OnboardingContainerHostAssignment[] ContainerHostAssignments;
        public string[] HiddenContainerIds;
        public string[] Keywords;
        public float PrimarySideWidth;
        public float SecondarySideWidth;
        public float PanelSize;
        public bool IsDefault;
        public int SortOrder;

        public OnboardingLayoutPresetContribution()
        {
            LayoutPresetId = string.Empty;
            DisplayName = string.Empty;
            Description = string.Empty;
            DefaultThemeId = string.Empty;
            DefaultFocusedContainerId = string.Empty;
            DefaultPrimarySideContainerId = string.Empty;
            DefaultSecondarySideContainerId = string.Empty;
            DefaultPanelContainerId = string.Empty;
            DefaultEditorContainerId = string.Empty;
            PreviewPrimaryLabel = string.Empty;
            PreviewSecondaryLabel = string.Empty;
            PreviewCenterLabel = string.Empty;
            PreviewPanelLabel = string.Empty;
            ContainerHostAssignments = new OnboardingContainerHostAssignment[0];
            HiddenContainerIds = new string[0];
            Keywords = new string[0];
            PrimarySideWidth = 360f;
            SecondarySideWidth = 320f;
            PanelSize = 280f;
            IsDefault = false;
            SortOrder = 0;
        }
    }
}
