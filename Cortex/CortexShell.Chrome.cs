using System;
using System.Collections.Generic;
using Cortex.Chrome;
using Cortex.Core.Models;
using Cortex.Modules.Shared;
using Cortex.Presentation.Models;
using UnityEngine;

namespace Cortex
{
    public sealed partial class CortexShell
    {
        private void DrawHeader(WorkbenchPresentationSnapshot snapshot)
        {
            GUILayout.BeginVertical(_sectionStyle);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Cortex In-Game IDE", _titleStyle);
            GUILayout.FlexibleSpace();
            if (_state.SelectedProject != null)
            {
                GUILayout.Label("Active Project: " + _state.SelectedProject.GetDisplayName());
            }
            GUILayout.Space(8f);
            var actions = new List<CortexWindowAction>();
            actions.Add(BuildGlyphWindowAction(
                "shell.collapse",
                "_",
                "Minimize Cortex",
                delegate
                {
                    _windowRect = CortexWindowChromeController.ToggleCollapsed(_state.Chrome.Main, _windowRect, 126f, 28f);
                }));
            actions.Add(new CortexWindowAction
            {
                ActionId = "shell.logs",
                Label = _windowRect.width < 1220f ? "Logs" : (_state.Logs.ShowDetachedWindow ? "Hide Logs" : "Show Logs"),
                ToolTip = _state.Logs.ShowDetachedWindow ? "Hide detached logs window" : "Show detached logs window",
                Width = _windowRect.width < 1220f ? 64f : 104f,
                Height = 22f,
                Execute = delegate { ExecuteCommand("cortex.logs.toggleWindow", null); }
            });
            actions.Add(new CortexWindowAction
            {
                ActionId = "shell.fit",
                Label = _windowRect.width < 1220f ? "Fit" : "Fit Screen",
                ToolTip = "Fit Cortex to the current game view",
                Width = _windowRect.width < 1220f ? 48f : 84f,
                Height = 22f,
                Execute = delegate { ExecuteCommand("cortex.shell.fitWindow", null); }
            });
            actions.Add(BuildGlyphWindowAction(
                "shell.close",
                "X",
                "Close Cortex",
                delegate
                {
                    PersistWorkbenchSession();
                    PersistWindowSettings();
                    _visible = false;
                }));
            CortexWindowChromeController.DrawActions(actions);
            GUILayout.EndHorizontal();
            DrawContributionToolbar(snapshot);
            GUILayout.Space(4f);
            GUILayout.Label("Toggle: F8 | Side tools, editors, and bottom panels stay separated so logs remain visible while you work.", _captionStyle);
            if (snapshot != null && !string.IsNullOrEmpty(snapshot.RendererSummary))
            {
                GUILayout.Label("Backend: " + snapshot.RendererSummary, _captionStyle);
            }
            GUILayout.EndVertical();
        }

        private void DrawWorkbenchSurface(WorkbenchPresentationSnapshot snapshot)
        {
            var isDragging = !string.IsNullOrEmpty(_draggingContainerId);
            var leftVisible = HasHostItems(snapshot, WorkbenchHostLocation.PrimarySideHost) || isDragging;
            var rightVisible = HasHostItems(snapshot, WorkbenchHostLocation.SecondarySideHost) || isDragging;
            var panelVisible = HasHostItems(snapshot, WorkbenchHostLocation.PanelHost) || isDragging;

            if (_workbenchRuntime != null)
            {
                _workbenchRuntime.WorkbenchState.PrimarySideHostVisible = leftVisible;
                _workbenchRuntime.WorkbenchState.SecondarySideHostVisible = rightVisible;
                _workbenchRuntime.WorkbenchState.PanelHostVisible = panelVisible;
            }

            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            if (leftVisible)
            {
                DrawDockHost(snapshot, WorkbenchHostLocation.PrimarySideHost, Mathf.Clamp(_workbenchRuntime != null ? _workbenchRuntime.LayoutState.PrimarySideWidth : 320f, 240f, 460f));
                if (_workbenchRuntime != null)
                {
                    _workbenchRuntime.LayoutState.PrimarySideWidth = CortexWindowChromeController.DrawVerticalSplitter(0xC101, _workbenchRuntime.LayoutState.PrimarySideWidth, 240f, 460f, 6f, false);
                }
            }

            if (leftVisible)
            {
                GUILayout.Space(4f);
            }
            DrawCentralWorkbench(snapshot, panelVisible);

            if (rightVisible)
            {
                GUILayout.Space(4f);
                if (_workbenchRuntime != null)
                {
                    _workbenchRuntime.LayoutState.SecondarySideWidth = CortexWindowChromeController.DrawVerticalSplitter(0xC102, _workbenchRuntime.LayoutState.SecondarySideWidth, 240f, 460f, 6f, true);
                }
                DrawDockHost(snapshot, WorkbenchHostLocation.SecondarySideHost, Mathf.Clamp(_workbenchRuntime != null ? _workbenchRuntime.LayoutState.SecondarySideWidth : 300f, 240f, 460f));
            }

            GUILayout.EndHorizontal();
        }

