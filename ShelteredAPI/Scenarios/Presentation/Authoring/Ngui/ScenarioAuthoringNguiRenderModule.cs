using System;
using System.Collections.Generic;
using System.Text;
using ModAPI.Core;
using ShelteredAPI.UI.Compatibility;
using UnityEngine;
using ShelteredAPI.Hooks;
using ShelteredAPI.Scenarios.Application.Authoring;
using ShelteredAPI.Scenarios.Composition;
using ShelteredAPI.Scenarios.Infrastructure.Unity;
using ShelteredAPI.Scenarios.Presentation.Authoring.Shell;
using ShelteredAPI.Scenarios.Presentation.Authoring.Windows;
namespace ShelteredAPI.Scenarios.Presentation.Authoring.Ngui{
    internal sealed class ScenarioAuthoringNguiRenderModule : IScenarioAuthoringRenderModule
    {
        private const string OverlayName = "ShelteredAPI_ScenarioAuthoringOverlay";
        private const float Margin = ScenarioAuthoringShellLayout.Margin;
        private const float Gutter = ScenarioAuthoringShellLayout.Gutter;
        private const float ToolRailWidth = ScenarioAuthoringShellLayout.ToolRailWidth;
        private const float InspectorWidth = ScenarioAuthoringShellLayout.InspectorWidth;
        private const float CommandDockHeight = ScenarioAuthoringShellLayout.CommandDockHeight;
        private const int BaseDepth = 20;

        private readonly Color _panelColor = new Color(0.09f, 0.085f, 0.075f, 0.94f);
        private readonly Color _panelAltColor = new Color(0.12f, 0.115f, 0.10f, 0.94f);
        private readonly Color _statusColor = new Color(0.07f, 0.075f, 0.08f, 0.96f);
        private readonly Color _buttonColor = new Color(0.18f, 0.165f, 0.13f, 0.96f);
        private readonly Color _buttonActiveColor = new Color(0.58f, 0.43f, 0.15f, 0.98f);
        private readonly Color _buttonDisabledColor = new Color(0.13f, 0.13f, 0.13f, 0.70f);
        private readonly Color _borderColor = new Color(0.68f, 0.56f, 0.34f, 0.95f);
        private readonly Color _titleColor = new Color(0.98f, 0.86f, 0.58f, 1f);
        private readonly Color _bodyColor = new Color(0.90f, 0.88f, 0.80f, 1f);
        private readonly Color _mutedColor = new Color(0.70f, 0.68f, 0.60f, 1f);
        private readonly Dictionary<string, int> _scrollOffsets = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private readonly List<Rect> _interactiveRects = new List<Rect>();

        private GameObject _root;
        private UIPanel _panel;
        private UIFontCache.FontResult _fonts;
        private float _scaledWidth;
        private float _scaledHeight;
        private float _coordinateScale = 1f;
        private string _lastSignature;
        private bool _windowMenuOpen;

        public string ModuleId
        {
            get { return "ShelteredAPI.NGUI"; }
        }

        public int Priority
        {
            get { return 50; }
        }

