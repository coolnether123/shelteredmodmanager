using System;
using System.Collections.Generic;
using System.Text;
using ModAPI.Core;
using UnityEngine;

namespace ShelteredAPI.Scenarios
{
    internal sealed class ScenarioAuthoringImguiRenderModule : IScenarioAuthoringRenderModule
    {
        private const string RuntimeObjectName = "ShelteredAPI.ScenarioAuthoring.Imgui";
        private static readonly object Sync = new object();
        private static Rect[] _interactiveRects = new Rect[0];
        private ScenarioAuthoringImguiRuntime _runtime;
        private ScenarioAuthoringPresentationSnapshot _snapshot;
        private bool _visible;
        private bool _hasLoggedVisibility;
        private bool _lastLoggedVisibility;

        public string ModuleId
        {
            get { return "ShelteredAPI.IMGUI"; }
        }

        public int Priority
        {
            get { return 100; }
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
            SetVisible(snapshot != null && snapshot.State != null && snapshot.State.IsActive, "render");
        }

        public void Hide()
        {
            _snapshot = null;
            SetInteractiveRects(new Rect[0]);
            SetVisible(false, "hide");
        }

        public static bool IsPointerOverInteractiveUi()
        {
            Vector2 mouse = EventCompatibleMousePosition();
            lock (Sync)
            {
                for (int i = 0; i < _interactiveRects.Length; i++)
                {
                    if (_interactiveRects[i].Contains(mouse))
                        return true;
                }
            }

            return false;
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
                MMLog.WriteInfo("[ScenarioAuthoringIMGUI] Created runtime GameObject '" + RuntimeObjectName + "'.");
            }
            else
            {
                MMLog.WriteInfo("[ScenarioAuthoringIMGUI] Reusing runtime GameObject '" + RuntimeObjectName + "'.");
            }

            _runtime = runtimeObject.GetComponent<ScenarioAuthoringImguiRuntime>();
            if (_runtime == null)
            {
                _runtime = runtimeObject.AddComponent<ScenarioAuthoringImguiRuntime>();
                MMLog.WriteInfo("[ScenarioAuthoringIMGUI] Added ScenarioAuthoringImguiRuntime component.");
            }
            else
            {
                MMLog.WriteInfo("[ScenarioAuthoringIMGUI] Reusing existing ScenarioAuthoringImguiRuntime component.");
            }

            _runtime.Initialize(this);
            _runtime.enabled = _visible;
        }

        private void SetVisible(bool visible, string source)
        {
            _visible = visible;
            if (_runtime != null)
                _runtime.enabled = _visible;

            if (!_hasLoggedVisibility || _lastLoggedVisibility != visible)
            {
                _hasLoggedVisibility = true;
                _lastLoggedVisibility = visible;
                MMLog.WriteInfo("[ScenarioAuthoringIMGUI] Visibility changed to " + (visible ? "visible" : "hidden")
                    + " via " + source + ".");
            }
        }

        private void Draw()
        {
            if (!_visible || _snapshot == null || _snapshot.State == null || !_snapshot.State.IsActive)
            {
                SetInteractiveRects(new Rect[0]);
                return;
            }

            EnsureStyles();

            List<Rect> interactiveRects = new List<Rect>();
            float shellWidth = 520f;
            float inspectorWidth = 440f;
            float hoverWidth = 360f;
            float screenWidth = Mathf.Max(1280f, Screen.width);

            Rect shellRect = _runtime.ShellRect;
            Rect inspectorRect = _runtime.InspectorRect;
            Rect hoverRect = BuildHoverRect(_snapshot.HoverDocument, hoverWidth);

            if (_snapshot.State.ShellVisible && _snapshot.ShellDocument != null)
            {
                shellRect.width = shellWidth;
                shellRect.height = EstimateWindowHeight(_snapshot.ShellDocument, shellWidth, true);
                _runtime.ShellRect = GUI.Window(
                    712341,
                    shellRect,
                    DrawShellWindow,
                    string.Empty,
                    _runtime.ChromeWindowStyle);
                interactiveRects.Add(_runtime.ShellRect);
            }

            if (_snapshot.State.SelectedTarget != null && _snapshot.InspectorDocument != null)
            {
                inspectorRect.x = Mathf.Clamp(inspectorRect.x, 12f, screenWidth - inspectorWidth - 12f);
                inspectorRect.width = inspectorWidth;
                inspectorRect.height = EstimateWindowHeight(_snapshot.InspectorDocument, inspectorWidth, true);
                _runtime.InspectorRect = GUI.Window(
                    712342,
                    inspectorRect,
                    DrawInspectorWindow,
                    string.Empty,
                    _runtime.ChromeWindowStyle);
                interactiveRects.Add(_runtime.InspectorRect);
            }

            if (_snapshot.State.SelectionModeActive && _snapshot.State.HoveredTarget != null && _snapshot.HoverDocument != null)
            {
                DrawHoverWindow(hoverRect, _snapshot.HoverDocument);
                interactiveRects.Add(hoverRect);
            }

            SetInteractiveRects(interactiveRects.ToArray());
        }

