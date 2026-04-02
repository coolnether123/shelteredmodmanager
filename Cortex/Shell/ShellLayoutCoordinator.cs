using System;
using System.Collections.Generic;
using Cortex.Chrome;
using Cortex.Core.Models;
using Cortex.Presentation.Abstractions;
using Cortex.Presentation.Models;
using Cortex.Rendering.RuntimeUi;
using UnityEngine;

namespace Cortex.Shell
{
    internal sealed class ShellLayoutCoordinator
    {
        private readonly CortexShellState _state;
        private readonly CortexShellLayoutHostRouter _layoutHostRouter;
        private readonly Func<IWorkbenchRuntime> _runtimeProvider;
        private readonly Func<CortexShellModuleRenderService> _renderServiceProvider;
        private readonly Func<WorkbenchFrameInputSnapshot> _frameInputProvider;

        private string _draggingContainerId = string.Empty;
        private WorkbenchHostLocation _draggingContainerSourceHost = WorkbenchHostLocation.PrimarySideHost;
        private CortexShellLayoutContext _layoutContext;

        public ShellLayoutCoordinator(
            CortexShellState state,
            CortexShellLayoutHostRouter layoutHostRouter,
            Func<IWorkbenchRuntime> runtimeProvider,
            Func<CortexShellModuleRenderService> renderServiceProvider,
            Func<WorkbenchFrameInputSnapshot> frameInputProvider)
        {
            _state = state;
            _layoutHostRouter = layoutHostRouter;
            _runtimeProvider = runtimeProvider;
            _renderServiceProvider = renderServiceProvider;
            _frameInputProvider = frameInputProvider;
        }

        public string DraggingContainerId => _draggingContainerId;

        public void DrawWorkbenchSurface(WorkbenchPresentationSnapshot snapshot, Rect workspaceRect, GUIStyle tabStyle, GUIStyle activeTabStyle, GUIStyle tabCloseButtonStyle, GUIStyle captionStyle)
        {
            if (workspaceRect.width <= 0f || workspaceRect.height <= 0f) return;

            var layoutRoot = BuildLayoutTree(snapshot, workspaceRect);
            _state.Workbench.LayoutRoot = layoutRoot;
            DrawLayoutTree(layoutRoot, workspaceRect, snapshot, tabStyle, activeTabStyle, tabCloseButtonStyle, captionStyle);
        }

        private CortexLayoutNode BuildLayoutTree(WorkbenchPresentationSnapshot snapshot, Rect workspaceRect)
        {
            var isDragging = !string.IsNullOrEmpty(_draggingContainerId);
            var leftVisible = HasHostItems(snapshot, WorkbenchHostLocation.PrimarySideHost) || isDragging;
            var rightVisible = HasHostItems(snapshot, WorkbenchHostLocation.SecondarySideHost) || isDragging;
            var panelVisible = HasHostItems(snapshot, WorkbenchHostLocation.PanelHost) || isDragging;

            var runtime = _runtimeProvider();
            if (runtime != null)
            {
                runtime.WorkbenchState.PrimarySideHostVisible = leftVisible;
                runtime.WorkbenchState.SecondarySideHostVisible = rightVisible;
                runtime.WorkbenchState.PanelHostVisible = panelVisible;
            }

            var root = CreateLeaf("layout.editor", WorkbenchHostLocation.DocumentHost, snapshot);
            if (panelVisible) root = CreateSplitNode("layout.panel", CortexLayoutSplitDirection.Vertical, root, CreateLeaf("layout.panel.leaf", WorkbenchHostLocation.PanelHost, snapshot));
            if (rightVisible) root = CreateSplitNode("layout.secondary", CortexLayoutSplitDirection.Horizontal, root, CreateLeaf("layout.secondary.leaf", WorkbenchHostLocation.SecondarySideHost, snapshot));
            if (leftVisible) root = CreateSplitNode("layout.primary", CortexLayoutSplitDirection.Horizontal, CreateLeaf("layout.primary.leaf", WorkbenchHostLocation.PrimarySideHost, snapshot), root);

            return root;
        }