        public bool CanRender()
        {
            if (UIRoot.list == null || UIRoot.list.Count == 0)
                return false;

            try
            {
                ScenarioAuthoringState state = ScenarioAuthoringBackendService.Instance.CurrentState;
                if (state != null
                    && state.Settings != null
                    && string.Equals(state.Settings.Get("shell.renderer_mode", "ngui"), "imgui", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }
            catch
            {
            }

            return true;
        }

        public void Render(ScenarioAuthoringPresentationSnapshot snapshot)
        {
            if (snapshot == null || snapshot.State == null || !snapshot.State.IsActive)
            {
                Hide();
                return;
            }

            if (!EnsureUi())
                return;

            ResolveMetrics();
            ScenarioAuthoringInputCaptureService inputCapture = ScenarioCompositionRoot.Resolve<ScenarioAuthoringInputCaptureService>();
            if (inputCapture != null)
                inputCapture.BeginFrame(_coordinateScale);

            string signature = BuildSignature(snapshot);
            bool rebuild = !string.Equals(signature, _lastSignature, StringComparison.Ordinal)
                || Math.Abs(UnityEngine.Input.GetAxis("Mouse ScrollWheel")) > 0.001f;
            if (rebuild)
            {
                _lastSignature = signature;
                ClearOverlay();
                BuildShell(snapshot);
            }

            RegisterInteractiveRects(inputCapture);
            if (inputCapture != null)
            {
                inputCapture.SetPopupOpen(_windowMenuOpen
                    || (snapshot.ShellViewModel != null && snapshot.ShellViewModel.Settings != null)
                    || (snapshot.ShellViewModel != null && snapshot.ShellViewModel.SpritePickerDocument != null)
                    || (snapshot.ShellViewModel != null && snapshot.ShellViewModel.ContextMenu != null && snapshot.ShellViewModel.ContextMenu.Visible));
                inputCapture.SetKeyboardCaptured(snapshot.ShellViewModel != null
                    && (snapshot.ShellViewModel.Settings != null || snapshot.ShellViewModel.SpritePickerDocument != null));
                inputCapture.CompleteFrame();
            }
        }

        public void Hide()
        {
            _lastSignature = null;
            _windowMenuOpen = false;
            _interactiveRects.Clear();
            ClearOverlay();
        }

        private bool EnsureUi()
        {
            UIFontCache.RefreshIfMissing();
            _panel = UIUtil.EnsureOverlayPanel(OverlayName, 65000);
            if (_panel == null)
                return false;

            _root = _panel.gameObject;
            _fonts = UIFontCache.GetFonts();
            UIFontCache.SeedFromGameObject(_root, OverlayName);
            return true;
        }

        private void ResolveMetrics()
        {
            UIRoot root = UIRoot.list != null && UIRoot.list.Count > 0 ? UIRoot.list[0] : null;
            _scaledHeight = root != null ? root.activeHeight : Screen.height;
            _scaledWidth = _scaledHeight * ((float)Screen.width / Math.Max(1, Screen.height));
            _coordinateScale = Screen.height / Math.Max(1f, _scaledHeight);
        }

        private void BuildShell(ScenarioAuthoringPresentationSnapshot snapshot)
        {
            _interactiveRects.Clear();
            ScenarioAuthoringShellViewModel shell = snapshot.ShellViewModel;
            if (shell == null || !snapshot.State.ShellVisible)
                return;

            Rect hudReserveRect = ScenarioAuthoringShellLayout.BuildHudReserveRect(_scaledWidth);
            Rect topRect = ScenarioAuthoringShellLayout.BuildTopBarRect(_scaledWidth, hudReserveRect);
            Rect statusRect = ScenarioAuthoringShellLayout.BuildStatusRect(_scaledWidth, _scaledHeight);
            Rect contentRect = ScenarioAuthoringShellLayout.BuildContentRect(_scaledWidth, topRect, statusRect);

            Rect windowsButton = DrawTopBar(topRect, shell);
            DrawToolRail(contentRect, snapshot.State);
            DrawCommandDock(contentRect, snapshot.State);
            DrawStatusBar(statusRect, shell, snapshot.State);
            DrawWindows(shell.Windows, contentRect);
            DrawCollapsedWindows(statusRect, shell.Windows);

            if (_windowMenuOpen)
                DrawWindowMenu(windowsButton, shell.WindowMenuActions, hudReserveRect);

            if (shell.ContextMenu != null && shell.ContextMenu.Visible)
                DrawContextMenu(shell.ContextMenu, hudReserveRect);

            if (shell.SpritePickerDocument != null)
                DrawDocumentModal("sprite_picker", shell.SpritePickerDocument, hudReserveRect, 980f, 680f);

            if (shell.Settings != null)
                DrawSettingsModal(shell.Settings, hudReserveRect);
        }

        private Rect DrawTopBar(Rect rect, ScenarioAuthoringShellViewModel shell)
        {
            DrawPanel("TopBar", rect, _panelColor, true, BaseDepth);
            DrawLabel("TopTitle", rect, new Rect(18f, 9f, 215f, 28f), "SHELTERED", 24, _titleColor, NGUIText.Alignment.Left, BaseDepth + 4);
            DrawLabel("TopSubtitle", rect, new Rect(18f, 36f, 230f, 20f), "SCENARIO WORKSHOP", 15, _mutedColor, NGUIText.Alignment.Left, BaseDepth + 4);

            float actionRight = rect.xMax - 14f;
            float toolbarX = Math.Max(rect.x + 250f, actionRight - MeasureTopBarActionsWidth(shell.ToolbarActions));
            Rect windowButton = DrawWindowMenuButton(shell.LayoutActions, actionRight, rect.y + 52f);

            float tabX = rect.x + 250f;
            float tabY = rect.y + 10f;
            float tabRight = Math.Max(tabX, toolbarX - 10f);
            for (int i = 0; shell.Tabs != null && i < shell.Tabs.Length; i++)
            {
                ScenarioAuthoringInspectorAction tab = shell.Tabs[i];
                if (tab == null || IsChildStageTab(tab))
                    continue;

                float width = ClampTextWidth(tab.Label, 92f, 156f);
                if (tabX + width > tabRight)
                    break;

                DrawButton(new Rect(tabX, tabY, width, 34f), tab, true, "TopTab" + i);
                tabX += width + 4f;
            }

            float childX = rect.x + 250f;
            float childY = rect.y + 54f;
            float childRight = windowButton.width > 0f ? windowButton.x - 10f : rect.xMax - 14f;
            for (int i = 0; shell.Tabs != null && i < shell.Tabs.Length; i++)
            {
                ScenarioAuthoringInspectorAction tab = shell.Tabs[i];
                if (tab == null || !IsChildStageTab(tab))
                    continue;

                ScenarioAuthoringInspectorAction child = CloneAction(tab, CleanChildStageLabel(tab.Label));
                float width = ClampTextWidth(child.Label, 92f, 126f);
                if (childX + width > childRight)
                    break;

                DrawButton(new Rect(childX, childY, width, 28f), child, true, "ChildTab" + i);
                childX += width + 4f;
            }

            float actionX = toolbarX;
            for (int i = 0; shell.ToolbarActions != null && i < shell.ToolbarActions.Length; i++)
            {
                ScenarioAuthoringInspectorAction action = shell.ToolbarActions[i];
                float width = ClampTextWidth(action != null ? action.Label : null, 86f, 118f);
                if (actionX + width > actionRight)
                    break;

                DrawButton(new Rect(actionX, rect.y + 14f, width, 30f), action, false, "TopAction" + i);
                actionX += width + 5f;
            }

            RegisterRect(rect);
            return windowButton;
        }

        private float MeasureTopBarActionsWidth(ScenarioAuthoringInspectorAction[] actions)
        {
            float width = 0f;
            for (int i = 0; actions != null && i < actions.Length; i++)
            {
                ScenarioAuthoringInspectorAction action = actions[i];
                width += ClampTextWidth(action != null ? action.Label : null, 86f, 118f);
                if (i + 1 < actions.Length)
                    width += 5f;
            }

            return width;
        }

        private Rect DrawWindowMenuButton(ScenarioAuthoringInspectorAction[] actions, float right, float y)
        {
            for (int i = 0; actions != null && i < actions.Length; i++)
            {
                ScenarioAuthoringInspectorAction action = actions[i];
                if (!IsWindowMenuAction(action))
                    continue;

                float width = ClampTextWidth(action.Label, 92f, 116f);
                Rect rect = new Rect(right - width, y, width, 28f);
                DrawButton(rect, _windowMenuOpen ? CloneEmphasized(action) : action, false, "WindowMenuButton");
                return rect;
            }

            return RuntimeCompat.ZeroRect();
        }

        private void DrawToolRail(Rect contentRect, ScenarioAuthoringState state)
        {
            Rect rect = new Rect(contentRect.x + 4f, contentRect.y + 24f, ToolRailWidth, 504f);
            DrawPanel("ToolRail", rect, _panelColor, true, BaseDepth);

            float y = rect.y + 10f;
            DrawToolButton(new Rect(rect.x + 8f, y, rect.width - 16f, 66f), state, ScenarioAuthoringTool.Select, ScenarioAuthoringActionIds.ActionToolSelect, "SEL", "Select", "ToolSelect");
            y += 72f;
            DrawToolButton(new Rect(rect.x + 8f, y, rect.width - 16f, 66f), state, ScenarioAuthoringTool.Objects, ScenarioAuthoringActionIds.ActionToolObjects, "OBJ", "Objects", "ToolObjects");
            y += 72f;
            DrawToolButton(new Rect(rect.x + 8f, y, rect.width - 16f, 66f), state, ScenarioAuthoringTool.Shelter, ScenarioAuthoringActionIds.ActionToolShelter, "STR", "Structure", "ToolStructure");
            y += 72f;
            DrawToolButton(new Rect(rect.x + 8f, y, rect.width - 16f, 78f), state, ScenarioAuthoringTool.Wiring, ScenarioAuthoringActionIds.ActionToolWiring, "WIR", "Walls\nWiring", "ToolWiring");
            y += 84f;
            DrawToolButton(new Rect(rect.x + 8f, y, rect.width - 16f, 66f), state, ScenarioAuthoringTool.Assets, ScenarioAuthoringActionIds.ActionToolAssets, "AST", "Assets", "ToolAssets");
            y += 72f;
            DrawToolButton(new Rect(rect.x + 8f, y, rect.width - 16f, 66f), state, ScenarioAuthoringTool.WinLoss, ScenarioAuthoringActionIds.ActionToolWinLoss, "WIN", "Win/Loss", "ToolWinLoss");
            y += 72f;
            DrawToolButton(new Rect(rect.x + 8f, y, rect.width - 16f, 66f), state, ScenarioAuthoringTool.Family, ScenarioAuthoringActionIds.ActionToolPeople, "PPL", "People", "ToolPeople");
            RegisterRect(rect);
        }

        private void DrawToolButton(Rect rect, ScenarioAuthoringState state, ScenarioAuthoringTool tool, string actionId, string badge, string label, string name)
        {
            bool active = state != null && (state.ActiveTool == tool || (tool == ScenarioAuthoringTool.Family && state.ActiveTool == ScenarioAuthoringTool.People));
            ScenarioAuthoringInspectorAction action = new ScenarioAuthoringInspectorAction
            {
                Id = actionId,
                Label = label,
                Badge = badge,
                Enabled = true,
                Emphasized = active
            };
            DrawButton(rect, action, false, name);
        }

        private void DrawCommandDock(Rect contentRect, ScenarioAuthoringState state)
        {
            Rect rect = new Rect(contentRect.x + ((contentRect.width - 596f) * 0.5f), contentRect.yMax - CommandDockHeight - 16f, 596f, CommandDockHeight);
            DrawPanel("CommandDock", rect, _panelColor, true, BaseDepth);
            float x = rect.x + 10f;
            DrawButton(new Rect(x, rect.y + 8f, 82f, 32f), CommandAction(ScenarioAuthoringActionIds.ActionToolSelect, "Select", state != null && state.ActiveTool == ScenarioAuthoringTool.Select), false, "CommandSelect");
            x += 90f;
            DrawButton(new Rect(x, rect.y + 8f, 78f, 32f), DisabledAction("Move"), false, "CommandMove");
            x += 86f;
            DrawButton(new Rect(x, rect.y + 8f, 84f, 32f), DisabledAction("Rotate"), false, "CommandRotate");
            x += 92f;
            DrawButton(new Rect(x, rect.y + 8f, 96f, 32f), DisabledAction("Duplicate"), false, "CommandDuplicate");
            x += 104f;
            DrawButton(new Rect(x, rect.y + 8f, 74f, 32f), CommandAction(ScenarioAuthoringActionIds.ActionSelectionClear, "Clear", false), false, "CommandClear");
            x += 82f;
            DrawButton(new Rect(x, rect.y + 8f, 82f, 32f), CommandAction(ScenarioAuthoringActionIds.ActionPlaytest, "Playtest", ScenarioAuthoringRuntimeGuards.IsPlaytesting()), false, "CommandPlaytest");
            RegisterRect(rect);
        }

        private void DrawStatusBar(Rect rect, ScenarioAuthoringShellViewModel shell, ScenarioAuthoringState state)
        {
            DrawPanel("StatusBar", rect, _statusColor, false, BaseDepth);
            float x = rect.x + 18f;
            for (int i = 0; shell.StatusEntries != null && i < shell.StatusEntries.Length; i++)
            {
                string value = shell.StatusEntries[i] ?? string.Empty;
                float width = Mathf.Clamp(value.Length * 7.5f + 24f, 84f, 260f);
                if (x + width > rect.xMax - 238f)
                    break;

                DrawLabel("Status" + i, rect, new Rect(x - rect.x, 13f, width, 20f), Shorten(value, 38), 14, _mutedColor, NGUIText.Alignment.Left, BaseDepth + 4);
                x += width + 8f;
            }

            DrawButton(new Rect(rect.xMax - 220f, rect.y + 8f, 72f, 30f), CommandAction(ScenarioAuthoringActionIds.ActionSave, "Save", true), false, "StatusSave");
            DrawButton(new Rect(rect.xMax - 142f, rect.y + 8f, 86f, 30f), CommandAction(ScenarioAuthoringActionIds.ActionPlaytest, ScenarioAuthoringRuntimeGuards.IsPlaytesting() ? "Stop Test" : "Playtest", ScenarioAuthoringRuntimeGuards.IsPlaytesting()), false, "StatusPlaytest");
            DrawButton(new Rect(rect.xMax - 50f, rect.y + 8f, 42f, 30f), CommandAction(ScenarioAuthoringActionIds.ActionShellToggle, "X", false), false, "StatusHide");
            RegisterRect(rect);
        }

        private void DrawWindows(ScenarioAuthoringShellWindowViewModel[] windows, Rect contentRect)
        {
            Dictionary<string, Rect> rects = ResolveWindowRects(windows, contentRect);
            for (int i = 0; windows != null && i < windows.Length; i++)
            {
                ScenarioAuthoringShellWindowViewModel window = windows[i];
                Rect rect;
                if (window == null || !window.Visible || window.Collapsed || !rects.TryGetValue(window.Id, out rect))
                    continue;

                DrawWindow(rect, window, i);
            }
        }

        private Dictionary<string, Rect> ResolveWindowRects(ScenarioAuthoringShellWindowViewModel[] windows, Rect contentRect)
        {
            Dictionary<string, Rect> rects = new Dictionary<string, Rect>(StringComparer.OrdinalIgnoreCase);
            float viewportLeft = contentRect.x + ToolRailWidth + Gutter;
            float viewportRight = contentRect.xMax - InspectorWidth - Gutter;

            for (int i = 0; windows != null && i < windows.Length; i++)
            {
                ScenarioAuthoringShellWindowViewModel window = windows[i];
                if (window == null)
                    continue;

                if (string.Equals(window.Id, ScenarioAuthoringWindowIds.Inspector, StringComparison.OrdinalIgnoreCase))
                {
                    rects[window.Id] = ScenarioAuthoringShellLayout.BuildInspectorRect(contentRect);
                }
                else if (string.Equals(window.Id, ScenarioAuthoringWindowIds.BuildTools, StringComparison.OrdinalIgnoreCase))
                {
                    rects[window.Id] = ScenarioAuthoringShellLayout.BuildBottomTrayRect(contentRect, viewportLeft, viewportRight);
                }
                else if (string.Equals(window.Id, ScenarioAuthoringWindowIds.Hierarchy, StringComparison.OrdinalIgnoreCase))
                {
                    rects[window.Id] = new Rect(viewportLeft, contentRect.y + 40f, Mathf.Min(360f, Math.Max(300f, window.Width)), Mathf.Min(470f, contentRect.height - 160f));
                }
                else if (string.Equals(window.Id, ScenarioAuthoringWindowIds.SelectionStack, StringComparison.OrdinalIgnoreCase))
                {
                    rects[window.Id] = new Rect(viewportLeft, contentRect.yMax - 356f, Mathf.Min(360f, Math.Max(300f, window.Width)), 268f);
                }
                else
                {
                    Rect workspace = ScenarioAuthoringShellLayout.BuildWorkspaceRect(contentRect, false);
                    float width = Mathf.Min(window.Width > 0f ? window.Width : 720f, Math.Max(520f, viewportRight - viewportLeft));
                    float height = Mathf.Min(window.Height > 0f ? window.Height : 420f, Math.Max(260f, contentRect.height - 120f));
                    float x = workspace.x + ((workspace.width - width) * 0.5f) + ((i % 3) * 18f);
                    float y = workspace.y + ((workspace.height - height) * 0.5f) + ((i % 2) * 18f);
                    rects[window.Id] = ScenarioAuthoringShellLayout.ClampAwayFromHud(new Rect(x, y, width, height), _scaledWidth, _scaledHeight, ScenarioAuthoringShellLayout.BuildHudReserveRect(_scaledWidth));
                }
            }

            return rects;
        }

        private void DrawWindow(Rect rect, ScenarioAuthoringShellWindowViewModel window, int index)
        {
            DrawPanel("WindowBorder" + window.Id, rect, _borderColor, true, BaseDepth + 2 + index);
            DrawPanel("Window" + window.Id, new Rect(rect.x + 2f, rect.y + 2f, rect.width - 4f, rect.height - 4f), _panelColor, true, BaseDepth + 3 + index);
            DrawLabel("WindowTitle" + window.Id, rect, new Rect(12f, 10f, rect.width - 144f, 24f), window.Title, 18, _titleColor, NGUIText.Alignment.Left, BaseDepth + 8 + index);

            float actionX = rect.xMax - 76f;
            for (int i = 0; window.HeaderActions != null && i < window.HeaderActions.Length && i < 2; i++)
            {
                DrawButton(new Rect(actionX, rect.y + 8f, 30f, 24f), window.HeaderActions[i], false, "WinHeader" + window.Id + i);
                actionX += 34f;
            }

            List<RowModel> rows = BuildRows(window.Sections);
            int rowHeight = 24;
            int startY = 44;
            int visibleRows = Math.Max(3, Mathf.FloorToInt((rect.height - startY - 12f) / rowHeight));
            int maxOffset = Math.Max(0, rows.Count - visibleRows);
            int offset = GetScrollOffset(window.Id, rect, maxOffset);

            for (int i = offset; i < rows.Count && i < offset + visibleRows; i++)
            {
                RowModel row = rows[i];
                Rect rowRect = new Rect(rect.x + 10f, rect.y + startY + ((i - offset) * rowHeight), rect.width - 20f, rowHeight - 3f);
                DrawRow(rowRect, row, "Row" + window.Id + i, BaseDepth + 10 + index);
            }

            if (maxOffset > 0)
            {
                string marker = (offset + 1) + "-" + Math.Min(rows.Count, offset + visibleRows) + " / " + rows.Count;
                DrawLabel("ScrollMarker" + window.Id, rect, new Rect(rect.width - 96f, rect.height - 24f, 84f, 18f), marker, 12, _mutedColor, NGUIText.Alignment.Right, BaseDepth + 20 + index);
            }

            RegisterRect(rect);
        }

        private List<RowModel> BuildRows(ScenarioAuthoringInspectorSection[] sections)
        {
            List<RowModel> rows = new List<RowModel>();
            for (int i = 0; sections != null && i < sections.Length; i++)
            {
                ScenarioAuthoringInspectorSection section = sections[i];
                if (section == null)
                    continue;

                rows.Add(RowModel.Header(section.Title));
                for (int j = 0; section.Items != null && j < section.Items.Length; j++)
                {
                    ScenarioAuthoringInspectorItem item = section.Items[j];
                    if (item != null)
                        rows.Add(RowModel.FromItem(item));
                }
            }

            if (rows.Count == 0)
                rows.Add(RowModel.Text("No data is available."));
            return rows;
        }

        private void DrawRow(Rect rect, RowModel row, string name, int depth)
        {
            if (row == null)
                return;

            if (row.Action != null)
            {
                DrawButton(rect, row.Action, false, name);
                return;
            }

            if (row.IsHeader)
            {
                DrawLabel(name, rect, new Rect(0f, 0f, rect.width, rect.height), row.Label, 14, _titleColor, NGUIText.Alignment.Left, depth + 1);
                return;
            }

            string text = !string.IsNullOrEmpty(row.Label) ? (row.Label + ": " + row.Value) : row.Value;
            DrawLabel(name, rect, new Rect(0f, 1f, rect.width, rect.height), Shorten(text, 92), 13, row.Emphasized ? _titleColor : _bodyColor, NGUIText.Alignment.Left, depth + 1);
        }

        private int GetScrollOffset(string id, Rect rect, int maxOffset)
        {
            int current;
            if (!_scrollOffsets.TryGetValue(id, out current))
                current = 0;

            if (maxOffset <= 0)
            {
                _scrollOffsets[id] = 0;
                return 0;
            }

            Vector2 pointer = GetPointerPosition();
            if (rect.Contains(pointer))
            {
                float scroll = UnityEngine.Input.GetAxis("Mouse ScrollWheel");
                if (Math.Abs(scroll) > 0.001f)
                    current += scroll > 0f ? -2 : 2;
            }

            current = Mathf.Clamp(current, 0, maxOffset);
            _scrollOffsets[id] = current;
            return current;
        }

        private void DrawCollapsedWindows(Rect statusRect, ScenarioAuthoringShellWindowViewModel[] windows)
        {
            float x = statusRect.x + 16f;
            float y = statusRect.y - 34f;
            for (int i = 0; windows != null && i < windows.Length; i++)
            {
                ScenarioAuthoringShellWindowViewModel window = windows[i];
                if (window == null || !window.Collapsed)
                    continue;

                ScenarioAuthoringInspectorAction action = new ScenarioAuthoringInspectorAction
                {
                    Id = ScenarioAuthoringActionIds.ActionWindowRestorePrefix + window.Id,
                    Label = window.Title,
                    Enabled = true,
                    Badge = "WN"
                };
                float width = ClampTextWidth(window.Title, 92f, 160f);
                DrawButton(new Rect(x, y, width, 28f), action, false, "Collapsed" + i);
                x += width + 6f;
            }
        }

        private void DrawWindowMenu(Rect buttonRect, ScenarioAuthoringInspectorAction[] actions, Rect hudReserveRect)
        {
            Rect rect = new Rect(buttonRect.xMax - 250f, buttonRect.yMax + 8f, 250f, Math.Min(420f, 44f + Count(actions) * 30f));
            rect = ScenarioAuthoringShellLayout.ClampAwayFromHud(rect, _scaledWidth, _scaledHeight, hudReserveRect);
            DrawPanel("WindowMenu", rect, _panelAltColor, true, BaseDepth + 90);
            DrawLabel("WindowMenuTitle", rect, new Rect(12f, 8f, rect.width - 24f, 22f), "Windows", 16, _titleColor, NGUIText.Alignment.Left, BaseDepth + 94);

            float y = rect.y + 36f;
            for (int i = 0; actions != null && i < actions.Length; i++)
            {
                DrawButton(new Rect(rect.x + 10f, y, rect.width - 20f, 24f), actions[i], false, "WindowMenuAction" + i);
                y += 28f;
            }

            RegisterRect(rect);
        }

        private void DrawContextMenu(ScenarioAuthoringContextMenuModel menu, Rect hudReserveRect)
        {
            float rectWidth = 260f;
            float rectHeight = Math.Min(340f, 72f + Count(menu.Actions) * 30f);
            for (int i = 0; menu.Actions != null && i < menu.Actions.Length; i++)
            {
                ScenarioAuthoringInspectorAction action = menu.Actions[i];
                if (action != null && !action.Enabled && !string.IsNullOrEmpty(action.DisabledReason))
                    rectHeight = Math.Min(340f, rectHeight + MeasureContextReasonHeight(action.DisabledReason, rectWidth - 24f));
            }
            Rect rect = menu.CenterOnScreen
                ? ScenarioAuthoringShellLayout.BuildCenteredPopupRect(_scaledWidth, _scaledHeight, rectWidth, rectHeight, hudReserveRect)
                : ScenarioAuthoringShellLayout.ClampAwayFromHud(new Rect(menu.AnchorX, menu.AnchorY, rectWidth, rectHeight), _scaledWidth, _scaledHeight, hudReserveRect);
            DrawPanel("ContextMenu", rect, _panelAltColor, true, BaseDepth + 100);
            DrawLabel("ContextTitle", rect, new Rect(12f, 8f, rect.width - 24f, 22f), menu.Title ?? "Selection", 16, _titleColor, NGUIText.Alignment.Left, BaseDepth + 104);
            DrawLabel("ContextDetail", rect, new Rect(12f, 30f, rect.width - 24f, 18f), menu.Detail ?? string.Empty, 12, _mutedColor, NGUIText.Alignment.Left, BaseDepth + 104);
            float y = rect.y + 56f;
            for (int i = 0; menu.Actions != null && i < menu.Actions.Length; i++)
            {
                ScenarioAuthoringInspectorAction action = menu.Actions[i];
                DrawButton(new Rect(rect.x + 10f, y, rect.width - 20f, 24f), action, false, "ContextAction" + i);
                y += 28f;
                if (action != null && !action.Enabled && !string.IsNullOrEmpty(action.DisabledReason))
                {
                    float reasonHeight = MeasureContextReasonHeight(action.DisabledReason, rect.width - 24f);
                    DrawLabel("ContextActionReason" + i, rect, new Rect(12f, y - rect.y, rect.width - 24f, reasonHeight), FormatContextReason(action.DisabledReason, rect.width - 24f), 11, _mutedColor, NGUIText.Alignment.Left, BaseDepth + 104);
                    y += reasonHeight;
                }
            }
            RegisterRect(rect);
        }

        private static float MeasureContextReasonHeight(string reason, float width)
        {
            if (string.IsNullOrEmpty(reason))
                return 0f;

            return Math.Min(54f, Math.Max(18f, CountContextReasonLines(reason, width) * 15f + 3f));
        }

        private static string FormatContextReason(string reason, float width)
        {
            if (string.IsNullOrEmpty(reason))
                return string.Empty;

            int maxChars = Math.Max(24, Mathf.FloorToInt(width / 6.2f));
            int lines = CountContextReasonLines(reason, width);
            if (lines <= 1)
                return reason;

            string[] words = reason.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            StringBuilder builder = new StringBuilder();
            int lineLength = 0;
            int writtenLines = 1;
            for (int i = 0; i < words.Length; i++)
            {
                string word = words[i];
                int nextLength = lineLength == 0 ? word.Length : lineLength + 1 + word.Length;
                if (nextLength > maxChars && lineLength > 0 && writtenLines < 3)
                {
                    builder.Append('\n');
                    lineLength = 0;
                    writtenLines++;
                }
                else if (lineLength > 0)
                {
                    builder.Append(' ');
                    lineLength++;
                }

                if (writtenLines >= 3 && lineLength + word.Length > maxChars)
                {
                    builder.Append(ShortenStatic(word, Math.Max(3, maxChars - lineLength)));
                    return builder.ToString();
                }

                builder.Append(word);
                lineLength += word.Length;
            }

            return builder.ToString();
        }

        private static int CountContextReasonLines(string reason, float width)
        {
            if (string.IsNullOrEmpty(reason))
                return 0;

            int maxChars = Math.Max(24, Mathf.FloorToInt(width / 6.2f));
            int lines = 1;
            int lineLength = 0;
            string[] words = reason.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < words.Length; i++)
            {
                int wordLength = words[i].Length;
                int nextLength = lineLength == 0 ? wordLength : lineLength + 1 + wordLength;
                if (nextLength > maxChars && lineLength > 0)
                {
                    lines++;
                    lineLength = wordLength;
                }
                else
                {
                    lineLength = nextLength;
                }
            }

            return Math.Min(3, lines);
        }

        private static string ShortenStatic(string value, int max)
        {
            if (string.IsNullOrEmpty(value) || max <= 0 || value.Length <= max)
                return value ?? string.Empty;
            if (max <= 3)
                return value.Substring(0, max);
            return value.Substring(0, max - 3) + "...";
        }

        private void DrawDocumentModal(string id, ScenarioAuthoringInspectorDocument document, Rect hudReserveRect, float preferredWidth, float preferredHeight)
        {
            Rect rect = new Rect((_scaledWidth - preferredWidth) * 0.5f, (_scaledHeight - preferredHeight) * 0.5f, Math.Min(preferredWidth, _scaledWidth - 32f), Math.Min(preferredHeight, _scaledHeight - 120f));
            rect = ScenarioAuthoringShellLayout.ClampAwayFromHud(rect, _scaledWidth, _scaledHeight, hudReserveRect);
            DrawPanel(id + "Modal", rect, _panelColor, true, BaseDepth + 110);
            DrawLabel(id + "Title", rect, new Rect(14f, 10f, rect.width - 28f, 26f), document.Title, 20, _titleColor, NGUIText.Alignment.Left, BaseDepth + 114);

            ScenarioAuthoringShellWindowViewModel window = new ScenarioAuthoringShellWindowViewModel
            {
                Id = id,
                Title = document.Title,
                Sections = document.Sections,
                HeaderActions = document.HeaderActions
            };
            DrawWindow(new Rect(rect.x + 10f, rect.y + 42f, rect.width - 20f, rect.height - 52f), window, 0);
            RegisterRect(rect);
        }

        private void DrawSettingsModal(ScenarioAuthoringSettingsViewModel settings, Rect hudReserveRect)
        {
            Rect rect = new Rect((_scaledWidth - 720f) * 0.5f, (_scaledHeight - 540f) * 0.5f, Math.Min(720f, _scaledWidth - 32f), Math.Min(540f, _scaledHeight - 120f));
            rect = ScenarioAuthoringShellLayout.ClampAwayFromHud(rect, _scaledWidth, _scaledHeight, hudReserveRect);
            DrawPanel("SettingsModal", rect, _panelColor, true, BaseDepth + 120);
            DrawLabel("SettingsTitle", rect, new Rect(14f, 10f, rect.width - 160f, 26f), settings.Title, 20, _titleColor, NGUIText.Alignment.Left, BaseDepth + 124);

            float x = rect.xMax - 150f;
            for (int i = 0; settings.HeaderActions != null && i < settings.HeaderActions.Length; i++)
            {
                DrawButton(new Rect(x, rect.y + 10f, 66f, 24f), settings.HeaderActions[i], false, "SettingsHeader" + i);
                x += 72f;
            }

            List<RowModel> rows = new List<RowModel>();
            for (int i = 0; settings.Sections != null && i < settings.Sections.Length; i++)
            {
                ScenarioAuthoringSettingsSectionViewModel section = settings.Sections[i];
                if (section == null)
                    continue;

                rows.Add(RowModel.Header(section.Title));
                for (int j = 0; section.Items != null && j < section.Items.Length; j++)
                    AppendSettingRows(rows, section.Items[j]);
            }

            int rowHeight = 26;
            int visibleRows = Math.Max(4, Mathf.FloorToInt((rect.height - 58f) / rowHeight));
            int offset = GetScrollOffset("settings_modal", rect, Math.Max(0, rows.Count - visibleRows));
            for (int i = offset; i < rows.Count && i < offset + visibleRows; i++)
            {
                DrawRow(new Rect(rect.x + 12f, rect.y + 46f + ((i - offset) * rowHeight), rect.width - 24f, rowHeight - 3f), rows[i], "SettingsRow" + i, BaseDepth + 126);
            }
            RegisterRect(rect);
        }

        private static void AppendSettingRows(List<RowModel> rows, ScenarioAuthoringSettingsItemViewModel item)
        {
            if (item == null)
                return;

            if (item.Kind == ScenarioAuthoringSettingKind.Toggle)
            {
                rows.Add(RowModel.ActionRow(new ScenarioAuthoringInspectorAction
                {
                    Id = ScenarioAuthoringActionIds.ActionSettingTogglePrefix + item.Id,
                    Label = item.Label + ": " + (item.BoolValue ? "On" : "Off"),
                    Enabled = item.Enabled,
                    Emphasized = item.BoolValue,
                    Detail = item.Description
                }));
            }
            else if (item.Kind == ScenarioAuthoringSettingKind.Float || item.Kind == ScenarioAuthoringSettingKind.Integer)
            {
                rows.Add(RowModel.Text(item.Label, item.ValueText));
                rows.Add(RowModel.ActionRow(new ScenarioAuthoringInspectorAction { Id = ScenarioAuthoringActionIds.ActionSettingDecreasePrefix + item.Id, Label = item.Label + " -", Enabled = item.CanDecrease, Detail = item.Description }));
                rows.Add(RowModel.ActionRow(new ScenarioAuthoringInspectorAction { Id = ScenarioAuthoringActionIds.ActionSettingIncreasePrefix + item.Id, Label = item.Label + " +", Enabled = item.CanIncrease, Detail = item.Description }));
            }
            else if (item.Kind == ScenarioAuthoringSettingKind.Choice)
            {
                rows.Add(RowModel.Text(item.Label, item.ValueText));
                for (int i = 0; item.ChoiceValues != null && item.ChoiceLabels != null && i < item.ChoiceValues.Length && i < item.ChoiceLabels.Length; i++)
                {
                    rows.Add(RowModel.ActionRow(new ScenarioAuthoringInspectorAction
                    {
                        Id = ScenarioAuthoringActionIds.ActionSettingSelectPrefix + item.Id + "." + item.ChoiceValues[i],
                        Label = item.ChoiceLabels[i],
                        Enabled = item.Enabled,
                        Emphasized = i == item.SelectedChoiceIndex,
                        Detail = item.Description
                    }));
                }
            }
            else
            {
                rows.Add(RowModel.Text(item.Label, item.ValueText));
            }
        }

        private GameObject DrawPanel(string name, Rect rect, Color color, bool collider, int depth)
        {
            GameObject go = CreateObject(name, rect);
            UITexture texture = go.AddComponent<UITexture>();
            texture.mainTexture = UIUtil.WhiteTexture;
            texture.color = color;
            texture.width = Mathf.RoundToInt(rect.width);
            texture.height = Mathf.RoundToInt(rect.height);
            texture.pivot = UIWidget.Pivot.TopLeft;
            texture.depth = depth;
            if (collider)
                AddCollider(go, rect.width, rect.height);
            return go;
        }

        private void DrawButton(Rect rect, ScenarioAuthoringInspectorAction action, bool tab, string name)
        {
            bool enabled = action != null && action.Enabled;
            Color color = !enabled ? _buttonDisabledColor : action.Emphasized ? _buttonActiveColor : _buttonColor;
            GameObject buttonObject = DrawPanel(name + "Bg", rect, color, true, BaseDepth + 30);

            if (action != null && action.PreviewSprite != null)
                DrawPreviewSprite(name + "Preview", rect, new Rect(4f, 4f, 28f, rect.height - 8f), action.PreviewSprite, BaseDepth + 34);

            string label = action != null ? action.Label : string.Empty;
            string badge = action != null && !string.IsNullOrEmpty(action.Badge) ? action.Badge : null;
            float labelX = action != null && action.PreviewSprite != null ? 36f : 8f;
            if (!string.IsNullOrEmpty(badge))
            {
                DrawLabel(name + "Badge", rect, new Rect(6f, 4f, 32f, rect.height - 8f), badge, 11, _titleColor, NGUIText.Alignment.Center, BaseDepth + 35);
                labelX = 42f;
            }

            DrawLabel(name + "Label", rect, new Rect(labelX, 5f, rect.width - labelX - 8f, rect.height - 8f), Shorten(label, tab ? 18 : 28), tab ? 13 : 12, enabled ? _bodyColor : _mutedColor, NGUIText.Alignment.Center, BaseDepth + 35);

            if (enabled && action != null && !string.IsNullOrEmpty(action.Id))
            {
                BindClick(buttonObject, action);
            }

            RegisterRect(rect);
        }

        private void DrawLabel(string name, Rect parentRect, Rect localRect, string text, int size, Color color, NGUIText.Alignment alignment, int depth)
        {
            Rect rect = new Rect(parentRect.x + localRect.x, parentRect.y + localRect.y, localRect.width, localRect.height);
            GameObject go = CreateObject(name, rect);
            UILabel label = go.AddComponent<UILabel>();
            if (_fonts.Bitmap != null)
                label.bitmapFont = _fonts.Bitmap;
            else
                label.trueTypeFont = _fonts.TTF;
            label.fontSize = size;
            label.color = color;
            label.depth = depth;
            label.pivot = UIWidget.Pivot.TopLeft;
            label.alignment = alignment;
            label.overflowMethod = UILabel.Overflow.ShrinkContent;
            label.width = Mathf.RoundToInt(localRect.width);
            label.height = Mathf.RoundToInt(localRect.height);
            label.text = text ?? string.Empty;
        }

        private void DrawPreviewSprite(string name, Rect parentRect, Rect localRect, Sprite sprite, int depth)
        {
            if (sprite == null)
                return;

            Rect rect = new Rect(parentRect.x + localRect.x, parentRect.y + localRect.y, localRect.width, localRect.height);
            GameObject go = CreateObject(name, rect);
            UI2DSprite preview = go.AddComponent<UI2DSprite>();
            preview.sprite2D = sprite;
            preview.depth = depth;
            preview.width = Mathf.RoundToInt(localRect.width);
            preview.height = Mathf.RoundToInt(localRect.height);
            preview.pivot = UIWidget.Pivot.TopLeft;
        }

        private GameObject CreateObject(string name, Rect rect)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(_root.transform, false);
            go.layer = _root.layer;
            go.transform.localPosition = ToLocalTopLeft(rect);
            go.transform.localScale = Vector3.one;
            return go;
        }