        private void DrawShellWindow(int id)
        {
            DrawWindowContents(_snapshot != null ? _snapshot.ShellDocument : null, true, ref _runtime.ShellScrollPosition);
            GUI.DragWindow(new Rect(0f, 0f, 10000f, 34f));
        }

        private void DrawInspectorWindow(int id)
        {
            DrawWindowContents(_snapshot != null ? _snapshot.InspectorDocument : null, true, ref _runtime.InspectorScrollPosition);
            GUI.DragWindow(new Rect(0f, 0f, 10000f, 34f));
        }

        private void DrawHoverWindow(Rect rect, ScenarioAuthoringInspectorDocument document)
        {
            GUI.Box(rect, GUIContent.none, _runtime.TooltipBoxStyle);
            GUILayout.BeginArea(new Rect(rect.x + 14f, rect.y + 12f, rect.width - 28f, rect.height - 24f));
            _runtime.HoverScrollPosition = GUILayout.BeginScrollView(_runtime.HoverScrollPosition, false, false);
            DrawDocument(document, false, false);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawWindowContents(ScenarioAuthoringInspectorDocument document, bool showHeaderActions, ref Vector2 scrollPosition)
        {
            GUILayout.BeginVertical();
            DrawDocument(document, showHeaderActions, true);
            GUILayout.Space(4f);
            scrollPosition = GUILayout.BeginScrollView(scrollPosition, false, true);
            DrawSections(document);
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }

        // suppressSections=true  → sections are drawn by the caller inside a ScrollView (shell/inspector windows).
        // suppressSections=false → sections are drawn inline here (hover tooltip).
        private void DrawDocument(ScenarioAuthoringInspectorDocument document, bool showHeaderActions, bool suppressSections)
        {
            if (document == null)
            {
                GUILayout.Label("No document.", _runtime.BodyLabelStyle);
                return;
            }

            if (!string.IsNullOrEmpty(document.Title))
                GUILayout.Label(document.Title, _runtime.TitleLabelStyle);

            if (!string.IsNullOrEmpty(document.Subtitle))
            {
                GUILayout.Space(2f);
                GUILayout.Label(document.Subtitle, _runtime.SubtitleLabelStyle);
            }

            if (showHeaderActions && document.HeaderActions != null && document.HeaderActions.Length > 0)
            {
                GUILayout.Space(8f);
                GUILayout.BeginHorizontal();
                for (int i = 0; i < document.HeaderActions.Length; i++)
                    DrawActionButton(document.HeaderActions[i], 114f);
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }

            if (!suppressSections)
            {
                GUILayout.Space(8f);
                DrawSections(document);
            }
        }

        private void DrawSections(ScenarioAuthoringInspectorDocument document)
        {
            if (document == null)
                return;

            GUILayout.Space(6f);
            ScenarioAuthoringInspectorSection[] sections = document.Sections;
            for (int i = 0; sections != null && i < sections.Length; i++)
            {
                ScenarioAuthoringInspectorSection section = sections[i];
                if (section == null)
                    continue;

                if (!string.IsNullOrEmpty(section.Title))
                    GUILayout.Label(section.Title, _runtime.SectionLabelStyle);

                GUILayout.BeginVertical(_runtime.SectionBoxStyle);
                DrawSectionItems(section.Items);
                GUILayout.EndVertical();
                GUILayout.Space(10f);
            }
        }

        private void DrawSectionItems(ScenarioAuthoringInspectorItem[] items)
        {
            for (int i = 0; items != null && i < items.Length; i++)
            {
                ScenarioAuthoringInspectorItem item = items[i];
                if (item == null)
                    continue;

                switch (item.Kind)
                {
                    case ScenarioAuthoringInspectorItemKind.Property:
                        DrawPropertyItem(item);
                        break;

                    case ScenarioAuthoringInspectorItemKind.Action:
                        DrawActionButton(item.Action, 200f);
                        break;

                    default:
                        GUILayout.Label(item.Value ?? string.Empty, _runtime.BodyLabelStyle);
                        GUILayout.Space(4f);
                        break;
                }
            }
        }

        private void DrawPropertyItem(ScenarioAuthoringInspectorItem item)
        {
            string label = item.Label ?? string.Empty;
            string value = item.Value ?? string.Empty;
            bool stack = ShouldStackProperty(value);

            if (stack)
            {
                GUILayout.Label(label, _runtime.PropertyKeyStyle);
                GUILayout.Space(1f);
                GUILayout.Label(value, _runtime.PropertyValueStyle);
            }
            else
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(label, _runtime.PropertyKeyStyle, GUILayout.Width(136f));
                GUILayout.Label(value, _runtime.PropertyValueStyle);
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(6f);
        }

        private static bool ShouldStackProperty(string value)
        {
            if (string.IsNullOrEmpty(value))
                return false;

            return value.Length > 28
                || value.IndexOf('\\') >= 0
                || value.IndexOf('/') >= 0
                || value.IndexOf('\n') >= 0;
        }

        private void DrawActionButton(ScenarioAuthoringInspectorAction action, float width)
        {
            if (action == null)
                return;

            bool enabled = action.Enabled;
            GUI.enabled = enabled;
            GUIStyle style = action.Emphasized ? _runtime.EmphasisButtonStyle : _runtime.ActionButtonStyle;
            if (GUILayout.Button(new GUIContent(action.Label ?? string.Empty, action.Hint ?? string.Empty), style, GUILayout.Width(width), GUILayout.Height(32f)))
            {
                bool changed = ScenarioAuthoringBackendService.Instance.ExecuteAction(action.Id);
                if (changed)
                    ScenarioAuthoringBackendService.Instance.Refresh();
            }
            GUI.enabled = true;
        }

        private Rect BuildHoverRect(ScenarioAuthoringInspectorDocument document, float width)
        {
            Vector2 mouse = EventCompatibleMousePosition();
            float x = mouse.x + 20f;
            float y = mouse.y + 26f;
            float height = EstimateWindowHeight(document, width, false);

            if (x + width > Screen.width - 12f)
                x = Screen.width - width - 12f;
            if (y + height > Screen.height - 12f)
                y = Screen.height - height - 12f;
            if (x < 12f)
                x = 12f;
            if (y < 12f)
                y = 12f;

            return new Rect(x, y, width, height);
        }

        private float EstimateWindowHeight(ScenarioAuthoringInspectorDocument document, float width, bool includeHeaderButtons)
        {
            if (document == null)
                return 180f;

            float height = 34f;
            height += 26f;
            if (!string.IsNullOrEmpty(document.Subtitle))
                height += MeasureHeight(document.Subtitle, _runtime.SubtitleLabelStyle, width - 32f);
            if (includeHeaderButtons && document.HeaderActions != null && document.HeaderActions.Length > 0)
                height += 42f;

            ScenarioAuthoringInspectorSection[] sections = document.Sections;
            for (int i = 0; sections != null && i < sections.Length; i++)
            {
                ScenarioAuthoringInspectorSection section = sections[i];
                if (section == null)
                    continue;

                if (!string.IsNullOrEmpty(section.Title))
                    height += MeasureHeight(section.Title, _runtime.SectionLabelStyle, width - 30f) + 4f;

                height += 16f;
                ScenarioAuthoringInspectorItem[] items = section.Items;
                for (int j = 0; items != null && j < items.Length; j++)
                {
                    ScenarioAuthoringInspectorItem item = items[j];
                    if (item == null)
                        continue;

                    switch (item.Kind)
                    {
                        case ScenarioAuthoringInspectorItemKind.Property:
                            if (ShouldStackProperty(item.Value))
                            {
                                height += MeasureHeight(item.Label ?? string.Empty, _runtime.PropertyKeyStyle, width - 34f);
                                height += MeasureHeight(item.Value ?? string.Empty, _runtime.PropertyValueStyle, width - 34f) + 8f;
                            }
                            else
                            {
                                height += Mathf.Max(
                                    MeasureHeight(item.Label ?? string.Empty, _runtime.PropertyKeyStyle, 136f),
                                    MeasureHeight(item.Value ?? string.Empty, _runtime.PropertyValueStyle, width - 190f)) + 8f;
                            }
                            break;
                        case ScenarioAuthoringInspectorItemKind.Action:
                            height += 36f;
                            break;
                        default:
                            height += MeasureHeight(item.Value ?? string.Empty, _runtime.BodyLabelStyle, width - 34f) + 8f;
                            break;
                    }
                }

                height += 14f;
            }

            return Mathf.Clamp(height, 220f, 640f);
        }

        private float MeasureHeight(string text, GUIStyle style, float width)
        {
            if (style == null || string.IsNullOrEmpty(text))
                return 0f;

            return Mathf.Max(18f, style.CalcHeight(new GUIContent(text), width));
        }

        private void EnsureStyles()
        {
            EnsureRuntime();
            if (_runtime != null)
                _runtime.EnsureStyles();
        }

        private static void SetInteractiveRects(Rect[] rects)
        {
            lock (Sync)
            {
                _interactiveRects = rects ?? new Rect[0];
            }
        }

        private static Vector2 EventCompatibleMousePosition()
        {
            Vector3 mouse = UnityEngine.Input.mousePosition;
            return new Vector2(mouse.x, Screen.height - mouse.y);
        }

        private sealed class ScenarioAuthoringImguiRuntime : MonoBehaviour
        {
            private ScenarioAuthoringImguiRenderModule _owner;
            private Texture2D _surfaceTexture;
            private Texture2D _borderTexture;
            private Texture2D _tooltipTexture;
            private GUIStyle _chromeWindowStyle;
            private GUIStyle _tooltipBoxStyle;
            private GUIStyle _sectionBoxStyle;
            private GUIStyle _titleLabelStyle;
            private GUIStyle _subtitleLabelStyle;
            private GUIStyle _sectionLabelStyle;
            private GUIStyle _bodyLabelStyle;
            private GUIStyle _propertyKeyStyle;
            private GUIStyle _propertyValueStyle;
            private GUIStyle _actionButtonStyle;
            private GUIStyle _emphasisButtonStyle;
            private Font _monoFont;
            private bool _stylesLogged;

            public Rect ShellRect = new Rect(18f, 54f, 520f, 460f);
            public Rect InspectorRect = new Rect(574f, 54f, 440f, 360f);
            public Vector2 ShellScrollPosition = Vector2.zero;
            public Vector2 InspectorScrollPosition = Vector2.zero;
            public Vector2 HoverScrollPosition = Vector2.zero;

            public GUIStyle ChromeWindowStyle { get { return _chromeWindowStyle; } }
            public GUIStyle TooltipBoxStyle { get { return _tooltipBoxStyle; } }
            public GUIStyle SectionBoxStyle { get { return _sectionBoxStyle; } }
            public GUIStyle TitleLabelStyle { get { return _titleLabelStyle; } }
            public GUIStyle SubtitleLabelStyle { get { return _subtitleLabelStyle; } }
            public GUIStyle SectionLabelStyle { get { return _sectionLabelStyle; } }
            public GUIStyle BodyLabelStyle { get { return _bodyLabelStyle; } }
            public GUIStyle PropertyKeyStyle { get { return _propertyKeyStyle; } }
            public GUIStyle PropertyValueStyle { get { return _propertyValueStyle; } }
            public GUIStyle ActionButtonStyle { get { return _actionButtonStyle; } }
            public GUIStyle EmphasisButtonStyle { get { return _emphasisButtonStyle; } }

            public void Initialize(ScenarioAuthoringImguiRenderModule owner)
            {
                _owner = owner;
                name = RuntimeObjectName;
                DontDestroyOnLoad(gameObject);
            }

            public void EnsureStyles()
            {
                if (_chromeWindowStyle != null)
                    return;

                _surfaceTexture = MakeTexture(new Color(0.07f, 0.08f, 0.09f, 0.97f));
                _borderTexture = MakeTexture(new Color(0.16f, 0.19f, 0.22f, 1f));
                _tooltipTexture = MakeTexture(new Color(0.09f, 0.10f, 0.12f, 0.98f));
                _monoFont = CreateMonospaceFont();

                _chromeWindowStyle = new GUIStyle(GUI.skin.window);
                _chromeWindowStyle.normal.background = _surfaceTexture;
                _chromeWindowStyle.active.background = _surfaceTexture;
                _chromeWindowStyle.hover.background = _surfaceTexture;
                _chromeWindowStyle.focused.background = _surfaceTexture;
                _chromeWindowStyle.normal.textColor = new Color(0.98f, 0.96f, 0.90f, 1f);
                _chromeWindowStyle.fontSize = 17;
                _chromeWindowStyle.padding = new RectOffset(14, 14, 14, 14);
                _chromeWindowStyle.border = new RectOffset(2, 2, 2, 2);

                _tooltipBoxStyle = new GUIStyle(GUI.skin.box);
                _tooltipBoxStyle.normal.background = _tooltipTexture;
                _tooltipBoxStyle.normal.textColor = new Color(0.98f, 0.97f, 0.95f, 1f);
                _tooltipBoxStyle.padding = new RectOffset(12, 12, 12, 12);
                _tooltipBoxStyle.border = new RectOffset(2, 2, 2, 2);

                _sectionBoxStyle = new GUIStyle(GUI.skin.box);
                _sectionBoxStyle.normal.background = _borderTexture;
                _sectionBoxStyle.padding = new RectOffset(12, 12, 10, 10);
                _sectionBoxStyle.margin = new RectOffset(0, 0, 0, 0);
                _sectionBoxStyle.border = new RectOffset(2, 2, 2, 2);

                _titleLabelStyle = new GUIStyle(GUI.skin.label);
                _titleLabelStyle.normal.textColor = new Color(0.98f, 0.93f, 0.80f, 1f);
                _titleLabelStyle.fontSize = 22;
                _titleLabelStyle.fontStyle = FontStyle.Bold;
                _titleLabelStyle.wordWrap = true;

                _subtitleLabelStyle = new GUIStyle(GUI.skin.label);
                _subtitleLabelStyle.normal.textColor = new Color(0.80f, 0.86f, 0.91f, 1f);
                _subtitleLabelStyle.fontSize = 14;
                _subtitleLabelStyle.wordWrap = true;

                _sectionLabelStyle = new GUIStyle(GUI.skin.label);
                _sectionLabelStyle.normal.textColor = new Color(0.98f, 0.89f, 0.66f, 1f);
                _sectionLabelStyle.fontSize = 16;
                _sectionLabelStyle.fontStyle = FontStyle.Bold;

                _bodyLabelStyle = new GUIStyle(GUI.skin.label);
                _bodyLabelStyle.normal.textColor = new Color(0.96f, 0.97f, 0.98f, 1f);
                _bodyLabelStyle.fontSize = 14;
                _bodyLabelStyle.richText = false;
                _bodyLabelStyle.wordWrap = true;

                _propertyKeyStyle = new GUIStyle(_bodyLabelStyle);
                _propertyKeyStyle.normal.textColor = new Color(0.82f, 0.88f, 0.95f, 1f);
                _propertyKeyStyle.fontStyle = FontStyle.Bold;
                _propertyKeyStyle.fontSize = 13;
                if (_monoFont != null)
                    _propertyKeyStyle.font = _monoFont;

                _propertyValueStyle = new GUIStyle(_bodyLabelStyle);
                _propertyValueStyle.wordWrap = true;
                _propertyValueStyle.fontSize = 14;
                if (_monoFont != null)
                    _propertyValueStyle.font = _monoFont;

                _actionButtonStyle = new GUIStyle(GUI.skin.button);
                _actionButtonStyle.normal.background = _borderTexture;
                _actionButtonStyle.hover.background = MakeTexture(new Color(0.22f, 0.26f, 0.30f, 1f));
                _actionButtonStyle.active.background = MakeTexture(new Color(0.12f, 0.16f, 0.20f, 1f));
                _actionButtonStyle.normal.textColor = new Color(0.97f, 0.98f, 0.99f, 1f);
                _actionButtonStyle.fontSize = 13;
                _actionButtonStyle.padding = new RectOffset(10, 10, 6, 6);
                _actionButtonStyle.alignment = TextAnchor.MiddleCenter;

                _emphasisButtonStyle = new GUIStyle(_actionButtonStyle);
                _emphasisButtonStyle.normal.background = MakeTexture(new Color(0.22f, 0.36f, 0.52f, 1f));
                _emphasisButtonStyle.hover.background = MakeTexture(new Color(0.28f, 0.43f, 0.61f, 1f));
                _emphasisButtonStyle.active.background = MakeTexture(new Color(0.18f, 0.30f, 0.45f, 1f));
                _emphasisButtonStyle.normal.textColor = Color.white;
                _emphasisButtonStyle.fontStyle = FontStyle.Bold;

                if (!_stylesLogged)
                {
                    _stylesLogged = true;
                    MMLog.WriteInfo("[ScenarioAuthoringIMGUI] Initialized IMGUI styles and window chrome.");
                }
            }

            private void OnGUI()
            {
                if (_owner != null)
                    _owner.Draw();
            }

            private static Texture2D MakeTexture(Color color)
            {
                Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                for (int x = 0; x < 2; x++)
                {
                    for (int y = 0; y < 2; y++)
                        texture.SetPixel(x, y, color);
                }

                texture.Apply();
                return texture;
            }

            private static Font CreateMonospaceFont()
            {
                try
                {
                    return Font.CreateDynamicFontFromOSFont(new[] { "Consolas", "Courier New", "Courier" }, 13);
                }
                catch
                {
                    return null;
                }
            }
        }
    }
}