        private CortexLayoutNode CreateLeaf(string nodeId, WorkbenchHostLocation hostLocation, WorkbenchPresentationSnapshot snapshot)
        {
            var node = new CortexLayoutNode { NodeId = nodeId, HostLocation = hostLocation, Split = CortexLayoutSplitDirection.None };
            node.ActiveModuleId = GetActiveContainerForHost(snapshot, hostLocation);
            var items = GetHostItems(snapshot, hostLocation);
            foreach (var item in items)
            {
                if (item != null && !string.IsNullOrEmpty(item.ContainerId)) node.ContainedModuleIds.Add(item.ContainerId);
            }
            return node;
        }

        private static CortexLayoutNode CreateSplitNode(string nodeId, CortexLayoutSplitDirection split, CortexLayoutNode childA, CortexLayoutNode childB)
        {
            return new CortexLayoutNode { NodeId = nodeId, Split = split, ChildA = childA, ChildB = childB };
        }

        private void DrawLayoutTree(CortexLayoutNode node, Rect allocatedArea, WorkbenchPresentationSnapshot snapshot, GUIStyle tabStyle, GUIStyle activeTabStyle, GUIStyle tabCloseButtonStyle, GUIStyle captionStyle)
        {
            if (node == null || allocatedArea.width <= 0f || allocatedArea.height <= 0f) return;

            const float splitterThickness = 5f;
            if (node.Split == CortexLayoutSplitDirection.None)
            {
                GUILayout.BeginArea(allocatedArea);
                DrawLayoutLeaf(node, snapshot, allocatedArea, tabStyle, activeTabStyle, tabCloseButtonStyle, captionStyle);
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
                var updatedSplitPoint = CortexWindowChromeController.DrawVerticalSplitter(node.NodeId.GetHashCode(), splitterRect, splitPoint, 180f, Mathf.Max(181f, allocatedArea.width - 180f), false);
                StoreSplitRatio(node, updatedSplitPoint / Mathf.Max(1f, allocatedArea.width), allocatedArea);
                rectA.width = updatedSplitPoint; splitterRect.x = rectA.xMax; rectB.x = splitterRect.xMax; rectB.width = Mathf.Max(0f, allocatedArea.width - rectA.width - splitterThickness);
                DrawLayoutTree(node.ChildA, rectA, snapshot, tabStyle, activeTabStyle, tabCloseButtonStyle, captionStyle);
                DrawLayoutTree(node.ChildB, rectB, snapshot, tabStyle, activeTabStyle, tabCloseButtonStyle, captionStyle);
            }
            else
            {
                var maxVerticalSplit = Mathf.Max(141f, allocatedArea.height - 120f - splitterThickness);
                var verticalSplitPoint = Mathf.Clamp(allocatedArea.height * splitRatio, 140f, maxVerticalSplit);
                var topRect = new Rect(allocatedArea.x, allocatedArea.y, allocatedArea.width, verticalSplitPoint);
                var horizontalSplitterRect = new Rect(allocatedArea.x, topRect.yMax, allocatedArea.width, splitterThickness);
                var bottomRect = new Rect(allocatedArea.x, horizontalSplitterRect.yMax, allocatedArea.width, Mathf.Max(0f, allocatedArea.height - verticalSplitPoint - splitterThickness));
                var updatedVerticalSplit = CortexWindowChromeController.DrawHorizontalSplitter(node.NodeId.GetHashCode(), horizontalSplitterRect, verticalSplitPoint, 140f, Mathf.Max(141f, allocatedArea.height - 120f));
                StoreSplitRatio(node, updatedVerticalSplit / Mathf.Max(1f, allocatedArea.height), allocatedArea);
                topRect.height = updatedVerticalSplit; horizontalSplitterRect.y = topRect.yMax; bottomRect.y = horizontalSplitterRect.yMax; bottomRect.height = Mathf.Max(0f, allocatedArea.height - topRect.height - splitterThickness);
                DrawLayoutTree(node.ChildA, topRect, snapshot, tabStyle, activeTabStyle, tabCloseButtonStyle, captionStyle);
                DrawLayoutTree(node.ChildB, bottomRect, snapshot, tabStyle, activeTabStyle, tabCloseButtonStyle, captionStyle);
            }
        }