        private GameObject LastChild()
        {
            if (_root == null || _root.transform.childCount == 0)
                return null;
            return _root.transform.GetChild(_root.transform.childCount - 1).gameObject;
        }

        private void BindClick(GameObject go, ScenarioAuthoringInspectorAction action)
        {
            if (go == null || action == null)
                return;

            string actionId = action.Id;
            UIEventListener listener = UIEventListener.Get(go);
            listener.onClick = delegate(GameObject sender)
            {
                if (string.Equals(actionId, ScenarioAuthoringActionIds.ActionShellToggleWindowMenu, StringComparison.Ordinal))
                {
                    _windowMenuOpen = !_windowMenuOpen;
                    ForceRebuild();
                    return;
                }

                if (!string.IsNullOrEmpty(actionId))
                    ScenarioAuthoringBackendService.Instance.ExecuteAction(actionId);
                _windowMenuOpen = false;
                ForceRebuild();
            };
        }

        private void AddCollider(GameObject go, float width, float height)
        {
            BoxCollider collider = go.AddComponent<BoxCollider>();
            collider.isTrigger = true;
            collider.center = new Vector3(width * 0.5f, -height * 0.5f, 0f);
            collider.size = new Vector3(width, height, 1f);
        }

        private Vector3 ToLocalTopLeft(Rect rect)
        {
            return new Vector3(rect.x - (_scaledWidth * 0.5f), (_scaledHeight * 0.5f) - rect.y, 0f);
        }

