using System;
using System.Collections.Generic;
using ModAPI.Core;
using ModAPI.Scenarios;
using UnityEngine;

using ShelteredAPI.Hooks;
using ShelteredAPI.Scenarios.Application.Assets;
using ShelteredAPI.Scenarios.Application.Authoring;
using ShelteredAPI.Scenarios.Composition;
using ShelteredAPI.Scenarios.Domain.Stages;
using ShelteredAPI.Scenarios.Infrastructure.Assets;
using ShelteredAPI.Scenarios.Infrastructure.Unity;
using ShelteredAPI.Scenarios.Presentation.Authoring.Windows;
using ShelteredAPI.Scenarios.Presentation.UiKit;
using ShelteredAPI.Scenarios.Presentation.UiKit.Animation;
using ShelteredAPI.Scenarios.Presentation.UiKit.Frame;
using ShelteredAPI.Scenarios.Presentation.UiKit.Theme;
using ShelteredAPI.Scenarios.Presentation.UiKit.Widgets;
using ShelteredAPI.UI.FieldManual.Tooltips;
namespace ShelteredAPI.Scenarios.Presentation.Authoring.Shell{
    internal sealed partial class ScenarioAuthoringShellImguiRenderModule : IScenarioAuthoringRenderModule
    {
        private const string RuntimeObjectName = "ShelteredAPI.ScenarioAuthoring.ShellImgui";
        private const string CandidateFilterAll = "all";
        private const string CandidateFilterActive = "active";
        private const string CandidateFilterVanilla = "vanilla";
        private const string CandidateFilterScenario = "scenario";

        // Layout constants live in ScenarioAuthoringShellLayout. These aliases keep
        // the render code readable without re-declaring the values.
        private const float Margin = ScenarioAuthoringShellLayout.Margin;
        private const float Gutter = ScenarioAuthoringShellLayout.Gutter;
        private const float TopBarHeight = ScenarioAuthoringShellLayout.TopBarHeight;
        private const float StatusHeight = ScenarioAuthoringShellLayout.StatusHeight;
        private const float ToolRailWidth = ScenarioAuthoringShellLayout.ToolRailWidth;
        private const float InspectorWidth = ScenarioAuthoringShellLayout.InspectorWidth;
        private const float BottomTrayHeight = ScenarioAuthoringShellLayout.BottomTrayHeight;
        private const float CommandDockHeight = ScenarioAuthoringShellLayout.CommandDockHeight;

        private readonly ScenarioAuthoringShellAnimationService _animations;
        private readonly List<ScenarioAuthoringShellAnimationService.WindowVisualState> _closingWindowBuffer =
            new List<ScenarioAuthoringShellAnimationService.WindowVisualState>();
        private ScenarioAuthoringShellRuntime _runtime;
        private ScenarioAuthoringPresentationSnapshot _snapshot;
        private ScenarioUiContext _uiContext;
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
        private GUIStyle _sectionTitleStyle;
        private GUIStyle _statusStyle;
        private float _styleOpacity = -1f;
        private bool _windowMenuOpen;
        private readonly Dictionary<string, Vector2> _windowScrollPositions = new Dictionary<string, Vector2>(StringComparer.OrdinalIgnoreCase);
        private Vector2 _settingsScrollPosition = Vector2.zero;
        private string _dragWindowId;
        private FloatingWindowDragMode _dragMode = FloatingWindowDragMode.None;
        private Vector2 _dragStartMouse = Vector2.zero;
        private Rect _dragStartRect = RuntimeCompat.ZeroRect();
        private Rect _dragLastRect = RuntimeCompat.ZeroRect();
        private string _assetBrowserSearchText = string.Empty;
        private string _assetBrowserCandidateFilter = CandidateFilterAll;
        private bool _assetBrowserSearchFocused;
        private string _spritePickerSearchText = string.Empty;
        private string _spritePickerCandidateFilter = CandidateFilterAll;
        private bool _spritePickerSearchFocused;
        private float _activeContentWidth;
        private int _scaledWindowDrawDepth;

        public string ModuleId
        {
            get { return "ShelteredAPI.ShellIMGUI"; }
        }