        private void DrawDockHost(WorkbenchPresentationSnapshot snapshot, WorkbenchHostLocation hostLocation, float width)
        {
            var activeContainerId = GetActiveContainerForHost(snapshot, hostLocation);
            CortexIdeLayout.DrawGroup(GetHostTitle(snapshot, hostLocation, activeContainerId), delegate
            {
                DrawHostDropTarget(hostLocation);
                GUILayout.BeginHorizontal();
                var items = GetHostItems(snapshot, hostLocation);
                for (var i = 0; i < items.Count; i++)
                {
                    DrawDockTabButton(items[i], hostLocation);
                }
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
                GUILayout.Space(4f);

                if (string.IsNullOrEmpty(activeContainerId))
                {
                    GUILayout.Label(GetHostDescription(hostLocation), _captionStyle);
                    return;
                }

                var previousContentColor = GUI.contentColor;
                GUI.contentColor = CortexIdeLayout.GetHostAccentColor(hostLocation);
                GUILayout.Label(GetHostDescription(hostLocation), _statusStyle);
                GUI.contentColor = previousContentColor;
                DrawActiveModule(snapshot, activeContainerId, false);
            }, GUILayout.Width(width), GUILayout.ExpandHeight(true));
        }

        private void DrawCentralWorkbench(WorkbenchPresentationSnapshot snapshot, bool panelVisible)
        {
            GUILayout.BeginVertical(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            DrawEditorHost(snapshot);
            if (panelVisible)
            {
                GUILayout.Space(4f);
                if (_workbenchRuntime != null)
                {
                    _workbenchRuntime.LayoutState.PanelSize = CortexWindowChromeController.DrawHorizontalSplitter(0xC103, _workbenchRuntime.LayoutState.PanelSize, 180f, 360f, 6f);
                }
                GUILayout.Space(4f);
                DrawPanelHost(snapshot);
            }
            GUILayout.EndVertical();
        }

        private void DrawEditorHost(WorkbenchPresentationSnapshot snapshot)
        {
            CortexIdeLayout.DrawGroup("Editor | " + GetContainerTitle(snapshot, _state.Workbench.EditorContainerId), delegate
            {
                DrawHostDropTarget(WorkbenchHostLocation.DocumentHost);
                GUILayout.Label("Open docs: " + _state.Documents.OpenDocuments.Count + " | Active: " + (_state.Documents.ActiveDocument != null ? _state.Documents.ActiveDocument.FilePath : "None"), _captionStyle);
                GUILayout.Space(4f);
                DrawActiveModule(snapshot, _state.Workbench.EditorContainerId, false);
            }, GUILayout.ExpandHeight(true), GUILayout.ExpandWidth(true));
        }

        private void DrawPanelHost(WorkbenchPresentationSnapshot snapshot)
        {
            var panelHeight = Mathf.Clamp(_workbenchRuntime != null ? _workbenchRuntime.LayoutState.PanelSize : 260f, 180f, 360f);
            var activeContainerId = GetActiveContainerForHost(snapshot, WorkbenchHostLocation.PanelHost);
            CortexIdeLayout.DrawGroup("Panel | " + GetContainerTitle(snapshot, _state.Workbench.PanelContainerId), delegate
            {
                DrawHostDropTarget(WorkbenchHostLocation.PanelHost);
                GUILayout.BeginHorizontal();
                var items = GetHostItems(snapshot, WorkbenchHostLocation.PanelHost);
                for (var i = 0; i < items.Count; i++)
                {
                    DrawDockTabButton(items[i], WorkbenchHostLocation.PanelHost);
                }
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
                GUILayout.Space(4f);
                if (string.IsNullOrEmpty(activeContainerId))
                {
                    GUILayout.Label(GetHostDescription(WorkbenchHostLocation.PanelHost), _captionStyle);
                    return;
                }

                DrawActiveModule(snapshot, activeContainerId, false);
            }, GUILayout.Height(panelHeight), GUILayout.ExpandWidth(true));
        }

        private void DrawDockTabButton(ToolRailItem item, WorkbenchHostLocation hostLocation)
        {
            if (item == null)
            {
                return;
            }

            var activeContainerId = GetActiveContainerForHost(null, hostLocation);
            var isSelected = string.Equals(activeContainerId, item.ContainerId, StringComparison.OrdinalIgnoreCase);
            var previousBackground = GUI.backgroundColor;
            var previousContent = GUI.contentColor;
            GUI.backgroundColor = CortexIdeLayout.GetInteractiveFillColor(isSelected, hostLocation);
            GUI.contentColor = CortexIdeLayout.GetInteractiveTextColor(isSelected);
            if (GUILayout.Toggle(isSelected, BuildTabLabel(item), isSelected ? _activeTabStyle : _tabStyle, GUILayout.Width(116f), GUILayout.Height(24f)))
            {
                ActivateContainer(item.ContainerId);
            }
            var rect = GUILayoutUtility.GetLastRect();
            HandleTabDrag(item.ContainerId, hostLocation, rect);
            GUI.backgroundColor = previousBackground;
            GUI.contentColor = previousContent;
        }

        private void HandleTabDrag(string containerId, WorkbenchHostLocation hostLocation, Rect rect)
        {
            var current = Event.current;
            if (current == null || string.IsNullOrEmpty(containerId))
            {
                return;
            }

            if (current.type == EventType.MouseDrag && rect.Contains(current.mousePosition))
            {
                _draggingContainerId = containerId;
                _draggingContainerSourceHost = hostLocation;
                current.Use();
            }
            else if (!string.IsNullOrEmpty(_draggingContainerId) &&
                (current.type == EventType.MouseUp || current.rawType == EventType.MouseUp) &&
                string.Equals(_draggingContainerId, containerId, StringComparison.OrdinalIgnoreCase))
            {
                _draggingContainerId = string.Empty;
            }
        }

        private void DrawHostDropTarget(WorkbenchHostLocation hostLocation)
        {
            var isDragging = !string.IsNullOrEmpty(_draggingContainerId) && hostLocation != WorkbenchHostLocation.DocumentHost;
            var label = hostLocation == WorkbenchHostLocation.DocumentHost
                ? "Editor workspace"
                : isDragging
                    ? "Release here to dock into " + CortexIdeLayout.GetHostDisplayName(hostLocation).ToLowerInvariant() + "."
                    : "Drag a tab here to dock it.";
            GUILayout.Box(label, GUILayout.ExpandWidth(true), GUILayout.Height(20f));
            var rect = GUILayoutUtility.GetLastRect();
            HandleDockDropTarget(hostLocation, rect);
        }

        private void HandleDockDropTarget(WorkbenchHostLocation hostLocation, Rect rect)
        {
            var current = Event.current;
            if (current == null || string.IsNullOrEmpty(_draggingContainerId))
            {
                return;
            }

            if ((current.type == EventType.MouseUp || current.rawType == EventType.MouseUp) && rect.Contains(current.mousePosition))
            {
                DockContainer(_draggingContainerId, hostLocation);
                _draggingContainerId = string.Empty;
                current.Use();
            }
            else if (current.type == EventType.MouseUp || current.rawType == EventType.MouseUp)
            {
                _draggingContainerId = string.Empty;
            }
        }

        private bool HasHostItems(WorkbenchPresentationSnapshot snapshot, WorkbenchHostLocation hostLocation)
        {
            return GetHostItems(snapshot, hostLocation).Count > 0;
        }

        private List<ToolRailItem> GetHostItems(WorkbenchPresentationSnapshot snapshot, WorkbenchHostLocation hostLocation)
        {
            var items = new List<ToolRailItem>();
            if (snapshot == null)
            {
                return items;
            }

            for (var i = 0; i < snapshot.ToolRailItems.Count; i++)
            {
                var item = snapshot.ToolRailItems[i];
                if (item != null && ResolveHostLocation(item.ContainerId) == hostLocation)
                {
                    items.Add(item);
                }
            }

            return items;
        }

        private string GetActiveContainerForHost(WorkbenchPresentationSnapshot snapshot, WorkbenchHostLocation hostLocation)
        {
            switch (hostLocation)
            {
                case WorkbenchHostLocation.PanelHost:
                    return !string.IsNullOrEmpty(_state.Workbench.PanelContainerId)
                        ? _state.Workbench.PanelContainerId
                        : FindFirstHostItem(snapshot, hostLocation);
                case WorkbenchHostLocation.SecondarySideHost:
                    return !string.IsNullOrEmpty(_state.Workbench.SecondarySideContainerId)
                        ? _state.Workbench.SecondarySideContainerId
                        : FindFirstHostItem(snapshot, hostLocation);
                case WorkbenchHostLocation.DocumentHost:
                    return _state.Workbench.EditorContainerId;
                case WorkbenchHostLocation.PrimarySideHost:
                default:
                    return !string.IsNullOrEmpty(_state.Workbench.SideContainerId)
                        ? _state.Workbench.SideContainerId
                        : FindFirstHostItem(snapshot, hostLocation);
            }
        }

        private string FindFirstHostItem(WorkbenchPresentationSnapshot snapshot, WorkbenchHostLocation hostLocation)
        {
            var items = GetHostItems(snapshot, hostLocation);
            return items.Count > 0 && items[0] != null ? items[0].ContainerId : string.Empty;
        }

        private string GetHostTitle(WorkbenchPresentationSnapshot snapshot, WorkbenchHostLocation hostLocation, string activeContainerId)
        {
            return CortexIdeLayout.GetHostDisplayName(hostLocation) + " | " + GetContainerTitle(snapshot, activeContainerId);
        }

        private static string GetHostDescription(WorkbenchHostLocation hostLocation)
        {
            switch (hostLocation)
            {
                case WorkbenchHostLocation.PrimarySideHost:
                    return "Primary sidebar for explorer-style tools and references.";
                case WorkbenchHostLocation.SecondarySideHost:
                    return "Secondary sidebar for companion tools docked on the right.";
                case WorkbenchHostLocation.PanelHost:
                    return "Bottom panel for logs, build output, and transient tool panes.";
                case WorkbenchHostLocation.DocumentHost:
                default:
                    return "Central editor workspace with persistent documents.";
            }
        }

        private void DrawContributionToolbar(WorkbenchPresentationSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return;
            }

            DrawMenuProjectionRow(snapshot.ToolbarItems, "Quick");
        }