        private void ClearOverlay()
        {
            if (_root == null)
                return;

            List<GameObject> children = new List<GameObject>();
            for (int i = 0; i < _root.transform.childCount; i++)
                children.Add(_root.transform.GetChild(i).gameObject);

            for (int i = 0; i < children.Count; i++)
            {
                if (children[i] == null)
                    continue;
                children[i].SetActive(false);
                UnityEngine.Object.Destroy(children[i]);
            }
        }

        private void RegisterRect(Rect rect)
        {
            if (rect.width > 0f && rect.height > 0f)
                _interactiveRects.Add(rect);
        }

        private void RegisterInteractiveRects(ScenarioAuthoringInputCaptureService inputCapture)
        {
            if (inputCapture == null)
                return;

            for (int i = 0; i < _interactiveRects.Count; i++)
                inputCapture.RegisterInteractiveRect(_interactiveRects[i]);
        }

        private void ForceRebuild()
        {
            _lastSignature = null;
        }

        private string BuildSignature(ScenarioAuthoringPresentationSnapshot snapshot)
        {
            StringBuilder builder = new StringBuilder();
            ScenarioAuthoringState state = snapshot.State;
            ScenarioAuthoringShellViewModel shell = snapshot.ShellViewModel;
            builder.Append(state.IsActive).Append('|')
                .Append(_scaledWidth).Append('|')
                .Append(_scaledHeight).Append('|')
                .Append(state.ShellVisible).Append('|')
                .Append(state.ActiveStage).Append('|')
                .Append(state.ActiveTool).Append('|')
                .Append(state.ActiveSelectionStackIndex).Append('|')
                .Append(state.SelectionStackSignature).Append('|')
                .Append(state.StatusMessage).Append('|')
                .Append(_windowMenuOpen);

            AppendActions(builder, shell != null ? shell.Tabs : null);
            AppendActions(builder, shell != null ? shell.ToolbarActions : null);
            AppendActions(builder, shell != null ? shell.LayoutActions : null);
            AppendActions(builder, shell != null ? shell.WindowMenuActions : null);
            AppendWindows(builder, shell != null ? shell.Windows : null);
            AppendDocument(builder, shell != null ? shell.SpritePickerDocument : null);
            if (shell != null && shell.Settings != null)
                builder.Append("|settings");
            if (shell != null && shell.ContextMenu != null)
            {
                builder.Append("|ctx").Append(shell.ContextMenu.Visible).Append(shell.ContextMenu.Title).Append(shell.ContextMenu.Detail);
                AppendActions(builder, shell.ContextMenu.Actions);
            }
            return builder.ToString();
        }

