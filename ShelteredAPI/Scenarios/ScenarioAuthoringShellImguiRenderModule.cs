using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShelteredAPI.Scenarios
{
    internal sealed class ScenarioAuthoringShellImguiRenderModule : IScenarioAuthoringRenderModule
    {
        private const string RuntimeObjectName = "ShelteredAPI.ScenarioAuthoring.ShellImgui";
        private const float Margin = 14f;
        private const float Gutter = 12f;
        private const float TopBarHeight = 92f;
        private const float StatusHeight = 30f;

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

            ScenarioAuthoringInputCaptureService inputCapture = ScenarioCompositionRoot.Resolve<ScenarioAuthoringInputCaptureService>();
            inputCapture.BeginFrame();

            ScenarioAuthoringShellViewModel shell = _snapshot.ShellViewModel;
            float uiScale = _snapshot.State != null && _snapshot.State.Settings != null
                ? _snapshot.State.Settings.GetFloat("shell.ui_scale", 1f)
                : 1f;
            float panelOpacity = _snapshot.State != null && _snapshot.State.Settings != null
                ? Mathf.Clamp(_snapshot.State.Settings.GetFloat("shell.panel_opacity", 0.82f), 0.55f, 1f)
                : 0.82f;
            EnsureStyles(panelOpacity);

            Matrix4x4 oldMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(uiScale, uiScale, 1f));

            float scaledWidth = Screen.width / uiScale;
            float scaledHeight = Screen.height / uiScale;
            Rect topRect = new Rect(Margin, Margin, scaledWidth - (Margin * 2f), TopBarHeight);
            Rect statusRect = new Rect(Margin, scaledHeight - Margin - StatusHeight, scaledWidth - (Margin * 2f), StatusHeight);
            Rect windowMenuButtonRect = DrawTopBar(topRect, shell);
            DrawStatusBar(statusRect, shell);
            inputCapture.RegisterInteractiveRect(topRect);
            inputCapture.RegisterInteractiveRect(statusRect);

            Rect contentRect = new Rect(
                Margin,
                topRect.yMax + Gutter,
                scaledWidth - (Margin * 2f),
                statusRect.y - (topRect.yMax + (Gutter * 2f)));

            Dictionary<string, Rect> windowRects = ResolveWindowRects(contentRect, shell.Windows);

            for (int i = 0; shell.Windows != null && i < shell.Windows.Length; i++)
            {
                ScenarioAuthoringShellWindowViewModel window = shell.Windows[i];
                Rect rect;
                if (window == null || !window.Visible || !windowRects.TryGetValue(window.Id, out rect))
                    continue;

                DrawWindow(rect, window);
                inputCapture.RegisterInteractiveRect(rect);
                inputCapture.RegisterScrollRect(window.Id, rect);
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

            if (shell.Settings != null)
            {
                Rect settingsRect = new Rect(
                    Math.Max(Margin, (scaledWidth - 720f) * 0.5f),
                    Math.Max(topRect.yMax + Gutter, (scaledHeight - 520f) * 0.5f),
                    Math.Min(720f, scaledWidth - (Margin * 2f)),
                    Math.Min(520f, scaledHeight - topRect.height - StatusHeight - (Margin * 3f)));
                DrawSettingsWindow(settingsRect, shell.Settings);
                inputCapture.RegisterInteractiveRect(settingsRect);
                inputCapture.SetPopupOpen(true);
            }

            inputCapture.SetKeyboardCaptured(shell.Settings != null || (shell.ContextMenu != null && shell.ContextMenu.Visible));
            inputCapture.CompleteFrame();
            GUI.matrix = oldMatrix;
        }

        private Dictionary<string, Rect> ResolveWindowRects(Rect contentRect, ScenarioAuthoringShellWindowViewModel[] windows)
        {
            Dictionary<string, Rect> rects = new Dictionary<string, Rect>(StringComparer.OrdinalIgnoreCase);
            float leftWidth = Mathf.Clamp(contentRect.width * 0.17f, 258f, 286f);
            float rightWidth = Mathf.Clamp(contentRect.width * 0.18f, 288f, 318f);
            float bottomPrimaryHeight = Mathf.Clamp(contentRect.height * 0.17f, 144f, 164f);
            float bottomSecondaryHeight = 34f;
            float viewportTop = contentRect.y;
            float viewportLeft = contentRect.x;
            float viewportRight = contentRect.xMax;
            float viewportBottom = contentRect.yMax;

            if (HasVisibleWindow(windows, ScenarioAuthoringShellDock.Left))
                viewportLeft += leftWidth + Gutter;
            if (HasVisibleWindow(windows, ScenarioAuthoringShellDock.Right))
                viewportRight -= rightWidth + Gutter;
            if (HasVisibleWindow(windows, ScenarioAuthoringWindowIds.BuildTools))
                viewportBottom -= bottomPrimaryHeight + Gutter;
            if (HasAnyVisibleBottomStrip(windows))
                viewportBottom -= bottomSecondaryHeight + Gutter;

            float leftY = contentRect.y;
            float scenarioHeight = Mathf.Clamp(contentRect.height * 0.24f, 182f, 212f);
            float layersHeight = Mathf.Clamp(contentRect.height * 0.18f, 136f, 164f);
            AppendStackRect(rects, windows, ScenarioAuthoringWindowIds.Scenario, new Rect(contentRect.x, leftY, leftWidth, scenarioHeight));
            leftY += scenarioHeight + Gutter;
            AppendStackRect(rects, windows, ScenarioAuthoringWindowIds.Layers, new Rect(contentRect.x, leftY, leftWidth, layersHeight));
            leftY += layersHeight + Gutter;
            AppendStackRect(rects, windows, ScenarioAuthoringWindowIds.TilesPalette, new Rect(contentRect.x, leftY, leftWidth, Math.Max(220f, viewportBottom - leftY)));

            AppendStackRect(rects, windows, ScenarioAuthoringWindowIds.Inspector, new Rect(viewportRight + Gutter, contentRect.y, rightWidth, contentRect.height));

            Rect buildToolsRect = new Rect(
                viewportLeft,
                viewportBottom + (HasAnyVisibleBottomStrip(windows) ? bottomSecondaryHeight + Gutter : 0f),
                Math.Max(480f, viewportRight - viewportLeft),
                bottomPrimaryHeight);
            AppendStackRect(rects, windows, ScenarioAuthoringWindowIds.BuildTools, buildToolsRect);

            float bottomStripY = contentRect.yMax - bottomSecondaryHeight;
            float bottomStripWidth = Math.Max(200f, viewportRight - viewportLeft);
            float panelWidth = (bottomStripWidth - (Gutter * 3f)) / 4f;
            AppendStackRect(rects, windows, ScenarioAuthoringWindowIds.Triggers, new Rect(viewportLeft, bottomStripY, panelWidth, bottomSecondaryHeight));
            AppendStackRect(rects, windows, ScenarioAuthoringWindowIds.Survivors, new Rect(viewportLeft + panelWidth + Gutter, bottomStripY, panelWidth, bottomSecondaryHeight));
            AppendStackRect(rects, windows, ScenarioAuthoringWindowIds.Stockpile, new Rect(viewportLeft + ((panelWidth + Gutter) * 2f), bottomStripY, panelWidth, bottomSecondaryHeight));
            AppendStackRect(rects, windows, ScenarioAuthoringWindowIds.Quests, new Rect(viewportLeft + ((panelWidth + Gutter) * 3f), bottomStripY, panelWidth, bottomSecondaryHeight));
            return rects;
        }

        private Rect DrawTopBar(Rect rect, ScenarioAuthoringShellViewModel shell)
        {
            GUI.Box(rect, GUIContent.none, _rootPanelStyle);
            Rect windowMenuButtonRect = Rect.zero;

            Rect brandRect = new Rect(rect.x + 10f, rect.y + 8f, 146f, rect.height - 16f);
            GUI.Label(new Rect(brandRect.x, brandRect.y, brandRect.width, 26f), "SHELTERED", _titleStyle);
            GUI.Label(new Rect(brandRect.x, brandRect.y + 26f, brandRect.width, 22f), "SCENARIO EDITOR", _smallTitleStyle);

            float tabX = brandRect.xMax + 8f;
            for (int i = 0; shell.Tabs != null && i < shell.Tabs.Length; i++)
            {
                ScenarioAuthoringInspectorAction tab = shell.Tabs[i];
                float tabWidth = Mathf.Clamp(MeasureButtonWidth(tab, true, 26f), 78f, 104f);
                Rect tabRect = new Rect(tabX, rect.y + 4f, tabWidth, 42f);
                DrawButton(tabRect, tab, true);
                tabX = tabRect.xMax + 4f;
            }

            Rect actionsRect = new Rect(rect.x + 8f, rect.y + 50f, rect.width - 420f, 32f);
            float actionX = actionsRect.x;
            for (int i = 0; shell.ToolbarActions != null && i < shell.ToolbarActions.Length; i++)
            {
                ScenarioAuthoringInspectorAction action = shell.ToolbarActions[i];
                ScenarioAuthoringInspectorAction displayAction = IsWindowMenuAction(action) && _windowMenuOpen
                    ? new ScenarioAuthoringInspectorAction
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
                    }
                    : action;
                float width = Mathf.Clamp(MeasureButtonWidth(displayAction, false, 28f), 92f, 170f);
                Rect buttonRect = new Rect(actionX, actionsRect.y, width + 20f, actionsRect.height);
                DrawButton(buttonRect, displayAction, false);
                if (IsWindowMenuAction(action))
                    windowMenuButtonRect = buttonRect;
                actionX = buttonRect.xMax + 6f;
            }

            Rect infoRect = new Rect(rect.xMax - 382f, rect.y + 4f, 370f, 70f);
            GUI.Box(infoRect, GUIContent.none, _sectionStyle);
            GUI.Label(new Rect(infoRect.x + 12f, infoRect.y + 10f, 210f, 20f), "Draft: " + (shell.DraftLabel ?? string.Empty), _textStyle);
            GUI.Label(new Rect(infoRect.x + 12f, infoRect.y + 34f, 120f, 18f), "Mode: " + (shell.ModeLabel ?? string.Empty), _mutedTextStyle);
            GUI.Label(new Rect(infoRect.x + 260f, infoRect.y + 8f, 90f, 34f), shell.TimeLabel ?? "--:--", _titleStyle);
            GUI.Label(new Rect(infoRect.x + 212f, infoRect.y + 12f, 42f, 18f), "Day 1", _textStyle);
            return windowMenuButtonRect;
        }

        private void DrawStatusBar(Rect rect, ScenarioAuthoringShellViewModel shell)
        {
            GUI.Box(rect, GUIContent.none, _statusStyle);
            float x = rect.x + 8f;
            for (int i = 0; shell.StatusEntries != null && i < shell.StatusEntries.Length; i++)
            {
                string value = shell.StatusEntries[i] ?? string.Empty;
                GUI.Label(new Rect(x, rect.y + 4f, Math.Min(220f, value.Length * 8f + 30f), 18f), value, _mutedTextStyle);
                x += Math.Min(220f, value.Length * 8f + 34f);
            }
        }

        private void DrawWindow(Rect rect, ScenarioAuthoringShellWindowViewModel window)
        {
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
                return;

            Rect bodyRect = new Rect(rect.x + 10f, headerRect.yMax + 8f, rect.width - 20f, rect.height - headerRect.height - 18f);
            GUILayout.BeginArea(bodyRect);
            for (int i = 0; window.Sections != null && i < window.Sections.Length; i++)
            {
                DrawSection(window.Sections[i]);
                if (i < window.Sections.Length - 1)
                    GUILayout.Space(6f);
            }
            GUILayout.EndArea();
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

        private void DrawSettingsWindow(Rect rect, ScenarioAuthoringSettingsViewModel settings)
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
            GUILayout.EndArea();
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

            if (IsWindowMenuAction(action))
            {
                GUI.enabled = action.Enabled;
                GUIStyle menuStyle = tab
                    ? (action.Emphasized ? _activeTabStyle : _tabStyle)
                    : (action.Emphasized ? _activeButtonStyle : _buttonStyle);
                if (GUI.Button(rect, action.Label ?? string.Empty, menuStyle) && action.Enabled)
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
            if (GUI.Button(rect, action.Label ?? string.Empty, style) && action.Enabled)
            {
                ScenarioAuthoringBackendService.Instance.ExecuteAction(action.Id);
                if (Event.current != null)
                    Event.current.Use();
            }
            GUI.enabled = true;
        }

        private void EnsureStyles(float panelOpacity)
        {
            if (_rootPanelStyle != null && Mathf.Abs(_styleOpacity - panelOpacity) <= 0.001f)
                return;

            _styleOpacity = panelOpacity;
            _panelTexture = MakeTexture(new Color(0.09f, 0.08f, 0.06f, panelOpacity));
            _panelAltTexture = MakeTexture(new Color(0.11f, 0.10f, 0.07f, Mathf.Min(1f, panelOpacity + 0.06f)));
            _lineTexture = MakeTexture(new Color(0.44f, 0.35f, 0.19f, 0.82f));
            _activeTexture = MakeTexture(new Color(0.55f, 0.43f, 0.15f, Mathf.Min(1f, panelOpacity + 0.10f)));
            _dangerTexture = MakeTexture(new Color(0.48f, 0.12f, 0.08f, 1f));
            _viewportTexture = MakeTexture(new Color(0.16f, 0.13f, 0.10f, 0.24f));

            _rootPanelStyle = BuildBoxStyle(_panelTexture, 10, new RectOffset(2, 2, 2, 2));
            _headerStyle = BuildBoxStyle(_panelAltTexture, 6, new RectOffset(1, 1, 1, 1));
            _sectionStyle = BuildBoxStyle(_panelAltTexture, 8, new RectOffset(1, 1, 1, 1));
            _statusStyle = BuildBoxStyle(_panelAltTexture, 6, new RectOffset(1, 1, 1, 1));
            _menuStyle = BuildBoxStyle(_panelAltTexture, 8, new RectOffset(1, 1, 1, 1));

            _titleStyle = BuildTextStyle(26, FontStyle.Bold, new Color(0.95f, 0.84f, 0.60f, 1f));
            _smallTitleStyle = BuildTextStyle(18, FontStyle.Bold, new Color(0.89f, 0.77f, 0.54f, 1f));
            _sectionTitleStyle = BuildTextStyle(17, FontStyle.Bold, new Color(0.94f, 0.83f, 0.61f, 1f));
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
                || HasVisibleWindow(windows, ScenarioAuthoringWindowIds.Quests);
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