        private void DrawMenuProjectionRow(IList<MenuItemProjection> items, string fallbackGroup)
        {
            if (items == null || items.Count == 0)
            {
                return;
            }

            GUILayout.Space(6f);
            GUILayout.BeginHorizontal();
            var currentGroup = string.Empty;
            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (!string.Equals(currentGroup, item.Group, StringComparison.OrdinalIgnoreCase))
                {
                    if (!string.IsNullOrEmpty(currentGroup))
                    {
                        GUILayout.Space(10f);
                    }

                    currentGroup = string.IsNullOrEmpty(item.Group) ? fallbackGroup : item.Group;
                    if (!string.IsNullOrEmpty(currentGroup))
                    {
                        GUILayout.Label(currentGroup + ":", _captionStyle);
                    }
                }

                var label = BuildMenuLabel(item);
                if (GUILayout.Button(label, GUILayout.Width(Mathf.Max(96f, label.Length * 7f))))
                {
                    ExecuteCommand(item.CommandId, null);
                }
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        private void DrawStatusStrip(WorkbenchPresentationSnapshot snapshot)
        {
            GUILayout.BeginVertical(_sectionStyle);
            GUILayout.BeginHorizontal();
            DrawStatusItems(snapshot != null ? snapshot.LeftStatusItems : null);
            GUILayout.FlexibleSpace();
            GUILayout.Label(string.IsNullOrEmpty(_state.StatusMessage) ? "Status: Ready" : "Status: " + _state.StatusMessage, _statusStyle);
            GUILayout.FlexibleSpace();
            DrawStatusItems(snapshot != null ? snapshot.RightStatusItems : null);
            GUILayout.EndHorizontal();
            GUILayout.Label(BuildStatusLine(snapshot), _captionStyle);
            GUILayout.EndVertical();
        }

        private void DrawStatusItems(IList<StatusItemContribution> items)
        {
            if (items == null || items.Count == 0)
            {
                return;
            }

            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item == null)
                {
                    continue;
                }

                var previousContentColor = GUI.contentColor;
                GUI.contentColor = GetStatusItemColor(item.Severity);
                var label = item.Text ?? item.ItemId ?? "Status";
                if (!string.IsNullOrEmpty(item.CommandId) && GUILayout.Button(label, GUILayout.Width(Mathf.Max(90f, label.Length * 7f))))
                {
                    ExecuteCommand(item.CommandId, null);
                }
                else
                {
                    GUILayout.Label(label, _captionStyle);
                }
                GUI.contentColor = previousContentColor;
                GUILayout.Space(6f);
            }
        }