        private static void AppendActions(StringBuilder builder, ScenarioAuthoringInspectorAction[] actions)
        {
            for (int i = 0; actions != null && i < actions.Length; i++)
            {
                ScenarioAuthoringInspectorAction action = actions[i];
                if (action != null)
                    builder.Append('|').Append(action.Id).Append(action.Label).Append(action.Enabled).Append(action.Emphasized).Append(action.Badge).Append(action.Detail).Append(action.Hint).Append(action.DisabledReason);
            }
        }

        private static void AppendWindows(StringBuilder builder, ScenarioAuthoringShellWindowViewModel[] windows)
        {
            for (int i = 0; windows != null && i < windows.Length; i++)
            {
                ScenarioAuthoringShellWindowViewModel window = windows[i];
                if (window == null)
                    continue;

                builder.Append("|win").Append(window.Id).Append(window.Visible).Append(window.Collapsed).Append(window.Title);
                AppendActions(builder, window.HeaderActions);
                AppendSections(builder, window.Sections);
            }
        }

        private static void AppendDocument(StringBuilder builder, ScenarioAuthoringInspectorDocument document)
        {
            if (document == null)
                return;

            builder.Append("|doc").Append(document.Title).Append(document.Subtitle);
            AppendActions(builder, document.HeaderActions);
            AppendSections(builder, document.Sections);
        }