        private void DrawLayoutLeaf(CortexLayoutNode node, WorkbenchPresentationSnapshot snapshot, Rect allocatedArea, GUIStyle tabStyle, GUIStyle activeTabStyle, GUIStyle tabCloseButtonStyle, GUIStyle captionStyle)
        {
            if (node == null) return;
            switch (node.HostLocation)
            {
                case WorkbenchHostLocation.PrimarySideHost:
                case WorkbenchHostLocation.SecondarySideHost:
                    DrawDockHost(snapshot, node.HostLocation, allocatedArea.width, tabStyle, activeTabStyle, tabCloseButtonStyle, captionStyle);
                    break;
                case WorkbenchHostLocation.PanelHost:
                    DrawPanelHost(snapshot, allocatedArea.height, tabStyle, activeTabStyle, tabCloseButtonStyle, captionStyle);
                    break;
                case WorkbenchHostLocation.DocumentHost:
                default:
                    DrawEditorHost(snapshot);
                    break;
            }
        }

        private float ResolveSplitRatio(CortexLayoutNode node, Rect allocatedArea)
        {
            var runtime = _runtimeProvider();
            if (runtime != null && !string.IsNullOrEmpty(node.NodeId) && runtime.LayoutState.HostDimensions.ContainsKey(node.NodeId))
            {
                return Mathf.Clamp(runtime.LayoutState.HostDimensions[node.NodeId], 0.18f, 0.82f);
            }

            if (string.Equals(node.NodeId, "layout.primary", StringComparison.OrdinalIgnoreCase))
            {
                var width = Mathf.Clamp(runtime != null ? runtime.LayoutState.PrimarySideWidth : 280f, 220f, 380f);
                return Mathf.Clamp(width / Mathf.Max(1f, allocatedArea.width), 0.18f, 0.4f);
            }
            if (string.Equals(node.NodeId, "layout.secondary", StringComparison.OrdinalIgnoreCase))
            {
                var width = Mathf.Clamp(runtime != null ? runtime.LayoutState.SecondarySideWidth : 320f, 260f, 420f);
                return Mathf.Clamp((allocatedArea.width - width) / Mathf.Max(1f, allocatedArea.width), 0.35f, 0.82f);
            }
            if (string.Equals(node.NodeId, "layout.panel", StringComparison.OrdinalIgnoreCase))
            {
                var height = Mathf.Clamp(runtime != null ? runtime.LayoutState.PanelSize : 240f, 150f, 340f);
                return Mathf.Clamp((allocatedArea.height - height) / Mathf.Max(1f, allocatedArea.height), 0.4f, 0.84f);
            }
            return 0.5f;
        }

        private void StoreSplitRatio(CortexLayoutNode node, float splitRatio, Rect allocatedArea)
        {
            var runtime = _runtimeProvider();
            if (runtime == null || node == null || string.IsNullOrEmpty(node.NodeId)) return;

            splitRatio = Mathf.Clamp(splitRatio, 0.18f, 0.82f);
            runtime.LayoutState.HostDimensions[node.NodeId] = splitRatio;
            if (string.Equals(node.NodeId, "layout.primary", StringComparison.OrdinalIgnoreCase)) runtime.LayoutState.PrimarySideWidth = Mathf.Clamp(allocatedArea.width * splitRatio, 220f, 380f);
            else if (string.Equals(node.NodeId, "layout.secondary", StringComparison.OrdinalIgnoreCase)) runtime.LayoutState.SecondarySideWidth = Mathf.Clamp(allocatedArea.width * (1f - splitRatio), 260f, 420f);
            else if (string.Equals(node.NodeId, "layout.panel", StringComparison.OrdinalIgnoreCase)) runtime.LayoutState.PanelSize = Mathf.Clamp(allocatedArea.height * (1f - splitRatio), 150f, 340f);
        }

        private void DrawDockHost(WorkbenchPresentationSnapshot snapshot, WorkbenchHostLocation hostLocation, float width, GUIStyle tabStyle, GUIStyle activeTabStyle, GUIStyle tabCloseButtonStyle, GUIStyle captionStyle)
        {
            var activeContainerId = GetActiveContainerForHost(snapshot, hostLocation);
            CortexIdeLayout.DrawGroup(null, delegate
            {
                DrawHostDropTarget(hostLocation);
                GUILayout.BeginHorizontal();
                var items = GetHostItems(snapshot, hostLocation);
                foreach (var item in items) DrawDockTabButton(item, hostLocation, tabStyle, activeTabStyle, tabCloseButtonStyle);
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
                GUILayout.Space(2f);

                if (string.IsNullOrEmpty(activeContainerId)) GUILayout.Label(GetHostDescription(hostLocation), captionStyle);
                else _renderServiceProvider().DrawActiveModule(snapshot, activeContainerId, false);
            }, GUILayout.Width(width), GUILayout.ExpandHeight(true));
        }