        private static string BuildTabLabel(ToolRailItem item)
        {
            if (item == null)
            {
                return "Item";
            }

            return string.IsNullOrEmpty(item.IconAlias)
                ? item.Title
                : item.IconAlias + " " + item.Title;
        }

        private static string BuildMenuLabel(MenuItemProjection item)
        {
            if (item == null)
            {
                return "Command";
            }

            return string.IsNullOrEmpty(item.IconAlias)
                ? item.DisplayName
                : item.IconAlias + " " + item.DisplayName;
        }

        private static Color GetStatusItemColor(string severity)
        {
            return RuntimeLogVisuals.GetAccentColor(string.IsNullOrEmpty(severity) ? "Info" : severity);
        }

        private static string GetContainerTitle(WorkbenchPresentationSnapshot snapshot, string containerId)
        {
            if (snapshot != null)
            {
                for (var i = 0; i < snapshot.ToolRailItems.Count; i++)
                {
                    if (string.Equals(snapshot.ToolRailItems[i].ContainerId, containerId, StringComparison.OrdinalIgnoreCase))
                    {
                        return snapshot.ToolRailItems[i].Title;
                    }
                }
            }

            return string.IsNullOrEmpty(containerId) ? "Workspace" : containerId;
        }

        private string BuildStatusLine(WorkbenchPresentationSnapshot snapshot)
        {
            var renderer = snapshot != null ? snapshot.RendererSummary : string.Empty;
            return "Side: " + GetContainerTitle(snapshot, _state.Workbench.SideContainerId) +
                " | Editor: " + GetContainerTitle(snapshot, _state.Workbench.EditorContainerId) +
                " | Panel: " + (_state.Logs.ShowDetachedWindow ? "Detached Logs" : GetContainerTitle(snapshot, _state.Workbench.PanelContainerId)) +
                " | Theme: " + (snapshot != null && !string.IsNullOrEmpty(snapshot.ActiveThemeId) ? snapshot.ActiveThemeId : "cortex.default") +
                " | Open docs: " + _state.Documents.OpenDocuments.Count +
                " | Projects: " + _projectCatalog.GetProjects().Count +
                " | Logs window: " + (_state.Logs.ShowDetachedWindow ? "Open" : "Docked") +
                (string.IsNullOrEmpty(renderer) ? string.Empty : " | " + renderer);
        }
    }
}