        private static void AppendSections(StringBuilder builder, ScenarioAuthoringInspectorSection[] sections)
        {
            for (int i = 0; sections != null && i < sections.Length; i++)
            {
                ScenarioAuthoringInspectorSection section = sections[i];
                if (section == null)
                    continue;

                builder.Append("|sec").Append(section.Id).Append(section.Title);
                for (int j = 0; section.Items != null && j < section.Items.Length; j++)
                {
                    ScenarioAuthoringInspectorItem item = section.Items[j];
                    if (item != null)
                        builder.Append("|it").Append(item.Label).Append(item.Value).Append(item.Detail).Append(item.Emphasized)
                            .Append(item.Action != null ? item.Action.Id : null)
                            .Append(item.Action != null ? item.Action.Label : null);
                }
            }
        }

        private Vector2 GetPointerPosition()
        {
            Vector3 mouse = UnityEngine.Input.mousePosition;
            return new Vector2(mouse.x / _coordinateScale, (Screen.height - mouse.y) / _coordinateScale);
        }

        private static ScenarioAuthoringInspectorAction CommandAction(string id, string label, bool active)
        {
            return new ScenarioAuthoringInspectorAction
            {
                Id = id,
                Label = label,
                Enabled = true,
                Emphasized = active
            };
        }

