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
            DrawMenuBar();
            GUILayout.Space(2f);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Cortex", _titleStyle, GUILayout.Width(72f));
            GUILayout.Label(
                _state.SelectedProject != null
                    ? "Solution: " + _state.SelectedProject.GetDisplayName()
                    : "Solution: ShelteredModManager",
                _captionStyle);
            GUILayout.FlexibleSpace();
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
            GUILayout.Space(3f);
            DrawContributionToolbar(snapshot);
            if (snapshot != null && !string.IsNullOrEmpty(snapshot.RendererSummary))
            {
                GUILayout.Space(2f);
                GUILayout.Label("Runtime: " + snapshot.RendererSummary + " | Toggle: F8", _captionStyle);
            }
            GUILayout.EndVertical();
        }

        private void DrawWorkbenchSurface(WorkbenchPresentationSnapshot snapshot, Rect workspaceRect)
        {
            if (workspaceRect.width <= 0f || workspaceRect.height <= 0f)
            {
                return;
            }

            var layoutRoot = BuildLayoutTree(snapshot, workspaceRect);
            _state.Workbench.LayoutRoot = layoutRoot;
            DrawLayoutTree(layoutRoot, workspaceRect, snapshot);
        }

        private CortexLayoutNode BuildLayoutTree(WorkbenchPresentationSnapshot snapshot, Rect workspaceRect)
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

            var root = CreateLeaf("layout.editor", WorkbenchHostLocation.DocumentHost, snapshot);
            if (panelVisible)
            {
                root = CreateSplitNode("layout.panel", CortexLayoutSplitDirection.Vertical, root, CreateLeaf("layout.panel.leaf", WorkbenchHostLocation.PanelHost, snapshot));
            }

            if (rightVisible)
            {
                root = CreateSplitNode("layout.secondary", CortexLayoutSplitDirection.Horizontal, root, CreateLeaf("layout.secondary.leaf", WorkbenchHostLocation.SecondarySideHost, snapshot));
            }

            if (leftVisible)
            {
                root = CreateSplitNode("layout.primary", CortexLayoutSplitDirection.Horizontal, CreateLeaf("layout.primary.leaf", WorkbenchHostLocation.PrimarySideHost, snapshot), root);
            }

            return root;
        }

        private CortexLayoutNode CreateLeaf(string nodeId, WorkbenchHostLocation hostLocation, WorkbenchPresentationSnapshot snapshot)
        {
            var node = new CortexLayoutNode();
            node.NodeId = nodeId;
            node.HostLocation = hostLocation;
            node.Split = CortexLayoutSplitDirection.None;
            node.ActiveModuleId = GetActiveContainerForHost(snapshot, hostLocation);
            var items = GetHostItems(snapshot, hostLocation);
            for (var i = 0; i < items.Count; i++)
            {
                if (items[i] != null && !string.IsNullOrEmpty(items[i].ContainerId))
                {
                    node.ContainedModuleIds.Add(items[i].ContainerId);
                }
            }

            return node;
        }

        private static CortexLayoutNode CreateSplitNode(string nodeId, CortexLayoutSplitDirection split, CortexLayoutNode childA, CortexLayoutNode childB)
        {
            var node = new CortexLayoutNode();
            node.NodeId = nodeId;
            node.Split = split;
            node.ChildA = childA;
            node.ChildB = childB;
            return node;
        }

        private void DrawLayoutTree(CortexLayoutNode node, Rect allocatedArea, WorkbenchPresentationSnapshot snapshot)
        {
            if (node == null || allocatedArea.width <= 0f || allocatedArea.height <= 0f)
            {
                return;
            }

            const float splitterThickness = 5f;
            if (node.Split == CortexLayoutSplitDirection.None)
            {
                GUILayout.BeginArea(allocatedArea);
                DrawLayoutLeaf(node, snapshot, allocatedArea);
                GUILayout.EndArea();
                return;
            }

            var splitRatio = ResolveSplitRatio(node, allocatedArea);
            if (node.Split == CortexLayoutSplitDirection.Horizontal)
            {
                var maxHorizontalSplit = Mathf.Max(181f, allocatedArea.width - 180f - splitterThickness);
                var splitPoint = Mathf.Clamp(allocatedArea.width * splitRatio, 180f, maxHorizontalSplit);
                var rectA = new Rect(allocatedArea.x, allocatedArea.y, splitPoint, allocatedArea.height);
                var splitterRect = new Rect(rectA.xMax, allocatedArea.y, splitterThickness, allocatedArea.height);
                var rectB = new Rect(splitterRect.xMax, allocatedArea.y, Mathf.Max(0f, allocatedArea.width - splitPoint - splitterThickness), allocatedArea.height);
                var updatedSplitPoint = CortexWindowChromeController.DrawVerticalSplitter(GetSplitterId(node.NodeId), splitterRect, splitPoint, 180f, Mathf.Max(181f, allocatedArea.width - 180f), false);
                StoreSplitRatio(node, updatedSplitPoint / Mathf.Max(1f, allocatedArea.width), allocatedArea);
                rectA.width = updatedSplitPoint;
                splitterRect.x = rectA.xMax;
                rectB.x = splitterRect.xMax;
                rectB.width = Mathf.Max(0f, allocatedArea.width - rectA.width - splitterThickness);
                DrawLayoutTree(node.ChildA, rectA, snapshot);
                DrawLayoutTree(node.ChildB, rectB, snapshot);
                return;
            }

            var maxVerticalSplit = Mathf.Max(141f, allocatedArea.height - 120f - splitterThickness);
            var verticalSplitPoint = Mathf.Clamp(allocatedArea.height * splitRatio, 140f, maxVerticalSplit);
            var topRect = new Rect(allocatedArea.x, allocatedArea.y, allocatedArea.width, verticalSplitPoint);
            var horizontalSplitterRect = new Rect(allocatedArea.x, topRect.yMax, allocatedArea.width, splitterThickness);
            var bottomRect = new Rect(allocatedArea.x, horizontalSplitterRect.yMax, allocatedArea.width, Mathf.Max(0f, allocatedArea.height - verticalSplitPoint - splitterThickness));
            var updatedVerticalSplit = CortexWindowChromeController.DrawHorizontalSplitter(GetSplitterId(node.NodeId), horizontalSplitterRect, verticalSplitPoint, 140f, Mathf.Max(141f, allocatedArea.height - 120f));
            StoreSplitRatio(node, updatedVerticalSplit / Mathf.Max(1f, allocatedArea.height), allocatedArea);
            topRect.height = updatedVerticalSplit;
            horizontalSplitterRect.y = topRect.yMax;
            bottomRect.y = horizontalSplitterRect.yMax;
            bottomRect.height = Mathf.Max(0f, allocatedArea.height - topRect.height - splitterThickness);
            DrawLayoutTree(node.ChildA, topRect, snapshot);
            DrawLayoutTree(node.ChildB, bottomRect, snapshot);
        }

        private void DrawLayoutLeaf(CortexLayoutNode node, WorkbenchPresentationSnapshot snapshot, Rect allocatedArea)
        {
            if (node == null)
            {
                return;
            }

            switch (node.HostLocation)
            {
                case WorkbenchHostLocation.PrimarySideHost:
                case WorkbenchHostLocation.SecondarySideHost:
                    DrawDockHost(snapshot, node.HostLocation, allocatedArea.width);
                    break;
                case WorkbenchHostLocation.PanelHost:
                    DrawPanelHost(snapshot, allocatedArea.height);
                    break;
                case WorkbenchHostLocation.DocumentHost:
                default:
                    DrawEditorHost(snapshot);
                    break;
            }
        }

        private float ResolveSplitRatio(CortexLayoutNode node, Rect allocatedArea)
        {
            if (_workbenchRuntime != null && !string.IsNullOrEmpty(node.NodeId) && _workbenchRuntime.LayoutState.HostDimensions.ContainsKey(node.NodeId))
            {
                return Mathf.Clamp(_workbenchRuntime.LayoutState.HostDimensions[node.NodeId], 0.18f, 0.82f);
            }

            if (string.Equals(node.NodeId, "layout.primary", StringComparison.OrdinalIgnoreCase))
            {
                var width = Mathf.Clamp(_workbenchRuntime != null ? _workbenchRuntime.LayoutState.PrimarySideWidth : 280f, 220f, 380f);
                return Mathf.Clamp(width / Mathf.Max(1f, allocatedArea.width), 0.18f, 0.4f);
            }

            if (string.Equals(node.NodeId, "layout.secondary", StringComparison.OrdinalIgnoreCase))
            {
                var width = Mathf.Clamp(_workbenchRuntime != null ? _workbenchRuntime.LayoutState.SecondarySideWidth : 320f, 260f, 420f);
                return Mathf.Clamp((allocatedArea.width - width) / Mathf.Max(1f, allocatedArea.width), 0.35f, 0.82f);
            }

            if (string.Equals(node.NodeId, "layout.panel", StringComparison.OrdinalIgnoreCase))
            {
                var height = Mathf.Clamp(_workbenchRuntime != null ? _workbenchRuntime.LayoutState.PanelSize : 240f, 150f, 340f);
                return Mathf.Clamp((allocatedArea.height - height) / Mathf.Max(1f, allocatedArea.height), 0.4f, 0.84f);
            }

            return 0.5f;
        }

        private void StoreSplitRatio(CortexLayoutNode node, float splitRatio, Rect allocatedArea)
        {
            if (_workbenchRuntime == null || node == null || string.IsNullOrEmpty(node.NodeId))
            {
                return;
            }

            splitRatio = Mathf.Clamp(splitRatio, 0.18f, 0.82f);
            _workbenchRuntime.LayoutState.HostDimensions[node.NodeId] = splitRatio;
            if (string.Equals(node.NodeId, "layout.primary", StringComparison.OrdinalIgnoreCase))
            {
                _workbenchRuntime.LayoutState.PrimarySideWidth = Mathf.Clamp(allocatedArea.width * splitRatio, 220f, 380f);
            }
            else if (string.Equals(node.NodeId, "layout.secondary", StringComparison.OrdinalIgnoreCase))
            {
                _workbenchRuntime.LayoutState.SecondarySideWidth = Mathf.Clamp(allocatedArea.width * (1f - splitRatio), 260f, 420f);
            }
            else if (string.Equals(node.NodeId, "layout.panel", StringComparison.OrdinalIgnoreCase))
            {
                _workbenchRuntime.LayoutState.PanelSize = Mathf.Clamp(allocatedArea.height * (1f - splitRatio), 150f, 340f);
            }
        }

        private static int GetSplitterId(string nodeId)
        {
            return string.IsNullOrEmpty(nodeId) ? 0xC100 : nodeId.GetHashCode();
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

        private void DrawPanelHost(WorkbenchPresentationSnapshot snapshot, float panelHeight)
        {
            var activeContainerId = GetActiveContainerForHost(snapshot, WorkbenchHostLocation.PanelHost);
            CortexIdeLayout.DrawGroup(GetHostTitle(snapshot, WorkbenchHostLocation.PanelHost, activeContainerId), delegate
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
            if (hostLocation == WorkbenchHostLocation.SecondarySideHost)
            {
                return "Solution Explorer";
            }

            if (hostLocation == WorkbenchHostLocation.PanelHost)
            {
                return string.Equals(activeContainerId, CortexWorkbenchIds.BuildContainer, StringComparison.OrdinalIgnoreCase)
                    ? "Build Output"
                    : "Output";
            }

            return CortexIdeLayout.GetHostDisplayName(hostLocation) + " | " + GetContainerTitle(snapshot, activeContainerId);
        }

        private static string GetHostDescription(WorkbenchHostLocation hostLocation)
        {
            switch (hostLocation)
            {
                case WorkbenchHostLocation.PrimarySideHost:
                    return "Primary sidebar for explorer-style tools and references.";
                case WorkbenchHostLocation.SecondarySideHost:
                    return "Project and reference tooling docked to the right.";
                case WorkbenchHostLocation.PanelHost:
                    return "Bottom panel for logs, build output, and transient tool panes.";
                case WorkbenchHostLocation.DocumentHost:
                default:
                    return "Central editor workspace with persistent documents.";
            }
        }

        private void DrawContributionToolbar(WorkbenchPresentationSnapshot snapshot)
        {
            DrawMenuProjectionRow(snapshot != null ? snapshot.ToolbarItems : null, "Quick");
        }

        private void DrawMenuBar()
        {
            GUILayout.BeginHorizontal();
            var menuItems = new[] { "File", "Edit", "View", "Project", "Build", "Tools", "Window", "Help" };
            for (var i = 0; i < menuItems.Length; i++)
            {
                GUILayout.Label(menuItems[i], _menuStyle);
            }

            GUILayout.FlexibleSpace();
            GUILayout.Label("Search", _captionStyle, GUILayout.Width(48f));
            GUILayout.Box(CortexModuleUtil.GetDocumentDisplayName(_state.Documents.ActiveDocument), GUILayout.Width(220f), GUILayout.Height(20f));
            GUILayout.EndHorizontal();
        }

        private void DrawMenuProjectionRow(IList<MenuItemProjection> items, string fallbackGroup)
        {
            if (items == null || items.Count == 0)
            {
                return;
            }

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
            GUILayout.Label(string.IsNullOrEmpty(_state.StatusMessage) ? "Ready" : _state.StatusMessage, _statusStyle);
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
            return "Right: " + GetContainerTitle(snapshot, _state.Workbench.SecondarySideContainerId) +
                " | Editor: " + GetContainerTitle(snapshot, _state.Workbench.EditorContainerId) +
                " | Panel: " + (_state.Logs.ShowDetachedWindow ? "Detached Logs" : GetContainerTitle(snapshot, _state.Workbench.PanelContainerId)) +
                " | Theme: " + (snapshot != null && !string.IsNullOrEmpty(snapshot.ActiveThemeId) ? snapshot.ActiveThemeId : "cortex.vs-dark") +
                " | Open docs: " + _state.Documents.OpenDocuments.Count +
                " | Projects: " + _projectCatalog.GetProjects().Count +
                " | Logs window: " + (_state.Logs.ShowDetachedWindow ? "Open" : "Docked") +
                (string.IsNullOrEmpty(renderer) ? string.Empty : " | " + renderer);
        }
    }
}