        private void DrawEditorHost(WorkbenchPresentationSnapshot snapshot)
        {
            CortexIdeLayout.DrawGroup(null, delegate
            {
                DrawHostDropTarget(WorkbenchHostLocation.DocumentHost);
                _renderServiceProvider().DrawActiveModule(snapshot, _state.Workbench.EditorContainerId, false);
            }, GUILayout.ExpandHeight(true), GUILayout.ExpandWidth(true));
        }

        private void DrawPanelHost(WorkbenchPresentationSnapshot snapshot, float panelHeight, GUIStyle tabStyle, GUIStyle activeTabStyle, GUIStyle tabCloseButtonStyle, GUIStyle captionStyle)
        {
            var activeContainerId = GetActiveContainerForHost(snapshot, WorkbenchHostLocation.PanelHost);
            CortexIdeLayout.DrawGroup(null, delegate
            {
                DrawHostDropTarget(WorkbenchHostLocation.PanelHost);
                GUILayout.BeginHorizontal();
                var items = GetHostItems(snapshot, WorkbenchHostLocation.PanelHost);
                foreach (var item in items) DrawDockTabButton(item, WorkbenchHostLocation.PanelHost, tabStyle, activeTabStyle, tabCloseButtonStyle);
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();

                if (!string.IsNullOrEmpty(activeContainerId)) _renderServiceProvider().DrawActiveModule(snapshot, activeContainerId, false);
            }, GUILayout.Height(panelHeight), GUILayout.ExpandWidth(true));
        }

        private void DrawDockTabButton(ToolRailItem item, WorkbenchHostLocation hostLocation, GUIStyle tabStyle, GUIStyle activeTabStyle, GUIStyle tabCloseButtonStyle)
        {
            if (item == null) return;
            var activeContainerId = GetActiveContainerForHost(null, hostLocation);
            var isSelected = string.Equals(activeContainerId, item.ContainerId, StringComparison.OrdinalIgnoreCase);
            var prevBg = GUI.backgroundColor; var prevContent = GUI.contentColor;
            GUI.backgroundColor = CortexIdeLayout.GetInteractiveFillColor(isSelected, hostLocation);
            GUI.contentColor = CortexIdeLayout.GetInteractiveTextColor(isSelected);
            var rect = GUILayoutUtility.GetRect(116f, 116f, 24f, 24f);
            var input = _frameInputProvider != null ? _frameInputProvider() : new WorkbenchFrameInputSnapshot();
            var mousePos = ToVector2(input.PointerPosition);
            var isHovered = input.HasCurrentEvent && rect.Contains(mousePos);
            var closeRect = new Rect(rect.xMax - 18f, rect.y + 4f, 14f, Mathf.Max(12f, rect.height - 8f));

            if ((isHovered || isSelected) && input.CurrentEventKind == WorkbenchInputEventKind.MouseDown && input.CurrentMouseButton == 0 && closeRect.Contains(mousePos))
            {
                HideContainer(item.ContainerId); _state.StatusMessage = item.Title + " hidden.";
                GUI.backgroundColor = prevBg; GUI.contentColor = prevContent; return;
            }

            if (GUI.Toggle(rect, isSelected, (string.IsNullOrEmpty(item.IconAlias) ? "" : item.IconAlias + " ") + item.Title, isSelected ? activeTabStyle : tabStyle)) ActivateContainer(item.ContainerId);
            if ((isHovered || isSelected) && !string.Equals(item.ContainerId, CortexWorkbenchIds.EditorContainer, StringComparison.OrdinalIgnoreCase)) GUI.Box(closeRect, "x", tabCloseButtonStyle);
            HandleTabDrag(item.ContainerId, hostLocation, rect);
            GUI.backgroundColor = prevBg; GUI.contentColor = prevContent;
        }