        private static ScenarioAuthoringInspectorAction DisabledAction(string label)
        {
            return new ScenarioAuthoringInspectorAction
            {
                Label = label,
                Enabled = false,
                Detail = label + " is not available for the current target."
            };
        }

        private static ScenarioAuthoringInspectorAction CloneAction(ScenarioAuthoringInspectorAction action, string label)
        {
            if (action == null)
                return null;

            return new ScenarioAuthoringInspectorAction
            {
                Id = action.Id,
                Label = label,
                Hint = action.Hint,
                Detail = action.Detail,
                Badge = action.Badge,
                IconText = action.IconText,
                PreviewSprite = action.PreviewSprite,
                Enabled = action.Enabled,
                Emphasized = action.Emphasized
            };
        }

        private static ScenarioAuthoringInspectorAction CloneEmphasized(ScenarioAuthoringInspectorAction action)
        {
            ScenarioAuthoringInspectorAction clone = CloneAction(action, action != null ? action.Label : null);
            if (clone != null)
                clone.Emphasized = true;
            return clone;
        }

        private static bool IsWindowMenuAction(ScenarioAuthoringInspectorAction action)
        {
            return action != null
                && string.Equals(action.Id, ScenarioAuthoringActionIds.ActionShellToggleWindowMenu, StringComparison.Ordinal);
        }

