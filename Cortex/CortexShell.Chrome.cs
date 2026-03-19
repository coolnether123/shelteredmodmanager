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
        // ── Active open menu tracking (simple one-at-a-time popup model) ────────────────
        private string _openMenuGroup = string.Empty;

        private void DrawHeader(WorkbenchPresentationSnapshot snapshot)
        {
            GUILayout.BeginHorizontal(_sectionStyle, GUILayout.Height(26f));

            // ── Cortex branding (small, to save space) ──────────────────────────────────
            GUILayout.Label("Cortex", _titleStyle, GUILayout.Width(56f));
            GUILayout.Space(6f);

            // ── Menu bar (functional drop-down style) ───────────────────────────────────
            DrawMenuBar(snapshot);

            GUILayout.FlexibleSpace();

            // ── Solution title (compact) ────────────────────────────────────────────────
            if (_state.SelectedProject != null)
            {
                GUILayout.Label(_state.SelectedProject.GetDisplayName(), _captionStyle, GUILayout.ExpandWidth(false));
                GUILayout.Space(12f);
            }

            // ── Window chrome buttons ────────────────────────────────────────────────────
            var actions = new List<CortexWindowAction>();
            actions.Add(BuildGlyphWindowAction(
                "shell.collapse", "_", "Minimize",
                delegate
                {
                    _windowRect = CortexWindowChromeController.ToggleCollapsed(_state.Chrome.Main, _windowRect, 126f, 28f);
                }));
            actions.Add(BuildGlyphWindowAction(
                "shell.close", "X", "Close Cortex",
                delegate
                {
                    PersistWorkbenchSession();
                    PersistWindowSettings();
                    _visible = false;
                }));
            CortexWindowChromeController.DrawActions(actions);
            GUILayout.EndHorizontal();
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
            CortexIdeLayout.DrawGroup(null, delegate
            {
                DrawHostDropTarget(hostLocation);
                // Tab strip
                GUILayout.BeginHorizontal();
                var items = GetHostItems(snapshot, hostLocation);
                for (var i = 0; i < items.Count; i++)
                {
                    DrawDockTabButton(items[i], hostLocation);
                }
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
                GUILayout.Space(2f);

                if (string.IsNullOrEmpty(activeContainerId))
                {
                    GUILayout.Label(GetHostDescription(hostLocation), _captionStyle);
                    return;
                }

                DrawActiveModule(snapshot, activeContainerId, false);
            }, GUILayout.Width(width), GUILayout.ExpandHeight(true));
        }

        /// <summary>
        /// Central editor host: just the module, no extra chrome labels. Clean like VS.
        /// </summary>
        private void DrawEditorHost(WorkbenchPresentationSnapshot snapshot)
        {
            CortexIdeLayout.DrawGroup(null, delegate
            {
                DrawHostDropTarget(WorkbenchHostLocation.DocumentHost);
                DrawActiveModule(snapshot, _state.Workbench.EditorContainerId, false);
            }, GUILayout.ExpandHeight(true), GUILayout.ExpandWidth(true));
        }

        private void DrawPanelHost(WorkbenchPresentationSnapshot snapshot, float panelHeight)
        {
            var activeContainerId = GetActiveContainerForHost(snapshot, WorkbenchHostLocation.PanelHost);
            CortexIdeLayout.DrawGroup(null, delegate
            {
                DrawHostDropTarget(WorkbenchHostLocation.PanelHost);
                // Tab strip for panel items
                GUILayout.BeginHorizontal();
                var items = GetHostItems(snapshot, WorkbenchHostLocation.PanelHost);
                for (var i = 0; i < items.Count; i++)
                {
                    DrawDockTabButton(items[i], WorkbenchHostLocation.PanelHost);
                }
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();

                if (string.IsNullOrEmpty(activeContainerId))
                {
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
            var rect = GUILayoutUtility.GetRect(116f, 116f, 24f, 24f);
            var current = Event.current;
            var isHovered = current != null && rect.Contains(current.mousePosition);
            var closeRect = new Rect(rect.xMax - 18f, rect.y + 4f, 14f, Mathf.Max(12f, rect.height - 8f));
            if ((isHovered || isSelected) &&
                current != null &&
                current.type == EventType.MouseDown &&
                current.button == 0 &&
                closeRect.Contains(current.mousePosition))
            {
                HideContainer(item.ContainerId);
                _state.StatusMessage = GetContainerTitle(null, item.ContainerId) + " hidden.";
                current.Use();
                GUI.backgroundColor = previousBackground;
                GUI.contentColor = previousContent;
                return;
            }

            if (GUI.Toggle(rect, isSelected, BuildTabLabel(item), isSelected ? _activeTabStyle : _tabStyle))
            {
                ActivateContainer(item.ContainerId);
            }

            if ((isHovered || isSelected) && !string.Equals(item.ContainerId, CortexWorkbenchIds.EditorContainer, StringComparison.OrdinalIgnoreCase))
            {
                GUI.Box(closeRect, "x", _tabCloseButtonStyle ?? GUI.skin.button);
            }
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
            var isDragging = !string.IsNullOrEmpty(_draggingContainerId);
            if (!isDragging)
            {
                GUILayout.Space(2f);
                return;
            }

            var label = hostLocation == WorkbenchHostLocation.DocumentHost
                ? "Editor workspace"
                : "Release here to dock into " + CortexIdeLayout.GetHostDisplayName(hostLocation).ToLowerInvariant() + ".";
            GUILayout.Box(label, GUILayout.ExpandWidth(true), GUILayout.Height(18f));
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
            return _layoutHostRouter.HasHostItems(GetLayoutContext(), snapshot, hostLocation);
        }

        private List<ToolRailItem> GetHostItems(WorkbenchPresentationSnapshot snapshot, WorkbenchHostLocation hostLocation)
        {
            return _layoutHostRouter.GetHostItems(GetLayoutContext(), snapshot, hostLocation);
        }

        private string GetActiveContainerForHost(WorkbenchPresentationSnapshot snapshot, WorkbenchHostLocation hostLocation)
        {
            return _layoutHostRouter.GetActiveContainerForHost(GetLayoutContext(), snapshot, hostLocation);
        }

        private CortexShellLayoutContext GetLayoutContext()
        {
            if (_layoutContext == null)
            {
                _layoutContext = new CortexShellLayoutContext(
                    _state,
                    delegate { return _workbenchRuntime; });
            }

            return _layoutContext;
        }

        private string FindFirstHostItem(WorkbenchPresentationSnapshot snapshot, WorkbenchHostLocation hostLocation)
        {
            var items = GetHostItems(snapshot, hostLocation);
            return items.Count > 0 && items[0] != null ? items[0].ContainerId : string.Empty;
        }

        private string GetHostTitle(WorkbenchPresentationSnapshot snapshot, WorkbenchHostLocation hostLocation, string activeContainerId)
        {
            switch (hostLocation)
            {
                case WorkbenchHostLocation.PrimarySideHost:
                    return "Explorer";
                case WorkbenchHostLocation.SecondarySideHost:
                    return "Solution";
                case WorkbenchHostLocation.PanelHost:
                    return string.Equals(activeContainerId, CortexWorkbenchIds.BuildContainer, StringComparison.OrdinalIgnoreCase)
                        ? "Build Output"
                        : "Output";
                default:
                    return GetContainerTitle(snapshot, activeContainerId);
            }
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

        /// <summary>
        /// A single-row menu bar with anchored popup menus for each group.
        /// </summary>
        private void DrawMenuBar(WorkbenchPresentationSnapshot snapshot)
        {
            var menuItems = snapshot != null && snapshot.MainMenuItems != null
                ? snapshot.MainMenuItems
                : null;

            var staticGroups = new[] { "File", "Edit", "View", "Build", "Window" };
            var hasContributions = menuItems != null && menuItems.Count > 0;

            if (!hasContributions)
            {
                foreach (var group in staticGroups)
                {
                    DrawStaticMenuGroup(group);
                }

                return;
            }

            // Build group list preserving insertion order
            var seenGroups = new List<string>();
            for (var i = 0; i < menuItems.Count; i++)
            {
                var g = menuItems[i].Group ?? "Misc";
                if (!seenGroups.Contains(g))
                {
                    seenGroups.Add(g);
                }
            }

            foreach (var group in seenGroups)
            {
                var isOpen = string.Equals(_openMenuGroup, group, StringComparison.OrdinalIgnoreCase);
                if (GUILayout.Button(group, _menuStyle, GUILayout.ExpandWidth(false)))
                {
                    _openMenuGroup = isOpen ? string.Empty : group;
                }

                _menuGroupRects[group] = GUILayoutUtility.GetLastRect();
            }
        }

        /// <summary>
        /// Draws the open menu's item list immediately below the menu bar, overlapping the workspace.
        /// Rendered last (after everything else in the header horizontal) so it floats over content.
        /// </summary>
        private void DrawOpenMenuPanel(WorkbenchPresentationSnapshot snapshot, Rect headerRect)
        {
            if (string.IsNullOrEmpty(_openMenuGroup))
            {
                return;
            }

            Rect anchorRect;
            if (!_menuGroupRects.TryGetValue(_openMenuGroup, out anchorRect))
            {
                _openMenuGroup = string.Empty;
                return;
            }

            var items = BuildMenuItemsForGroup(snapshot, _openMenuGroup);
            if (items.Count == 0)
            {
                _openMenuGroup = string.Empty;
                return;
            }

            var popupWidth = 220f;
            var popupHeight = 8f + (items.Count * 26f) + 8f;
            var popupX = Mathf.Clamp(headerRect.x + anchorRect.x, 0f, Mathf.Max(0f, _windowRect.width - popupWidth - 8f));
            var popupY = headerRect.y + anchorRect.yMax + 2f;
            var popupRect = new Rect(popupX, popupY, popupWidth, popupHeight);

            GUILayout.BeginArea(popupRect, _sectionStyle);
            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (GUILayout.Button(item.DisplayName ?? item.CommandId, GUILayout.Height(22f), GUILayout.ExpandWidth(true)))
                {
                    ExecuteCommand(item.CommandId, null);
                    _openMenuGroup = string.Empty;
                }
            }
            GUILayout.EndArea();

            var ev = Event.current;
            if (ev == null)
            {
                return;
            }

            if (ev.type == EventType.MouseDown)
            {
                var overGroup = false;
                foreach (var rect in _menuGroupRects.Values)
                {
                    if (new Rect(headerRect.x + rect.x, headerRect.y + rect.y, rect.width, rect.height).Contains(ev.mousePosition))
                    {
                        overGroup = true;
                        break;
                    }
                }

                if (!popupRect.Contains(ev.mousePosition) && !overGroup)
                {
                    _openMenuGroup = string.Empty;
                }
            }
            else if (ev.type == EventType.KeyDown && ev.keyCode == KeyCode.Escape)
            {
                _openMenuGroup = string.Empty;
                ev.Use();
            }
        }

        private void DrawStaticMenuGroup(string group)
        {
            var isOpen = string.Equals(_openMenuGroup, group, StringComparison.OrdinalIgnoreCase);
            if (GUILayout.Button(group, _menuStyle, GUILayout.ExpandWidth(false)))
            {
                _openMenuGroup = isOpen ? string.Empty : group;
            }

            _menuGroupRects[group] = GUILayoutUtility.GetLastRect();
        }

        private List<MenuItemProjection> BuildMenuItemsForGroup(WorkbenchPresentationSnapshot snapshot, string group)
        {
            var items = new List<MenuItemProjection>();
            var menuItems = snapshot != null && snapshot.MainMenuItems != null
                ? snapshot.MainMenuItems
                : null;

            if (menuItems != null && menuItems.Count > 0)
            {
                for (var i = 0; i < menuItems.Count; i++)
                {
                    var item = menuItems[i];
                    if (item != null && string.Equals(item.Group, group, StringComparison.OrdinalIgnoreCase))
                    {
                        items.Add(item);
                    }
                }

                return items;
            }

            var commands = GetStaticCommandsForGroup(group);
            for (var i = 0; i < commands.Length; i++)
            {
                items.Add(new MenuItemProjection
                {
                    CommandId = commands[i],
                    DisplayName = GetStaticCommandDisplayName(commands[i]),
                    Group = group
                });
            }

            return items;
        }

        private static string[] GetStaticCommandsForGroup(string group)
        {
            switch (group)
            {
                case "File": return new[] { "cortex.file.saveAll", "cortex.file.closeActive", "cortex.file.settings" };
                case "Edit": return new string[0];
                case "View": return new[] { "cortex.view.fileExplorer", "cortex.win.theme", "cortex.logs.toggleWindow", "cortex.shell.fitWindow" };
                case "Build": return new[] { "cortex.build.execute" };
                case "Window": return new[]
                {
                    "cortex.window.explorer",
                    "cortex.window.projects",
                    "cortex.window.references",
                    "cortex.window.logs",
                    "cortex.window.build",
                    "cortex.window.runtime",
                    "cortex.window.settings",
                    "cortex.shell.fitWindow"
                };
                default: return new string[0];
            }
        }

        private static string GetStaticCommandDisplayName(string commandId)
        {
            switch (commandId)
            {
                case "cortex.file.saveAll": return "Save All";
                case "cortex.file.closeActive": return "Close";
                case "cortex.file.settings": return "Settings";
                case "cortex.view.fileExplorer": return "Toggle File Explorer";
                case "cortex.win.theme": return "Switch Theme";
                case "cortex.logs.toggleWindow": return "Detached Logs Window";
                case "cortex.shell.fitWindow": return "Fit Workbench To Screen";
                case "cortex.build.execute": return "Build Project";
                case "cortex.window.explorer": return "Explorer";
                case "cortex.window.projects": return "Projects";
                case "cortex.window.references": return "References";
                case "cortex.window.search": return "Search";
                case "cortex.window.logs": return "Logs";
                case "cortex.window.build": return "Build";
                case "cortex.window.runtime": return "Runtime";
                case "cortex.window.settings": return "Settings";
                default: return commandId;
            }
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

        /// <summary>
        /// Slim, single-line status bar like Visual Studio: left info items, centre status message,
        /// right items (renderer, theme). Replaces the old two-row verbose status strip.
        /// </summary>
        private void DrawStatusStrip(WorkbenchPresentationSnapshot snapshot)
        {
            GUILayout.BeginHorizontal(_sectionStyle, GUILayout.Height(22f));

            // Left items (e.g. renderer)
            DrawStatusItems(snapshot != null ? snapshot.LeftStatusItems : null);

            GUILayout.FlexibleSpace();

            // Status message
            var msg = string.IsNullOrEmpty(_state.StatusMessage)
                ? (snapshot != null && !string.IsNullOrEmpty(snapshot.RendererSummary) ? snapshot.RendererSummary : "Ready")
                : _state.StatusMessage;
            GUILayout.Label(msg, _statusStyle);

            GUILayout.FlexibleSpace();

            // Theme indicator
            var themeId = snapshot != null && !string.IsNullOrEmpty(snapshot.ActiveThemeId)
                ? snapshot.ActiveThemeId
                : ((_state.Settings != null && !string.IsNullOrEmpty(_state.Settings.ThemeId)) ? _state.Settings.ThemeId : "vs-dark");
            GUILayout.Label(themeId, _captionStyle, GUILayout.ExpandWidth(false));
            GUILayout.Space(8f);

            // Right contributor items
            DrawStatusItems(snapshot != null ? snapshot.RightStatusItems : null);

            GUILayout.EndHorizontal();
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
