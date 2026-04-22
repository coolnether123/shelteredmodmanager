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
        private const float Margin = 18f;
        private const float Gutter = 12f;
        private static readonly object Sync = new object();
        private static Rect[] _interactiveRects = new Rect[0];

        private readonly Dictionary<string, int> _sectionPages = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private readonly ScenarioAuthoringUiDebugService _uiDebug = ScenarioAuthoringUiDebugService.Instance;

        private ScenarioAuthoringImguiRuntime _runtime;
        private ScenarioAuthoringPresentationSnapshot _snapshot;
        private bool _visible;
        private bool _hasLoggedVisibility;
        private bool _lastLoggedVisibility;
        private Vector2 _sidebarScroll = Vector2.zero;
        private Vector2 _inspectorScroll = Vector2.zero;
        private Vector2 _browserSummaryScroll = Vector2.zero;
        private Vector2 _hoverScroll = Vector2.zero;
        private Rect _headerRect;
        private Rect _sidebarRect;
        private Rect _inspectorRect;
        private Rect _browserRect;
        private Rect _hoverRect;
        private Rect _lastGridFirstCardRect;
        private string _lastDebugSignature;

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

            _runtime = runtimeObject.GetComponent<ScenarioAuthoringImguiRuntime>();
            if (_runtime == null)
            {
                _runtime = runtimeObject.AddComponent<ScenarioAuthoringImguiRuntime>();
                MMLog.WriteInfo("[ScenarioAuthoringIMGUI] Added ScenarioAuthoringImguiRuntime component.");
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

            if (!_snapshot.State.ShellVisible)
            {
                SetInteractiveRects(new Rect[0]);
                return;
            }

            EnsureStyles();
            ResolveLayout();
            LogLayout();

            List<Rect> interactiveRects = new List<Rect>();
            interactiveRects.Add(_headerRect);
            interactiveRects.Add(_sidebarRect);
            interactiveRects.Add(_inspectorRect);
            interactiveRects.Add(_browserRect);

            DrawSurface(_headerRect, _runtime.HeaderSurfaceStyle, DrawHeader);
            DrawSurface(_sidebarRect, _runtime.SidebarSurfaceStyle, DrawSidebar);
            DrawSurface(_inspectorRect, _runtime.InspectorSurfaceStyle, DrawInspector);
            DrawSurface(_browserRect, _runtime.BrowserSurfaceStyle, DrawBrowser);

            if (_snapshot.State.SelectionModeActive && _snapshot.State.HoveredTarget != null && _snapshot.HoverDocument != null)
            {
                _hoverRect = BuildHoverRect(_snapshot.HoverDocument);
                interactiveRects.Add(_hoverRect);
                DrawSurface(_hoverRect, _runtime.HoverSurfaceStyle, delegate(Rect inner)
                {
                    DrawHover(inner, _snapshot.HoverDocument);
                });
            }

            SetInteractiveRects(interactiveRects.ToArray());
        }

        private void ResolveLayout()
        {
            float headerHeight = Mathf.Clamp(Screen.height * 0.14f, 104f, 132f);
            float sidebarWidth = Mathf.Clamp(Screen.width * 0.19f, 290f, 360f);
            float inspectorWidth = Mathf.Clamp(Screen.width * 0.21f, 320f, 380f);
            float browserHeight = Mathf.Clamp(Screen.height * 0.30f, 240f, 320f);

            _headerRect = new Rect(Margin, Margin, Screen.width - (Margin * 2f), headerHeight);

            float bodyTop = _headerRect.yMax + Gutter;
            float browserTop = Screen.height - Margin - browserHeight;
            float columnHeight = Mathf.Max(180f, browserTop - bodyTop - Gutter);

            _sidebarRect = new Rect(Margin, bodyTop, sidebarWidth, columnHeight);
            _inspectorRect = new Rect(Screen.width - Margin - inspectorWidth, bodyTop, inspectorWidth, Screen.height - bodyTop - Margin);

            float browserLeft = _sidebarRect.xMax + Gutter;
            float browserRight = _inspectorRect.x - Gutter;
            _browserRect = new Rect(browserLeft, browserTop, Mathf.Max(360f, browserRight - browserLeft), browserHeight);
        }

        private void LogLayout()
        {
            string signature = Screen.width + "x" + Screen.height
                + "|" + ComputeDocumentSignature(_snapshot != null ? _snapshot.ShellDocument : null)
                + "|" + ComputeDocumentSignature(_snapshot != null ? _snapshot.InspectorDocument : null)
                + "|" + ComputePageSignature();

            if (string.Equals(_lastDebugSignature, signature, StringComparison.Ordinal))
                return;

            _lastDebugSignature = signature;
            List<ScenarioAuthoringUiDebugService.LayoutRect> rects = new List<ScenarioAuthoringUiDebugService.LayoutRect>();
            rects.Add(ScenarioAuthoringUiDebugService.Capture("Header", _headerRect, "title/actions/tabs"));
            rects.Add(ScenarioAuthoringUiDebugService.Capture("Sidebar", _sidebarRect, "session/workflow/history"));
            rects.Add(ScenarioAuthoringUiDebugService.Capture("Inspector", _inspectorRect, "target/actions/summary"));
            rects.Add(ScenarioAuthoringUiDebugService.Capture("Browser", _browserRect, "tool summary/library"));
            if (_lastGridFirstCardRect.width > 0f && _lastGridFirstCardRect.height > 0f)
                rects.Add(ScenarioAuthoringUiDebugService.Capture("BrowserCard0", _lastGridFirstCardRect, "first candidate card"));
            _uiDebug.LogLayout(signature, rects);
        }

        private void DrawSurface(Rect rect, GUIStyle style, Action<Rect> drawContents)
        {
            GUI.Box(rect, GUIContent.none, style);
            Rect inner = new Rect(rect.x + 14f, rect.y + 14f, rect.width - 28f, rect.height - 28f);
            GUILayout.BeginArea(inner);
            drawContents(inner);
            GUILayout.EndArea();
        }

        private void DrawHeader(Rect inner)
        {
            ScenarioAuthoringInspectorDocument shell = _snapshot != null ? _snapshot.ShellDocument : null;
            ScenarioAuthoringInspectorSection toolsSection = FindSection(shell, "tools");
            ScenarioAuthoringInspectorSection statusSection = FindSection(shell, "status");

            GUILayout.BeginVertical();
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            GUILayout.Label("SHELTERED", _runtime.BrandStyle);
            GUILayout.Label("Scenario Editor", _runtime.TitleStyle);
            GUILayout.Label(shell != null ? shell.Subtitle ?? string.Empty : string.Empty, _runtime.SubtitleStyle);
            GUILayout.EndVertical();

            GUILayout.BeginHorizontal(GUILayout.Width(Mathf.Max(420f, inner.width * 0.48f)));
            DrawActionButtons(shell != null ? shell.HeaderActions : null, 112f, 40f, false, true);
            GUILayout.EndHorizontal();
            GUILayout.EndHorizontal();

            GUILayout.Space(8f);
            if (toolsSection != null)
            {
                List<ScenarioAuthoringInspectorAction> tabs = GetActions(toolsSection);
                GUILayout.BeginHorizontal();
                for (int i = 0; i < tabs.Count; i++)
                {
                    ScenarioAuthoringInspectorAction action = tabs[i];
                    DrawTabButton(action, GUILayout.Width(Mathf.Clamp(inner.width / 5f - 8f, 120f, 170f)), GUILayout.Height(34f));
                }
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }

            if (statusSection != null)
            {
                string status = JoinTexts(statusSection, 1);
                if (!string.IsNullOrEmpty(status))
                {
                    GUILayout.Space(8f);
                    GUILayout.Label(status, _runtime.StatusStyle);
                }
            }

            GUILayout.EndVertical();
        }

        private void DrawSidebar(Rect inner)
        {
            ScenarioAuthoringInspectorDocument shell = _snapshot != null ? _snapshot.ShellDocument : null;
            ScenarioAuthoringInspectorSection sessionSection = FindSection(shell, "session");
            ScenarioAuthoringInspectorSection workflowSection = FindSection(shell, "workflow");
            ScenarioAuthoringInspectorSection historySection = FindSection(shell, "history");
            ScenarioAuthoringInspectorSection toolSection = FindSection(shell, "tool");

            _sidebarScroll = GUILayout.BeginScrollView(_sidebarScroll, false, true);
            DrawSectionHeader("Scenario");
            DrawMetricSection(sessionSection, 2);
            DrawSectionHeader("Workflow");
            DrawActionStripSection(workflowSection, 1);
            DrawSectionHeader("History");
            DrawActionStripSection(historySection, 1);

            if (toolSection != null)
            {
                DrawSectionHeader(toolSection.Title ?? "Current Tool");
                DrawSummarySection(toolSection, 5, 2, false);
            }

            GUILayout.Space(6f);
            GUILayout.Label("Ctrl selects. F5 saves. F7 playtests. Ctrl+Z / Y / C / V / R drive asset editing.", _runtime.NoteStyle);
            GUILayout.EndScrollView();
        }

        private void DrawInspector(Rect inner)
        {
            ScenarioAuthoringInspectorDocument inspector = _snapshot != null ? _snapshot.InspectorDocument : null;
            ScenarioAuthoringInspectorSection targetSection = FindSection(inspector, "target");
            ScenarioAuthoringInspectorSection actionsSection = FindSection(inspector, "actions");
            ScenarioAuthoringInspectorSection selectionSection = FindSection(_snapshot != null ? _snapshot.ShellDocument : null, "selection");
            ScenarioAuthoringInspectorSection assetModeSection = FindSection(inspector, "asset_mode");
            ScenarioAuthoringInspectorSection summarySection = FindPrimaryAssetSummarySection(inspector);

            _inspectorScroll = GUILayout.BeginScrollView(_inspectorScroll, false, true);
            DrawSectionHeader(inspector != null ? inspector.Title ?? "Inspector" : "Inspector");
            if (targetSection != null)
                DrawSummarySection(targetSection, 6, 0, true);
            else
                GUILayout.Label("Hold Ctrl and click a target to inspect it.", _runtime.NoteStyle);

            if (actionsSection != null)
            {
                DrawSectionHeader("Actions");
                DrawActionStripSection(actionsSection, 1);
            }

            if (assetModeSection != null)
            {
                DrawSectionHeader("Asset Workflow");
                DrawActionStripSection(assetModeSection, 2);
            }

            if (summarySection != null)
            {
                DrawSectionHeader(summarySection.Title ?? "Summary");
                DrawSummarySection(summarySection, 4, 2, true);
            }

            if (selectionSection != null)
            {
                DrawSectionHeader("Selection");
                DrawMetricSection(selectionSection, 1);
            }
            GUILayout.EndScrollView();
        }

        private void DrawBrowser(Rect inner)
        {
            ScenarioAuthoringInspectorDocument shell = _snapshot != null ? _snapshot.ShellDocument : null;
            ScenarioAuthoringInspectorDocument inspector = _snapshot != null ? _snapshot.InspectorDocument : null;
            ScenarioAuthoringInspectorSection toolSection = FindSection(shell, "tool");
            ScenarioAuthoringInspectorSection assetModeSection = FindSection(inspector, "asset_mode");
            ScenarioAuthoringInspectorSection summarySection = FindPrimaryAssetSummarySection(inspector);
            ScenarioAuthoringInspectorSection candidateSection = FindPrimaryCandidateSection(inspector);
            ScenarioAuthoringInspectorSection candidateNoteSection = FindSecondaryCandidateSection(inspector, candidateSection);

            float summaryWidth = Mathf.Clamp(inner.width * 0.30f, 230f, 310f);
            Rect summaryRect = new Rect(0f, 0f, summaryWidth, inner.height);
            Rect libraryRect = new Rect(summaryRect.xMax + 12f, 0f, inner.width - summaryWidth - 12f, inner.height);

            GUI.Box(summaryRect, GUIContent.none, _runtime.SectionSurfaceStyle);
            GUILayout.BeginArea(new Rect(summaryRect.x + 10f, summaryRect.y + 10f, summaryRect.width - 20f, summaryRect.height - 20f));
            _browserSummaryScroll = GUILayout.BeginScrollView(_browserSummaryScroll, false, true);
            if (toolSection != null)
            {
                DrawSectionHeader(toolSection.Title ?? "Current Tool");
                DrawSummarySection(toolSection, 4, 2, false);
            }

            if (assetModeSection != null)
            {
                DrawSectionHeader("Mode");
                DrawActionStripSection(assetModeSection, 1);
            }

            if (summarySection != null)
            {
                DrawSectionHeader("Selection Summary");
                DrawSummarySection(summarySection, 4, 1, false);
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();

            DrawCandidateBrowser(libraryRect, candidateSection, candidateNoteSection);
        }

        private void DrawCandidateBrowser(Rect rect, ScenarioAuthoringInspectorSection candidateSection, ScenarioAuthoringInspectorSection noteSection)
        {
            GUI.Box(rect, GUIContent.none, _runtime.SectionSurfaceStyle);
            Rect inner = new Rect(rect.x + 12f, rect.y + 12f, rect.width - 24f, rect.height - 24f);

            string title = candidateSection != null ? candidateSection.Title : "Library";
            GUI.Label(new Rect(inner.x, inner.y, inner.width * 0.60f, 24f), title ?? "Library", _runtime.SectionTitleStyle);

            List<ScenarioAuthoringInspectorAction> actions = GetActions(candidateSection);
            int columns = Mathf.Max(2, Mathf.FloorToInt((inner.width - 24f) / 176f));
            int rows = Mathf.Max(1, Mathf.FloorToInt((inner.height - 58f) / 102f));
            int pageSize = Mathf.Max(1, columns * rows);
            int pageCount = Mathf.Max(1, Mathf.CeilToInt(actions.Count / (float)pageSize));
            int page = GetPage(candidateSection != null ? candidateSection.Id : "browser", pageCount);

            Rect headerRight = new Rect(inner.x + inner.width - 230f, inner.y, 230f, 26f);
            DrawPageHeader(headerRight, page, pageCount, candidateSection != null ? candidateSection.Id : "browser");

            Rect gridRect = new Rect(inner.x, inner.y + 34f, inner.width, inner.height - 34f);
            _lastGridFirstCardRect = Rect.zero;

            if (actions.Count == 0)
            {
                GUI.Label(new Rect(gridRect.x, gridRect.y, gridRect.width, 56f),
                    noteSection != null ? JoinTexts(noteSection, 2) : "Select a target to browse compatible sprite previews.",
                    _runtime.NoteStyle);
                return;
            }

            float cardWidth = (gridRect.width - ((columns - 1) * 12f)) / columns;
            float cardHeight = Mathf.Clamp((gridRect.height - ((rows - 1) * 12f)) / rows, 88f, 118f);
            int startIndex = page * pageSize;
            int endIndex = Mathf.Min(actions.Count, startIndex + pageSize);

            for (int i = startIndex; i < endIndex; i++)
            {
                int localIndex = i - startIndex;
                int column = localIndex % columns;
                int row = localIndex / columns;
                Rect cardRect = new Rect(
                    gridRect.x + (column * (cardWidth + 12f)),
                    gridRect.y + (row * (cardHeight + 12f)),
                    cardWidth,
                    cardHeight);

                if (_lastGridFirstCardRect.width <= 0f)
                    _lastGridFirstCardRect = ToBrowserScreenRect(cardRect);

                DrawCandidateCard(cardRect, actions[i]);
            }
        }

        private void DrawPageHeader(Rect rect, int page, int pageCount, string sectionId)
        {
            GUILayout.BeginArea(rect);
            GUILayout.BeginHorizontal();
            GUI.enabled = page > 0;
            if (GUILayout.Button("Prev", _runtime.MiniButtonStyle, GUILayout.Width(56f), GUILayout.Height(24f)))
                SetPage(sectionId, page - 1);
            GUI.enabled = true;

            GUILayout.Label((page + 1) + " / " + pageCount, _runtime.BadgeStyle, GUILayout.Width(62f));
            GUI.enabled = page < pageCount - 1;
            if (GUILayout.Button("Next", _runtime.MiniButtonStyle, GUILayout.Width(56f), GUILayout.Height(24f)))
                SetPage(sectionId, page + 1);
            GUI.enabled = true;
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private void DrawHover(Rect inner, ScenarioAuthoringInspectorDocument document)
        {
            _hoverScroll = GUILayout.BeginScrollView(_hoverScroll, false, true);
            GUILayout.Label(document != null ? document.Title ?? string.Empty : string.Empty, _runtime.SectionTitleStyle);
            if (document != null && !string.IsNullOrEmpty(document.Subtitle))
                GUILayout.Label(document.Subtitle, _runtime.CaptionStyle);
            DrawPropertySection(FindSection(document, "hover"), 6);
            GUILayout.EndScrollView();
        }

        private void DrawSectionHeader(string title)
        {
            if (string.IsNullOrEmpty(title))
                return;

            GUILayout.Space(4f);
            GUILayout.Label(title, _runtime.SectionTitleStyle);
            GUILayout.Space(4f);
        }

        private void DrawMetricSection(ScenarioAuthoringInspectorSection section, int columns)
        {
            List<ScenarioAuthoringInspectorItem> properties = GetProperties(section);
            if (properties.Count == 0)
            {
                GUILayout.Label("No metrics available.", _runtime.NoteStyle);
                return;
            }

            for (int i = 0; i < properties.Count; i += columns)
            {
                GUILayout.BeginHorizontal();
                for (int column = 0; column < columns; column++)
                {
                    int index = i + column;
                    if (index >= properties.Count)
                    {
                        GUILayout.FlexibleSpace();
                        continue;
                    }

                    DrawMetricTile(properties[index]);
                }
                GUILayout.EndHorizontal();
                GUILayout.Space(6f);
            }
        }

        private void DrawMetricTile(ScenarioAuthoringInspectorItem item)
        {
            GUILayout.BeginVertical(_runtime.MetricTileStyle, GUILayout.ExpandWidth(true), GUILayout.MinHeight(58f));
            GUILayout.Label(item != null ? item.Label ?? string.Empty : string.Empty, _runtime.MetricLabelStyle);
            GUILayout.Label(Shorten(item != null ? item.Value : null, 22), _runtime.MetricValueStyle);
            GUILayout.EndVertical();
        }

        private void DrawActionStripSection(ScenarioAuthoringInspectorSection section, int columns)
        {
            List<ScenarioAuthoringInspectorItem> properties = GetProperties(section);
            if (properties.Count > 0)
                DrawPropertyRows(properties, Math.Max(2, columns * 2), 3);

            List<ScenarioAuthoringInspectorAction> actions = GetActions(section);
            if (actions.Count == 0)
            {
                string notes = JoinTexts(section, 2);
                if (!string.IsNullOrEmpty(notes))
                    GUILayout.Label(notes, _runtime.NoteStyle);
                return;
            }

            for (int i = 0; i < actions.Count; i += columns)
            {
                GUILayout.BeginHorizontal();
                for (int column = 0; column < columns; column++)
                {
                    int index = i + column;
                    if (index >= actions.Count)
                    {
                        GUILayout.FlexibleSpace();
                        continue;
                    }

                    DrawActionButton(actions[index], 0f, 34f, false, false);
                }
                GUILayout.EndHorizontal();
                GUILayout.Space(6f);
            }

            string text = JoinTexts(section, 2);
            if (!string.IsNullOrEmpty(text))
                GUILayout.Label(text, _runtime.NoteStyle);
        }

        private void DrawSummarySection(ScenarioAuthoringInspectorSection section, int maxProperties, int maxNotes, bool emphasizePreview)
        {
            if (section == null)
            {
                GUILayout.Label("No summary available.", _runtime.NoteStyle);
                return;
            }

            ScenarioAuthoringInspectorItem previewItem = FindPreviewItem(section);
            if (previewItem != null)
                DrawPreviewBlock(previewItem, emphasizePreview);

            List<ScenarioAuthoringInspectorItem> properties = GetProperties(section);
            if (properties.Count > 0)
                DrawPropertyRows(properties, 2, maxProperties);

            List<ScenarioAuthoringInspectorAction> actions = GetActions(section);
            if (actions.Count > 0)
            {
                GUILayout.Space(6f);
                DrawActionButtons(actions.ToArray(), 0f, 32f, false, false);
            }

            string notes = JoinTexts(section, maxNotes);
            if (!string.IsNullOrEmpty(notes))
            {
                GUILayout.Space(6f);
                GUILayout.Label(notes, _runtime.NoteStyle);
            }
        }

        private void DrawPropertySection(ScenarioAuthoringInspectorSection section, int maxProperties)
        {
            if (section == null)
                return;

            List<ScenarioAuthoringInspectorItem> properties = GetProperties(section);
            if (properties.Count > 0)
                DrawPropertyRows(properties, 1, maxProperties);

            string notes = JoinTexts(section, 4);
            if (!string.IsNullOrEmpty(notes))
                GUILayout.Label(notes, _runtime.NoteStyle);
        }

        private void DrawPropertyRows(List<ScenarioAuthoringInspectorItem> properties, int columns, int maxProperties)
        {
            int total = Mathf.Min(maxProperties, properties.Count);
            for (int i = 0; i < total; i += columns)
            {
                GUILayout.BeginHorizontal();
                for (int column = 0; column < columns; column++)
                {
                    int index = i + column;
                    if (index >= total)
                    {
                        GUILayout.FlexibleSpace();
                        continue;
                    }

                    ScenarioAuthoringInspectorItem item = properties[index];
                    GUILayout.BeginVertical(_runtime.PropertyRowStyle, GUILayout.ExpandWidth(true));
                    GUILayout.Label(item.Label ?? string.Empty, _runtime.PropertyKeyStyle);
                    GUILayout.Label(Shorten(item.Value, columns == 1 ? 56 : 24), _runtime.PropertyValueStyle);
                    GUILayout.EndVertical();
                }
                GUILayout.EndHorizontal();
                GUILayout.Space(6f);
            }
        }

        private void DrawPreviewBlock(ScenarioAuthoringInspectorItem item, bool emphasized)
        {
            GUILayout.BeginVertical(_runtime.PreviewContainerStyle);
            Rect rowRect = GUILayoutUtility.GetRect(100f, 90f, GUILayout.ExpandWidth(true));
            Rect previewRect = new Rect(rowRect.x + 6f, rowRect.y + 6f, 84f, rowRect.height - 12f);
            DrawSpritePreview(previewRect, item.PreviewSprite, emphasized);

            Rect textRect = new Rect(previewRect.xMax + 12f, rowRect.y + 6f, rowRect.width - previewRect.width - 18f, rowRect.height - 12f);
            GUI.Label(new Rect(textRect.x, textRect.y, textRect.width, 24f), item.Value ?? string.Empty, _runtime.CardTitleStyle);

            string detail = !string.IsNullOrEmpty(item.Detail) ? item.Detail : item.Label;
            if (!string.IsNullOrEmpty(detail))
                GUI.Label(new Rect(textRect.x, textRect.y + 26f, textRect.width, 20f), Shorten(detail, 48), _runtime.CardDetailStyle);

            if (!string.IsNullOrEmpty(item.Badge))
            {
                Vector2 badgeSize = _runtime.BadgeStyle.CalcSize(new GUIContent(item.Badge));
                Rect badgeRect = new Rect(textRect.x, textRect.y + 52f, Mathf.Max(60f, badgeSize.x + 18f), 22f);
                GUI.Box(badgeRect, item.Badge, _runtime.BadgeBoxStyle);
            }
            GUILayout.EndVertical();
        }

        private void DrawCandidateCard(Rect rect, ScenarioAuthoringInspectorAction action)
        {
            bool clicked = GUI.Button(rect, GUIContent.none, action != null && action.Emphasized
                ? _runtime.ActiveCardButtonStyle
                : _runtime.CardButtonStyle);

            if (clicked && action != null && action.Enabled)
                ExecuteAction(action);

            DrawSpritePreview(new Rect(rect.x + 8f, rect.y + 8f, 72f, rect.height - 16f), action != null ? action.PreviewSprite : null, action != null && action.Emphasized);

            Rect contentRect = new Rect(rect.x + 90f, rect.y + 8f, rect.width - 98f, rect.height - 16f);
            GUI.Label(new Rect(contentRect.x, contentRect.y, contentRect.width, 22f), action != null ? action.Label ?? string.Empty : string.Empty, _runtime.CardTitleStyle);

            string detail = action != null
                ? (!string.IsNullOrEmpty(action.Detail) ? action.Detail : action.Hint)
                : null;
            if (!string.IsNullOrEmpty(detail))
                GUI.Label(new Rect(contentRect.x, contentRect.y + 24f, contentRect.width, 32f), Shorten(detail, 58), _runtime.CardDetailStyle);

            if (action != null && !string.IsNullOrEmpty(action.Badge))
            {
                Vector2 badgeSize = _runtime.BadgeStyle.CalcSize(new GUIContent(action.Badge));
                Rect badgeRect = new Rect(contentRect.x, rect.y + rect.height - 30f, Mathf.Max(56f, badgeSize.x + 18f), 22f);
                GUI.Box(badgeRect, action.Badge, _runtime.BadgeBoxStyle);
            }
        }

        private void DrawSpritePreview(Rect rect, Sprite sprite, bool emphasized)
        {
            GUI.Box(rect, GUIContent.none, emphasized ? _runtime.ActivePreviewStyle : _runtime.PreviewStyle);
            if (sprite == null || sprite.texture == null)
            {
                GUI.Label(rect, "No Sprite", _runtime.EmptyPreviewStyle);
                return;
            }

            Rect textureRect = sprite.textureRect;
            Texture2D texture = sprite.texture;
            Rect uv = new Rect(
                textureRect.x / texture.width,
                textureRect.y / texture.height,
                textureRect.width / texture.width,
                textureRect.height / texture.height);

            Rect fitted = FitRect(rect, textureRect.width, textureRect.height, 6f);
            GUI.DrawTextureWithTexCoords(fitted, texture, uv, true);
        }

        private void DrawActionButtons(ScenarioAuthoringInspectorAction[] actions, float width, float height, bool compact, bool headerMode)
        {
            for (int i = 0; actions != null && i < actions.Length; i++)
            {
                ScenarioAuthoringInspectorAction action = actions[i];
                if (action == null)
                    continue;

                DrawActionButton(action, width, height, compact, headerMode);
                if (i < actions.Length - 1)
                    GUILayout.Space(compact ? 6f : 8f);
            }
        }

        private void DrawActionButton(ScenarioAuthoringInspectorAction action, float width, float height, bool compact, bool headerMode)
        {
            if (action == null)
                return;

            GUI.enabled = action.Enabled;
            GUIStyle style = ResolveActionStyle(action, compact, headerMode);
            string label = BuildActionLabel(action, compact);

            List<GUILayoutOption> options = new List<GUILayoutOption>();
            if (width > 0f)
                options.Add(GUILayout.Width(width));
            if (height > 0f)
                options.Add(GUILayout.Height(height));
            if (!compact && width <= 0f)
                options.Add(GUILayout.ExpandWidth(true));

            if (GUILayout.Button(new GUIContent(label, action.Hint ?? string.Empty), style, options.ToArray()))
                ExecuteAction(action);

            GUI.enabled = true;
        }

        private void DrawTabButton(ScenarioAuthoringInspectorAction action, params GUILayoutOption[] options)
        {
            if (action == null)
                return;

            GUI.enabled = action.Enabled;
            GUIStyle style = action.Emphasized ? _runtime.ActiveTabButtonStyle : _runtime.TabButtonStyle;
            if (GUILayout.Button(new GUIContent(BuildActionLabel(action, false), action.Hint ?? string.Empty), style, options))
                ExecuteAction(action);
            GUI.enabled = true;
        }

        private GUIStyle ResolveActionStyle(ScenarioAuthoringInspectorAction action, bool compact, bool headerMode)
        {
            if (headerMode)
                return action.Emphasized ? _runtime.HeaderPrimaryButtonStyle : _runtime.HeaderButtonStyle;
            if (compact)
                return action.Emphasized ? _runtime.PrimaryCompactButtonStyle : _runtime.CompactButtonStyle;
            return action.Emphasized ? _runtime.PrimaryButtonStyle : _runtime.ButtonStyle;
        }

        private void ExecuteAction(ScenarioAuthoringInspectorAction action)
        {
            if (action == null || string.IsNullOrEmpty(action.Id))
                return;

            bool changed = ScenarioAuthoringBackendService.Instance.ExecuteAction(action.Id);
            if (changed)
                ScenarioAuthoringBackendService.Instance.Refresh();
        }

        private int GetPage(string sectionId, int pageCount)
        {
            if (string.IsNullOrEmpty(sectionId))
                return 0;

            int page;
            if (!_sectionPages.TryGetValue(sectionId, out page))
                page = 0;

            page = Mathf.Clamp(page, 0, Math.Max(0, pageCount - 1));
            _sectionPages[sectionId] = page;
            return page;
        }

        private void SetPage(string sectionId, int page)
        {
            if (string.IsNullOrEmpty(sectionId))
                return;

            _sectionPages[sectionId] = Math.Max(0, page);
            _lastDebugSignature = null;
        }

        private static ScenarioAuthoringInspectorSection FindSection(ScenarioAuthoringInspectorDocument document, string id)
        {
            if (document == null || string.IsNullOrEmpty(id) || document.Sections == null)
                return null;

            for (int i = 0; i < document.Sections.Length; i++)
            {
                ScenarioAuthoringInspectorSection section = document.Sections[i];
                if (section != null && string.Equals(section.Id, id, StringComparison.OrdinalIgnoreCase))
                    return section;
            }

            return null;
        }

        private static ScenarioAuthoringInspectorSection FindPrimaryAssetSummarySection(ScenarioAuthoringInspectorDocument document)
        {
            ScenarioAuthoringInspectorSection spriteSwap = FindSection(document, "sprite_swap");
            if (spriteSwap != null)
                return spriteSwap;

            return FindSection(document, "scene_sprite");
        }

        private static ScenarioAuthoringInspectorSection FindPrimaryCandidateSection(ScenarioAuthoringInspectorDocument document)
        {
            if (document == null || document.Sections == null)
                return null;

            ScenarioAuthoringInspectorSection firstCandidate = null;
            for (int i = 0; i < document.Sections.Length; i++)
            {
                ScenarioAuthoringInspectorSection section = document.Sections[i];
                if (section == null || section.Layout != ScenarioAuthoringInspectorSectionLayout.CandidateGrid)
                    continue;

                if (firstCandidate == null)
                    firstCandidate = section;

                if (GetActions(section).Count > 0)
                    return section;
            }

            return firstCandidate;
        }

        private static ScenarioAuthoringInspectorSection FindSecondaryCandidateSection(ScenarioAuthoringInspectorDocument document, ScenarioAuthoringInspectorSection primary)
        {
            if (document == null || document.Sections == null)
                return null;

            for (int i = 0; i < document.Sections.Length; i++)
            {
                ScenarioAuthoringInspectorSection section = document.Sections[i];
                if (section == null
                    || section == primary
                    || section.Layout != ScenarioAuthoringInspectorSectionLayout.CandidateGrid)
                {
                    continue;
                }

                return section;
            }

            return null;
        }

        private static ScenarioAuthoringInspectorItem FindPreviewItem(ScenarioAuthoringInspectorSection section)
        {
            if (section == null || section.Items == null)
                return null;

            for (int i = 0; i < section.Items.Length; i++)
            {
                ScenarioAuthoringInspectorItem item = section.Items[i];
                if (item != null && (item.PreviewSprite != null || !string.IsNullOrEmpty(item.Badge) || !string.IsNullOrEmpty(item.Detail)))
                    return item;
            }

            return null;
        }

        private static List<ScenarioAuthoringInspectorAction> GetActions(ScenarioAuthoringInspectorSection section)
        {
            List<ScenarioAuthoringInspectorAction> actions = new List<ScenarioAuthoringInspectorAction>();
            for (int i = 0; section != null && section.Items != null && i < section.Items.Length; i++)
            {
                ScenarioAuthoringInspectorItem item = section.Items[i];
                if (item != null && item.Kind == ScenarioAuthoringInspectorItemKind.Action && item.Action != null)
                    actions.Add(item.Action);
            }

            return actions;
        }

        private static List<ScenarioAuthoringInspectorItem> GetProperties(ScenarioAuthoringInspectorSection section)
        {
            List<ScenarioAuthoringInspectorItem> properties = new List<ScenarioAuthoringInspectorItem>();
            for (int i = 0; section != null && section.Items != null && i < section.Items.Length; i++)
            {
                ScenarioAuthoringInspectorItem item = section.Items[i];
                if (item != null && item.Kind == ScenarioAuthoringInspectorItemKind.Property)
                    properties.Add(item);
            }

            return properties;
        }

        private static string JoinTexts(ScenarioAuthoringInspectorSection section, int maxTexts)
        {
            if (section == null || section.Items == null || maxTexts <= 0)
                return string.Empty;

            List<string> values = new List<string>();
            for (int i = 0; i < section.Items.Length && values.Count < maxTexts; i++)
            {
                ScenarioAuthoringInspectorItem item = section.Items[i];
                if (item != null
                    && item.Kind == ScenarioAuthoringInspectorItemKind.Text
                    && item.PreviewSprite == null
                    && string.IsNullOrEmpty(item.Badge)
                    && string.IsNullOrEmpty(item.Detail)
                    && !string.IsNullOrEmpty(item.Value))
                {
                    values.Add(item.Value);
                }
            }

            return values.Count > 0 ? string.Join("\n", values.ToArray()) : string.Empty;
        }

        private static string BuildActionLabel(ScenarioAuthoringInspectorAction action, bool compact)
        {
            if (action == null)
                return string.Empty;

            string label = action.Label ?? string.Empty;
            if (string.IsNullOrEmpty(action.IconText))
                return label;

            return compact ? action.IconText : ("[" + action.IconText + "] " + label);
        }

        private static string Shorten(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
                return value ?? string.Empty;

            return value.Substring(0, Math.Max(0, maxLength - 3)) + "...";
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

        private Rect ToBrowserScreenRect(Rect localRect)
        {
            return new Rect(
                _browserRect.x + 14f + localRect.x,
                _browserRect.y + 14f + localRect.y,
                localRect.width,
                localRect.height);
        }

        private Rect BuildHoverRect(ScenarioAuthoringInspectorDocument document)
        {
            Vector2 mouse = EventCompatibleMousePosition();
            float width = 320f;
            float height = EstimateHoverHeight(document);
            float x = Mathf.Clamp(mouse.x + 22f, 12f, Screen.width - width - 12f);
            float y = Mathf.Clamp(mouse.y + 22f, 12f, Screen.height - height - 12f);
            return new Rect(x, y, width, height);
        }

        private static float EstimateHoverHeight(ScenarioAuthoringInspectorDocument document)
        {
            if (document == null)
                return 140f;

            int lineCount = 4;
            if (document.Sections != null)
            {
                for (int i = 0; i < document.Sections.Length; i++)
                {
                    ScenarioAuthoringInspectorSection section = document.Sections[i];
                    if (section != null && section.Items != null)
                        lineCount += section.Items.Length;
                }
            }

            return Mathf.Clamp(120f + (lineCount * 18f), 140f, 280f);
        }

        private static string ComputeDocumentSignature(ScenarioAuthoringInspectorDocument document)
        {
            if (document == null)
                return "<null>";

            StringBuilder builder = new StringBuilder();
            builder.Append(document.Title).Append("|").Append(document.Subtitle);
            for (int i = 0; document.HeaderActions != null && i < document.HeaderActions.Length; i++)
            {
                ScenarioAuthoringInspectorAction action = document.HeaderActions[i];
                if (action == null)
                    continue;

                builder.Append("|H:")
                    .Append(action.Id)
                    .Append(":")
                    .Append(action.Label)
                    .Append(":")
                    .Append(action.Enabled)
                    .Append(":")
                    .Append(action.Emphasized);
            }

            for (int i = 0; document.Sections != null && i < document.Sections.Length; i++)
            {
                ScenarioAuthoringInspectorSection section = document.Sections[i];
                if (section == null)
                    continue;

                builder.Append("|S:")
                    .Append(section.Id)
                    .Append(":")
                    .Append(section.Title)
                    .Append(":")
                    .Append((int)section.Layout);

                for (int j = 0; section.Items != null && j < section.Items.Length; j++)
                {
                    ScenarioAuthoringInspectorItem item = section.Items[j];
                    if (item == null)
                        continue;

                    builder.Append("|I:")
                        .Append((int)item.Kind)
                        .Append(":")
                        .Append(item.Label)
                        .Append(":")
                        .Append(item.Value)
                        .Append(":")
                        .Append(item.Detail)
                        .Append(":")
                        .Append(item.Badge);
                    if (item.Action != null)
                        builder.Append(":A=").Append(item.Action.Id).Append(":").Append(item.Action.Label);
                }
            }

            return builder.ToString();
        }

        private string ComputePageSignature()
        {
            if (_sectionPages.Count == 0)
                return "<pages>";

            List<string> keys = new List<string>(_sectionPages.Keys);
            keys.Sort(StringComparer.OrdinalIgnoreCase);
            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < keys.Count; i++)
            {
                if (i > 0)
                    builder.Append("|");
                builder.Append(keys[i]).Append("=").Append(_sectionPages[keys[i]]);
            }

            return builder.ToString();
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
            private Texture2D _headerTexture;
            private Texture2D _sidebarTexture;
            private Texture2D _inspectorTexture;
            private Texture2D _browserTexture;
            private Texture2D _hoverTexture;
            private Texture2D _sectionTexture;
            private Texture2D _buttonTexture;
            private Texture2D _buttonActiveTexture;
            private Texture2D _buttonPrimaryTexture;
            private Texture2D _tabTexture;
            private Texture2D _tabActiveTexture;
            private Texture2D _metricTexture;
            private Texture2D _cardTexture;
            private Texture2D _cardActiveTexture;
            private Texture2D _previewTexture;
            private Texture2D _previewActiveTexture;
            private Texture2D _badgeTexture;
            private GUIStyle _headerSurfaceStyle;
            private GUIStyle _sidebarSurfaceStyle;
            private GUIStyle _inspectorSurfaceStyle;
            private GUIStyle _browserSurfaceStyle;
            private GUIStyle _hoverSurfaceStyle;
            private GUIStyle _sectionSurfaceStyle;
            private GUIStyle _brandStyle;
            private GUIStyle _titleStyle;
            private GUIStyle _subtitleStyle;
            private GUIStyle _statusStyle;
            private GUIStyle _sectionTitleStyle;
            private GUIStyle _noteStyle;
            private GUIStyle _captionStyle;
            private GUIStyle _buttonStyle;
            private GUIStyle _primaryButtonStyle;
            private GUIStyle _compactButtonStyle;
            private GUIStyle _primaryCompactButtonStyle;
            private GUIStyle _headerButtonStyle;
            private GUIStyle _headerPrimaryButtonStyle;
            private GUIStyle _tabButtonStyle;
            private GUIStyle _activeTabButtonStyle;
            private GUIStyle _metricTileStyle;
            private GUIStyle _metricLabelStyle;
            private GUIStyle _metricValueStyle;
            private GUIStyle _propertyRowStyle;
            private GUIStyle _propertyKeyStyle;
            private GUIStyle _propertyValueStyle;
            private GUIStyle _previewContainerStyle;
            private GUIStyle _previewStyle;
            private GUIStyle _activePreviewStyle;
            private GUIStyle _emptyPreviewStyle;
            private GUIStyle _cardButtonStyle;
            private GUIStyle _activeCardButtonStyle;
            private GUIStyle _cardTitleStyle;
            private GUIStyle _cardDetailStyle;
            private GUIStyle _badgeStyle;
            private GUIStyle _badgeBoxStyle;
            private GUIStyle _miniButtonStyle;
            private bool _stylesReady;

            public GUIStyle HeaderSurfaceStyle { get { return _headerSurfaceStyle; } }
            public GUIStyle SidebarSurfaceStyle { get { return _sidebarSurfaceStyle; } }
            public GUIStyle InspectorSurfaceStyle { get { return _inspectorSurfaceStyle; } }
            public GUIStyle BrowserSurfaceStyle { get { return _browserSurfaceStyle; } }
            public GUIStyle HoverSurfaceStyle { get { return _hoverSurfaceStyle; } }
            public GUIStyle SectionSurfaceStyle { get { return _sectionSurfaceStyle; } }
            public GUIStyle BrandStyle { get { return _brandStyle; } }
            public GUIStyle TitleStyle { get { return _titleStyle; } }
            public GUIStyle SubtitleStyle { get { return _subtitleStyle; } }
            public GUIStyle StatusStyle { get { return _statusStyle; } }
            public GUIStyle SectionTitleStyle { get { return _sectionTitleStyle; } }
            public GUIStyle NoteStyle { get { return _noteStyle; } }
            public GUIStyle CaptionStyle { get { return _captionStyle; } }
            public GUIStyle ButtonStyle { get { return _buttonStyle; } }
            public GUIStyle PrimaryButtonStyle { get { return _primaryButtonStyle; } }
            public GUIStyle CompactButtonStyle { get { return _compactButtonStyle; } }
            public GUIStyle PrimaryCompactButtonStyle { get { return _primaryCompactButtonStyle; } }
            public GUIStyle HeaderButtonStyle { get { return _headerButtonStyle; } }
            public GUIStyle HeaderPrimaryButtonStyle { get { return _headerPrimaryButtonStyle; } }
            public GUIStyle TabButtonStyle { get { return _tabButtonStyle; } }
            public GUIStyle ActiveTabButtonStyle { get { return _activeTabButtonStyle; } }
            public GUIStyle MetricTileStyle { get { return _metricTileStyle; } }
            public GUIStyle MetricLabelStyle { get { return _metricLabelStyle; } }
            public GUIStyle MetricValueStyle { get { return _metricValueStyle; } }
            public GUIStyle PropertyRowStyle { get { return _propertyRowStyle; } }
            public GUIStyle PropertyKeyStyle { get { return _propertyKeyStyle; } }
            public GUIStyle PropertyValueStyle { get { return _propertyValueStyle; } }
            public GUIStyle PreviewContainerStyle { get { return _previewContainerStyle; } }
            public GUIStyle PreviewStyle { get { return _previewStyle; } }
            public GUIStyle ActivePreviewStyle { get { return _activePreviewStyle; } }
            public GUIStyle EmptyPreviewStyle { get { return _emptyPreviewStyle; } }
            public GUIStyle CardButtonStyle { get { return _cardButtonStyle; } }
            public GUIStyle ActiveCardButtonStyle { get { return _activeCardButtonStyle; } }
            public GUIStyle CardTitleStyle { get { return _cardTitleStyle; } }
            public GUIStyle CardDetailStyle { get { return _cardDetailStyle; } }
            public GUIStyle BadgeStyle { get { return _badgeStyle; } }
            public GUIStyle BadgeBoxStyle { get { return _badgeBoxStyle; } }
            public GUIStyle MiniButtonStyle { get { return _miniButtonStyle; } }

            public void Initialize(ScenarioAuthoringImguiRenderModule owner)
            {
                _owner = owner;
                name = RuntimeObjectName;
                DontDestroyOnLoad(gameObject);
            }

            public void EnsureStyles()
            {
                if (_stylesReady)
                    return;

                _headerTexture = MakeTexture(new Color(0.10f, 0.08f, 0.06f, 0.98f));
                _sidebarTexture = MakeTexture(new Color(0.07f, 0.07f, 0.07f, 0.96f));
                _inspectorTexture = MakeTexture(new Color(0.06f, 0.06f, 0.06f, 0.97f));
                _browserTexture = MakeTexture(new Color(0.09f, 0.08f, 0.07f, 0.98f));
                _hoverTexture = MakeTexture(new Color(0.06f, 0.06f, 0.07f, 0.98f));
                _sectionTexture = MakeTexture(new Color(0.13f, 0.11f, 0.09f, 0.98f));
                _buttonTexture = MakeTexture(new Color(0.20f, 0.17f, 0.13f, 1f));
                _buttonActiveTexture = MakeTexture(new Color(0.26f, 0.23f, 0.18f, 1f));
                _buttonPrimaryTexture = MakeTexture(new Color(0.17f, 0.32f, 0.49f, 1f));
                _tabTexture = MakeTexture(new Color(0.15f, 0.13f, 0.10f, 1f));
                _tabActiveTexture = MakeTexture(new Color(0.19f, 0.31f, 0.47f, 1f));
                _metricTexture = MakeTexture(new Color(0.15f, 0.12f, 0.09f, 1f));
                _cardTexture = MakeTexture(new Color(0.18f, 0.15f, 0.12f, 1f));
                _cardActiveTexture = MakeTexture(new Color(0.24f, 0.29f, 0.20f, 1f));
                _previewTexture = MakeTexture(new Color(0.08f, 0.08f, 0.09f, 1f));
                _previewActiveTexture = MakeTexture(new Color(0.16f, 0.21f, 0.14f, 1f));
                _badgeTexture = MakeTexture(new Color(0.42f, 0.33f, 0.20f, 1f));

                _headerSurfaceStyle = BuildSurface(_headerTexture, 18);
                _sidebarSurfaceStyle = BuildSurface(_sidebarTexture, 14);
                _inspectorSurfaceStyle = BuildSurface(_inspectorTexture, 14);
                _browserSurfaceStyle = BuildSurface(_browserTexture, 14);
                _hoverSurfaceStyle = BuildSurface(_hoverTexture, 12);
                _sectionSurfaceStyle = BuildSurface(_sectionTexture, 10);

                _brandStyle = new GUIStyle(GUI.skin.label);
                _brandStyle.normal.textColor = new Color(0.95f, 0.78f, 0.42f, 1f);
                _brandStyle.fontSize = 28;
                _brandStyle.fontStyle = FontStyle.Bold;

                _titleStyle = new GUIStyle(GUI.skin.label);
                _titleStyle.normal.textColor = new Color(0.97f, 0.93f, 0.84f, 1f);
                _titleStyle.fontSize = 22;
                _titleStyle.fontStyle = FontStyle.Bold;

                _subtitleStyle = new GUIStyle(GUI.skin.label);
                _subtitleStyle.normal.textColor = new Color(0.82f, 0.79f, 0.73f, 1f);
                _subtitleStyle.fontSize = 13;

                _statusStyle = new GUIStyle(GUI.skin.box);
                _statusStyle.normal.background = MakeTexture(new Color(0.16f, 0.14f, 0.11f, 1f));
                _statusStyle.normal.textColor = new Color(0.95f, 0.87f, 0.72f, 1f);
                _statusStyle.padding = new RectOffset(10, 10, 6, 6);
                _statusStyle.alignment = TextAnchor.MiddleLeft;
                _statusStyle.fontSize = 12;
                _statusStyle.wordWrap = true;

                _sectionTitleStyle = new GUIStyle(GUI.skin.label);
                _sectionTitleStyle.normal.textColor = new Color(0.96f, 0.82f, 0.48f, 1f);
                _sectionTitleStyle.fontSize = 16;
                _sectionTitleStyle.fontStyle = FontStyle.Bold;

                _noteStyle = new GUIStyle(GUI.skin.label);
                _noteStyle.normal.textColor = new Color(0.86f, 0.84f, 0.80f, 1f);
                _noteStyle.wordWrap = true;
                _noteStyle.fontSize = 12;

                _captionStyle = new GUIStyle(GUI.skin.label);
                _captionStyle.normal.textColor = new Color(0.74f, 0.72f, 0.68f, 1f);
                _captionStyle.fontSize = 11;

                _buttonStyle = BuildButton(_buttonTexture, new Color(0.97f, 0.93f, 0.86f, 1f), 12);
                _primaryButtonStyle = BuildButton(_buttonPrimaryTexture, Color.white, 12);
                _compactButtonStyle = BuildButton(_buttonTexture, new Color(0.96f, 0.92f, 0.86f, 1f), 11);
                _primaryCompactButtonStyle = BuildButton(_buttonPrimaryTexture, Color.white, 11);
                _headerButtonStyle = BuildButton(_buttonTexture, new Color(0.96f, 0.93f, 0.86f, 1f), 12);
                _headerPrimaryButtonStyle = BuildButton(_buttonPrimaryTexture, Color.white, 12);
                _tabButtonStyle = BuildButton(_tabTexture, new Color(0.95f, 0.91f, 0.84f, 1f), 12);
                _activeTabButtonStyle = BuildButton(_tabActiveTexture, Color.white, 12);

                _metricTileStyle = BuildSurface(_metricTexture, 10);
                _metricLabelStyle = new GUIStyle(GUI.skin.label);
                _metricLabelStyle.normal.textColor = new Color(0.80f, 0.74f, 0.66f, 1f);
                _metricLabelStyle.fontSize = 11;
                _metricLabelStyle.alignment = TextAnchor.UpperLeft;

                _metricValueStyle = new GUIStyle(GUI.skin.label);
                _metricValueStyle.normal.textColor = new Color(0.98f, 0.96f, 0.92f, 1f);
                _metricValueStyle.fontSize = 16;
                _metricValueStyle.fontStyle = FontStyle.Bold;
                _metricValueStyle.alignment = TextAnchor.MiddleLeft;

                _propertyRowStyle = BuildSurface(MakeTexture(new Color(0.11f, 0.10f, 0.08f, 1f)), 8);
                _propertyKeyStyle = new GUIStyle(GUI.skin.label);
                _propertyKeyStyle.normal.textColor = new Color(0.79f, 0.73f, 0.66f, 1f);
                _propertyKeyStyle.fontSize = 11;

                _propertyValueStyle = new GUIStyle(GUI.skin.label);
                _propertyValueStyle.normal.textColor = new Color(0.96f, 0.94f, 0.90f, 1f);
                _propertyValueStyle.fontSize = 12;
                _propertyValueStyle.wordWrap = true;

                _previewContainerStyle = BuildSurface(MakeTexture(new Color(0.12f, 0.10f, 0.08f, 1f)), 8);
                _previewStyle = BuildSurface(_previewTexture, 4);
                _activePreviewStyle = BuildSurface(_previewActiveTexture, 4);
                _emptyPreviewStyle = new GUIStyle(GUI.skin.label);
                _emptyPreviewStyle.normal.textColor = new Color(0.72f, 0.71f, 0.68f, 1f);
                _emptyPreviewStyle.alignment = TextAnchor.MiddleCenter;
                _emptyPreviewStyle.wordWrap = true;
                _emptyPreviewStyle.fontSize = 11;

                _cardButtonStyle = BuildButton(_cardTexture, new Color(0.98f, 0.95f, 0.90f, 1f), 12);
                _activeCardButtonStyle = BuildButton(_cardActiveTexture, Color.white, 12);
                _cardTitleStyle = new GUIStyle(GUI.skin.label);
                _cardTitleStyle.normal.textColor = new Color(0.98f, 0.95f, 0.88f, 1f);
                _cardTitleStyle.fontSize = 13;
                _cardTitleStyle.fontStyle = FontStyle.Bold;
                _cardTitleStyle.wordWrap = false;

                _cardDetailStyle = new GUIStyle(GUI.skin.label);
                _cardDetailStyle.normal.textColor = new Color(0.82f, 0.80f, 0.76f, 1f);
                _cardDetailStyle.fontSize = 11;
                _cardDetailStyle.wordWrap = true;

                _badgeStyle = new GUIStyle(GUI.skin.label);
                _badgeStyle.normal.textColor = new Color(1f, 0.96f, 0.88f, 1f);
                _badgeStyle.fontSize = 10;
                _badgeStyle.alignment = TextAnchor.MiddleCenter;

                _badgeBoxStyle = new GUIStyle(GUI.skin.box);
                _badgeBoxStyle.normal.background = _badgeTexture;
                _badgeBoxStyle.normal.textColor = new Color(1f, 0.96f, 0.88f, 1f);
                _badgeBoxStyle.fontSize = 10;
                _badgeBoxStyle.alignment = TextAnchor.MiddleCenter;
                _badgeBoxStyle.padding = new RectOffset(8, 8, 3, 3);

                _miniButtonStyle = BuildButton(_buttonTexture, new Color(0.97f, 0.93f, 0.86f, 1f), 11);

                _stylesReady = true;
                MMLog.WriteInfo("[ScenarioAuthoringIMGUI] Initialized fixed-layout scenario editor styles.");
            }

            private void OnGUI()
            {
                if (_owner != null)
                    _owner.Draw();
            }

            private static GUIStyle BuildSurface(Texture2D texture, int padding)
            {
                GUIStyle style = new GUIStyle(GUI.skin.box);
                style.normal.background = texture;
                style.border = new RectOffset(2, 2, 2, 2);
                style.padding = new RectOffset(padding, padding, padding, padding);
                return style;
            }

            private static GUIStyle BuildButton(Texture2D texture, Color textColor, int fontSize)
            {
                GUIStyle style = new GUIStyle(GUI.skin.button);
                style.normal.background = texture;
                style.hover.background = texture;
                style.active.background = texture;
                style.focused.background = texture;
                style.normal.textColor = textColor;
                style.hover.textColor = textColor;
                style.active.textColor = textColor;
                style.focused.textColor = textColor;
                style.fontSize = fontSize;
                style.alignment = TextAnchor.MiddleCenter;
                style.padding = new RectOffset(10, 10, 6, 6);
                style.wordWrap = true;
                return style;
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
        }
    }
}
