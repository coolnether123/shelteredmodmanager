using System;
using System.Collections.Generic;
using Cortex.Chrome;
using Cortex.Core.Models;
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
            actions.Add(new CortexWindowAction
            {
                ActionId = "shell.collapse",
                Label = "_",
                Width = 28f,
                Execute = delegate
                {
                    _windowRect = CortexWindowChromeController.ToggleCollapsed(_state.Chrome.Main, _windowRect, 126f, 28f);
                }
            });
            actions.Add(new CortexWindowAction
            {
                ActionId = "shell.logs",
                Label = _state.Logs.ShowDetachedWindow ? "Hide Logs Window" : "Show Logs Window",
                Width = 140f,
                Execute = delegate { ExecuteCommand("cortex.logs.toggleWindow", null); }
            });
            actions.Add(new CortexWindowAction
            {
                ActionId = "shell.fit",
                Label = "Fit Screen",
                Width = 90f,
                Execute = delegate { ExecuteCommand("cortex.shell.fitWindow", null); }
            });
            actions.Add(new CortexWindowAction
            {
                ActionId = "shell.close",
                Label = "Close",
                Width = 80f,
                Execute = delegate
                {
                    PersistWorkbenchSession();
                    PersistWindowSettings();
                    _visible = false;
                }
            });
            CortexWindowChromeController.DrawActions(actions);
            GUILayout.EndHorizontal();
            GUILayout.Space(4f);
            GUILayout.Label("Toggle: F8 | Side tools, editors, and bottom panels stay separated so logs remain visible while you work.", _captionStyle);
            if (snapshot != null && !string.IsNullOrEmpty(snapshot.RendererSummary))
            {
                GUILayout.Label("Backend: " + snapshot.RendererSummary, _captionStyle);
            }
            GUILayout.EndVertical();
        }

        private void DrawWorkbenchSelectors(WorkbenchPresentationSnapshot snapshot)
        {
            DrawHostSelector(snapshot, WorkbenchHostLocation.PrimarySideHost, "Side Host", "Projects, references, runtime tools, and settings.");
            DrawDocumentHostSummary(snapshot);
        }

        private void DrawHostSelector(WorkbenchPresentationSnapshot snapshot, WorkbenchHostLocation hostLocation, string title, string description)
        {
            if (snapshot == null)
            {
                return;
            }

            var items = snapshot.ToolRailItems;
            var hasItems = false;
            for (var i = 0; i < items.Count; i++)
            {
                if (items[i].HostLocation == hostLocation)
                {
                    hasItems = true;
                    break;
                }
            }

            if (!hasItems)
            {
                return;
            }

            GUILayout.BeginVertical(_sectionStyle);
            var previousContentColor = GUI.contentColor;
            GUI.contentColor = CortexIdeLayout.GetHostAccentColor(hostLocation);
            GUILayout.Label(title + " | " + CortexIdeLayout.GetHostDisplayName(hostLocation), _statusStyle);
            GUI.contentColor = previousContentColor;
            GUILayout.Label(description, _captionStyle);
            GUILayout.BeginHorizontal();
            for (var i = 0; i < items.Count; i++)
            {
                if (items[i].HostLocation != hostLocation)
                {
                    continue;
                }

                DrawTabButton(items[i].ContainerId, items[i].Title, false);
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }

        private void DrawTabButton(string containerId, string label, bool panelButton)
        {
            var hostLocation = ResolveHostLocation(containerId);
            var isSelected = panelButton
                ? string.Equals(_state.Workbench.PanelContainerId, containerId, StringComparison.OrdinalIgnoreCase)
                : hostLocation == WorkbenchHostLocation.DocumentHost
                    ? string.Equals(_state.Workbench.EditorContainerId, containerId, StringComparison.OrdinalIgnoreCase)
                    : string.Equals(_state.Workbench.SideContainerId, containerId, StringComparison.OrdinalIgnoreCase);
            var previousBackground = GUI.backgroundColor;
            var previousContent = GUI.contentColor;
            var accent = CortexIdeLayout.GetHostAccentColor(hostLocation);
            GUI.backgroundColor = isSelected ? new Color(accent.r * 0.45f, accent.g * 0.45f, accent.b * 0.45f, 1f) : new Color(0.16f, 0.17f, 0.2f, 1f);
            GUI.contentColor = isSelected ? Color.white : new Color(0.86f, 0.88f, 0.92f, 1f);
            if (GUILayout.Toggle(isSelected, label, isSelected ? _activeTabStyle : _tabStyle, GUILayout.Width(110f), GUILayout.Height(24f)))
            {
                ActivateContainer(containerId);
            }
            GUI.backgroundColor = previousBackground;
            GUI.contentColor = previousContent;
        }

        private void DrawDocumentHostSummary(WorkbenchPresentationSnapshot snapshot)
        {
            CortexIdeLayout.DrawGroup("Document Host | " + GetContainerTitle(snapshot, _state.Workbench.EditorContainerId), delegate
            {
                GUILayout.Label("The editor host stays mounted while you switch tools on the left.", _captionStyle);
                GUILayout.Label("Open docs: " + _state.Documents.OpenDocuments.Count + " | Active: " + (_state.Documents.ActiveDocument != null ? _state.Documents.ActiveDocument.FilePath : "None"), _captionStyle);
            });
        }

        private void DrawWorkbenchSurface(WorkbenchPresentationSnapshot snapshot)
        {
            var sideWidth = _workbenchRuntime != null ? _workbenchRuntime.LayoutState.PrimarySideWidth : 360f;
            CortexIdeLayout.DrawTwoPane(
                sideWidth,
                300f,
                delegate
                {
                    DrawSideHost(snapshot);
                },
                delegate
                {
                    DrawEditorHost(snapshot);
                });
        }

        private void DrawSideHost(WorkbenchPresentationSnapshot snapshot)
        {
            CortexIdeLayout.DrawGroup("Side Host | " + GetContainerTitle(snapshot, _state.Workbench.SideContainerId), delegate
            {
                var previousContentColor = GUI.contentColor;
                GUI.contentColor = CortexIdeLayout.GetHostAccentColor(WorkbenchHostLocation.PrimarySideHost);
                GUILayout.Label("Active side module: " + GetContainerTitle(snapshot, _state.Workbench.SideContainerId), _statusStyle);
                GUI.contentColor = previousContentColor;
                DrawActiveModule(_state.Workbench.SideContainerId, false);
            }, GUILayout.Width(Mathf.Max(300f, _workbenchRuntime != null ? _workbenchRuntime.LayoutState.PrimarySideWidth : 360f)));
        }

        private void DrawEditorHost(WorkbenchPresentationSnapshot snapshot)
        {
            CortexIdeLayout.DrawGroup("Editor Host | " + GetContainerTitle(snapshot, _state.Workbench.EditorContainerId), delegate
            {
                var previousContentColor = GUI.contentColor;
                GUI.contentColor = CortexIdeLayout.GetHostAccentColor(WorkbenchHostLocation.DocumentHost);
                GUILayout.Label("Editors remain visible while side tools and bottom panels change.", _statusStyle);
                GUI.contentColor = previousContentColor;
                DrawActiveModule(_state.Workbench.EditorContainerId, false);
            }, GUILayout.ExpandHeight(true), GUILayout.ExpandWidth(true));
        }

        private void DrawPanelHost(WorkbenchPresentationSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return;
            }

            var hasPanelItems = false;
            for (var i = 0; i < snapshot.ToolRailItems.Count; i++)
            {
                if (snapshot.ToolRailItems[i].HostLocation == WorkbenchHostLocation.PanelHost)
                {
                    hasPanelItems = true;
                    break;
                }
            }

            if (!hasPanelItems)
            {
                return;
            }

            CortexIdeLayout.DrawGroup("Bottom Panels | " + GetContainerTitle(snapshot, _state.Workbench.PanelContainerId), delegate
            {
                var previousContentColor = GUI.contentColor;
                GUI.contentColor = CortexIdeLayout.GetHostAccentColor(WorkbenchHostLocation.PanelHost);
                GUILayout.Label("Panels stay docked so logs and build output remain visible.", _statusStyle);
                GUI.contentColor = previousContentColor;

                GUILayout.BeginHorizontal();
                for (var i = 0; i < snapshot.ToolRailItems.Count; i++)
                {
                    if (snapshot.ToolRailItems[i].HostLocation != WorkbenchHostLocation.PanelHost)
                    {
                        continue;
                    }

                    DrawTabButton(snapshot.ToolRailItems[i].ContainerId, snapshot.ToolRailItems[i].Title, true);
                }
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
                GUILayout.Space(4f);
                DrawActiveModule(_state.Workbench.PanelContainerId, false);
            }, GUILayout.Height(Mathf.Clamp(_workbenchRuntime != null ? _workbenchRuntime.LayoutState.PanelSize : 320f, 240f, 460f)));
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
                " | Open docs: " + _state.Documents.OpenDocuments.Count +
                " | Projects: " + _projectCatalog.GetProjects().Count +
                " | Logs window: " + (_state.Logs.ShowDetachedWindow ? "Open" : "Docked") +
                (string.IsNullOrEmpty(renderer) ? string.Empty : " | " + renderer);
        }
    }
}