        private void HandleTabDrag(string containerId, WorkbenchHostLocation hostLocation, Rect rect)
        {
            var input = _frameInputProvider != null ? _frameInputProvider() : new WorkbenchFrameInputSnapshot();
            if (!input.HasCurrentEvent || string.IsNullOrEmpty(containerId)) return;

            if (input.CurrentEventKind == WorkbenchInputEventKind.MouseDrag && rect.Contains(ToVector2(input.PointerPosition)))
            {
                _draggingContainerId = containerId; _draggingContainerSourceHost = hostLocation;
            }
            else if (!string.IsNullOrEmpty(_draggingContainerId) && input.CurrentEventKind == WorkbenchInputEventKind.MouseUp && string.Equals(_draggingContainerId, containerId, StringComparison.OrdinalIgnoreCase))
            {
                _draggingContainerId = string.Empty;
            }
        }

        private void DrawHostDropTarget(WorkbenchHostLocation hostLocation)
        {
            if (string.IsNullOrEmpty(_draggingContainerId)) { GUILayout.Space(2f); return; }
            var label = hostLocation == WorkbenchHostLocation.DocumentHost ? "Editor workspace" : "Release here to dock into " + CortexIdeLayout.GetHostDisplayName(hostLocation).ToLowerInvariant() + ".";
            GUILayout.Box(label, GUILayout.ExpandWidth(true), GUILayout.Height(18f));
            HandleDockDropTarget(hostLocation, GUILayoutUtility.GetLastRect());
        }

        private void HandleDockDropTarget(WorkbenchHostLocation hostLocation, Rect rect)
        {
            var input = _frameInputProvider != null ? _frameInputProvider() : new WorkbenchFrameInputSnapshot();
            if (!input.HasCurrentEvent || string.IsNullOrEmpty(_draggingContainerId)) return;

            if (input.CurrentEventKind == WorkbenchInputEventKind.MouseUp && rect.Contains(ToVector2(input.PointerPosition)))
            {
                DockContainer(_draggingContainerId, hostLocation); _draggingContainerId = string.Empty;
            }
            else if (input.CurrentEventKind == WorkbenchInputEventKind.MouseUp) _draggingContainerId = string.Empty;
        }

        public void ActivateContainer(string containerId) => _layoutHostRouter.ActivateContainer(GetLayoutContext(), containerId);
        public void DockContainer(string containerId, WorkbenchHostLocation hostLocation) => _layoutHostRouter.DockContainer(GetLayoutContext(), containerId, hostLocation);
        public void HideContainer(string containerId) => _layoutHostRouter.HideContainer(GetLayoutContext(), containerId);
        public WorkbenchHostLocation ResolveHostLocation(string containerId) => _layoutHostRouter.ResolveHostLocation(GetLayoutContext(), containerId);

        private bool HasHostItems(WorkbenchPresentationSnapshot snapshot, WorkbenchHostLocation hostLocation) => _layoutHostRouter.HasHostItems(GetLayoutContext(), snapshot, hostLocation);
        private List<ToolRailItem> GetHostItems(WorkbenchPresentationSnapshot snapshot, WorkbenchHostLocation hostLocation) => _layoutHostRouter.GetHostItems(GetLayoutContext(), snapshot, hostLocation);
        private string GetActiveContainerForHost(WorkbenchPresentationSnapshot snapshot, WorkbenchHostLocation hostLocation) => _layoutHostRouter.GetActiveContainerForHost(GetLayoutContext(), snapshot, hostLocation);

        private CortexShellLayoutContext GetLayoutContext()
        {
            if (_layoutContext == null) _layoutContext = new CortexShellLayoutContext(_state, _runtimeProvider);
            return _layoutContext;
        }

        private static string GetHostDescription(WorkbenchHostLocation hostLocation)
        {
            switch (hostLocation)
            {
                case WorkbenchHostLocation.PrimarySideHost: return "Primary sidebar for explorer-style tools and references.";
                case WorkbenchHostLocation.SecondarySideHost: return "Project and reference tooling docked to the right.";
                case WorkbenchHostLocation.PanelHost: return "Bottom panel for logs, build output, and transient tool panes.";
                default: return "Central editor workspace with persistent documents.";
            }
        }

        private static Vector2 ToVector2(Cortex.Rendering.Models.RenderPoint point)
        {
            return new Vector2(point.X, point.Y);
        }
    }
}
