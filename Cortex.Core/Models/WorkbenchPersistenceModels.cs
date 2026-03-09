using System;

namespace Cortex.Core.Models
{
    [Serializable]
    public sealed class PersistedWorkbenchState
    {
        public string FocusedContainerId;
        public string SideContainerId;
        public string EditorContainerId;
        public string PanelContainerId;
        public bool ShowDetachedLogWindow;
        public bool EditorUnlocked;
        public string ActiveDocumentPath;
        public string[] OpenDocumentPaths;

        public PersistedWorkbenchState()
        {
            FocusedContainerId = CortexWorkbenchIds.EditorContainer;
            SideContainerId = CortexWorkbenchIds.ProjectsContainer;
            EditorContainerId = CortexWorkbenchIds.EditorContainer;
            PanelContainerId = CortexWorkbenchIds.LogsContainer;
            ShowDetachedLogWindow = false;
            EditorUnlocked = false;
            ActiveDocumentPath = string.Empty;
            OpenDocumentPaths = new string[0];
        }
    }
}
