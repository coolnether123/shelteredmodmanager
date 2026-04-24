using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShelteredAPI.Scenarios
{
    internal sealed class ScenarioAuthoringShellImguiRenderModule : IScenarioAuthoringRenderModule
    {
        private const string RuntimeObjectName = "ShelteredAPI.ScenarioAuthoring.ShellImgui";
        private const float Margin = 16f;
        private const float Gutter = 12f;
        private const float TopBarHeight = 108f;
        private const float StatusHeight = 46f;
        private const float ToolRailWidth = 74f;
        private const float InspectorWidth = 292f;
        private const float BottomTrayHeight = 272f;
        private const float CommandDockHeight = 48f;

        private ScenarioAuthoringShellRuntime _runtime;
        private ScenarioAuthoringPresentationSnapshot _snapshot;
        private bool _visible;
        private GUIStyle _rootPanelStyle;
        private GUIStyle _headerStyle;
        private GUIStyle _titleStyle;
        private GUIStyle _smallTitleStyle;
        private GUIStyle _textStyle;
        private GUIStyle _mutedTextStyle;
        private GUIStyle _buttonStyle;
        private GUIStyle _activeButtonStyle;
        private GUIStyle _tabStyle;
        private GUIStyle _activeTabStyle;
        private GUIStyle _sectionStyle;
        private GUIStyle _sectionTitleStyle;
        private GUIStyle _statusStyle;
        private GUIStyle _fieldStyle;
        private GUIStyle _menuStyle;
        private Texture2D _panelTexture;
        private Texture2D _panelAltTexture;
        private Texture2D _lineTexture;
        private Texture2D _activeTexture;
        private Texture2D _dangerTexture;
        private Texture2D _viewportTexture;
        private float _styleOpacity = -1f;
        private bool _windowMenuOpen;
        private readonly Dictionary<string, Vector2> _windowScrollPositions = new Dictionary<string, Vector2>(StringComparer.OrdinalIgnoreCase);
        private Vector2 _settingsScrollPosition = Vector2.zero;
        private readonly ScenarioSpriteSwapAuthoringService _spriteSwapAuthoringService;

        internal ScenarioAuthoringShellImguiRenderModule(ScenarioSpriteSwapAuthoringService spriteSwapAuthoringService)
        {
            _spriteSwapAuthoringService = spriteSwapAuthoringService;
        }

        public string ModuleId
        {
            get { return "ShelteredAPI.ShellIMGUI"; }
        }

        public int Priority
        {
            get { return 200; }
        }

        public bool CanRender()
        {
            EnsureRuntime();
            return _runtime != null;
        }

        public void Render(ScenarioAuthoringPresentationSnapshot snapshot)
        {
            EnsureRuntime();
            _snapshot = snapshot;
            _visible = snapshot != null
                && snapshot.State != null
                && snapshot.State.IsActive
                && snapshot.State.ShellVisible
                && snapshot.ShellViewModel != null;

            if (_runtime != null)
                _runtime.enabled = _visible;
        }

        public void Hide()
        {
            _snapshot = null;
            _visible = false;
            _windowMenuOpen = false;
            if (_runtime != null)
                _runtime.enabled = false;
        }

        private void EnsureRuntime()
        {
            if (_runtime != null)
                return;

            GameObject runtimeObject = GameObject.Find(RuntimeObjectName);
            if (runtimeObject == null)
            {
                runtimeObject = new GameObject(RuntimeObjectName);
                UnityEngine.Object.DontDestroyOnLoad(runtimeObject);
            }

            _runtime = runtimeObject.GetComponent<ScenarioAuthoringShellRuntime>();
            if (_runtime == null)
                _runtime = runtimeObject.AddComponent<ScenarioAuthoringShellRuntime>();
            _runtime.Initialize(this);
        }

        private void Draw()
        {
            if (!_visible || _snapshot == null || _snapshot.ShellViewModel == null)
                return;

            ScenarioAuthoringShellViewModel shell = _snapshot.ShellViewModel;
            float uiScale = _snapshot.State != null && _snapshot.State.Settings != null
                ? _snapshot.State.Settings.GetFloat("shell.ui_scale", 1f)
                : 1f;
            ScenarioAuthoringInputCaptureService inputCapture = ScenarioCompositionRoot.Resolve<ScenarioAuthoringInputCaptureService>();
            inputCapture.BeginFrame(uiScale);
            float panelOpacity = _snapshot.State != null && _snapshot.State.Settings != null
                ? Mathf.Clamp(_snapshot.State.Settings.GetFloat("shell.panel_opacity", 0.82f), 0.55f, 1f)
                : 0.82f;
            EnsureStyles(panelOpacity);

            Matrix4x4 oldMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(uiScale, uiScale, 1f));

            float scaledWidth = Screen.width / uiScale;
            float scaledHeight = Screen.height / uiScale;
            Rect topRect = new Rect(0f, 0f, scaledWidth, TopBarHeight);
            Rect statusRect = new Rect(0f, scaledHeight - StatusHeight, scaledWidth, StatusHeight);
            Rect windowMenuButtonRect = DrawTopBar(topRect, shell);
            DrawStatusBar(statusRect, shell);
            inputCapture.RegisterInteractiveRect(topRect);
            inputCapture.RegisterInteractiveRect(statusRect);

            Rect contentRect = new Rect(
                Margin,
                topRect.yMax + Gutter,
                scaledWidth - (Margin * 2f),
                statusRect.y - (topRect.yMax + Gutter));

            Dictionary<string, Rect> windowRects = ResolveWindowRects(contentRect, shell.Windows);

            Rect toolRailRect = DrawToolRail(contentRect, _snapshot.State);
            if (toolRailRect.width > 0f && toolRailRect.height > 0f)
                inputCapture.RegisterInteractiveRect(toolRailRect);

            Rect commandDockRect = DrawCommandDock(contentRect, _snapshot.State);
            if (commandDockRect.width > 0f && commandDockRect.height > 0f)
                inputCapture.RegisterInteractiveRect(commandDockRect);

            string activeWorkspaceId = GetActiveWorkspaceId(shell.Windows);
            Rect workspaceTabStripRect = Rect.zero;
            Rect workspaceRect;
            if (activeWorkspaceId != null && windowRects.TryGetValue(activeWorkspaceId, out workspaceRect))
            {
                workspaceTabStripRect = new Rect(workspaceRect.x, workspaceRect.y - 42f, workspaceRect.width, 36f);
            }

            for (int i = 0; shell.Windows != null && i < shell.Windows.Length; i++)
            {
                ScenarioAuthoringShellWindowViewModel window = shell.Windows[i];
                Rect rect;
                if (window == null || !window.Visible || !windowRects.TryGetValue(window.Id, out rect))
                    continue;

                Rect scrollRect = DrawWindow(rect, window);
                inputCapture.RegisterInteractiveRect(rect);
                if (scrollRect.width > 0f && scrollRect.height > 0f)
                    inputCapture.RegisterScrollRect(window.Id, scrollRect);
            }

            if (activeWorkspaceId != null && workspaceTabStripRect.width > 0f)
            {
                DrawWorkspaceTabs(workspaceTabStripRect, activeWorkspaceId, shell.Windows);
                inputCapture.RegisterInteractiveRect(workspaceTabStripRect);
            }

            Rect windowMenuRect = Rect.zero;
            if (_windowMenuOpen && shell.WindowMenuActions != null && shell.WindowMenuActions.Length > 0)
            {
                windowMenuRect = BuildWindowMenuRect(windowMenuButtonRect, shell.WindowMenuActions, scaledWidth, scaledHeight);
                DrawWindowMenu(windowMenuRect, shell.WindowMenuActions);
                inputCapture.RegisterInteractiveRect(windowMenuRect);
                inputCapture.SetPopupOpen(true);
            }

            Rect popupRect = Rect.zero;
            if (shell.ContextMenu != null && shell.ContextMenu.Visible)
            {
                popupRect = BuildPopupRect(shell.ContextMenu, scaledWidth, scaledHeight);
                DrawContextMenu(popupRect, shell.ContextMenu);
                inputCapture.RegisterInteractiveRect(popupRect);
                inputCapture.SetPopupOpen(true);
                if (Event.current != null
                    && Event.current.type == EventType.MouseDown
                    && !popupRect.Contains(Event.current.mousePosition))
                {
                    ScenarioCompositionRoot.Resolve<ScenarioAuthoringContextMenuService>().Close();
                    Event.current.Use();
                }
            }

            if (_windowMenuOpen
                && Event.current != null
                && Event.current.type == EventType.MouseDown
                && !windowMenuRect.Contains(Event.current.mousePosition)
                && !windowMenuButtonRect.Contains(Event.current.mousePosition))
            {
                _windowMenuOpen = false;
                Event.current.Use();
            }

            if (shell.SpritePickerDocument != null)
            {
                Rect pickerRect = new Rect(
                    Math.Max(Margin, (scaledWidth - 980f) * 0.5f),
                    Math.Max(topRect.yMax + Gutter, (scaledHeight - 680f) * 0.5f),
                    Math.Min(980f, scaledWidth - (Margin * 2f)),
                    Math.Min(680f, scaledHeight - topRect.height - StatusHeight - (Margin * 3f)));
                Rect pickerScrollRect = DrawDocumentModal(pickerRect, shell.SpritePickerDocument, "sprite_picker");
                inputCapture.RegisterInteractiveRect(pickerRect);
                if (pickerScrollRect.width > 0f && pickerScrollRect.height > 0f)
                    inputCapture.RegisterScrollRect("sprite_picker", pickerScrollRect);
                inputCapture.SetPopupOpen(true);
            }

            if (shell.Settings != null)
            {
                Rect settingsRect = new Rect(
                    Math.Max(Margin, (scaledWidth - 720f) * 0.5f),
                    Math.Max(topRect.yMax + Gutter, (scaledHeight - 520f) * 0.5f),
                    Math.Min(720f, scaledWidth - (Margin * 2f)),
                    Math.Min(520f, scaledHeight - topRect.height - StatusHeight - (Margin * 3f)));
                Rect settingsScrollRect = DrawSettingsWindow(settingsRect, shell.Settings);
                inputCapture.RegisterInteractiveRect(settingsRect);
                if (settingsScrollRect.width > 0f && settingsScrollRect.height > 0f)
                    inputCapture.RegisterScrollRect("settings", settingsScrollRect);
                inputCapture.SetPopupOpen(true);
            }

            inputCapture.SetKeyboardCaptured(
                shell.Settings != null
                || shell.SpritePickerDocument != null
                || (shell.ContextMenu != null && shell.ContextMenu.Visible));

            DrawTooltipOverlay(scaledWidth, scaledHeight);

            inputCapture.CompleteFrame();
            GUI.matrix = oldMatrix;
        }

        private Dictionary<string, Rect> ResolveWindowRects(Rect contentRect, ScenarioAuthoringShellWindowViewModel[] windows)
        {
            Dictionary<string, Rect> rects = new Dictionary<string, Rect>(StringComparer.OrdinalIgnoreCase);
            float viewportLeft = contentRect.x + ToolRailWidth + Gutter;
            float viewportRight = contentRect.xMax - InspectorWidth - Gutter;
            float viewportBottom = contentRect.yMax;

            bool showBottomTray = _snapshot != null
                && _snapshot.State != null
                && (( _snapshot.State.ActiveTool == ScenarioAuthoringTool.Assets
                    && HasVisibleWindow(windows, ScenarioAuthoringWindowIds.BuildTools))
                    || HasVisibleWindow(windows, ScenarioAuthoringWindowIds.Calendar));

            if (showBottomTray)
                viewportBottom -= BottomTrayHeight + Gutter;

            AppendStackRect(
                rects,
                windows,
                ScenarioAuthoringWindowIds.Inspector,
                new Rect(contentRect.xMax - InspectorWidth, contentRect.y + 20f, InspectorWidth, Mathf.Min(520f, contentRect.height - 44f)));

            Rect buildToolsRect = new Rect(
                viewportLeft,
                Math.Max(contentRect.y + 220f, contentRect.yMax - BottomTrayHeight),
                Math.Min(940f, Math.Max(520f, viewportRight - viewportLeft)),
                BottomTrayHeight);
            if (showBottomTray)
                AppendStackRect(rects, windows, ScenarioAuthoringWindowIds.BuildTools, buildToolsRect);
            AppendStackRect(rects, windows, ScenarioAuthoringWindowIds.Calendar, buildToolsRect);

            float workspaceWidth = Mathf.Clamp(contentRect.width * 0.58f, 640f, 980f);
            float workspaceHeight = Mathf.Clamp(contentRect.height * 0.72f, 400f, 620f);
            float workspaceX = contentRect.x + ((contentRect.width - workspaceWidth) * 0.5f);
            float workspaceY = contentRect.y + ((contentRect.height - workspaceHeight) * 0.5f);
            Rect workspaceRect = new Rect(workspaceX, workspaceY, workspaceWidth, workspaceHeight);
            AppendStackRect(rects, windows, ScenarioAuthoringWindowIds.Triggers, workspaceRect);
            AppendStackRect(rects, windows, ScenarioAuthoringWindowIds.Survivors, workspaceRect);
            AppendStackRect(rects, windows, ScenarioAuthoringWindowIds.Stockpile, workspaceRect);
            AppendStackRect(rects, windows, ScenarioAuthoringWindowIds.Quests, workspaceRect);
            AppendStackRect(rects, windows, ScenarioAuthoringWindowIds.Map, workspaceRect);
            AppendStackRect(rects, windows, ScenarioAuthoringWindowIds.Publish, workspaceRect);
            return rects;
        }

        private Rect DrawTopBar(Rect rect, ScenarioAuthoringShellViewModel shell)
        {
            GUI.Box(rect, GUIContent.none, _rootPanelStyle);
            Rect windowMenuButtonRect = Rect.zero;
            const float primaryRowY = 20f;
            const float primaryRowHeight = 40f;
            const float secondaryRowY = 68f;
            const float secondaryRowHeight = 34f;

            Rect brandRect = new Rect(rect.x + 22f, rect.y + 17f, 196f, rect.height - 20f);
            GUI.Label(new Rect(brandRect.x, brandRect.y - 1f, brandRect.width, 34f), "SHELTERED", _titleStyle);
            GUI.Label(new Rect(brandRect.x, brandRect.y + 31f, brandRect.width, 24f), "SCENARIO EDITOR", _smallTitleStyle);

            float primaryRowLeft = brandRect.xMax + 20f;
            float tabX = primaryRowLeft;
            for (int i = 0; shell.Tabs != null && i < shell.Tabs.Length; i++)
            {
                ScenarioAuthoringInspectorAction tab = shell.Tabs[i];
                if (IsChildStageTab(tab))
                    continue;

                float tabWidth = Mathf.Clamp(MeasureButtonWidth(tab, true, 30f), 92f, 170f);
                Rect tabRect = new Rect(tabX, rect.y + primaryRowY, tabWidth, primaryRowHeight);
                DrawButton(tabRect, tab, true);
                tabX = tabRect.xMax + 2f;
            }

            float chipX = Mathf.Min(tabX + 12f, rect.xMax - 680f);
            Rect modeChipRect = new Rect(Mathf.Max(primaryRowLeft, chipX), rect.y + 18f, 248f, 54f);
            DrawModeChip(modeChipRect, shell);

            Rect childTabsRect = new Rect(
                primaryRowLeft,
                rect.y + secondaryRowY,
                Math.Max(80f, modeChipRect.x - primaryRowLeft - 14f),
                secondaryRowHeight);
            float childX = childTabsRect.x;
            for (int i = 0; shell.Tabs != null && i < shell.Tabs.Length; i++)
            {
                ScenarioAuthoringInspectorAction tab = shell.Tabs[i];
                if (!IsChildStageTab(tab))
                    continue;

                ScenarioAuthoringInspectorAction displayTab = CloneWithLabel(tab, CleanChildStageLabel(tab.Label));
                float width = Mathf.Clamp(MeasureButtonWidth(displayTab, true, 26f), 94f, 122f);
                Rect tabRect = new Rect(childX, childTabsRect.y, width, childTabsRect.height);
                if (tabRect.xMax > childTabsRect.xMax)
                    break;
                DrawButton(tabRect, displayTab, true);
                childX = tabRect.xMax + 2f;
            }

            windowMenuButtonRect = DrawTopBarQuickActions(
                new Rect(modeChipRect.xMax + 12f, rect.y + 74f, Math.Max(390f, rect.xMax - modeChipRect.xMax - 300f), 30f),
                shell);

            return windowMenuButtonRect;
        }

        private Rect DrawTopBarQuickActions(Rect rect, ScenarioAuthoringShellViewModel shell)
        {
            Rect menuButtonRect = Rect.zero;
            float x = rect.x;
            for (int i = 0; shell.ToolbarActions != null && i < shell.ToolbarActions.Length; i++)
            {
                ScenarioAuthoringInspectorAction action = shell.ToolbarActions[i];
                if (action == null)
                    continue;

                float width = Mathf.Clamp(MeasureButtonWidth(action, false, 24f), 96f, 126f);
                Rect actionRect = new Rect(x, rect.y, width, rect.height);
                if (actionRect.xMax > rect.xMax)
                    break;
                DrawButton(actionRect, action, false);
                x = actionRect.xMax + 4f;
            }

            for (int i = 0; shell.LayoutActions != null && i < shell.LayoutActions.Length; i++)
            {
                ScenarioAuthoringInspectorAction action = shell.LayoutActions[i];
                if (!IsWindowMenuAction(action))
                    continue;

                ScenarioAuthoringInspectorAction displayAction = _windowMenuOpen ? CloneEmphasized(action) : action;
                Rect actionRect = new Rect(Mathf.Min(x, rect.xMax - 106f), rect.y, 106f, rect.height);
                DrawButton(actionRect, displayAction, false);
                menuButtonRect = actionRect;
                break;
            }

            return menuButtonRect;
        }

        private void DrawModeChip(Rect rect, ScenarioAuthoringShellViewModel shell)
        {
            GUI.Box(rect, GUIContent.none, _sectionStyle);
            string mode = string.IsNullOrEmpty(shell.ModeLabel) ? "Editing Draft" : shell.ModeLabel;
            string draft = string.IsNullOrEmpty(shell.DraftLabel) ? "Untitled" : shell.DraftLabel;

            GUI.Label(new Rect(rect.x + 12f, rect.y + 5f, rect.width - 24f, 20f), mode, _sectionTitleStyle);
            GUI.Label(new Rect(rect.x + 12f, rect.y + 25f, rect.width - 24f, 18f), draft, _mutedTextStyle);
        }

        private Rect DrawToolRail(Rect contentRect, ScenarioAuthoringState state)
        {
            Rect rect = new Rect(contentRect.x + 4f, contentRect.y + 26f, ToolRailWidth, 500f);
            GUI.Box(rect, GUIContent.none, _rootPanelStyle);

            float y = rect.y + 10f;
            DrawToolRailButton(new Rect(rect.x + 8f, y, rect.width - 16f, 72f), state, ScenarioAuthoringTool.Select, ScenarioAuthoringActionIds.ActionToolSelect, ">", "Select");
            y += 78f;
            DrawToolRailButton(new Rect(rect.x + 8f, y, rect.width - 16f, 72f), state, ScenarioAuthoringTool.Objects, ScenarioAuthoringActionIds.ActionToolObjects, "[]", "Objects");
            y += 78f;
            DrawToolRailButton(new Rect(rect.x + 8f, y, rect.width - 16f, 72f), state, ScenarioAuthoringTool.Shelter, ScenarioAuthoringActionIds.ActionToolShelter, "##", "Structure");
            y += 78f;
            DrawToolRailButton(new Rect(rect.x + 8f, y, rect.width - 16f, 82f), state, ScenarioAuthoringTool.Wiring, ScenarioAuthoringActionIds.ActionToolWiring, "/\\", "Walls &\nWiring");
            y += 88f;
            DrawToolRailButton(new Rect(rect.x + 8f, y, rect.width - 16f, 72f), state, ScenarioAuthoringTool.Assets, ScenarioAuthoringActionIds.ActionToolAssets, "P", "Assets");
            return rect;
        }

        private void DrawToolRailButton(Rect rect, ScenarioAuthoringState state, ScenarioAuthoringTool tool, string actionId, string icon, string label)
        {
            bool active = state != null && state.ActiveTool == tool;
            GUIStyle style = active ? _activeButtonStyle : _buttonStyle;
            if (GUI.Button(rect, GUIContent.none, style))
            {
                ScenarioAuthoringBackendService.Instance.ExecuteAction(actionId);
                if (Event.current != null)
                    Event.current.Use();
            }

            GUI.Label(new Rect(rect.x, rect.y + 11f, rect.width, 22f), icon, _sectionTitleStyle);
            GUI.Label(new Rect(rect.x + 2f, rect.y + 42f, rect.width - 4f, rect.height - 44f), label, _mutedTextStyle);
        }

        private Rect DrawCommandDock(Rect contentRect, ScenarioAuthoringState state)
        {
            if (state != null && state.ActiveTool == ScenarioAuthoringTool.Assets)
                return Rect.zero;

            float width = 430f;
            Rect rect = new Rect(
                contentRect.x + ((contentRect.width - width) * 0.5f),
                contentRect.yMax - CommandDockHeight - 22f,
                width,
                CommandDockHeight);
            GUI.Box(rect, GUIContent.none, _rootPanelStyle);
            float x = rect.x + 10f;
            DrawButton(new Rect(x, rect.y + 8f, 102f, 32f), new ScenarioAuthoringInspectorAction
            {
                Id = ScenarioAuthoringActionIds.ActionToolSelect,
                Label = "Select",
                Hint = "Switch to selection mode.",
                Enabled = true,
                Emphasized = state != null && state.ActiveTool == ScenarioAuthoringTool.Select
            }, false);
            x += 116f;
            DrawButton(new Rect(x, rect.y + 8f, 86f, 32f), DisabledAction("Move"), false);
            x += 96f;
            DrawButton(new Rect(x, rect.y + 8f, 90f, 32f), DisabledAction("Rotate"), false);
            x += 100f;
            DrawButton(new Rect(x, rect.y + 8f, 86f, 32f), BuildDeleteAction(state), false);
            return rect;
        }

        private static ScenarioAuthoringInspectorAction DisabledAction(string label)
        {
            return new ScenarioAuthoringInspectorAction
            {
                Label = label,
                Hint = label + " is not available for the current target.",
                Enabled = false
            };
        }

        private static ScenarioAuthoringInspectorAction BuildDeleteAction(ScenarioAuthoringState state)
        {
            ScenarioAuthoringTarget target = state != null ? state.SelectedTarget : null;
            bool canRemovePlacement = target != null && !string.IsNullOrEmpty(target.ScenarioReferenceId);
            return new ScenarioAuthoringInspectorAction
            {
                Id = canRemovePlacement
                    ? ScenarioAuthoringActionIds.ActionSceneSpritePlacementRemove
                    : ScenarioAuthoringActionIds.ActionSelectionClear,
                Label = canRemovePlacement ? "Delete" : "Clear",
                Hint = canRemovePlacement ? "Remove this authored placement." : "Clear the current selection.",
                Enabled = target != null,
                Emphasized = canRemovePlacement
            };
        }

        private static ScenarioAuthoringInspectorAction CloneEmphasized(ScenarioAuthoringInspectorAction action)
        {
            return new ScenarioAuthoringInspectorAction
            {
                Id = action.Id,
                Label = action.Label,
                Hint = action.Hint,
                Detail = action.Detail,
                Badge = action.Badge,
                IconText = action.IconText,
                PreviewSprite = action.PreviewSprite,
                Enabled = action.Enabled,
                Emphasized = true
            };
        }

        private static ScenarioAuthoringInspectorAction CloneWithLabel(ScenarioAuthoringInspectorAction action, string label)
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

        private static bool IsChildStageTab(ScenarioAuthoringInspectorAction action)
        {
            return action != null
                && !string.IsNullOrEmpty(action.Label)
                && action.Label.StartsWith("- ", StringComparison.Ordinal);
        }

        private static string CleanChildStageLabel(string label)
        {
            if (string.IsNullOrEmpty(label))
                return string.Empty;

            return label.StartsWith("- ", StringComparison.Ordinal) ? label.Substring(2) : label;
        }

        private void DrawStatusBar(Rect rect, ScenarioAuthoringShellViewModel shell)
        {
            GUI.Box(rect, GUIContent.none, _statusStyle);
            float x = rect.x + 26f;
            for (int i = 0; shell.StatusEntries != null && i < shell.StatusEntries.Length; i++)
            {
                string value = shell.StatusEntries[i] ?? string.Empty;
                float width = Math.Min(250f, value.Length * 7.5f + 30f);
                GUI.Label(new Rect(x, rect.y + 14f, width, 20f), value, _mutedTextStyle);
                x += width + 18f;
            }

            bool isPlaytesting = ScenarioAuthoringRuntimeGuards.IsPlaytesting();
            Rect playtestRect = new Rect(rect.xMax - 408f, rect.y + 9f, 120f, 28f);
            DrawButton(playtestRect, new ScenarioAuthoringInspectorAction
            {
                Id = ScenarioAuthoringActionIds.ActionPlaytest,
                Label = isPlaytesting ? "End Test" : "Playtest",
                Hint = isPlaytesting ? "Stop playtest and return to frozen authoring." : "Apply the current draft into the live world.",
                Enabled = true,
                Emphasized = isPlaytesting
            }, false);

            GUI.Label(new Rect(rect.xMax - 252f, rect.y + 14f, 18f, 18f), "-", _mutedTextStyle);
            GUI.Box(new Rect(rect.xMax - 224f, rect.y + 20f, 112f, 4f), GUIContent.none, _fieldStyle);
            GUI.Label(new Rect(rect.xMax - 98f, rect.y + 14f, 48f, 18f), "100%", _textStyle);
        }

        private Rect DrawWindow(Rect rect, ScenarioAuthoringShellWindowViewModel window)
        {
            if (window != null && string.Equals(window.Id, ScenarioAuthoringWindowIds.Inspector, StringComparison.OrdinalIgnoreCase))
                return DrawInspectorWindow(rect, window);

            if (window != null && string.Equals(window.Id, ScenarioAuthoringWindowIds.BuildTools, StringComparison.OrdinalIgnoreCase))
                return DrawBottomTrayWindow(rect, window);

            GUI.Box(rect, GUIContent.none, _rootPanelStyle);
            ScenarioAuthoringInspectorAction[] chromeActions = GetHeaderActions(window.HeaderActions, true);
            ScenarioAuthoringInspectorAction[] secondaryActions = GetHeaderActions(window.HeaderActions, false);
            bool hasSecondaryActions = secondaryActions.Length > 0;
            Rect headerRect = new Rect(rect.x + 4f, rect.y + 4f, rect.width - 8f, hasSecondaryActions ? 58f : 30f);
            GUI.Box(headerRect, GUIContent.none, _headerStyle);
            Rect titleRowRect = new Rect(headerRect.x, headerRect.y, headerRect.width, 30f);

            float actionX = titleRowRect.xMax - 28f;
            for (int i = chromeActions.Length - 1; i >= 0; i--)
            {
                ScenarioAuthoringInspectorAction action = chromeActions[i];
                Rect actionRect = new Rect(actionX, headerRect.y + 3f, 22f, 22f);
                DrawButton(actionRect, action, false);
                actionX -= 24f;
            }

            float titleWidth = Math.Max(80f, actionX - (titleRowRect.x + 8f));
            GUI.Label(new Rect(titleRowRect.x + 8f, titleRowRect.y + 5f, titleWidth, 20f), (window.Title ?? string.Empty).ToUpperInvariant(), _sectionTitleStyle);

            if (hasSecondaryActions)
            {
                Rect tabsRect = new Rect(headerRect.x + 6f, titleRowRect.yMax + 2f, headerRect.width - 12f, 22f);
                float tabX = tabsRect.x;
                for (int i = 0; i < secondaryActions.Length; i++)
                {
                    ScenarioAuthoringInspectorAction action = secondaryActions[i];
                    float width = Mathf.Clamp(MeasureButtonWidth(action, true, 18f), 42f, 74f);
                    Rect actionRect = new Rect(tabX, tabsRect.y, width, tabsRect.height);
                    DrawButton(actionRect, action, true);
                    tabX = actionRect.xMax + 4f;
                    if (tabX >= tabsRect.xMax)
                        break;
                }
            }

            if (window.Collapsed)
                return Rect.zero;

            Rect bodyRect = new Rect(rect.x + 10f, headerRect.yMax + 8f, rect.width - 20f, rect.height - headerRect.height - 18f);
            GUILayout.BeginArea(bodyRect);
            Vector2 scrollPosition = GetWindowScrollPosition(window.Id);
            scrollPosition = GUILayout.BeginScrollView(scrollPosition, false, false, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            for (int i = 0; window.Sections != null && i < window.Sections.Length; i++)
            {
                DrawSection(window.Sections[i]);
                if (i < window.Sections.Length - 1)
                    GUILayout.Space(6f);
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
            SetWindowScrollPosition(window.Id, scrollPosition);
            return bodyRect;
        }

        private Rect DrawInspectorWindow(Rect rect, ScenarioAuthoringShellWindowViewModel window)
        {
            GUI.Box(rect, GUIContent.none, _rootPanelStyle);
            Rect headerRect = new Rect(rect.x + 12f, rect.y + 12f, rect.width - 24f, 34f);
            GUI.Label(new Rect(headerRect.x, headerRect.y + 4f, headerRect.width - 26f, 22f), "INSPECTOR", _sectionTitleStyle);

            ScenarioAuthoringInspectorAction[] chromeActions = GetHeaderActions(window.HeaderActions, true);
            if (chromeActions.Length > 0)
                DrawButton(new Rect(headerRect.xMax - 24f, headerRect.y + 4f, 22f, 22f), chromeActions[0], false);

            Rect bodyRect = new Rect(rect.x + 14f, headerRect.yMax + 10f, rect.width - 28f, rect.height - 62f);
            GUILayout.BeginArea(bodyRect);
            Vector2 scrollPosition = GetWindowScrollPosition(window.Id);
            scrollPosition = GUILayout.BeginScrollView(scrollPosition, false, false, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            for (int i = 0; window.Sections != null && i < window.Sections.Length; i++)
            {
                DrawSection(window.Sections[i]);
                if (i < window.Sections.Length - 1)
                    GUILayout.Space(6f);
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
            SetWindowScrollPosition(window.Id, scrollPosition);
            return bodyRect;
        }

        private Rect DrawBottomTrayWindow(Rect rect, ScenarioAuthoringShellWindowViewModel window)
        {
            if (_snapshot != null && _snapshot.State != null && _snapshot.State.ActiveTool != ScenarioAuthoringTool.Assets)
                return Rect.zero;

            GUI.Box(rect, GUIContent.none, _rootPanelStyle);
            Rect headerRect = new Rect(rect.x + 14f, rect.y + 12f, rect.width - 28f, 28f);
            GUI.Label(headerRect, "ASSET PICKER", _sectionTitleStyle);

            Rect bodyRect = new Rect(rect.x + 14f, headerRect.yMax + 8f, rect.width - 28f, rect.height - 54f);
            Rect pickerRect = new Rect(bodyRect.x, bodyRect.y, Mathf.Max(420f, bodyRect.width * 0.62f), bodyRect.height);
            Rect detailsRect = new Rect(pickerRect.xMax + 16f, bodyRect.y, bodyRect.xMax - pickerRect.xMax - 16f, bodyRect.height);

            GUI.Box(new Rect(pickerRect.x, pickerRect.y, pickerRect.width, 28f), "  Search assets...", _fieldStyle);

            GUILayout.BeginArea(new Rect(pickerRect.x, pickerRect.y + 38f, pickerRect.width, pickerRect.height - 38f));
            Vector2 scrollPosition = GetWindowScrollPosition(window.Id);
            scrollPosition = GUILayout.BeginScrollView(scrollPosition, false, false, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            bool drewCandidateGrid = false;
            for (int i = 0; window.Sections != null && i < window.Sections.Length; i++)
            {
                ScenarioAuthoringInspectorSection section = window.Sections[i];
                if (section == null
                    || string.Equals(section.Id, "tools", StringComparison.OrdinalIgnoreCase)
                    || section.Layout != ScenarioAuthoringInspectorSectionLayout.CandidateGrid)
                    continue;

                DrawSection(section);
                GUILayout.Space(6f);
                drewCandidateGrid = true;
            }
            if (!drewCandidateGrid)
                GUILayout.Label("Switch to Place New Snapped to browse sprite assets here, or select a replaceable target and open the sprite picker.", _mutedTextStyle);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
            SetWindowScrollPosition(window.Id, scrollPosition);

            GUILayout.BeginArea(detailsRect);
            Vector2 detailsScroll = GetWindowScrollPosition(window.Id + ".details");
            detailsScroll = GUILayout.BeginScrollView(detailsScroll, false, false, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            for (int i = 0; window.Sections != null && i < window.Sections.Length; i++)
            {
                ScenarioAuthoringInspectorSection section = window.Sections[i];
                if (section == null
                    || string.Equals(section.Id, "tools", StringComparison.OrdinalIgnoreCase)
                    || section.Layout == ScenarioAuthoringInspectorSectionLayout.CandidateGrid)
                    continue;

                DrawSection(section);
                GUILayout.Space(6f);
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
            SetWindowScrollPosition(window.Id + ".details", detailsScroll);
            return bodyRect;
        }

        private Rect DrawDocumentModal(Rect rect, ScenarioAuthoringInspectorDocument document, string scrollId)
        {
            GUI.Box(rect, GUIContent.none, _rootPanelStyle);
            Rect headerRect = new Rect(rect.x + 4f, rect.y + 4f, rect.width - 8f, 46f);
            GUI.Box(headerRect, GUIContent.none, _headerStyle);

            string title = document != null && !string.IsNullOrEmpty(document.Title)
                ? document.Title.ToUpperInvariant()
                : "DOCUMENT";
            GUI.Label(new Rect(headerRect.x + 10f, headerRect.y + 5f, headerRect.width - 20f, 18f), title, _sectionTitleStyle);
            if (document != null && !string.IsNullOrEmpty(document.Subtitle))
                GUI.Label(new Rect(headerRect.x + 10f, headerRect.y + 23f, headerRect.width - 20f, 16f), document.Subtitle, _mutedTextStyle);

            Rect bodyRect = new Rect(rect.x + 10f, headerRect.yMax + 8f, rect.width - 20f, rect.height - headerRect.height - 18f);
            GUILayout.BeginArea(bodyRect);
            Vector2 scrollPosition = GetWindowScrollPosition(scrollId);
            scrollPosition = GUILayout.BeginScrollView(scrollPosition, false, false, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            for (int i = 0; document != null && document.Sections != null && i < document.Sections.Length; i++)
            {
                DrawSection(document.Sections[i]);
                if (i < document.Sections.Length - 1)
                    GUILayout.Space(6f);
            }

            if (string.Equals(scrollId, "sprite_picker", StringComparison.Ordinal))
                DrawCustomSpriteEditor();

            GUILayout.EndScrollView();
            GUILayout.EndArea();
            SetWindowScrollPosition(scrollId, scrollPosition);
            return bodyRect;
        }

        private void DrawCustomSpriteEditor()
        {
            ScenarioSpriteSwapAuthoringService.CustomEditorModel editor =
                _spriteSwapAuthoringService.GetCustomEditorModel(_snapshot != null ? _snapshot.State : null);
            if (editor == null || !editor.Visible)
                return;

            GUILayout.Space(6f);
            GUILayout.BeginVertical(_sectionStyle);
            GUILayout.Label(editor.IsCharacterEditor ? "Character Pixel Editor" : "Pixel Editor", _sectionTitleStyle);
            GUILayout.Label(
                "Source: " + (editor.SourceLabel ?? "<sprite>") + (editor.Dirty ? " | Modified" : " | Unchanged"),
                _mutedTextStyle);
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical(GUILayout.Width(308f));
            if (editor.IsCharacterEditor)
            {
                DrawCharacterPartToolbar(editor);
                GUILayout.Space(6f);
            }
            DrawCustomEditorToolbar(editor);
            GUILayout.Space(6f);
            DrawCustomClipboardToolbar(editor);
            GUILayout.Space(6f);
            DrawCustomZoomToolbar(editor);
            GUILayout.Space(6f);
            GUILayout.Label("Active Color", _smallTitleStyle);
            Rect colorRect = GUILayoutUtility.GetRect(112f, 44f, GUILayout.Width(112f), GUILayout.Height(44f));
            DrawColorPreview(colorRect, editor.ActiveColor);
            GUILayout.Label("#" + (editor.ActiveColorHex ?? "000000FF"), _textStyle);
            GUILayout.Label(BuildSelectionSummary(editor), _mutedTextStyle);
            GUILayout.Label(BuildClipboardSummary(editor), _mutedTextStyle);
            if (editor.IsCharacterEditor)
                GUILayout.Label("Editing: " + (editor.CharacterPartLabel ?? "Part"), _mutedTextStyle);
            GUILayout.Space(4f);

            GUILayout.BeginHorizontal();
            for (int i = 0; editor.BrushPalette != null && i < editor.BrushPalette.Length; i++)
            {
                Rect swatchRect = GUILayoutUtility.GetRect(24f, 24f, GUILayout.Width(24f), GUILayout.Height(24f));
                DrawBrushSwatch(swatchRect, editor.BrushPalette[i], i == editor.ActiveBrushIndex, i);
                GUILayout.Space(4f);
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(6f);

            DrawColorSlider("R", editor, 0);
            DrawColorSlider("G", editor, 1);
            DrawColorSlider("B", editor, 2);
            DrawColorSlider("A", editor, 3);
            GUILayout.Space(6f);
            GUILayout.Label(BuildToolHint(editor.ActiveTool), _mutedTextStyle);
            GUILayout.EndVertical();

            GUILayout.Space(10f);
            GUILayout.BeginVertical();
            float zoom = Mathf.Max(1f, editor.Zoom);
            float width = Mathf.Max(1f, editor.Width * zoom);
            float height = Mathf.Max(1f, editor.Height * zoom);
            GUILayout.Label("Canvas " + editor.Width + "x" + editor.Height + " @ " + editor.Zoom + "x", _smallTitleStyle);
            GUILayout.Label("Mouse wheel zooms. Right click always samples color.", _mutedTextStyle);
            Rect canvasRect = GUILayoutUtility.GetRect(width, height, GUILayout.Width(width), GUILayout.Height(height));
            DrawPixelCanvas(canvasRect, editor);
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }

        private void DrawCustomEditorToolbar(ScenarioSpriteSwapAuthoringService.CustomEditorModel editor)
        {
            GUILayout.BeginHorizontal();
            DrawInlineAction(
                ScenarioAuthoringActionIds.ActionSpriteSwapCustomToolPaint,
                "Paint",
                editor.ActiveTool == ScenarioSpriteSwapAuthoringService.CustomEditorTool.Paint,
                92f,
                "Paint pixels using the active color.");
            GUILayout.Space(4f);
            DrawInlineAction(
                ScenarioAuthoringActionIds.ActionSpriteSwapCustomToolPick,
                "Pick",
                editor.ActiveTool == ScenarioSpriteSwapAuthoringService.CustomEditorTool.Pick,
                92f,
                "Sample a pixel color from the canvas.");
            GUILayout.Space(4f);
            DrawInlineAction(
                ScenarioAuthoringActionIds.ActionSpriteSwapCustomToolSelect,
                "Select",
                editor.ActiveTool == ScenarioSpriteSwapAuthoringService.CustomEditorTool.Select,
                92f,
                "Drag a rectangular pixel selection.");
            GUILayout.EndHorizontal();
        }

        private void DrawCustomClipboardToolbar(ScenarioSpriteSwapAuthoringService.CustomEditorModel editor)
        {
            GUILayout.BeginHorizontal();
            DrawInlineAction(
                ScenarioAuthoringActionIds.ActionSpriteSwapCustomCopy,
                "Copy",
                false,
                92f,
                "Copy the current selection. If nothing is selected, copy the whole sprite.",
                true);
            GUILayout.Space(4f);
            DrawInlineAction(
                ScenarioAuthoringActionIds.ActionSpriteSwapCustomPaste,
                "Paste",
                editor.HasClipboard,
                92f,
                editor.HasClipboard ? "Paste the pixel clipboard into the canvas." : "Pixel clipboard is empty.",
                editor.HasClipboard);
            GUILayout.Space(4f);
            DrawInlineAction(
                ScenarioAuthoringActionIds.ActionSpriteSwapCustomSelectionClear,
                "Clear Sel",
                editor.HasSelection,
                92f,
                editor.HasSelection ? "Clear the current pixel selection." : "There is no active selection.",
                editor.HasSelection);
            GUILayout.EndHorizontal();
        }

        private void DrawCustomZoomToolbar(ScenarioSpriteSwapAuthoringService.CustomEditorModel editor)
        {
            GUILayout.BeginHorizontal();
            DrawInlineAction(
                ScenarioAuthoringActionIds.ActionSpriteSwapCustomZoomOut,
                "Zoom -",
                false,
                92f,
                "Zoom out of the canvas.",
                editor.Zoom > 1);
            GUILayout.Space(4f);
            DrawInlineAction(
                ScenarioAuthoringActionIds.ActionSpriteSwapCustomZoomReset,
                editor.Zoom + "x",
                false,
                68f,
                "Reset canvas zoom to 8x.");
            GUILayout.Space(4f);
            DrawInlineAction(
                ScenarioAuthoringActionIds.ActionSpriteSwapCustomZoomIn,
                "Zoom +",
                false,
                92f,
                "Zoom into the canvas.",
                editor.Zoom < 48);
            GUILayout.EndHorizontal();
        }

        private void DrawCharacterPartToolbar(ScenarioSpriteSwapAuthoringService.CustomEditorModel editor)
        {
            GUILayout.BeginHorizontal();
            DrawInlineAction(
                ScenarioAuthoringActionIds.ActionSpriteSwapCharacterPartHead,
                "Head",
                editor.CharacterPart == ScenarioCharacterTexturePart.Head,
                92f,
                "Edit the head texture for this family member.");
            GUILayout.Space(4f);
            DrawInlineAction(
                ScenarioAuthoringActionIds.ActionSpriteSwapCharacterPartTorso,
                "Torso",
                editor.CharacterPart == ScenarioCharacterTexturePart.Torso,
                92f,
                "Edit the torso texture for this family member.");
            GUILayout.Space(4f);
            DrawInlineAction(
                ScenarioAuthoringActionIds.ActionSpriteSwapCharacterPartLegs,
                "Legs",
                editor.CharacterPart == ScenarioCharacterTexturePart.Legs,
                92f,
                "Edit the legs texture for this family member.");
            GUILayout.EndHorizontal();
        }

        private void DrawBrushSwatch(Rect rect, Color color, bool active, int brushIndex)
        {
            Color previous = GUI.color;
            GUI.color = color.a <= 0.001f ? new Color(0f, 0f, 0f, 0.2f) : color;
            GUI.Box(rect, GUIContent.none, active ? _activeButtonStyle : _fieldStyle);
            GUI.color = previous;

            if (color.a <= 0.001f)
                GUI.Label(rect, "X", _mutedTextStyle);

            if (GUI.Button(rect, GUIContent.none, GUIStyle.none))
            {
                ScenarioAuthoringBackendService.Instance.ExecuteAction(
                    ScenarioSpriteSwapAuthoringService.BuildCustomPresetActionId(brushIndex));
                if (Event.current != null)
                    Event.current.Use();
            }
        }

        private void DrawInlineAction(string actionId, string label, bool emphasized, float width, string hint, bool enabled = true)
        {
            Rect rect = GUILayoutUtility.GetRect(width, 28f, GUILayout.Width(width), GUILayout.Height(28f));
            DrawButton(rect, new ScenarioAuthoringInspectorAction
            {
                Id = actionId,
                Label = label,
                Hint = hint,
                Enabled = enabled,
                Emphasized = emphasized
            }, false);
        }

        private void DrawColorSlider(string label, ScenarioSpriteSwapAuthoringService.CustomEditorModel editor, int channel)
        {
            Color activeColor = editor.ActiveColor;
            float currentValue = channel == 0
                ? activeColor.r
                : (channel == 1 ? activeColor.g : (channel == 2 ? activeColor.b : activeColor.a));
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, _textStyle, GUILayout.Width(18f));
            float nextValue = GUILayout.HorizontalSlider(currentValue, 0f, 1f, GUILayout.Width(184f));
            GUILayout.Label(Mathf.RoundToInt(nextValue * 255f).ToString(), _mutedTextStyle, GUILayout.Width(34f));
            GUILayout.EndHorizontal();

            if (Mathf.Abs(nextValue - currentValue) <= 0.0001f)
                return;

            Color updatedColor = activeColor;
            if (channel == 0) updatedColor.r = nextValue;
            else if (channel == 1) updatedColor.g = nextValue;
            else if (channel == 2) updatedColor.b = nextValue;
            else updatedColor.a = nextValue;

            ScenarioAuthoringBackendService.Instance.ExecuteAction(
                ScenarioSpriteSwapAuthoringService.BuildCustomColorActionId(updatedColor));
            if (Event.current != null)
                Event.current.Use();
        }

        private void DrawColorPreview(Rect rect, Color color)
        {
            GUI.Box(rect, GUIContent.none, _fieldStyle);
            Rect fillRect = new Rect(rect.x + 4f, rect.y + 4f, rect.width - 8f, rect.height - 8f);
            DrawCheckerboard(fillRect, 6);
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(fillRect, Texture2D.whiteTexture);
            GUI.color = previous;
        }

        private void DrawPixelCanvas(Rect rect, ScenarioSpriteSwapAuthoringService.CustomEditorModel editor)
        {
            GUI.Box(rect, GUIContent.none, _fieldStyle);
            if (editor.PreviewSprite == null || editor.PreviewSprite.texture == null)
            {
                GUI.Label(rect, "No Sprite", _mutedTextStyle);
                return;
            }

            if (editor.Checkerboard)
                DrawCheckerboard(rect, editor.Zoom);

            GUI.DrawTextureWithTexCoords(rect, editor.PreviewSprite.texture, new Rect(0f, 0f, 1f, 1f), true);
            DrawPixelGrid(rect, editor);
            DrawSelectionOverlay(rect, editor);

            Event current = Event.current;
            if (current != null && rect.Contains(current.mousePosition))
            {
                if (current.type == EventType.ScrollWheel)
                {
                    string zoomActionId = current.delta.y < 0f
                        ? ScenarioAuthoringActionIds.ActionSpriteSwapCustomZoomIn
                        : ScenarioAuthoringActionIds.ActionSpriteSwapCustomZoomOut;
                    ScenarioAuthoringBackendService.Instance.ExecuteAction(zoomActionId);
                    current.Use();
                    return;
                }

                int pixelX;
                int pixelY;
                if (!TryGetCanvasPixel(rect, editor, current.mousePosition, out pixelX, out pixelY))
                    return;

                string actionId = null;
                if (current.button == 1)
                {
                    if (current.type == EventType.MouseDown || current.type == EventType.MouseDrag)
                        actionId = ScenarioSpriteSwapAuthoringService.BuildCustomPickActionId(pixelX, pixelY);
                }
                else if (editor.ActiveTool == ScenarioSpriteSwapAuthoringService.CustomEditorTool.Select)
                {
                    if (current.type == EventType.MouseDown)
                        actionId = ScenarioSpriteSwapAuthoringService.BuildCustomSelectStartActionId(pixelX, pixelY);
                    else if (current.type == EventType.MouseDrag)
                        actionId = ScenarioSpriteSwapAuthoringService.BuildCustomSelectDragActionId(pixelX, pixelY);
                    else if (current.type == EventType.MouseUp)
                        actionId = ScenarioSpriteSwapAuthoringService.BuildCustomSelectEndActionId(pixelX, pixelY);
                }
                else if (current.type == EventType.MouseDown || current.type == EventType.MouseDrag)
                {
                    actionId = editor.ActiveTool == ScenarioSpriteSwapAuthoringService.CustomEditorTool.Pick
                        ? ScenarioSpriteSwapAuthoringService.BuildCustomPickActionId(pixelX, pixelY)
                        : ScenarioSpriteSwapAuthoringService.BuildCustomPaintActionId(pixelX, pixelY);
                }

                if (!string.IsNullOrEmpty(actionId))
                {
                    ScenarioAuthoringBackendService.Instance.ExecuteAction(actionId);
                    current.Use();
                }
            }
        }

        private static bool TryGetCanvasPixel(
            Rect rect,
            ScenarioSpriteSwapAuthoringService.CustomEditorModel editor,
            Vector2 pointer,
            out int pixelX,
            out int pixelY)
        {
            pixelX = Mathf.Clamp(
                Mathf.FloorToInt((pointer.x - rect.x) / Mathf.Max(1f, editor.Zoom)),
                0,
                Mathf.Max(0, editor.Width - 1));
            pixelY = Mathf.Clamp(
                editor.Height - 1 - Mathf.FloorToInt((pointer.y - rect.y) / Mathf.Max(1f, editor.Zoom)),
                0,
                Mathf.Max(0, editor.Height - 1));
            return editor.Width > 0 && editor.Height > 0;
        }

        private void DrawPixelGrid(Rect rect, ScenarioSpriteSwapAuthoringService.CustomEditorModel editor)
        {
            if (editor.Zoom < 8 || editor.Width <= 0 || editor.Height <= 0)
                return;

            Color previous = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.18f);
            for (int x = 1; x < editor.Width; x++)
            {
                float lineX = rect.x + (x * editor.Zoom);
                GUI.DrawTexture(new Rect(lineX, rect.y, 1f, rect.height), Texture2D.whiteTexture);
            }

            for (int y = 1; y < editor.Height; y++)
            {
                float lineY = rect.y + (y * editor.Zoom);
                GUI.DrawTexture(new Rect(rect.x, lineY, rect.width, 1f), Texture2D.whiteTexture);
            }
            GUI.color = previous;
        }

        private void DrawSelectionOverlay(Rect rect, ScenarioSpriteSwapAuthoringService.CustomEditorModel editor)
        {
            if (!editor.HasSelection || editor.SelectionWidth <= 0 || editor.SelectionHeight <= 0)
                return;

            float zoom = Mathf.Max(1f, editor.Zoom);
            Rect selectionRect = new Rect(
                rect.x + (editor.SelectionX * zoom),
                rect.y + ((editor.Height - (editor.SelectionY + editor.SelectionHeight)) * zoom),
                editor.SelectionWidth * zoom,
                editor.SelectionHeight * zoom);

            Color previous = GUI.color;
            GUI.color = new Color(1f, 0.83f, 0.23f, 0.18f);
            GUI.DrawTexture(selectionRect, Texture2D.whiteTexture);
            GUI.color = new Color(1f, 0.87f, 0.30f, 1f);
            DrawRectBorder(selectionRect, 2f);
            GUI.color = previous;
        }

        private static void DrawRectBorder(Rect rect, float thickness)
        {
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, thickness), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.x, rect.y, thickness, rect.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), Texture2D.whiteTexture);
        }

        private static string BuildSelectionSummary(ScenarioSpriteSwapAuthoringService.CustomEditorModel editor)
        {
            if (editor == null || !editor.HasSelection)
                return "Selection: none";

            return "Selection: " + editor.SelectionWidth + "x" + editor.SelectionHeight
                + " at (" + editor.SelectionX + ", " + editor.SelectionY + ")";
        }

        private static string BuildClipboardSummary(ScenarioSpriteSwapAuthoringService.CustomEditorModel editor)
        {
            if (editor == null || !editor.HasClipboard)
                return "Clipboard: empty";

            return "Clipboard: " + editor.ClipboardWidth + "x" + editor.ClipboardHeight;
        }

        private static string BuildToolHint(ScenarioSpriteSwapAuthoringService.CustomEditorTool tool)
        {
            if (tool == ScenarioSpriteSwapAuthoringService.CustomEditorTool.Pick)
                return "Pick tool: click pixels to sample their exact RGBA color. Right click always samples.";
            if (tool == ScenarioSpriteSwapAuthoringService.CustomEditorTool.Select)
                return "Select tool: drag a rectangle, then use Copy and Paste to move pixel regions.";

            return "Paint tool: drag to paint individual pixels. If a selection exists, painting is limited to it.";
        }

        private void DrawCheckerboard(Rect rect, int zoom)
        {
            int tile = Mathf.Max(4, zoom);
            Color previous = GUI.color;
            for (int y = 0; y < rect.height; y += tile)
            {
                for (int x = 0; x < rect.width; x += tile)
                {
                    bool dark = (((x / tile) + (y / tile)) % 2) == 0;
                    GUI.color = dark
                        ? new Color(0.22f, 0.20f, 0.18f, 1f)
                        : new Color(0.33f, 0.30f, 0.27f, 1f);
                    GUI.DrawTexture(new Rect(rect.x + x, rect.y + y, tile, tile), Texture2D.whiteTexture);
                }
            }
            GUI.color = previous;
        }

        private void DrawSection(ScenarioAuthoringInspectorSection section)
        {
            if (section == null)
                return;

            GUILayout.BeginVertical(_sectionStyle);
            if (!string.IsNullOrEmpty(section.Title))
                GUILayout.Label(section.Title, _sectionTitleStyle);

            if (section.Layout == ScenarioAuthoringInspectorSectionLayout.ActionStrip || section.Layout == ScenarioAuthoringInspectorSectionLayout.TabStrip)
            {
                bool renderAsTabs = section.Layout == ScenarioAuthoringInspectorSectionLayout.TabStrip;
                GUILayout.BeginHorizontal();
                for (int i = 0; section.Items != null && i < section.Items.Length; i++)
                {
                    ScenarioAuthoringInspectorItem item = section.Items[i];
                    if (item == null || item.Action == null)
                        continue;

                    float width = Mathf.Clamp(
                        MeasureButtonWidth(item.Action, renderAsTabs, 20f),
                        renderAsTabs ? 72f : 94f,
                        renderAsTabs ? 148f : 184f);
                    Rect rect = GUILayoutUtility.GetRect(width, 30f, GUILayout.Width(width), GUILayout.Height(30f));
                    DrawButton(rect, item.Action, renderAsTabs);
                    GUILayout.Space(4f);
                }
                GUILayout.EndHorizontal();

                for (int i = 0; section.Items != null && i < section.Items.Length; i++)
                {
                    ScenarioAuthoringInspectorItem item = section.Items[i];
                    if (item == null || item.Action != null)
                        continue;

                    DrawItem(item);
                }
            }
            else if (section.Layout == ScenarioAuthoringInspectorSectionLayout.CandidateGrid)
            {
                for (int i = 0; section.Items != null && i < section.Items.Length; i++)
                {
                    ScenarioAuthoringInspectorItem item = section.Items[i];
                    if (item == null || item.Action != null)
                        continue;

                    DrawItem(item);
                }

                int columns = 3;
                int count = 0;
                GUILayout.BeginHorizontal();
                for (int i = 0; section.Items != null && i < section.Items.Length; i++)
                {
                    ScenarioAuthoringInspectorItem item = section.Items[i];
                    if (item == null || item.Action == null)
                        continue;

                    Rect rect = GUILayoutUtility.GetRect(176f, 84f, GUILayout.Width(176f), GUILayout.Height(84f));
                    DrawCandidateCard(rect, item.Action);
                    count++;
                    if (count % columns == 0 && i < section.Items.Length - 1)
                    {
                        GUILayout.EndHorizontal();
                        GUILayout.BeginHorizontal();
                    }
                    else
                    {
                        GUILayout.Space(4f);
                    }
                }
                GUILayout.EndHorizontal();
            }
            else
            {
                for (int i = 0; section.Items != null && i < section.Items.Length; i++)
                    DrawItem(section.Items[i]);
            }
            GUILayout.EndVertical();
        }

        private void DrawItem(ScenarioAuthoringInspectorItem item)
        {
            if (item == null)
                return;

            if (item.PreviewSprite != null || !string.IsNullOrEmpty(item.Detail) || !string.IsNullOrEmpty(item.Badge))
            {
                DrawRichItem(item);
                return;
            }

            switch (item.Kind)
            {
                case ScenarioAuthoringInspectorItemKind.Property:
                    GUILayout.BeginHorizontal();
                    GUILayout.Label(item.Label ?? string.Empty, _mutedTextStyle, GUILayout.Width(116f));
                    GUILayout.Label(item.Value ?? string.Empty, _textStyle);
                    GUILayout.EndHorizontal();
                    break;
                case ScenarioAuthoringInspectorItemKind.Action:
                    if (item.Action != null)
                    {
                        Rect rect = GUILayoutUtility.GetRect(96f, 30f, GUILayout.Height(30f));
                        DrawButton(rect, item.Action, false);
                    }
                    break;
                default:
                    GUILayout.Label(item.Value ?? string.Empty, _textStyle);
                    break;
            }
        }

        private void DrawRichItem(ScenarioAuthoringInspectorItem item)
        {
            GUILayout.BeginVertical(_sectionStyle);
            Rect rowRect = GUILayoutUtility.GetRect(120f, 92f, GUILayout.ExpandWidth(true));
            Rect previewRect = new Rect(rowRect.x + 6f, rowRect.y + 6f, 84f, rowRect.height - 12f);
            DrawSpritePreview(previewRect, item.PreviewSprite, item.Emphasized);

            string title = item.Value ?? string.Empty;
            string detail = item.Kind == ScenarioAuthoringInspectorItemKind.Property
                ? CombineDetail(item.Label, item.Detail)
                : item.Detail;
            Rect textRect = new Rect(previewRect.xMax + 12f, rowRect.y + 6f, rowRect.width - previewRect.width - 18f, rowRect.height - 12f);
            GUI.Label(new Rect(textRect.x, textRect.y, textRect.width, 24f), title, _sectionTitleStyle);
            if (!string.IsNullOrEmpty(detail))
                GUI.Label(new Rect(textRect.x, textRect.y + 26f, textRect.width, 34f), detail, _mutedTextStyle);

            if (!string.IsNullOrEmpty(item.Badge))
            {
                Vector2 badgeSize = _mutedTextStyle.CalcSize(new GUIContent(item.Badge));
                Rect badgeRect = new Rect(textRect.x, rowRect.yMax - 26f, Mathf.Max(56f, badgeSize.x + 18f), 20f);
                GUI.Box(badgeRect, item.Badge, _fieldStyle);
            }
            GUILayout.EndVertical();
        }

        private void DrawCandidateCard(Rect rect, ScenarioAuthoringInspectorAction action)
        {
            if (action == null)
                return;

            GUI.enabled = action.Enabled;
            GUIStyle style = action.Emphasized ? _activeButtonStyle : _buttonStyle;
            if (GUI.Button(rect, GUIContent.none, style) && action.Enabled)
            {
                ScenarioAuthoringBackendService.Instance.ExecuteAction(action.Id);
                if (Event.current != null)
                    Event.current.Use();
            }
            GUI.enabled = true;

            Rect previewRect = new Rect(rect.x + 6f, rect.y + 6f, 70f, rect.height - 12f);
            DrawSpritePreview(previewRect, action.PreviewSprite, action.Emphasized);

            Rect textRect = new Rect(previewRect.xMax + 10f, rect.y + 8f, rect.width - previewRect.width - 22f, rect.height - 16f);
            GUI.Label(new Rect(textRect.x, textRect.y, textRect.width, 20f), action.Label ?? string.Empty, _textStyle);
            string detail = !string.IsNullOrEmpty(action.Detail) ? action.Detail : action.Hint;
            if (!string.IsNullOrEmpty(detail))
                GUI.Label(new Rect(textRect.x, textRect.y + 22f, textRect.width, 30f), detail, _mutedTextStyle);

            if (!string.IsNullOrEmpty(action.Badge))
            {
                Vector2 badgeSize = _mutedTextStyle.CalcSize(new GUIContent(action.Badge));
                Rect badgeRect = new Rect(textRect.x, rect.yMax - 22f, Mathf.Max(52f, badgeSize.x + 16f), 18f);
                GUI.Box(badgeRect, action.Badge, _fieldStyle);
            }
        }

        private void DrawSpritePreview(Rect rect, Sprite sprite, bool emphasized)
        {
            GUI.Box(rect, GUIContent.none, emphasized ? _activeButtonStyle : _fieldStyle);
            if (sprite == null || sprite.texture == null)
            {
                GUI.Label(rect, "No Sprite", _mutedTextStyle);
                return;
            }

            Rect textureRect = sprite.textureRect;
            Texture2D texture = sprite.texture;
            Rect uv = new Rect(
                textureRect.x / texture.width,
                textureRect.y / texture.height,
                textureRect.width / texture.width,
                textureRect.height / texture.height);

            Rect fitted = FitRect(rect, textureRect.width, textureRect.height, 4f);
            GUI.DrawTextureWithTexCoords(fitted, texture, uv, true);
        }

        private static string CombineDetail(string primary, string secondary)
        {
            if (string.IsNullOrEmpty(primary))
                return secondary ?? string.Empty;
            if (string.IsNullOrEmpty(secondary))
                return primary;
            return primary + " | " + secondary;
        }

        private static Rect FitRect(Rect rect, float sourceWidth, float sourceHeight, float padding)
        {
            Rect inner = new Rect(rect.x + padding, rect.y + padding, rect.width - (padding * 2f), rect.height - (padding * 2f));
            if (sourceWidth <= 0f || sourceHeight <= 0f || inner.width <= 0f || inner.height <= 0f)
                return inner;

            float scale = Math.Min(inner.width / sourceWidth, inner.height / sourceHeight);
            float width = sourceWidth * scale;
            float height = sourceHeight * scale;
            return new Rect(
                inner.x + ((inner.width - width) * 0.5f),
                inner.y + ((inner.height - height) * 0.5f),
                width,
                height);
        }

        private Rect DrawSettingsWindow(Rect rect, ScenarioAuthoringSettingsViewModel settings)
        {
            GUI.Box(rect, GUIContent.none, _rootPanelStyle);
            Rect headerRect = new Rect(rect.x + 4f, rect.y + 4f, rect.width - 8f, 30f);
            GUI.Box(headerRect, GUIContent.none, _headerStyle);
            GUI.Label(new Rect(headerRect.x + 8f, headerRect.y + 4f, headerRect.width - 120f, 22f), settings.Title ?? "Editor Settings", _sectionTitleStyle);

            float actionX = headerRect.xMax - 90f;
            for (int i = 0; settings.HeaderActions != null && i < settings.HeaderActions.Length; i++)
            {
                Rect actionRect = new Rect(actionX, headerRect.y + 3f, 82f, 22f);
                DrawButton(actionRect, settings.HeaderActions[i], false);
                actionX -= 86f;
            }

            Rect bodyRect = new Rect(rect.x + 10f, headerRect.yMax + 8f, rect.width - 20f, rect.height - headerRect.height - 16f);
            GUILayout.BeginArea(bodyRect);
            _settingsScrollPosition = GUILayout.BeginScrollView(_settingsScrollPosition, false, false, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            GUILayout.Label(settings.Subtitle ?? string.Empty, _mutedTextStyle);
            GUILayout.Space(8f);
            for (int i = 0; settings.Sections != null && i < settings.Sections.Length; i++)
            {
                ScenarioAuthoringSettingsSectionViewModel section = settings.Sections[i];
                if (section == null)
                    continue;

                GUILayout.BeginVertical(_sectionStyle);
                GUILayout.Label(section.Title ?? string.Empty, _sectionTitleStyle);
                for (int j = 0; section.Items != null && j < section.Items.Length; j++)
                {
                    DrawSettingItem(section.Items[j]);
                }
                GUILayout.EndVertical();
                GUILayout.Space(6f);
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
            return bodyRect;
        }

        private Vector2 GetWindowScrollPosition(string windowId)
        {
            if (string.IsNullOrEmpty(windowId))
                return Vector2.zero;

            Vector2 scrollPosition;
            if (_windowScrollPositions.TryGetValue(windowId, out scrollPosition))
                return scrollPosition;

            return Vector2.zero;
        }

        private void SetWindowScrollPosition(string windowId, Vector2 scrollPosition)
        {
            if (string.IsNullOrEmpty(windowId))
                return;

            _windowScrollPositions[windowId] = scrollPosition;
        }

        private void DrawSettingItem(ScenarioAuthoringSettingsItemViewModel item)
        {
            if (item == null)
                return;

            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical(GUILayout.Width(280f));
            GUILayout.Label(item.Label ?? string.Empty, _textStyle);
            GUILayout.Label(item.Description ?? string.Empty, _mutedTextStyle);
            GUILayout.EndVertical();

            if (item.Kind == ScenarioAuthoringSettingKind.Toggle)
            {
                DrawButton(GUILayoutUtility.GetRect(84f, 24f, GUILayout.Width(84f), GUILayout.Height(24f)),
                    new ScenarioAuthoringInspectorAction
                    {
                        Id = ScenarioAuthoringActionIds.ActionSettingTogglePrefix + item.Id,
                        Label = item.BoolValue ? "On" : "Off",
                        Enabled = item.Enabled,
                        Emphasized = item.BoolValue
                    },
                    false);
            }
            else if (item.Kind == ScenarioAuthoringSettingKind.Float || item.Kind == ScenarioAuthoringSettingKind.Integer)
            {
                DrawButton(GUILayoutUtility.GetRect(26f, 24f, GUILayout.Width(26f), GUILayout.Height(24f)),
                    new ScenarioAuthoringInspectorAction
                    {
                        Id = ScenarioAuthoringActionIds.ActionSettingDecreasePrefix + item.Id,
                        Label = "-",
                        Enabled = item.Enabled && item.CanDecrease
                    },
                    false);
                GUILayout.Label(item.ValueText ?? string.Empty, _fieldStyle, GUILayout.Width(84f), GUILayout.Height(24f));
                DrawButton(GUILayoutUtility.GetRect(26f, 24f, GUILayout.Width(26f), GUILayout.Height(24f)),
                    new ScenarioAuthoringInspectorAction
                    {
                        Id = ScenarioAuthoringActionIds.ActionSettingIncreasePrefix + item.Id,
                        Label = "+",
                        Enabled = item.Enabled && item.CanIncrease
                    },
                    false);
            }
            else
            {
                GUILayout.Label(item.ValueText ?? string.Empty, _fieldStyle, GUILayout.Width(160f), GUILayout.Height(24f));
            }

            GUILayout.EndHorizontal();
        }

        private void DrawContextMenu(Rect rect, ScenarioAuthoringContextMenuModel menu)
        {
            GUI.Box(rect, GUIContent.none, _menuStyle);
            GUILayout.BeginArea(new Rect(rect.x + 8f, rect.y + 8f, rect.width - 16f, rect.height - 16f));
            GUILayout.Label(menu.Title ?? "Context", _sectionTitleStyle);
            if (!string.IsNullOrEmpty(menu.Detail))
                GUILayout.Label(menu.Detail, _mutedTextStyle);
            GUILayout.Space(4f);
            for (int i = 0; menu.Actions != null && i < menu.Actions.Length; i++)
            {
                ScenarioAuthoringInspectorAction action = menu.Actions[i];
                if (action == null)
                    continue;

                Rect buttonRect = GUILayoutUtility.GetRect(rect.width - 24f, 24f, GUILayout.Height(24f));
                DrawButton(buttonRect, action, false);
            }
            GUILayout.EndArea();
        }

        private Rect BuildPopupRect(ScenarioAuthoringContextMenuModel menu, float width, float height)
        {
            float rectWidth = 220f;
            float rectHeight = 54f + ((menu.Actions != null ? menu.Actions.Length : 0) * 28f);
            return new Rect(
                Mathf.Clamp(menu.AnchorX + 16f, Margin, width - rectWidth - Margin),
                Mathf.Clamp(menu.AnchorY + 16f, Margin, height - rectHeight - Margin),
                rectWidth,
                rectHeight);
        }

        private Rect BuildWindowMenuRect(Rect buttonRect, ScenarioAuthoringInspectorAction[] actions, float width, float height)
        {
            float rectWidth = 220f;
            for (int i = 0; actions != null && i < actions.Length; i++)
                rectWidth = Math.Max(rectWidth, MeasureButtonWidth(actions[i], false, 26f) + 24f);

            rectWidth = Mathf.Clamp(rectWidth, 220f, 320f);
            float rectHeight = 16f + ((actions != null ? actions.Length : 0) * 28f);
            return new Rect(
                Mathf.Clamp(buttonRect.x, Margin, width - rectWidth - Margin),
                Mathf.Clamp(buttonRect.yMax + 4f, Margin, height - rectHeight - Margin),
                rectWidth,
                rectHeight);
        }

        private void DrawWindowMenu(Rect rect, ScenarioAuthoringInspectorAction[] actions)
        {
            GUI.Box(rect, GUIContent.none, _menuStyle);
            GUILayout.BeginArea(new Rect(rect.x + 8f, rect.y + 8f, rect.width - 16f, rect.height - 16f));
            for (int i = 0; actions != null && i < actions.Length; i++)
            {
                ScenarioAuthoringInspectorAction action = actions[i];
                if (action == null)
                    continue;

                Rect buttonRect = GUILayoutUtility.GetRect(rect.width - 24f, 24f, GUILayout.Height(24f));
                DrawButton(buttonRect, action, false);
            }
            GUILayout.EndArea();
        }

        private void DrawButton(Rect rect, ScenarioAuthoringInspectorAction action, bool tab)
        {
            if (action == null)
                return;

            GUIContent content = new GUIContent(action.Label ?? string.Empty, action.Hint ?? string.Empty);

            if (IsWindowMenuAction(action))
            {
                GUI.enabled = action.Enabled;
                GUIStyle menuStyle = tab
                    ? (action.Emphasized ? _activeTabStyle : _tabStyle)
                    : (action.Emphasized ? _activeButtonStyle : _buttonStyle);
                if (GUI.Button(rect, content, menuStyle) && action.Enabled)
                {
                    _windowMenuOpen = !_windowMenuOpen;
                    if (Event.current != null)
                        Event.current.Use();
                }
                GUI.enabled = true;
                return;
            }

            GUI.enabled = action.Enabled;
            GUIStyle style = tab
                ? (action.Emphasized ? _activeTabStyle : _tabStyle)
                : (action.Emphasized ? _activeButtonStyle : _buttonStyle);
            if (GUI.Button(rect, content, style) && action.Enabled)
            {
                ScenarioAuthoringBackendService.Instance.ExecuteAction(action.Id);
                if (Event.current != null)
                    Event.current.Use();
            }
            GUI.enabled = true;
        }

        private void DrawTooltipOverlay(float scaledWidth, float scaledHeight)
        {
            string tip = GUI.tooltip;
            if (string.IsNullOrEmpty(tip))
                return;

            GUIStyle tipStyle = _mutedTextStyle;
            if (tipStyle == null)
                return;
            tipStyle.wordWrap = true;
            Vector2 mouse = Event.current != null ? Event.current.mousePosition : Vector2.zero;
            float maxWidth = 320f;
            Vector2 size = tipStyle.CalcSize(new GUIContent(tip));
            float width = Math.Min(maxWidth, size.x + 18f);
            float height = tipStyle.CalcHeight(new GUIContent(tip), width - 14f) + 10f;
            float x = Math.Min(scaledWidth - width - 6f, mouse.x + 16f);
            float y = Math.Min(scaledHeight - height - 6f, mouse.y + 20f);
            if (x < 6f) x = 6f;
            if (y < 6f) y = 6f;
            Rect tipRect = new Rect(x, y, width, height);
            GUI.Box(tipRect, GUIContent.none, _menuStyle);
            GUI.Label(new Rect(tipRect.x + 7f, tipRect.y + 5f, tipRect.width - 14f, tipRect.height - 10f), tip, tipStyle);
        }

        private void EnsureStyles(float panelOpacity)
        {
            if (_rootPanelStyle != null && Mathf.Abs(_styleOpacity - panelOpacity) <= 0.001f)
                return;

            _styleOpacity = panelOpacity;
            _panelTexture = MakeTexture(new Color(0.07f, 0.07f, 0.06f, panelOpacity));
            _panelAltTexture = MakeTexture(new Color(0.13f, 0.13f, 0.11f, Mathf.Min(1f, panelOpacity + 0.06f)));
            _lineTexture = MakeTexture(new Color(0.62f, 0.47f, 0.14f, 0.88f));
            _activeTexture = MakeTexture(new Color(0.34f, 0.29f, 0.14f, Mathf.Min(1f, panelOpacity + 0.10f)));
            _dangerTexture = MakeTexture(new Color(0.48f, 0.12f, 0.08f, 1f));
            _viewportTexture = MakeTexture(new Color(0.04f, 0.05f, 0.05f, 0.16f));

            _rootPanelStyle = BuildBoxStyle(_panelTexture, 10, new RectOffset(2, 2, 2, 2));
            _headerStyle = BuildBoxStyle(_panelAltTexture, 6, new RectOffset(1, 1, 1, 1));
            _sectionStyle = BuildBoxStyle(_panelAltTexture, 8, new RectOffset(1, 1, 1, 1));
            _statusStyle = BuildBoxStyle(_panelTexture, 6, new RectOffset(1, 1, 1, 1));
            _menuStyle = BuildBoxStyle(_panelAltTexture, 8, new RectOffset(1, 1, 1, 1));

            _titleStyle = BuildTextStyle(27, FontStyle.Bold, new Color(0.94f, 0.80f, 0.52f, 1f));
            _smallTitleStyle = BuildTextStyle(17, FontStyle.Bold, new Color(0.88f, 0.74f, 0.49f, 1f));
            _sectionTitleStyle = BuildTextStyle(15, FontStyle.Bold, new Color(0.94f, 0.80f, 0.52f, 1f));
            _textStyle = BuildTextStyle(15, FontStyle.Normal, new Color(0.92f, 0.89f, 0.82f, 1f));
            _mutedTextStyle = BuildTextStyle(13, FontStyle.Normal, new Color(0.77f, 0.72f, 0.63f, 1f));
            _fieldStyle = BuildBoxStyle(_panelTexture, 4, new RectOffset(1, 1, 1, 1));
            _fieldStyle.normal.textColor = new Color(0.90f, 0.87f, 0.79f, 1f);
            _fieldStyle.alignment = TextAnchor.MiddleCenter;
            _fieldStyle.fontSize = 13;

            _buttonStyle = BuildButtonStyle(_panelAltTexture, _lineTexture, new Color(0.90f, 0.87f, 0.79f, 1f));
            _activeButtonStyle = BuildButtonStyle(_activeTexture, _lineTexture, new Color(0.98f, 0.92f, 0.74f, 1f));
            _tabStyle = BuildButtonStyle(_panelTexture, _lineTexture, new Color(0.87f, 0.79f, 0.66f, 1f));
            _activeTabStyle = BuildButtonStyle(_activeTexture, _lineTexture, new Color(0.98f, 0.92f, 0.74f, 1f));
        }

        private static Texture2D MakeTexture(Color color)
        {
            Texture2D texture = new Texture2D(1, 1, TextureFormat.ARGB32, false);
            texture.hideFlags = HideFlags.HideAndDontSave;
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }

        private static GUIStyle BuildBoxStyle(Texture2D texture, int padding, RectOffset border)
        {
            GUIStyle style = new GUIStyle(GUI.skin.box);
            style.normal.background = texture;
            style.border = border;
            style.padding = new RectOffset(padding, padding, padding, padding);
            style.margin = new RectOffset(0, 0, 0, 0);
            return style;
        }

        private static GUIStyle BuildTextStyle(int size, FontStyle fontStyle, Color color)
        {
            GUIStyle style = new GUIStyle(GUI.skin.label);
            style.fontSize = size;
            style.fontStyle = fontStyle;
            style.normal.textColor = color;
            style.wordWrap = true;
            return style;
        }

        private static GUIStyle BuildButtonStyle(Texture2D background, Texture2D hover, Color textColor)
        {
            GUIStyle style = new GUIStyle(GUI.skin.button);
            style.normal.background = background;
            style.hover.background = hover;
            style.active.background = hover;
            style.normal.textColor = textColor;
            style.hover.textColor = textColor;
            style.active.textColor = textColor;
            style.alignment = TextAnchor.MiddleCenter;
            style.fontSize = 14;
            style.border = new RectOffset(1, 1, 1, 1);
            style.padding = new RectOffset(8, 8, 4, 4);
            style.margin = new RectOffset(0, 0, 0, 0);
            style.wordWrap = false;
            return style;
        }

        private static bool HasVisibleWindow(ScenarioAuthoringShellWindowViewModel[] windows, ScenarioAuthoringShellDock dock)
        {
            for (int i = 0; windows != null && i < windows.Length; i++)
            {
                ScenarioAuthoringShellWindowViewModel window = windows[i];
                if (window != null && window.Visible && window.Dock == dock)
                    return true;
            }

            return false;
        }

        private static bool HasVisibleWindow(ScenarioAuthoringShellWindowViewModel[] windows, string id)
        {
            for (int i = 0; windows != null && i < windows.Length; i++)
            {
                ScenarioAuthoringShellWindowViewModel window = windows[i];
                if (window != null && window.Visible && string.Equals(window.Id, id, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static bool HasAnyVisibleBottomStrip(ScenarioAuthoringShellWindowViewModel[] windows)
        {
            return HasVisibleWindow(windows, ScenarioAuthoringWindowIds.Triggers)
                || HasVisibleWindow(windows, ScenarioAuthoringWindowIds.Survivors)
                || HasVisibleWindow(windows, ScenarioAuthoringWindowIds.Stockpile)
                || HasVisibleWindow(windows, ScenarioAuthoringWindowIds.Quests)
                || HasVisibleWindow(windows, ScenarioAuthoringWindowIds.Map)
                || HasVisibleWindow(windows, ScenarioAuthoringWindowIds.Publish)
                || HasVisibleWindow(windows, ScenarioAuthoringWindowIds.Calendar);
        }

        private static readonly string[] _workspaceOrder = new[]
        {
            ScenarioAuthoringWindowIds.Triggers,
            ScenarioAuthoringWindowIds.Survivors,
            ScenarioAuthoringWindowIds.Stockpile,
            ScenarioAuthoringWindowIds.Quests,
            ScenarioAuthoringWindowIds.Map,
            ScenarioAuthoringWindowIds.Publish,
            ScenarioAuthoringWindowIds.Calendar
        };

        private static readonly string[] _workspaceLabels = new[]
        {
            "Triggers",
            "Survivors",
            "Stockpile",
            "Quests",
            "Map",
            "Publish",
            "Calendar"
        };

        private static string GetActiveWorkspaceId(ScenarioAuthoringShellWindowViewModel[] windows)
        {
            if (windows == null)
                return null;
            for (int i = 0; i < _workspaceOrder.Length; i++)
            {
                if (HasVisibleWindow(windows, _workspaceOrder[i]))
                    return _workspaceOrder[i];
            }
            return null;
        }

        private void DrawWorkspaceTabs(Rect rect, string activeId, ScenarioAuthoringShellWindowViewModel[] windows)
        {
            GUI.Box(rect, GUIContent.none, _headerStyle);
            float tabWidth = (rect.width - 8f) / _workspaceOrder.Length;
            for (int i = 0; i < _workspaceOrder.Length; i++)
            {
                Rect tabRect = new Rect(rect.x + 4f + (tabWidth * i), rect.y + 3f, tabWidth - 4f, rect.height - 6f);
                bool isActive = string.Equals(_workspaceOrder[i], activeId, StringComparison.OrdinalIgnoreCase);
                ScenarioAuthoringInspectorAction action = new ScenarioAuthoringInspectorAction
                {
                    Id = ScenarioAuthoringActionIds.ActionWindowTogglePrefix + _workspaceOrder[i],
                    Label = _workspaceLabels[i],
                    Hint = "Open the " + _workspaceLabels[i] + " workspace.",
                    Enabled = true,
                    Emphasized = isActive
                };
                DrawButton(tabRect, action, true);
            }
        }

        private static void AppendStackRect(Dictionary<string, Rect> rects, ScenarioAuthoringShellWindowViewModel[] windows, string id, Rect rect)
        {
            if (!HasVisibleWindow(windows, id))
                return;

            rects[id] = rect;
        }

        private static bool IsWindowMenuAction(ScenarioAuthoringInspectorAction action)
        {
            return action != null
                && string.Equals(action.Id, ScenarioAuthoringActionIds.ActionShellToggleWindowMenu, StringComparison.Ordinal);
        }

        private ScenarioAuthoringInspectorAction[] GetHeaderActions(ScenarioAuthoringInspectorAction[] actions, bool chromeOnly)
        {
            if (actions == null || actions.Length == 0)
                return new ScenarioAuthoringInspectorAction[0];

            List<ScenarioAuthoringInspectorAction> filtered = new List<ScenarioAuthoringInspectorAction>();
            for (int i = 0; i < actions.Length; i++)
            {
                ScenarioAuthoringInspectorAction action = actions[i];
                if (action == null)
                    continue;

                bool isChrome = action.Id != null
                    && (action.Id.StartsWith(ScenarioAuthoringActionIds.ActionWindowCollapsePrefix, StringComparison.Ordinal)
                        || action.Id.StartsWith(ScenarioAuthoringActionIds.ActionWindowTogglePrefix, StringComparison.Ordinal));
                if (isChrome == chromeOnly)
                    filtered.Add(action);
            }

            return filtered.ToArray();
        }

        private float MeasureButtonWidth(ScenarioAuthoringInspectorAction action, bool tab, float extraPadding)
        {
            GUIStyle style = tab
                ? (action != null && action.Emphasized ? _activeTabStyle : _tabStyle)
                : (action != null && action.Emphasized ? _activeButtonStyle : _buttonStyle);
            Vector2 size = style.CalcSize(new GUIContent(action != null ? action.Label ?? string.Empty : string.Empty));
            return size.x + extraPadding;
        }

        private sealed class ScenarioAuthoringShellRuntime : MonoBehaviour
        {
            private ScenarioAuthoringShellImguiRenderModule _owner;

            public void Initialize(ScenarioAuthoringShellImguiRenderModule owner)
            {
                _owner = owner;
            }

            private void OnGUI()
            {
                if (_owner != null)
                    _owner.Draw();
            }
        }
    }
}
