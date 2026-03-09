using System;
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
        public string SecondarySideContainerId = string.Empty;
        public string EditorContainerId = CortexWorkbenchIds.EditorContainer;
        public string PanelContainerId = CortexWorkbenchIds.LogsContainer;
        public string RequestedContainerId = string.Empty;
        public int RequestedTabIndex = -1;
        public readonly Dictionary<string, WorkbenchHostLocation> HostOverrides = new Dictionary<string, WorkbenchHostLocation>(StringComparer.OrdinalIgnoreCase);

        public WorkbenchHostLocation GetAssignedHost(string containerId, WorkbenchHostLocation fallback)
        {
            WorkbenchHostLocation hostLocation;
            return !string.IsNullOrEmpty(containerId) && HostOverrides.TryGetValue(containerId, out hostLocation)
                ? hostLocation
                : fallback;
        }

        public void AssignHost(string containerId, WorkbenchHostLocation hostLocation)
        {
            if (string.IsNullOrEmpty(containerId))
            {
                return;
            }

            HostOverrides[containerId] = hostLocation;
        }
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

    public sealed class CortexInterfaceDiagnosticState
    {
        public readonly List<string> Entries = new List<string>();

        public void Add(string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                return;
            }

            Entries.Add(System.DateTime.Now.ToString("HH:mm:ss") + "  " + message);
            if (Entries.Count > 40)
            {
                Entries.RemoveAt(0);
            }
        }
    }

    public sealed class CortexShellState
    {
        public readonly CortexWindowChromeWorkspaceState Chrome = new CortexWindowChromeWorkspaceState();
        public readonly CortexWorkbenchSelectionState Workbench = new CortexWorkbenchSelectionState();
        public readonly CortexDocumentWorkspaceState Documents = new CortexDocumentWorkspaceState();
        public readonly CortexLogSelectionState Logs = new CortexLogSelectionState();
        public readonly CortexInterfaceDiagnosticState Diagnostics = new CortexInterfaceDiagnosticState();
        public CortexProjectDefinition SelectedProject;
        public BuildResult LastBuildResult;
        public DecompilerResponse LastReferenceResult;
        public CortexSettings Settings;
        public string StatusMessage;
        public bool ReloadSettingsRequested;
    }
}
