using System.Collections.Generic;
using Cortex.Core.Models;
using UnityEngine;

namespace Cortex
{
    public sealed class CortexWindowChromeState
    {
        public Rect ExpandedRect;
        public Rect CollapsedRect;
        public bool IsCollapsed;
    }

    public sealed class CortexWindowChromeWorkspaceState
    {
        public readonly CortexWindowChromeState Main = new CortexWindowChromeState();
        public readonly CortexWindowChromeState Logs = new CortexWindowChromeState();
    }

    public sealed class CortexWorkbenchSelectionState
    {
        public string FocusedContainerId = CortexWorkbenchIds.EditorContainer;
        public string SideContainerId = CortexWorkbenchIds.ProjectsContainer;
        public string EditorContainerId = CortexWorkbenchIds.EditorContainer;
        public string PanelContainerId = CortexWorkbenchIds.LogsContainer;
        public string RequestedContainerId = string.Empty;
        public int RequestedTabIndex = -1;
    }

    public sealed class CortexDocumentWorkspaceState
    {
        public DocumentSession ActiveDocument;
        public string ActiveDocumentPath;
        public bool EditorUnlocked;
        public readonly List<DocumentSession> OpenDocuments = new List<DocumentSession>();
    }

    public sealed class CortexLogSelectionState
    {
        public RuntimeLogEntry SelectedEntry;
        public int SelectedFrameIndex = -1;
        public bool ShowDetachedWindow;
    }

    public sealed class CortexShellState
    {
        public readonly CortexWindowChromeWorkspaceState Chrome = new CortexWindowChromeWorkspaceState();
        public readonly CortexWorkbenchSelectionState Workbench = new CortexWorkbenchSelectionState();
        public readonly CortexDocumentWorkspaceState Documents = new CortexDocumentWorkspaceState();
        public readonly CortexLogSelectionState Logs = new CortexLogSelectionState();
        public CortexProjectDefinition SelectedProject;
        public BuildResult LastBuildResult;
        public DecompilerResponse LastReferenceResult;
        public CortexSettings Settings;
        public string StatusMessage;
        public bool ReloadSettingsRequested;
    }
}
