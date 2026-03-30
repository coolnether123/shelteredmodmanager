using System;

namespace Cortex.Core.Models
{
    [Serializable]
    public sealed class ContainerHostAssignment
    {
        public string ContainerId;
        public WorkbenchHostLocation HostLocation;

        public ContainerHostAssignment()
        {
            ContainerId = string.Empty;
            HostLocation = WorkbenchHostLocation.PrimarySideHost;
        }
    }

    [Serializable]
    public sealed class PersistedModuleStateEntry
    {
        public string ModuleId;
        public string Key;
        public string Value;

        public PersistedModuleStateEntry()
        {
            ModuleId = string.Empty;
            Key = string.Empty;
            Value = string.Empty;
        }
    }

    [Serializable]
    public sealed class PersistedWorkbenchState
    {
        public string FocusedContainerId;
        public string SideContainerId;
        public string SecondarySideContainerId;
        public string EditorContainerId;
        public string PanelContainerId;
        public bool ShowDetachedLogWindow;
        public string SelectedProjectModId;
        public string SelectedProjectSourceRoot;
        public string ActiveDocumentPath;
        public string[] OpenDocumentPaths;
        public ContainerHostAssignment[] ContainerHostAssignments;
        public string[] HiddenContainerIds;
        public PersistedModuleStateEntry[] ModulePersistentStateEntries;
        public string ActiveOnboardingProfileId;
        public string ActiveOnboardingLayoutPresetId;
        public string ActiveOnboardingThemeId;

        public PersistedWorkbenchState()
        {
            FocusedContainerId = CortexWorkbenchIds.EditorContainer;
            SideContainerId = string.Empty;
            SecondarySideContainerId = CortexWorkbenchIds.ProjectsContainer;
            EditorContainerId = CortexWorkbenchIds.EditorContainer;
            PanelContainerId = CortexWorkbenchIds.LogsContainer;
            ShowDetachedLogWindow = false;
            SelectedProjectModId = string.Empty;
            SelectedProjectSourceRoot = string.Empty;
            ActiveDocumentPath = string.Empty;
            OpenDocumentPaths = new string[0];
            ContainerHostAssignments = new ContainerHostAssignment[0];
            HiddenContainerIds = new string[0];
            ModulePersistentStateEntries = new PersistedModuleStateEntry[0];
            ActiveOnboardingProfileId = string.Empty;
            ActiveOnboardingLayoutPresetId = string.Empty;
            ActiveOnboardingThemeId = string.Empty;
        }
    }
}