        private static bool IsChildStageTab(ScenarioAuthoringInspectorAction action)
        {
            return action != null && !string.IsNullOrEmpty(action.Label) && action.Label.StartsWith("- ", StringComparison.Ordinal);
        }

        private static string CleanChildStageLabel(string label)
        {
            return !string.IsNullOrEmpty(label) && label.StartsWith("- ", StringComparison.Ordinal) ? label.Substring(2) : label;
        }

        private static int Count(Array array)
        {
            return array != null ? array.Length : 0;
        }

        private static float ClampTextWidth(string text, float min, float max)
        {
            return Mathf.Clamp((text != null ? text.Length : 0) * 7.4f + 28f, min, max);
        }

        private static string Shorten(string text, int max)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= max)
                return text ?? string.Empty;
            return text.Substring(0, Math.Max(0, max - 3)) + "...";
        }

        private sealed class RowModel
        {
            public bool IsHeader;
            public string Label;
            public string Value;
            public bool Emphasized;
            public ScenarioAuthoringInspectorAction Action;

            public static RowModel Header(string label)
            {
                return new RowModel { IsHeader = true, Label = label };
            }

            public static RowModel Text(string value)
            {
                return new RowModel { Value = value };
            }

            public static RowModel Text(string label, string value)
            {
                return new RowModel { Label = label, Value = value };
            }

            public static RowModel ActionRow(ScenarioAuthoringInspectorAction action)
            {
                return new RowModel { Action = action, Emphasized = action != null && action.Emphasized };
            }

            public static RowModel FromItem(ScenarioAuthoringInspectorItem item)
            {
                if (item.Action != null)
                    return ActionRow(item.Action);

                if (item.Kind == ScenarioAuthoringInspectorItemKind.Property)
                    return Text(item.Label, item.Value);

                string value = !string.IsNullOrEmpty(item.Value) ? item.Value : item.Label;
                if (!string.IsNullOrEmpty(item.Detail))
                    value += " / " + item.Detail;
                RowModel row = Text(value);
                row.Emphasized = item.Emphasized;
                return row;
            }
        }
    }
}
