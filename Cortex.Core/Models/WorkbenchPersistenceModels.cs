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
    public sealed class PersistedWorkbenchState
    {
        public string FocusedContainerId;
        public string SideContainerId;
        public string SecondarySideContainerId;
        public string EditorContainerId;
        public string PanelContainerId;
        public bool ShowDetachedLogWindow;
        public bool EditorUnlocked;
        public string SelectedProjectModId;
        public string SelectedProjectSourceRoot;
        public string ActiveDocumentPath;
        public string[] OpenDocumentPaths;
        public ContainerHostAssignment[] ContainerHostAssignments;
        public string[] HiddenContainerIds;

        public PersistedWorkbenchState()
        {
            FocusedContainerId = CortexWorkbenchIds.EditorContainer;
            SideContainerId = string.Empty;
            SecondarySideContainerId = CortexWorkbenchIds.ProjectsContainer;
            EditorContainerId = CortexWorkbenchIds.EditorContainer;
            PanelContainerId = CortexWorkbenchIds.LogsContainer;
            ShowDetachedLogWindow = false;
            EditorUnlocked = false;
            SelectedProjectModId = string.Empty;
            SelectedProjectSourceRoot = string.Empty;
            ActiveDocumentPath = string.Empty;
            OpenDocumentPaths = new string[0];
            ContainerHostAssignments = new ContainerHostAssignment[0];
            HiddenContainerIds = new string[0];
        }
    }
}