        public ScenarioAuthoringShellImguiRenderModule(ScenarioAuthoringShellAnimationService animations)
        {
            _animations = animations ?? new ScenarioAuthoringShellAnimationService();
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

            if (!_visible)
            {
                DisposeUiContext();
                ClearInputCapture();
            }
        }

        public void Hide()
        {
            _snapshot = null;
            _visible = false;
            _windowMenuOpen = false;
            ClearFloatingDrag();
            if (_runtime != null)
                _runtime.enabled = false;
            DisposeUiContext();
            ClearInputCapture();
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

        private static void ClearInputCapture()
        {
            try
            {
                ScenarioAuthoringInputCaptureService inputCapture = ScenarioCompositionRoot.Resolve<ScenarioAuthoringInputCaptureService>();
                if (inputCapture != null)
                    inputCapture.Clear();
            }
            catch
            {
            }
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
            EnsureStyles(_snapshot.State != null ? _snapshot.State.Settings : null);
            _animations.BeginFrame(_snapshot.State != null ? _snapshot.State.Settings : null);

            Matrix4x4 oldMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(uiScale, uiScale, 1f));
            try
            {

                float scaledWidth = Screen.width / uiScale;
                float scaledHeight = Screen.height / uiScale;
                Rect hudReserveRect = ScenarioAuthoringShellLayout.BuildHudReserveRect(scaledWidth);
                Rect topRect = ScenarioAuthoringShellLayout.BuildTopBarRect(scaledWidth, hudReserveRect);
                Rect statusRect = ScenarioAuthoringShellLayout.BuildStatusRect(scaledWidth, scaledHeight);
                Rect windowMenuButtonRect = DrawTopBarCore(topRect, shell);
                Rect collapsedStripRect = DrawCollapsedWindowStripCore(statusRect, shell.Windows);
                DrawStatusBarCore(statusRect, shell);
                inputCapture.RegisterInteractiveRect(topRect);
                inputCapture.RegisterInteractiveRect(statusRect);
                if (collapsedStripRect.width > 0f && collapsedStripRect.height > 0f)
                    inputCapture.RegisterInteractiveRect(collapsedStripRect);

            Rect contentRect = ScenarioAuthoringShellLayout.BuildContentRect(scaledWidth, topRect, statusRect);

            Dictionary<string, Rect> windowRects = ResolveWindowRects(contentRect, shell.Windows);
            RegisterWindowAnimationStates(shell.Windows, windowRects);

            Rect toolRailRect = DrawToolRailCore(contentRect, shell, _snapshot.State);
            if (toolRailRect.width > 0f && toolRailRect.height > 0f)
                inputCapture.RegisterInteractiveRect(toolRailRect);

            Rect commandDockRect = DrawCommandDockCore(contentRect, _snapshot.State);
            if (commandDockRect.width > 0f && commandDockRect.height > 0f)
                inputCapture.RegisterInteractiveRect(commandDockRect);

            string activeWorkspaceId = GetActiveWorkspaceId(shell.Windows);
            Rect workspaceTabStripRect = RuntimeCompat.ZeroRect();
            Rect workspaceRect;
            if (activeWorkspaceId != null && windowRects.TryGetValue(activeWorkspaceId, out workspaceRect))
            {
                workspaceTabStripRect = new Rect(workspaceRect.x, workspaceRect.y - 42f, workspaceRect.width, 36f);
            }

            DrawWindowSet(shell.Windows, windowRects, false, contentRect, inputCapture);

            if (activeWorkspaceId != null && workspaceTabStripRect.width > 0f)
            {
                DrawWorkspaceTabsCore(workspaceTabStripRect, activeWorkspaceId, shell.Windows);
                inputCapture.RegisterInteractiveRect(workspaceTabStripRect);
            }

            DrawWindowSet(shell.Windows, windowRects, true, contentRect, inputCapture);

            Rect windowMenuRect = RuntimeCompat.ZeroRect();
            if (_windowMenuOpen && shell.WindowMenuActions != null && shell.WindowMenuActions.Length > 0)
            {
                windowMenuRect = BuildWindowMenuRectCore(windowMenuButtonRect, shell.WindowMenuActions, scaledWidth, scaledHeight, hudReserveRect);
                float menuProgress = _animations.GetPopupProgress(true, true);
                Rect animatedMenuRect = SlidePopupRect(windowMenuRect, menuProgress);
                using (ScenarioUiGuiScope.Apply(menuProgress, animatedMenuRect, 1f))
                    DrawWindowMenuCore(animatedMenuRect, shell.WindowMenuActions);
                inputCapture.RegisterInteractiveRect(windowMenuRect);
                inputCapture.SetPopupOpen(true);
            }
            else
            {
                _animations.GetPopupProgress(false, true);
            }

            Rect popupRect = RuntimeCompat.ZeroRect();
            if (shell.ContextMenu != null && shell.ContextMenu.Visible)
            {
                popupRect = BuildPopupRectCore(shell.ContextMenu, scaledWidth, scaledHeight, hudReserveRect);
                float popupProgress = _animations.GetPopupProgress(true, false);
                Rect animatedPopupRect = SlidePopupRect(popupRect, popupProgress);
                using (ScenarioUiGuiScope.Apply(popupProgress, animatedPopupRect, 1f))
                    DrawContextMenuCore(animatedPopupRect, shell.ContextMenu);
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
            else
            {
                _animations.GetPopupProgress(false, false);
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
                float dimAlpha = _animations.GetModalDimAlpha(true);
                if (dimAlpha > 0.001f)
                {
                    Color oldColor = GUI.color;
                    GUI.color = new Color(0f, 0f, 0f, dimAlpha);
                    GUI.DrawTexture(new Rect(0f, topRect.yMax, scaledWidth, scaledHeight - topRect.yMax - StatusHeight), Texture2D.whiteTexture);
                    GUI.color = oldColor;
                }

                Rect pickerRect = new Rect(
                    Math.Max(Margin, (scaledWidth - 980f) * 0.5f),
                    Math.Max(topRect.yMax + Gutter, (scaledHeight - 680f) * 0.5f),
                    Math.Min(980f, scaledWidth - (Margin * 2f)),
                    Math.Min(680f, scaledHeight - topRect.height - StatusHeight - (Margin * 3f)));
                float panelProgress = _animations.GetModalPanelProgress(true);
                float panelScale = Mathf.Lerp(0.975f, 1f, panelProgress);
                Rect pickerScrollRect;
                using (ScenarioUiGuiScope.Apply(panelProgress, pickerRect, panelScale))
                    pickerScrollRect = DrawDocumentModalCore(pickerRect, shell.SpritePickerDocument, "sprite_picker");
                inputCapture.RegisterInteractiveRect(pickerRect);
                if (pickerScrollRect.width > 0f && pickerScrollRect.height > 0f)
                    inputCapture.RegisterScrollRect("sprite_picker", pickerScrollRect);
                inputCapture.SetPopupOpen(true);
            }
            else
            {
                _animations.GetModalDimAlpha(false);
                _animations.GetModalPanelProgress(false);
            }

            Rect overlayRect = new Rect(0f, topRect.yMax, scaledWidth, scaledHeight - topRect.yMax - StatusHeight);
            DrawHelpModalCore(overlayRect, shell.Help, inputCapture);
            DrawTutorialOverlayCore(overlayRect, topRect, statusRect, windowRects, shell, inputCapture);

            inputCapture.SetKeyboardCaptured(
                shell.SpritePickerDocument != null
                || shell.Help != null
                || _assetBrowserSearchFocused
                || _spritePickerSearchFocused
                || (shell.ContextMenu != null && shell.ContextMenu.Visible));
            inputCapture.SetTransitionActive(_animations.TransitionActive);

            DrawTooltipOverlayCore(scaledWidth, scaledHeight, hudReserveRect);

                inputCapture.CompleteFrame();
            }
            finally
            {
                GUI.matrix = oldMatrix;
            }
        }

        private void RegisterWindowAnimationStates(
            ScenarioAuthoringShellWindowViewModel[] windows,
            Dictionary<string, Rect> windowRects)
        {
            for (int i = 0; windows != null && i < windows.Length; i++)
            {
                ScenarioAuthoringShellWindowViewModel window = windows[i];
                Rect rect;
                if (window != null && windowRects != null && windowRects.TryGetValue(window.Id, out rect))
                    _animations.RegisterWindow(window, rect);
            }

            _animations.CompleteWindowRegistration();
        }

        private Dictionary<string, Rect> ResolveWindowRects(Rect contentRect, ScenarioAuthoringShellWindowViewModel[] windows)
        {
            Dictionary<string, Rect> rects = new Dictionary<string, Rect>(StringComparer.OrdinalIgnoreCase);
            float viewportLeft = contentRect.x + ToolRailWidth + Gutter;
            float viewportRight = contentRect.xMax - InspectorWidth - Gutter;

            bool showBottomTray = HasVisibleDockedRenderer(windows, ScenarioAuthoringShellRendererKind.BottomTray);

            AppendStackRect(
                rects,
                windows,
                ScenarioAuthoringWindowIds.Inspector,
                ScenarioAuthoringShellLayout.BuildInspectorRect(contentRect));

            if (showBottomTray)
            {
                Rect buildToolsRect = ScenarioAuthoringShellLayout.BuildBottomTrayRect(contentRect, viewportLeft, viewportRight);
                AppendRendererRects(rects, windows, ScenarioAuthoringShellRendererKind.BottomTray, buildToolsRect);
            }

            Rect workspaceRect = ScenarioAuthoringShellLayout.BuildWorkspaceRect(contentRect, showBottomTray);
            AppendWorkspaceRects(rects, windows, workspaceRect);
            AppendFloatingRects(rects, windows, contentRect);
            return rects;
        }

        private void DrawWindowSet(
            ScenarioAuthoringShellWindowViewModel[] windows,
            Dictionary<string, Rect> windowRects,
            bool floating,
            Rect contentRect,
            ScenarioAuthoringInputCaptureService inputCapture)
        {
            ScenarioAuthoringShellWindowViewModel[] drawList = BuildWindowDrawList(windows, floating);
            for (int i = 0; i < drawList.Length; i++)
            {
                ScenarioAuthoringShellWindowViewModel window = drawList[i];
                Rect rect;
                if (window == null
                    || !window.Visible
                    || window.Collapsed
                    || !windowRects.TryGetValue(window.Id, out rect))
                {
                    continue;
                }

                if (floating)
                    rect = HandleFloatingWindowInput(window, rect, contentRect);

                _animations.UpdateWindowRect(window.Id, rect);
                Rect scrollRect = DrawWindowCore(rect, window);
                inputCapture.RegisterInteractiveRect(rect);
                if (scrollRect.width > 0f && scrollRect.height > 0f)
                    inputCapture.RegisterScrollRect(window.Id, scrollRect);
            }

            _animations.CollectClosingWindows(floating, _closingWindowBuffer);
            for (int i = 0; i < _closingWindowBuffer.Count; i++)
            {
                ScenarioAuthoringShellAnimationService.WindowVisualState state = _closingWindowBuffer[i];
                if (state == null || state.Window == null)
                    continue;

                Rect rect = state.LastRect;
                DrawWindowCore(rect, state.Window);
                inputCapture.RegisterInteractiveRect(rect);
            }
        }

        private static Rect SlidePopupRect(Rect rect, float progress)
        {
            float offset = (1f - Mathf.Clamp01(progress)) * -8f;
            return new Rect(rect.x, rect.y + offset, rect.width, rect.height);
        }

        private static ScenarioAuthoringShellWindowViewModel[] BuildWindowDrawList(
            ScenarioAuthoringShellWindowViewModel[] windows,
            bool floating)
        {
            List<ScenarioAuthoringShellWindowViewModel> drawList = new List<ScenarioAuthoringShellWindowViewModel>();
            for (int i = 0; windows != null && i < windows.Length; i++)
            {
                ScenarioAuthoringShellWindowViewModel window = windows[i];
                if (window != null && (window.Dock == ScenarioAuthoringShellDock.Floating) == floating)
                    drawList.Add(window);
            }

            if (floating)
            {
                drawList.Sort(delegate(ScenarioAuthoringShellWindowViewModel left, ScenarioAuthoringShellWindowViewModel right)
                {
                    int byZ = left.ZIndex.CompareTo(right.ZIndex);
                    if (byZ != 0)
                        return byZ;
                    return string.Compare(left.Id, right.Id, StringComparison.OrdinalIgnoreCase);
                });
            }

            return drawList.ToArray();
        }

        private Rect HandleFloatingWindowInput(ScenarioAuthoringShellWindowViewModel window, Rect rect, Rect contentRect)
        {
            if (window == null)
                return rect;

            if (IsDraggingWindow(window.Id))
                rect = _dragLastRect;

            Event evt = Event.current;
            if (evt == null)
                return rect;

            Rect headerDragRect = BuildFloatingHeaderDragRect(rect, window);
            Rect resizeRect = BuildFloatingResizeRect(rect);
            Vector2 mouse = evt.mousePosition;

            if (evt.type == EventType.MouseDown && evt.button == 0 && rect.Contains(mouse))
            {
                BringFloatingWindowToFront(window.Id);
                _windowMenuOpen = false;

                if (resizeRect.Contains(mouse))
                {
                    BeginFloatingWindowDrag(window.Id, FloatingWindowDragMode.Resize, rect, mouse);
                    evt.Use();
                }
                else if (headerDragRect.Contains(mouse))
                {
                    BeginFloatingWindowDrag(window.Id, FloatingWindowDragMode.Move, rect, mouse);
                    evt.Use();
                }
            }
            else if (IsDraggingWindow(window.Id) && evt.type == EventType.MouseDrag && evt.button == 0)
            {
                rect = UpdateFloatingWindowDrag(window, mouse, contentRect, false);
                evt.Use();
            }
            else if (IsDraggingWindow(window.Id) && (evt.type == EventType.MouseUp || evt.rawType == EventType.MouseUp))
            {
                rect = UpdateFloatingWindowDrag(window, mouse, contentRect, true);
                ClearFloatingDrag();
                evt.Use();
            }

            return rect;
        }

        private void BeginFloatingWindowDrag(
            string windowId,
            FloatingWindowDragMode mode,
            Rect rect,
            Vector2 mouse)
        {
            _dragWindowId = windowId;
            _dragMode = mode;
            _dragStartRect = rect;
            _dragLastRect = rect;
            _dragStartMouse = mouse;
        }

        private Rect UpdateFloatingWindowDrag(
            ScenarioAuthoringShellWindowViewModel window,
            Vector2 mouse,
            Rect contentRect,
            bool persist)
        {
            Vector2 delta = mouse - _dragStartMouse;
            Rect next = _dragStartRect;
            if (_dragMode == FloatingWindowDragMode.Move)
            {
                next.x += delta.x;
                next.y += delta.y;
            }
            else if (_dragMode == FloatingWindowDragMode.Resize)
            {
                next.width += delta.x;
                next.height += delta.y;
            }

            float minWidth = window != null && window.MinWidth > 0f ? window.MinWidth : 260f;
            float minHeight = window != null && window.MinHeight > 0f ? window.MinHeight : 140f;
            next = ScenarioAuthoringShellLayout.ClampWindowRect(next, contentRect, minWidth, minHeight);
            _dragLastRect = next;
            CommitFloatingWindowFrame(window != null ? window.Id : null, next, persist);
            return next;
        }

        private bool IsDraggingWindow(string windowId)
        {
            return _dragMode != FloatingWindowDragMode.None
                && !string.IsNullOrEmpty(_dragWindowId)
                && string.Equals(_dragWindowId, windowId, StringComparison.OrdinalIgnoreCase);
        }

        private void ClearFloatingDrag()
        {
            _dragWindowId = null;
            _dragMode = FloatingWindowDragMode.None;
            _dragStartMouse = Vector2.zero;
            _dragStartRect = RuntimeCompat.ZeroRect();
            _dragLastRect = RuntimeCompat.ZeroRect();
        }

        private static Rect BuildFloatingHeaderDragRect(Rect rect, ScenarioAuthoringShellWindowViewModel window)
        {
            int chromeCount = CountChromeActions(window != null ? window.HeaderActions : null);
            float reservedRight = 18f + (chromeCount * 24f);
            if (window != null && string.Equals(window.Id, ScenarioAuthoringWindowIds.Settings, StringComparison.OrdinalIgnoreCase))
                reservedRight += 176f;
            float width = Math.Max(40f, rect.width - reservedRight - 16f);
            return new Rect(rect.x + 8f, rect.y + 4f, width, 30f);
        }

        private static Rect BuildFloatingResizeRect(Rect rect)
        {
            return new Rect(rect.xMax - 20f, rect.yMax - 20f, 16f, 16f);
        }

        private static int CountChromeActions(ScenarioAuthoringInspectorAction[] actions)
        {
            int count = 0;
            for (int i = 0; actions != null && i < actions.Length; i++)
            {
                ScenarioAuthoringInspectorAction action = actions[i];
                if (action != null
                    && action.Id != null
                    && (action.Id.StartsWith(ScenarioAuthoringActionIds.ActionWindowCollapsePrefix, StringComparison.Ordinal)
                        || action.Id.StartsWith(ScenarioAuthoringActionIds.ActionWindowTogglePrefix, StringComparison.Ordinal)))
                {
                    count++;
                }
            }

            return count;
        }

        private static void CommitFloatingWindowFrame(string windowId, Rect rect, bool persist)
        {
            if (string.IsNullOrEmpty(windowId))
                return;

            try
            {
                ScenarioAuthoringBackendService.Instance.UpdateWindowFrame(windowId, rect.x, rect.y, rect.width, rect.height, persist);
            }
            catch
            {
            }
        }

        private static void BringFloatingWindowToFront(string windowId)
        {
            if (string.IsNullOrEmpty(windowId))
                return;

            try
            {
                ScenarioAuthoringBackendService.Instance.BringWindowToFront(windowId);
            }
            catch
            {
            }
        }

        private static Rect ClampAwayFromHud(Rect rect, float width, float height, Rect hudReserveRect)
        {
            return ScenarioAuthoringShellLayout.ClampAwayFromHud(rect, width, height, hudReserveRect);
        }

        private void EnsureStyles(ScenarioAuthoringSettingsSnapshot settings)
        {
            float panelOpacity = ScenarioUiTheme.ResolvePanelOpacity(settings);
            if (_uiContext != null && Mathf.Abs(_styleOpacity - panelOpacity) <= 0.001f)
                return;

            DisposeUiContext();
            _uiContext = ScenarioUiKit.Build(settings);
            _styleOpacity = panelOpacity;
            ScenarioUiStyleSheet styles = _uiContext.Styles;
            _rootPanelStyle = styles.PanelBase;
            _headerStyle = styles.Header;
            _statusStyle = styles.Status;
            _titleStyle = styles.BrandTitleText;
            _smallTitleStyle = styles.TitleText;
            _sectionTitleStyle = styles.SectionTitleText;
            _textStyle = styles.BodyText;
            _mutedTextStyle = styles.MutedText;
            _buttonStyle = styles.Button;
            _activeButtonStyle = styles.ButtonActive;
            _tabStyle = styles.Tab;
            _activeTabStyle = styles.TabActive;
        }

        private void DisposeUiContext()
        {
            if (_uiContext != null)
                _uiContext.Dispose();

            _uiContext = null;
            _rootPanelStyle = null;
            _headerStyle = null;
            _titleStyle = null;
            _smallTitleStyle = null;
            _textStyle = null;
            _mutedTextStyle = null;
            _buttonStyle = null;
            _activeButtonStyle = null;
            _tabStyle = null;
            _activeTabStyle = null;
            _sectionTitleStyle = null;
            _statusStyle = null;
            _styleOpacity = -1f;
        }

        private static bool HasVisibleWindow(ScenarioAuthoringShellWindowViewModel[] windows, ScenarioAuthoringShellDock dock)
        {
            for (int i = 0; windows != null && i < windows.Length; i++)
            {
                ScenarioAuthoringShellWindowViewModel window = windows[i];
                if (window != null && window.Visible && !window.Collapsed && window.Dock == dock)
                    return true;
            }

            return false;
        }

        private static bool HasVisibleWindow(ScenarioAuthoringShellWindowViewModel[] windows, string id)
        {
            for (int i = 0; windows != null && i < windows.Length; i++)
            {
                ScenarioAuthoringShellWindowViewModel window = windows[i];
                if (window != null && window.Visible && !window.Collapsed && string.Equals(window.Id, id, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static bool HasVisibleDockedRenderer(ScenarioAuthoringShellWindowViewModel[] windows, ScenarioAuthoringShellRendererKind rendererKind)
        {
            for (int i = 0; windows != null && i < windows.Length; i++)
            {
                ScenarioAuthoringShellWindowViewModel window = windows[i];
                if (window != null
                    && window.Visible
                    && !window.Collapsed
                    && window.Dock != ScenarioAuthoringShellDock.Floating
                    && window.RendererKind == rendererKind)
                {
                    return true;
                }
            }

            return false;
        }

        private static string GetActiveWorkspaceId(ScenarioAuthoringShellWindowViewModel[] windows)
        {
            if (windows == null)
                return null;
            for (int i = 0; i < windows.Length; i++)
            {
                ScenarioAuthoringShellWindowViewModel window = windows[i];
                if (IsWorkspaceTabWindow(window) && window.Visible)
                    return window.Id;
            }
            return null;
        }

        private static ScenarioAuthoringShellWindowViewModel[] GetWorkspaceTabWindows(ScenarioAuthoringShellWindowViewModel[] windows)
        {
            List<ScenarioAuthoringShellWindowViewModel> tabWindows = new List<ScenarioAuthoringShellWindowViewModel>();
            for (int i = 0; windows != null && i < windows.Length; i++)
            {
                ScenarioAuthoringShellWindowViewModel window = windows[i];
                if (IsWorkspaceTabWindow(window))
                    tabWindows.Add(window);
            }

            return tabWindows.ToArray();
        }

        private static bool IsWorkspaceTabWindow(ScenarioAuthoringShellWindowViewModel window)
        {
            return window != null
                && window.WorkspaceTabVisible
                && window.WorkspaceStage != ScenarioStageKind.None;
        }

        private static void AppendWorkspaceRects(Dictionary<string, Rect> rects, ScenarioAuthoringShellWindowViewModel[] windows, Rect rect)
        {
            for (int i = 0; windows != null && i < windows.Length; i++)
            {
                ScenarioAuthoringShellWindowViewModel window = windows[i];
                if (window != null && window.WorkspaceStage != ScenarioStageKind.None && window.Visible && !window.Collapsed)
                    rects[window.Id] = rect;
            }
        }

        private static void AppendFloatingRects(Dictionary<string, Rect> rects, ScenarioAuthoringShellWindowViewModel[] windows, Rect contentRect)
        {
            int visibleIndex = 0;
            for (int i = 0; windows != null && i < windows.Length; i++)
            {
                ScenarioAuthoringShellWindowViewModel window = windows[i];
                if (window == null
                    || !window.Visible
                    || window.Collapsed
                    || window.Dock != ScenarioAuthoringShellDock.Floating)
                {
                    continue;
                }

                rects[window.Id] = ScenarioAuthoringShellLayout.BuildFloatingWindowRect(window, contentRect, visibleIndex);
                visibleIndex++;
            }
        }

        private static void AppendRendererRects(
            Dictionary<string, Rect> rects,
            ScenarioAuthoringShellWindowViewModel[] windows,
            ScenarioAuthoringShellRendererKind rendererKind,
            Rect rect)
        {
            for (int i = 0; windows != null && i < windows.Length; i++)
            {
                ScenarioAuthoringShellWindowViewModel window = windows[i];
                if (window != null && window.Visible && !window.Collapsed && window.RendererKind == rendererKind)
                    rects[window.Id] = rect;
            }
        }

        private static void AppendStackRect(Dictionary<string, Rect> rects, ScenarioAuthoringShellWindowViewModel[] windows, string id, Rect rect)
        {
            if (!HasVisibleWindow(windows, id))
                return;

            rects[id] = rect;
        }

        private enum FloatingWindowDragMode
        {
            None = 0,
            Move = 1,
            Resize = 2
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
