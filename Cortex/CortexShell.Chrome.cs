using System;
using System.Collections.Generic;
using Cortex.Chrome;
using Cortex.Core.Models;
using Cortex.Modules.Shared;
using Cortex.Presentation.Abstractions;
using Cortex.Presentation.Models;
using Cortex.Rendering.RuntimeUi;
using UnityEngine;

namespace Cortex
{
    public sealed partial class CortexShellController
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
                    _sessionCoordinator.Visible = false;
                }));
            CortexWindowChromeController.DrawActions(actions);
            GUILayout.EndHorizontal();
        }

        private void DrawWorkbenchSurface(WorkbenchPresentationSnapshot snapshot, Rect workspaceRect)
        {
            _layoutCoordinator.DrawWorkbenchSurface(snapshot, workspaceRect, _tabStyle, _activeTabStyle, _tabCloseButtonStyle, _captionStyle);
        }

        private void DrawActiveModule(WorkbenchPresentationSnapshot snapshot, string containerId, bool detachedWindow)
        {
            GetModuleRenderService().DrawActiveModule(snapshot, containerId, detachedWindow);
        }

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

            if (!HasCurrentInputEvent())
            {
                return;
            }

            if (IsCurrentInputEvent(WorkbenchInputEventKind.MouseDown))
            {
                var mousePosition = GetCurrentMousePosition();
                var overGroup = false;
                foreach (var rect in _menuGroupRects.Values)
                {
                    if (new Rect(headerRect.x + rect.x, headerRect.y + rect.y, rect.width, rect.height).Contains(mousePosition))
                    {
                        overGroup = true;
                        break;
                    }
                }

                if (!popupRect.Contains(mousePosition) && !overGroup)
                {
                    _openMenuGroup = string.Empty;
                }
            }
            else if (IsCurrentInputEvent(WorkbenchInputEventKind.KeyDown) && IsCurrentKey(WorkbenchInputKey.Escape))
            {
                _openMenuGroup = string.Empty;
                ConsumeCurrentInputEvent();
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
                " | Projects: " + ProjectCatalog.GetProjects().Count +
                " | Logs window: " + (_state.Logs.ShowDetachedWindow ? "Open" : "Docked") +
                (string.IsNullOrEmpty(renderer) ? string.Empty : " | " + renderer);
        }
    }
}
