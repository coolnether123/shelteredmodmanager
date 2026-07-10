using System;
using System.Collections.Generic;
using ModAPI.Core;
using ModAPI.Scenarios;
using ModAPI.UI.ColorPicker;
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
        private const string ShellRootAnimationKey = "shell.root";
        private const string ShellChromeAnimationKey = "shell.chrome";

        // Layout constants live in ScenarioAuthoringShellLayout. These aliases keep
        // the render code readable without re-declaring the values.
        private const float Margin = ScenarioAuthoringShellLayout.Margin;
        private const float Gutter = ScenarioAuthoringShellLayout.Gutter;
        private const float TopBarHeight = ScenarioAuthoringShellLayout.TopBarHeight;
        private const float StatusHeight = ScenarioAuthoringShellLayout.StatusHeight;
        private const float ToolRailWidth = ScenarioAuthoringShellLayout.ToolRailWidth;
        private const float InspectorWidth = ScenarioAuthoringShellLayout.InspectorWidth;
        private const float BottomTrayHeight = ScenarioAuthoringShellLayout.BottomTrayHeight;
        private const float ChromeInteractionThreshold = 0.06f;
        private const float WindowInteractionAlphaThreshold = 0.03f;
        private const float CommandDockHeight = ScenarioAuthoringShellLayout.CommandDockHeight;

        private enum ScenarioAssetPlaceflowPhase
        {
            Hidden = 0,
            Browse = 1,
            Placement = 2
        }

        private Rect _chromeViewportRect;

        private readonly ScenarioAuthoringShellAnimationService _animations;
        private readonly List<ScenarioAuthoringShellAnimationService.WindowVisualState> _closingWindowBuffer =
            new List<ScenarioAuthoringShellAnimationService.WindowVisualState>();
        private ScenarioAuthoringShellRuntime _runtime;
        private ScenarioAuthoringPresentationSnapshot _snapshot;
        private ScenarioUiContext _uiContext;
        private bool _visible;
        private bool _disposeWhenHidden;
        private float _rootAlpha;
        private int _pixelEditorWheelHandledFrame = -1;
        private bool _pixelEditorWheelAxisActive;
        private GUIStyle _rootPanelStyle;
        private GUIStyle _headerStyle;
        private GUIStyle _titleStyle;
        private GUIStyle _smallTitleStyle;
        private GUIStyle _textStyle;
        private GUIStyle _mutedTextStyle;
        private GUIStyle _buttonStyle;
        private GUIStyle _activeButtonStyle;
        private GUIStyle _buttonContentStyle;
        private GUIStyle _activeButtonContentStyle;
        private GUIStyle _disabledButtonContentStyle;
        private GUIStyle _tabStyle;
        private GUIStyle _activeTabStyle;
        private GUIStyle _tabContentStyle;
        private GUIStyle _activeTabContentStyle;
        private GUIStyle _disabledTabContentStyle;
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
        private string _buildPaletteSearchText = string.Empty;
        private bool _buildPaletteSearchFocused;
        private readonly Dictionary<string, string> _editableFieldDrafts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _editableFieldsFocusedLastFrame = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private bool _editableFieldFocused;
        private string _toolRailActiveKey;
        private float _toolRailIndicatorY = -1f;
        private bool _topBarMoreMenuOpen;
        private Rect _topBarMoreButtonRect = RuntimeCompat.ZeroRect();
        private Rect _topBarMoreMenuRect = RuntimeCompat.ZeroRect();
        private ScenarioAuthoringInspectorAction[] _topBarOverflowTabs = new ScenarioAuthoringInspectorAction[0];
        private string _lastWorkshopWorkspaceId;
        private string _spritePickerSearchText = string.Empty;
        private string _spritePickerCandidateFilter = CandidateFilterAll;
        private bool _spritePickerSearchFocused;
        private string _assetBrowserSearchText = string.Empty;
        private string _assetBrowserCategoryFilter = CandidateFilterAll;
        private bool _assetBrowserSearchFocused;
        private bool _assetBrowserDefaultResolved;
        private string _survivorTraitPickerKey;
        private string _survivorTraitPickerSearchText = string.Empty;
        private Rect _survivorTraitPickerButtonRect = RuntimeCompat.ZeroRect();
        private float _activeContentWidth;
        private float _activeUiScale = 1f;
        private int _scaledWindowDrawDepth;
        private Vector2 _pixelEditorPan = Vector2.zero;
        private bool _pixelEditorPanning;
        private Vector2 _pixelEditorPanStartMouse = Vector2.zero;
        private Vector2 _pixelEditorPanStart = Vector2.zero;
        private RichHoverHelpModel _richHoverCandidate;
        private RichHoverHelpModel _activeRichHoverHelp;
        private Rect _richHoverCandidateSourceRect = RuntimeCompat.ZeroRect();
        private Rect _activeRichHoverSourceRect = RuntimeCompat.ZeroRect();
        private Rect _activeRichHoverPopupRect = RuntimeCompat.ZeroRect();
        private Rect _survivorColorPickerRect = RuntimeCompat.ZeroRect();
        private Vector2 _richHoverMouseLastPosition = Vector2.zero;
        private int _richHoverMouseSampleFrame = -1;
        private bool _richHoverMouseHasLastPosition;
        private bool _richHoverMouseMovingFastThisFrame;
        private string _richHoverCandidateKey;
        private float _richHoverCandidateSince;
        private float _richHoverLastHoveredAt;
        private bool _richHoverSourceHoveredThisFrame;
        private string _richHoverSourceKeyThisFrame;
        private readonly List<string> _richHoverTopicBackStack = new List<string>();
        private readonly List<Rect> _richHoverSiblingAvoidRects = new List<Rect>();
        private SurvivorColorPickerPopup _survivorColorPicker;
        private string _lastSurvivorColorPickerRequestKey;
        private bool _tutorialCardDragging;
        private string _tutorialCardDragKey;
        private Vector2 _tutorialCardDragOffset = Vector2.zero;
        private Rect _tutorialCardManualRect = RuntimeCompat.ZeroRect();

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
            bool wasVisible = _visible;
            _snapshot = snapshot ?? _snapshot;
            if (ScenarioOpeningCutsceneAuthoringService.IsPreviewActive)
            {
                _visible = false;
                _rootAlpha = 0f;
                _disposeWhenHidden = true;
                if (_runtime != null)
                    _runtime.enabled = false;
                DisposeUiContext();
                ClearInputCapture();
                return;
            }

            bool vanillaBlockingPanelOpen = ScenarioCompositionRoot.Resolve<ScenarioAuthoringVanillaPanelVisibilityService>().HasBlockingPanelOpen();
            bool isPlaytesting = ScenarioAuthoringRuntimeGuards.IsPlaytesting();
            bool reloadPending = snapshot != null && snapshot.State != null && snapshot.State.ReloadPending;
            bool worldLoading = snapshot != null && snapshot.State != null && snapshot.State.WorldLoading;
            bool vanillaInteractionActive = snapshot != null && snapshot.State != null && snapshot.State.VanillaInteractionActive;
            bool mapAuthoringActive = snapshot != null && snapshot.State != null && snapshot.State.MapAuthoringActive;
            _visible = snapshot != null
                && snapshot.State != null
                && snapshot.State.IsActive
                && (snapshot.State.ShellVisible || isPlaytesting || reloadPending || worldLoading || vanillaInteractionActive || mapAuthoringActive)
                && snapshot.ShellViewModel != null
                && (!vanillaBlockingPanelOpen || worldLoading || vanillaInteractionActive || mapAuthoringActive);

            if (_runtime != null)
                _runtime.enabled = _visible || wasVisible || _rootAlpha > 0.001f;

            if (!_visible)
            {
                _disposeWhenHidden = true;
                if (_runtime == null || !_runtime.enabled)
                {
                    DisposeUiContext();
                    ClearInputCapture();
                }
            }
            else
            {
                _disposeWhenHidden = false;
            }
        }

        public void Hide()
        {
            _visible = false;
            _windowMenuOpen = false;
            _disposeWhenHidden = true;
            CloseSurvivorColorPicker();
            ClearFloatingDrag();
            if (_runtime != null)
                _runtime.enabled = true;
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
            if (_snapshot == null || _snapshot.ShellViewModel == null)
            {
                FinishHiddenRuntime();
                return;
            }

            ScenarioAuthoringShellViewModel shell = _snapshot.ShellViewModel;
            float uiScale = _snapshot.State != null && _snapshot.State.Settings != null
                ? _snapshot.State.Settings.GetFloat("shell.ui_scale", 1f)
                : 1f;
            _activeUiScale = uiScale > 0.001f ? uiScale : 1f;
            ScenarioAuthoringInputCaptureService inputCapture = ScenarioCompositionRoot.Resolve<ScenarioAuthoringInputCaptureService>();
            inputCapture.BeginFrame(uiScale);
            ScenarioAuthoringTourTargetRegistry tourTargets = ScenarioCompositionRoot.Resolve<ScenarioAuthoringTourTargetRegistry>();
            if (tourTargets != null)
                tourTargets.ClearFrame();
            EnsureStyles(_snapshot.State != null ? _snapshot.State.Settings : null);
            _editableFieldFocused = false;
            BeginRichHoverFrame();
            BeginVisualSurfaceFrame();
            _animations.BeginFrame(_snapshot.State != null ? _snapshot.State.Settings : null);
            if (_snapshot.State != null)
                _windowMenuOpen = _snapshot.State.WindowMenuOpen;
            _topBarMoreMenuOpen = ScenarioAuthoringRendererInteractionState.Instance.TopBarMoreOpen;
            _rootAlpha = _animations.GetBinaryProgress(ShellRootAnimationKey, _visible, 0.18f, ScenarioUiEasing.EaseOut, true);
            if (!_visible && _rootAlpha <= 0.001f)
            {
                FinishHiddenRuntime();
                return;
            }

            Matrix4x4 oldMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(uiScale, uiScale, 1f));
            try
            {
                using (ScenarioUiGuiScope.Apply(_rootAlpha, new Rect(0f, 0f, Screen.width / uiScale, Screen.height / uiScale), 1f))
                {
                float scaledWidth = Screen.width / uiScale;
                float scaledHeight = Screen.height / uiScale;
                if (_snapshot.State != null && _snapshot.State.WorldLoading)
                    inputCapture.RegisterInteractiveRect(new Rect(0f, 0f, scaledWidth, scaledHeight));
                _chromeViewportRect = new Rect(0f, 0f, scaledWidth, scaledHeight);
                Rect hudReserveRect = ScenarioAuthoringShellLayout.BuildHudReserveRect(scaledWidth);
                Rect topRect = ScenarioAuthoringShellLayout.BuildTopBarRect(scaledWidth, hudReserveRect);
                Rect statusRect = ScenarioAuthoringShellLayout.BuildStatusRect(scaledWidth, scaledHeight);
                // The workshop's top chrome is the persistent navigation surface.  Major
                // workspaces (notably Home at compact resolutions) must not animate it
                // away, or the user loses the only visible route between editor areas.
                float chromeProgress = _animations.GetChromeProgress(ShellChromeAnimationKey, true);
                if (_snapshot.State != null && _snapshot.State.ReloadPending)
                {
                    Rect playtestStatusRect = DrawStatusBarCore(statusRect, shell, chromeProgress);
                    if (chromeProgress > ChromeInteractionThreshold && playtestStatusRect.width > 0f && playtestStatusRect.height > 0f)
                    {
                        inputCapture.RegisterInteractiveRect(playtestStatusRect);
                    }

                    inputCapture.SetTextFieldFocused(false);
                    inputCapture.SetKeyboardCaptured(false);
                    inputCapture.SetPopupOpen(false);
                    inputCapture.SetTransitionActive(_animations.TransitionActive);
                    Rect reloadContentRect = ScenarioAuthoringShellLayout.BuildContentRect(scaledWidth, topRect, statusRect);
                    DrawTooltipOverlayCore(scaledWidth, scaledHeight, hudReserveRect, reloadContentRect);
                    return;
                }

                if (ScenarioAuthoringRuntimeGuards.IsPlaytesting()
                    && (_snapshot.State == null || _snapshot.State.ActiveStage != ScenarioStageKind.Test))
                {
                    Rect playtestStatusRect = DrawStatusBarCore(statusRect, shell, chromeProgress);
                    if (chromeProgress > ChromeInteractionThreshold && playtestStatusRect.width > 0f && playtestStatusRect.height > 0f)
                    {
                        inputCapture.RegisterInteractiveRect(playtestStatusRect);
                    }

                    inputCapture.SetTextFieldFocused(false);
                    inputCapture.SetKeyboardCaptured(false);
                    inputCapture.SetPopupOpen(false);
                    inputCapture.SetTransitionActive(_animations.TransitionActive);
                    Rect playtestContentRect = ScenarioAuthoringShellLayout.BuildContentRect(scaledWidth, topRect, statusRect);
                    DrawTooltipOverlayCore(scaledWidth, scaledHeight, hudReserveRect, playtestContentRect);
                    return;
                }

                if (_snapshot.State != null && _snapshot.State.VanillaInteractionActive)
                {
                    DrawVanillaInteractionAssistStripCore(scaledWidth, scaledHeight, inputCapture);
                    if (_snapshot.State.MapAuthoringActive)
                        DrawMapAuthoringOverlayCore(scaledWidth, scaledHeight, inputCapture);
                    inputCapture.SetTextFieldFocused(false);
                    inputCapture.SetKeyboardCaptured(false);
                    inputCapture.SetPopupOpen(false);
                    inputCapture.SetTransitionActive(false);
                    return;
                }

                if (_snapshot.State != null && _snapshot.State.MapAuthoringActive)
                {
                    DrawMapAuthoringOverlayCore(scaledWidth, scaledHeight, inputCapture);
                    inputCapture.SetTextFieldFocused(false);
                    inputCapture.SetKeyboardCaptured(false);
                    inputCapture.SetPopupOpen(false);
                    inputCapture.SetTransitionActive(false);
                    return;
                }

                RegisterVisualSurface("chrome.top", topRect);
                RegisterVisualSurface("chrome.status", statusRect);
                Rect windowMenuButtonRect;
                using (EnterVisualSurface("chrome.top", chromeProgress > ChromeInteractionThreshold))
                    windowMenuButtonRect = DrawTopBarCore(topRect, shell, chromeProgress);
                RegisterTopBarMoreMenu(inputCapture, chromeProgress > ChromeInteractionThreshold);
                if (_topBarMoreMenuOpen && _topBarMoreMenuRect.width > 0f && _topBarMoreMenuRect.height > 0f)
                    RegisterVisualSurface("popup.topbar.more", _topBarMoreMenuRect);
                Rect collapsedStripRect = RuntimeCompat.ZeroRect();
                Rect animatedStatusRect;
                using (EnterVisualSurface("chrome.status", chromeProgress > ChromeInteractionThreshold))
                    animatedStatusRect = DrawStatusBarCore(statusRect, shell, chromeProgress);
                if (chromeProgress > ChromeInteractionThreshold && windowMenuButtonRect.width > 0f && windowMenuButtonRect.height > 0f)
                    inputCapture.RegisterInteractiveRect(windowMenuButtonRect);
                if (chromeProgress > ChromeInteractionThreshold && _globalSearchButtonRect.width > 0f && _globalSearchButtonRect.height > 0f)
                    inputCapture.RegisterInteractiveRect(_globalSearchButtonRect);
                if (chromeProgress > ChromeInteractionThreshold && animatedStatusRect.width > 0f && animatedStatusRect.height > 0f)
                    inputCapture.RegisterInteractiveRect(animatedStatusRect);

            Rect contentRect = ScenarioAuthoringShellLayout.BuildContentRect(scaledWidth, topRect, statusRect);

            Dictionary<string, Rect> windowRects = ResolveWindowRects(contentRect, shell.Windows);
            RegisterWindowAnimationStates(shell.Windows, windowRects);
            string activeWorkspaceId = GetActiveWorkspaceId(shell.Windows);
            UpdateAssetBrowserOpenState(activeWorkspaceId, shell.Windows);
            bool workshopSurface = IsWorkshopSurface(_snapshot.State, activeWorkspaceId);
            RegisterWindowVisualSurfaces(shell.Windows, windowRects, false);
            RegisterWindowVisualSurfaces(shell.Windows, windowRects, true);

            if (_windowMenuOpen && shell.WindowMenuActions != null && shell.WindowMenuActions.Length > 0)
                RegisterVisualSurface("popup.window-menu", BuildWindowMenuRectCore(windowMenuButtonRect, shell.WindowMenuActions, scaledWidth, scaledHeight, hudReserveRect));
            if (shell.ContextMenu != null && shell.ContextMenu.Visible)
                RegisterVisualSurface("popup.context-menu", BuildPopupRectCore(shell.ContextMenu, scaledWidth, scaledHeight, hudReserveRect));
            if (shell.FocusedEditorDocument != null || shell.SpritePickerDocument != null)
                RegisterVisualSurface("modal.document", BuildDocumentModalRect(shell.FocusedEditorDocument, shell.SpritePickerDocument, scaledWidth, scaledHeight));
            if (_survivorColorPicker != null)
                RegisterVisualSurface("popup.survivor-color-picker", BuildSurvivorColorPickerRect(scaledWidth, scaledHeight, hudReserveRect));
            if (shell.Help != null)
                RegisterVisualSurface("modal.help", new Rect(0f, topRect.yMax, scaledWidth, scaledHeight - topRect.yMax - StatusHeight));
            if (shell.Help == null && ((shell.Tutorial != null && shell.Tutorial.Visible) || (shell.Tour != null && shell.Tour.Visible)))
                RegisterVisualSurface("modal.tutorial", new Rect(0f, topRect.yMax, scaledWidth, scaledHeight - topRect.yMax - StatusHeight));
            if (_snapshot.State != null && _snapshot.State.GlobalSearchOpen)
                RegisterVisualSurface("modal.global-search", BuildGlobalSearchRect(scaledWidth, scaledHeight, hudReserveRect));
            if (IsRichHoverHelpActive() && _activeRichHoverPopupRect.width > 0f && _activeRichHoverPopupRect.height > 0f)
                RegisterVisualSurface("popup.rich-help", _activeRichHoverPopupRect);
            Rect placementHudRect = RuntimeCompat.ZeroRect();
            if (IsAnyPlacementActive())
            {
                placementHudRect = BuildPlacementHudRect(scaledWidth, scaledHeight);
                RegisterVisualSurface("placement.hud", placementHudRect);
            }
            if (!workshopSurface && placementHudRect.width > 0f)
                DrawPlacementGridOverlayCore(contentRect);

                if (workshopSurface)
                {
                    RegisterVisualSurface("workspace:" + activeWorkspaceId, ScenarioAuthoringShellLayout.BuildWorkshopPageRect(contentRect));
                    Rect pageRect;
                    using (EnterVisualSurface("workspace:" + activeWorkspaceId))
                        pageRect = DrawWorkshopSurfaceCore(contentRect, shell.Windows, activeWorkspaceId);
                    using (EnterVisualSurface("chrome.top", chromeProgress > ChromeInteractionThreshold))
                        windowMenuButtonRect = DrawTopBarCore(topRect, shell, chromeProgress);
                    RegisterTopBarMoreMenu(inputCapture, chromeProgress > ChromeInteractionThreshold);
                    using (EnterVisualSurface("chrome.status", chromeProgress > ChromeInteractionThreshold))
                        animatedStatusRect = DrawStatusBarCore(statusRect, shell, chromeProgress);
                    if (pageRect.width > 0f && pageRect.height > 0f)
                        inputCapture.RegisterInteractiveRect(pageRect);
                    if (chromeProgress > ChromeInteractionThreshold && windowMenuButtonRect.width > 0f && windowMenuButtonRect.height > 0f)
                        inputCapture.RegisterInteractiveRect(windowMenuButtonRect);
                    if (chromeProgress > ChromeInteractionThreshold && _globalSearchButtonRect.width > 0f && _globalSearchButtonRect.height > 0f)
                        inputCapture.RegisterInteractiveRect(_globalSearchButtonRect);
                    if (chromeProgress > ChromeInteractionThreshold && animatedStatusRect.width > 0f && animatedStatusRect.height > 0f)
                        inputCapture.RegisterInteractiveRect(animatedStatusRect);
                }
                else
                {
                // TODO(centralize): This is the legacy multi-surface path. Merge the tool rail,
                // command dock, docked windows, and floating overlays into the central workspace
                // once the remaining scenario editor migration plan is defined.
                int restoreChipCount = CountCollapsedWorldToolWindows(shell.Windows);
                Rect toolRailRect;
                using (EnterVisualSurface("chrome.toolrail", chromeProgress > ChromeInteractionThreshold))
                    toolRailRect = DrawToolRailCore(contentRect, shell, _snapshot.State, restoreChipCount, chromeProgress);
                if (chromeProgress > ChromeInteractionThreshold && toolRailRect.width > 0f && toolRailRect.height > 0f)
                    inputCapture.RegisterInteractiveRect(toolRailRect);
                Rect restoreChipsRect = toolRailRect.width > 0f && toolRailRect.height > 0f
                    ? DrawWorldToolRestoreChips(contentRect, toolRailRect, shell.Windows)
                    : RuntimeCompat.ZeroRect();
                if (restoreChipsRect.width > 0f && restoreChipsRect.height > 0f)
                    inputCapture.RegisterInteractiveRect(restoreChipsRect);

                if (activeWorkspaceId == null)
                {
                    Rect commandDockRect;
                    using (EnterVisualSurface("chrome.command-dock", chromeProgress > ChromeInteractionThreshold))
                        commandDockRect = DrawCommandDockCore(contentRect, _snapshot.State, chromeProgress);
                    if (chromeProgress > ChromeInteractionThreshold && commandDockRect.width > 0f && commandDockRect.height > 0f)
                        inputCapture.RegisterInteractiveRect(commandDockRect);
                }

                if (activeWorkspaceId == null)
                    DrawWindowSet(shell.Windows, windowRects, false, contentRect, inputCapture);
            }

            DrawWindowSet(shell.Windows, windowRects, true, contentRect, inputCapture);
            if (!workshopSurface)
            {
                using (EnterVisualSurface("chrome.collapsed-strip"))
                    collapsedStripRect = DrawCollapsedWindowStripCore(statusRect, shell.Windows);
                if (collapsedStripRect.width > 0f && collapsedStripRect.height > 0f)
                    inputCapture.RegisterInteractiveRect(collapsedStripRect);
            }

            if (placementHudRect.width > 0f && placementHudRect.height > 0f)
            {
                using (EnterVisualSurface("placement.hud"))
                    DrawPlacementHudCore(placementHudRect, shell.Windows);
                inputCapture.RegisterInteractiveRect(placementHudRect);
            }
            if (!workshopSurface
                && activeWorkspaceId == null
                && shell.Help == null
                && shell.FocusedEditorDocument == null
                && shell.SpritePickerDocument == null)
            {
                DrawWorldInteractionLegendCore(scaledWidth, scaledHeight, placementHudRect);
            }
            if (!workshopSurface && placementHudRect.width > 0f)
                DrawPlacementPointerAidCore(scaledWidth, scaledHeight);

            Rect windowMenuRect = RuntimeCompat.ZeroRect();
            // TODO(centralize): Window menu still exposes separate panel toggles. Re-home these
            // controls into central workspace navigation when the window model is consolidated.
            if (_windowMenuOpen && shell.WindowMenuActions != null && shell.WindowMenuActions.Length > 0)
            {
                windowMenuRect = BuildWindowMenuRectCore(windowMenuButtonRect, shell.WindowMenuActions, scaledWidth, scaledHeight, hudReserveRect);
                float menuProgress = _animations.GetPopupProgress(true, true);
                Rect animatedMenuRect = SlidePopupRect(windowMenuRect, menuProgress);
                using (ScenarioUiGuiScope.Apply(menuProgress, animatedMenuRect, 1f))
                using (EnterVisualSurface("popup.window-menu"))
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
                using (EnterVisualSurface("popup.context-menu"))
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
                ScenarioAuthoringBackendService.Instance.ExecuteAction(ScenarioAuthoringActionIds.ActionShellToggleWindowMenu);
                _windowMenuOpen = _snapshot.State != null && _snapshot.State.WindowMenuOpen;
                Event.current.Use();
            }

            if (_topBarMoreMenuOpen
                && Event.current != null
                && Event.current.type == EventType.MouseDown
                && !_topBarMoreMenuRect.Contains(Event.current.mousePosition)
                && !_topBarMoreButtonRect.Contains(Event.current.mousePosition))
            {
                ScenarioAuthoringBackendService.Instance.ExecuteAction(ScenarioAuthoringActionIds.ActionRendererTopBarMoreToggle);
                _topBarMoreMenuOpen = ScenarioAuthoringRendererInteractionState.Instance.TopBarMoreOpen;
                Event.current.Use();
            }

            ScenarioAuthoringInspectorDocument modalDocument = shell.FocusedEditorDocument ?? shell.SpritePickerDocument;
            string modalScrollId = shell.FocusedEditorDocument != null ? "focused_editor" : "sprite_picker";
            // TODO(centralize): Focused editor and sprite picker documents still open as modal
            // panels. Merge them into a central workspace surface when ownership is clear.
            if (modalDocument != null)
            {
                float dimAlpha = _animations.GetModalDimAlpha(true);
                if (dimAlpha > 0.001f)
                {
                    Color oldColor = GUI.color;
                    GUI.color = new Color(0f, 0f, 0f, dimAlpha);
                    GUI.DrawTexture(new Rect(0f, topRect.yMax, scaledWidth, scaledHeight - topRect.yMax - StatusHeight), Texture2D.whiteTexture);
                    GUI.color = oldColor;
                }

                Rect pickerRect = BuildDocumentModalRect(shell.FocusedEditorDocument, shell.SpritePickerDocument, scaledWidth, scaledHeight);
                float panelProgress = _animations.GetModalPanelProgress(true);
                float panelScale = Mathf.Lerp(0.975f, 1f, panelProgress);
                Rect pickerScrollRect;
                using (ScenarioUiGuiScope.Apply(panelProgress, pickerRect, panelScale))
                using (EnterVisualSurface("modal.document"))
                    pickerScrollRect = DrawDocumentModalCore(pickerRect, modalDocument, modalScrollId);
                inputCapture.RegisterInteractiveRect(pickerRect);
                if (pickerScrollRect.width > 0f && pickerScrollRect.height > 0f)
                    inputCapture.RegisterScrollRect(modalScrollId, pickerScrollRect);
                inputCapture.SetPopupOpen(true);
            }
            else
            {
                _animations.GetModalDimAlpha(false);
                _animations.GetModalPanelProgress(false);
                CloseSurvivorColorPicker();
                _lastSurvivorColorPickerRequestKey = null;
            }

            if (modalDocument != null
                && shell.FocusedEditorDocument != null
                && _snapshot != null
                && _snapshot.State != null
                && !string.IsNullOrEmpty(_snapshot.State.SurvivorColorPickerChannel))
            {
                string requestedChannel = _snapshot.State.SurvivorColorPickerChannel;
                string requestKey = (_snapshot.State.FocusedEditorKind ?? string.Empty)
                    + ":"
                    + _snapshot.State.FocusedEditorIndex.ToString()
                    + ":"
                    + requestedChannel
                    + ":"
                    + _snapshot.State.SurvivorColorPickerRequestId.ToString();
                if (!string.Equals(requestKey, _lastSurvivorColorPickerRequestKey, StringComparison.Ordinal))
                {
                    ScenarioSurvivorColorRowViewModel requestedRow = null;
                    for (int sectionIndex = 0;
                        shell.FocusedEditorDocument.Sections != null
                        && requestedRow == null
                        && sectionIndex < shell.FocusedEditorDocument.Sections.Length;
                        sectionIndex++)
                    {
                        ScenarioAuthoringInspectorSection section = shell.FocusedEditorDocument.Sections[sectionIndex];
                        ScenarioSurvivorEditorViewModel editor = section != null ? section.SurvivorEditor : null;
                        for (int rowIndex = 0; editor != null && editor.ColorRows != null && rowIndex < editor.ColorRows.Length; rowIndex++)
                        {
                            ScenarioSurvivorColorRowViewModel row = editor.ColorRows[rowIndex];
                            if (row != null && string.Equals(row.Channel, requestedChannel, StringComparison.OrdinalIgnoreCase))
                            {
                                requestedRow = row;
                                break;
                            }
                        }
                        for (int rowIndex = 0; section != null && requestedRow == null && section.ModFieldRows != null && rowIndex < section.ModFieldRows.Length; rowIndex++)
                        {
                            ScenarioSurvivorColorRowViewModel row = section.ModFieldRows[rowIndex] != null ? section.ModFieldRows[rowIndex].ColorRow : null;
                            if (row != null && string.Equals(row.Channel, requestedChannel, StringComparison.OrdinalIgnoreCase))
                            {
                                requestedRow = row;
                                break;
                            }
                        }
                    }

                    if (requestedRow != null)
                    {
                        OpenSurvivorColorPicker(requestedRow);
                        _lastSurvivorColorPickerRequestKey = requestKey;
                    }
                }
            }

            if (_survivorColorPicker != null)
            {
                _survivorColorPickerRect = BuildSurvivorColorPickerRect(scaledWidth, scaledHeight, hudReserveRect);
                using (EnterVisualSurface("popup.survivor-color-picker"))
                    DrawSurvivorColorPickerPopup(_survivorColorPickerRect);
                inputCapture.RegisterInteractiveRect(_survivorColorPickerRect);
                inputCapture.SetPopupOpen(true);
            }

            Rect overlayRect = new Rect(0f, topRect.yMax, scaledWidth, scaledHeight - topRect.yMax - StatusHeight);
            using (EnterVisualSurface("modal.help"))
                DrawHelpModalCore(overlayRect, shell.Help, inputCapture);
            if (shell.Help == null)
            {
                using (EnterVisualSurface("modal.tutorial"))
                    DrawTutorialOverlayCore(overlayRect, topRect, statusRect, windowRects, shell, inputCapture);
            }

            bool globalSearchOpen = _snapshot.State != null && _snapshot.State.GlobalSearchOpen;
            if (globalSearchOpen)
            {
                Rect globalSearchRect = BuildGlobalSearchRect(scaledWidth, scaledHeight, hudReserveRect);
                using (EnterVisualSurface("modal.global-search"))
                    DrawGlobalSearchOverlayCore(globalSearchRect, inputCapture);
                inputCapture.RegisterInteractiveRect(globalSearchRect);
                inputCapture.SetPopupOpen(true);
            }
            else
            {
                ResetGlobalSearchIndex();
            }

            bool textFieldFocused = _buildPaletteSearchFocused
                || _spritePickerSearchFocused
                || _assetBrowserSearchFocused
                || _editableFieldFocused
                || (globalSearchOpen && _globalSearchFocused);
            inputCapture.SetTextFieldFocused(textFieldFocused);
            inputCapture.SetKeyboardCaptured(
                modalDocument != null
                || _survivorColorPicker != null
                || shell.Help != null
                || globalSearchOpen
                || IsRichHoverHelpActive()
                || textFieldFocused
                || (shell.ContextMenu != null && shell.ContextMenu.Visible));
            inputCapture.SetTransitionActive(_animations.TransitionActive);

            bool richHelpOpen;
            using (EnterVisualSurface("popup.rich-help"))
                richHelpOpen = DrawRichHoverHelpOverlayCore(scaledWidth, scaledHeight, hudReserveRect, contentRect, inputCapture);
            if (!richHelpOpen)
                DrawTooltipOverlayCore(scaledWidth, scaledHeight, hudReserveRect, contentRect);
                }
            }
            finally
            {
                inputCapture.CompleteFrame();
                GUI.matrix = oldMatrix;
            }
        }

        private void FinishHiddenRuntime()
        {
            _rootAlpha = 0f;
            if (_runtime != null)
                _runtime.enabled = false;
            if (_disposeWhenHidden)
            {
                _snapshot = null;
                DisposeUiContext();
                ClearInputCapture();
                _disposeWhenHidden = false;
            }
        }

        private void RegisterTopBarMoreMenu(ScenarioAuthoringInputCaptureService inputCapture, bool registerRect)
        {
            if (!_topBarMoreMenuOpen || inputCapture == null || !registerRect)
                return;

            inputCapture.RegisterInteractiveRect(_topBarMoreButtonRect);
            inputCapture.RegisterInteractiveRect(_topBarMoreMenuRect);
            inputCapture.SetPopupOpen(true);
        }

        private static bool HasMajorWindowOpen(ScenarioAuthoringShellWindowViewModel[] windows)
        {
            for (int i = 0; windows != null && i < windows.Length; i++)
            {
                if (IsMajorWindowOpen(windows[i]))
                    return true;
            }

            return false;
        }

        private static bool IsMajorWindowOpen(ScenarioAuthoringShellWindowViewModel window)
        {
            if (window == null || !window.Visible || window.Collapsed)
                return false;

            if (window.Dock == ScenarioAuthoringShellDock.Overlay)
                return true;

            return string.Equals(window.Id, ScenarioAuthoringWindowIds.PixelEditor, StringComparison.OrdinalIgnoreCase)
                || string.Equals(window.Id, ScenarioAuthoringWindowIds.BuildTools, StringComparison.OrdinalIgnoreCase)
                || string.Equals(window.Id, ScenarioAuthoringWindowIds.Settings, StringComparison.OrdinalIgnoreCase)
                || string.Equals(window.Id, ScenarioAuthoringWindowIds.AssetBrowser, StringComparison.OrdinalIgnoreCase);
        }

        private Rect ResolveSlidingChromeRect(Rect rect, float openProgress, ScenarioUiSlideDirection direction)
        {
            if (rect.width <= 0f || rect.height <= 0f)
                return rect;

            return ScenarioUiAnimator.ResolveSlidingRect(
                rect,
                Mathf.Clamp01(openProgress),
                direction,
                ScenarioUiAnimator.ResolveSlideDistance(rect, direction));
        }

        private Rect ResolveWindowSlidingRect(Rect rect, float openProgress)
        {
            ScenarioUiSlideDirection direction = ResolveWindowSlideDirection(rect, _chromeViewportRect);
            return ResolveSlidingChromeRect(rect, openProgress, direction);
        }

        private static ScenarioUiSlideDirection ResolveWindowSlideDirection(Rect rect, Rect viewport)
        {
            if (viewport.width <= 0f || viewport.height <= 0f)
                return ScenarioUiSlideDirection.Up;

            float left = Mathf.Max(0f, rect.x - viewport.x);
            float right = Mathf.Max(0f, (viewport.x + viewport.width) - rect.xMax);
            float top = Mathf.Max(0f, rect.y - viewport.y);
            float bottom = Mathf.Max(0f, (viewport.y + viewport.height) - rect.yMax);
            float nearest = left;
            ScenarioUiSlideDirection direction = ScenarioUiSlideDirection.Left;
            if (right < nearest)
            {
                nearest = right;
                direction = ScenarioUiSlideDirection.Right;
            }

            if (top < nearest)
            {
                nearest = top;
                direction = ScenarioUiSlideDirection.Up;
            }

            if (bottom < nearest)
                direction = ScenarioUiSlideDirection.Down;

            return direction;
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
            ScenarioAuthoringShellWindowViewModel inspectorWindow = FindWindow(windows, ScenarioAuthoringWindowIds.Inspector);
            float inspectorWidth = ResolveInspectorWidth(inspectorWindow);
            float viewportRight = contentRect.xMax - inspectorWidth - Gutter;
            string activeWorkspaceId = GetActiveWorkspaceId(windows);
            bool workspaceStageActive = activeWorkspaceId != null;

            bool showBottomTray = !workspaceStageActive && HasVisibleDockedRenderer(windows, ScenarioAuthoringShellRendererKind.BottomTray);
            ScenarioAssetPlaceflowPhase placeflowPhase = ResolvePlaceflowPhase(showBottomTray);
            bool placeflowBrowseMode = placeflowPhase == ScenarioAssetPlaceflowPhase.Browse;

            if (!workspaceStageActive)
            {
                // TODO(centralize): Right-side inspector remains outside the central workspace.
                // Marked for merge once selection details have a central panel destination.
                if (!showBottomTray && !IsEmptyInspector(inspectorWindow))
                {
                    AppendStackRect(
                        rects,
                        windows,
                        ScenarioAuthoringWindowIds.Inspector,
                        ScenarioAuthoringShellLayout.BuildInspectorRect(contentRect, inspectorWidth));
                }
            }

            if (placeflowBrowseMode)
            {
                Rect buildToolsRect = ScenarioAuthoringShellLayout.BuildPlaceflowBrowserRect(_chromeViewportRect);
                AppendRendererRects(rects, windows, ScenarioAuthoringShellRendererKind.BottomTray, buildToolsRect);
            }

            Rect workspaceRect = ScenarioAuthoringShellLayout.BuildWorkspaceRect(contentRect, placeflowBrowseMode, inspectorWidth);
            AppendWorkspaceRects(rects, windows, workspaceRect);
            // TODO(centralize): Floating windows are still resolved independently from the
            // workspace page. Fold remaining floating tools into central workspace regions.
            AppendFloatingRects(rects, windows, contentRect);
            foreach (KeyValuePair<string, Rect> windowRect in rects)
                RegisterTourTarget("window:" + windowRect.Key, windowRect.Value);
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
                ScenarioAuthoringShellAnimationService.WindowVisualState visual = _animations.GetWindowVisual(window != null ? window.Id : null);
                Rect rect;
                if (window == null
                    || !window.Visible
                    || window.Collapsed
                    || !windowRects.TryGetValue(window.Id, out rect))
                {
                    continue;
                }

                if (floating)
                {
                    // TODO(centralize): Floating windows remain movable/resizable overlays.
                    // Replace with central workspace panels once each tool has a target region.
                    bool canInteract = visual == null || visual.Alpha > WindowInteractionAlphaThreshold;
                    if (canInteract)
                    {
                        rect = HandleFloatingWindowInput(
                            window,
                            rect,
                            contentRect,
                            IsTopmostFloatingWindowForInput(drawList, windowRects, i));
                    }
                }
                else if (window.Dock == ScenarioAuthoringShellDock.Right
                    && string.Equals(window.Id, ScenarioAuthoringWindowIds.Inspector, StringComparison.OrdinalIgnoreCase))
                {
                    rect = HandleDockedInspectorResize(window, rect, contentRect);
                }

                _animations.UpdateWindowRect(window.Id, rect);
                bool visualInteractive = visual == null || visual.Alpha > WindowInteractionAlphaThreshold;
                Rect interactiveRect;
                using (EnterVisualSurface(VisualSurfaceIdForWindow(window.Id), visualInteractive))
                    interactiveRect = DrawWindowCore(rect, window);
                if (interactiveRect.width > 0f && interactiveRect.height > 0f && (visual == null || visual.Alpha > WindowInteractionAlphaThreshold))
                {
                    inputCapture.RegisterInteractiveRect(interactiveRect);
                }
            }

            _animations.CollectClosingWindows(floating, _closingWindowBuffer);
            for (int i = 0; i < _closingWindowBuffer.Count; i++)
            {
                ScenarioAuthoringShellAnimationService.WindowVisualState state = _closingWindowBuffer[i];
                if (state == null || state.Window == null)
                    continue;

                Rect rect = state.LastRect;
                Rect interactiveRect;
                using (EnterVisualSurface(VisualSurfaceIdForWindow(state.Window.Id), state.Alpha > WindowInteractionAlphaThreshold))
                    interactiveRect = DrawWindowCore(rect, state.Window);
                if (interactiveRect.width > 0f && interactiveRect.height > 0f && state.Alpha > WindowInteractionAlphaThreshold)
                {
                    inputCapture.RegisterInteractiveRect(interactiveRect);
                }
            }
        }

        private static Rect SlidePopupRect(Rect rect, float progress)
        {
            float offset = (1f - Mathf.Clamp01(progress)) * -8f;
            return new Rect(rect.x, rect.y + offset, rect.width, rect.height);
        }

        private static Rect BuildDocumentModalRect(
            ScenarioAuthoringInspectorDocument focusedDocument,
            ScenarioAuthoringInspectorDocument spritePickerDocument,
            float scaledWidth,
            float scaledHeight)
        {
            bool spritePicker = focusedDocument == null && spritePickerDocument != null;
            bool survivorEditor = HasSurvivorEditorLayout(focusedDocument);
            float targetWidth = spritePicker ? 980f : (survivorEditor ? 1040f : 720f);
            float targetHeight = spritePicker ? 680f : (survivorEditor ? 620f : 520f);
            return new Rect(
                Math.Max(Margin, (scaledWidth - targetWidth) * 0.5f),
                Math.Max(TopBarHeight + Gutter, (scaledHeight - targetHeight) * 0.5f),
                Math.Min(targetWidth, scaledWidth - (Margin * 2f)),
                Math.Min(targetHeight, scaledHeight - TopBarHeight - StatusHeight - (Margin * 3f)));
        }

        private static bool HasSurvivorEditorLayout(ScenarioAuthoringInspectorDocument document)
        {
            for (int i = 0; document != null && document.Sections != null && i < document.Sections.Length; i++)
            {
                ScenarioAuthoringInspectorSection section = document.Sections[i];
                if (section != null && section.Layout == ScenarioAuthoringInspectorSectionLayout.SurvivorEditor)
                    return true;
            }

            return false;
        }

        private Rect BuildSurvivorColorPickerRect(float scaledWidth, float scaledHeight, Rect hudReserveRect)
        {
            Vector2 preferred = _survivorColorPicker != null && _survivorColorPicker.Control != null
                ? _survivorColorPicker.Control.PreferredSize
                : new Vector2(430f, 300f);
            Vector2 minimum = _survivorColorPicker != null && _survivorColorPicker.Control != null
                ? _survivorColorPicker.Control.MinimumSize
                : new Vector2(430f, 300f);
            float minWidth = Math.Max(430f, minimum.x + 28f);
            float minHeight = Math.Max(330f, minimum.y + 62f);
            float maxWidth = Math.Max(minWidth, scaledWidth - (Margin * 2f));
            float maxHeight = Math.Max(minHeight, scaledHeight - TopBarHeight - StatusHeight - (Margin * 3f));
            float width = Mathf.Clamp(preferred.x + 28f, minWidth, maxWidth);
            float height = Mathf.Clamp(preferred.y + 62f, minHeight, maxHeight);
            return ScenarioAuthoringShellLayout.BuildCenteredPopupRect(scaledWidth, scaledHeight, width, height, hudReserveRect);
        }

        private void DrawSurvivorColorPickerPopup(Rect rect)
        {
            if (_survivorColorPicker == null || _survivorColorPicker.Control == null)
                return;

            GUI.Box(rect, GUIContent.none, _uiContext.Styles.Section);
            Rect inner = Inset(rect, 14f);
            GUI.Label(new Rect(inner.x, inner.y, inner.width, 24f), _survivorColorPicker.Title ?? "Color", _sectionTitleStyle);

            ColorPickerImguiStyle style = _survivorColorPicker.Control.Style;
            style.Label = _textStyle;
            style.Field = GUI.skin.textField;
            style.Button = _buttonStyle;
            style.SmallButton = _buttonStyle;
            style.SwatchLabel = _mutedTextStyle;

            Rect pickerRect = new Rect(inner.x, inner.y + 34f, inner.width, Math.Max(120f, inner.height - 34f));
            _survivorColorPicker.Control.Draw(pickerRect);
            if (_survivorColorPicker.Session != null && _survivorColorPicker.Session.WantsClose)
                CloseSurvivorColorPicker();
        }

        private void OpenSurvivorColorPicker(ScenarioSurvivorColorRowViewModel row)
        {
            if (row == null || string.IsNullOrEmpty(row.ApplyColorActionPrefix))
                return;

            CloseSurvivorColorPicker();
            ModColor initial = new ModColor(row.Color.r, row.Color.g, row.Color.b, row.Color.a);
            string actionPrefix = row.ApplyColorActionPrefix;
            ColorPickerOptions options = new ColorPickerOptions();
            options.OnCommit = delegate(ModColor color, ColorPickerCommitKind commitKind)
            {
                if (commitKind != ColorPickerCommitKind.Apply && commitKind != ColorPickerCommitKind.Ok)
                    return;

                string hex = color.ToHexRgba();
                if (hex.StartsWith("#", StringComparison.Ordinal))
                    hex = hex.Substring(1);
                ScenarioAuthoringBackendService.Instance.ExecuteAction(actionPrefix + hex);
            };
            options.OnClosed = delegate
            {
                GUI.FocusControl(null);
            };

            ColorPickerSession session = new ColorPickerSession(
                initial,
                ColorPickerPaletteStore.LoadDefault(),
                options,
                ColorPickerPaletteStore.DefaultPalettePath);

            _survivorColorPicker = new SurvivorColorPickerPopup
            {
                Title = (row.Label ?? "Appearance") + " Color",
                Session = session,
                Control = new ColorPickerImguiControl(session)
            };

            if (Event.current != null)
                Event.current.Use();
        }

        private void CloseSurvivorColorPicker()
        {
            if (_survivorColorPicker == null)
                return;

            if (_survivorColorPicker.Control != null)
                _survivorColorPicker.Control.Dispose();
            _survivorColorPicker = null;
            _survivorColorPickerRect = RuntimeCompat.ZeroRect();
        }

        private sealed class SurvivorColorPickerPopup
        {
            public string Title;
            public ColorPickerSession Session;
            public ColorPickerImguiControl Control;
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

        private Rect HandleFloatingWindowInput(
            ScenarioAuthoringShellWindowViewModel window,
            Rect rect,
            Rect contentRect,
            bool canStartDrag)
        {
            if (window == null)
                return rect;

            if (IsDraggingWindow(window.Id))
                rect = _dragLastRect;

            Event evt = Event.current;
            if (evt == null)
                return rect;

            ScenarioAuthoringInputCaptureService inputCapture = ScenarioCompositionRoot.Resolve<ScenarioAuthoringInputCaptureService>();
            if (IsDraggingWindow(window.Id) && inputCapture != null)
                inputCapture.SetDraggingShellChrome(true);

            Rect headerDragRect = BuildFloatingHeaderDragRect(rect, window);
            Rect resizeRect = BuildFloatingResizeRect(rect);
            Vector2 eventMouse = evt.mousePosition;
            Vector2 rawMouse = new Vector2(UnityEngine.Input.mousePosition.x, Screen.height - UnityEngine.Input.mousePosition.y);
            bool eventPrimaryDown = evt.type == EventType.MouseDown && evt.button == 0;
            bool rawPrimaryDown = UnityEngine.Input.GetMouseButtonDown(0);
            bool eventPrimaryUp = evt.type == EventType.MouseUp || evt.rawType == EventType.MouseUp;
            bool rawPrimaryUp = UnityEngine.Input.GetMouseButtonUp(0);
            bool rawPrimaryHeld = UnityEngine.Input.GetMouseButton(0);

            if (canStartDrag
                && !IsDraggingWindow(window.Id)
                && (eventPrimaryDown || rawPrimaryDown))
            {
                Vector2 mouse = eventPrimaryDown ? eventMouse : rawMouse;
                if (rect.Contains(mouse))
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
            }
            else if (IsDraggingWindow(window.Id) && (eventPrimaryUp || rawPrimaryUp))
            {
                Vector2 mouse = eventPrimaryUp ? eventMouse : rawMouse;
                rect = UpdateFloatingWindowDrag(window, mouse, contentRect, true);
                ClearFloatingDrag();
                evt.Use();
            }
            else if (IsDraggingWindow(window.Id) && (evt.type == EventType.MouseDrag || rawPrimaryHeld))
            {
                Vector2 mouse = evt.type == EventType.MouseDrag ? eventMouse : rawMouse;
                rect = UpdateFloatingWindowDrag(window, mouse, contentRect, false);
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

        private static Rect BuildDockedInspectorResizeRect(Rect rect)
        {
            return new Rect(rect.x - 5f, rect.y, 10f, rect.height);
        }

        private Rect HandleDockedInspectorResize(ScenarioAuthoringShellWindowViewModel window, Rect rect, Rect contentRect)
        {
            Event current = Event.current;
            Rect gripRect = BuildDockedInspectorResizeRect(rect);
            if (current != null && current.type == EventType.MouseDown && current.button == 0 && gripRect.Contains(current.mousePosition))
            {
                _dragWindowId = window.Id;
                _dragMode = FloatingWindowDragMode.Resize;
                _dragStartMouse = current.mousePosition;
                _dragStartRect = rect;
                _dragLastRect = rect;
                current.Use();
            }

            if (IsDraggingWindow(window.Id) && _dragMode == FloatingWindowDragMode.Resize)
            {
                if (current != null && (current.type == EventType.MouseDrag || current.type == EventType.Repaint || current.type == EventType.Layout))
                {
                    float deltaX = current.mousePosition.x - _dragStartMouse.x;
                    float width = Mathf.Clamp(
                        _dragStartRect.width - deltaX,
                        ScenarioAuthoringShellLayout.InspectorMinWidth,
                        Math.Min(ScenarioAuthoringShellLayout.InspectorMaxWidth, Math.Max(ScenarioAuthoringShellLayout.InspectorMinWidth, contentRect.width - ToolRailWidth - (Gutter * 3f))));
                    rect = new Rect(contentRect.xMax - width, rect.y, width, rect.height);
                    _dragLastRect = rect;
                    CommitFloatingWindowFrame(window.Id, rect, false);
                    if (current.type == EventType.MouseDrag)
                        current.Use();
                }

                if (current != null && current.type == EventType.MouseUp)
                {
                    CommitFloatingWindowFrame(window.Id, _dragLastRect, true);
                    ClearFloatingDrag();
                    current.Use();
                }
            }

            if (Event.current != null && Event.current.type == EventType.Repaint)
            {
                Color oldColor = GUI.color;
                GUI.color = new Color(0.86f, 0.78f, 0.64f, 0.70f);
                GUI.DrawTexture(new Rect(Mathf.Max(0f, rect.x - 1f), rect.y + 10f, 2f, 40f), Texture2D.whiteTexture);
                GUI.color = oldColor;
            }

            return rect;
        }

        private static bool IsTopmostFloatingWindowForInput(
            ScenarioAuthoringShellWindowViewModel[] drawList,
            Dictionary<string, Rect> windowRects,
            int index)
        {
            Event evt = Event.current;
            if (evt == null || evt.type == EventType.Used || drawList == null || windowRects == null || index < 0 || index >= drawList.Length)
                return false;

            ScenarioAuthoringShellWindowViewModel window = drawList[index];
            Rect rect;
            Vector2 pointer = evt.mousePosition;
            if (UnityEngine.Input.GetMouseButton(0)
                || UnityEngine.Input.GetMouseButtonDown(0)
                || UnityEngine.Input.GetMouseButtonUp(0))
            {
                pointer = new Vector2(UnityEngine.Input.mousePosition.x, Screen.height - UnityEngine.Input.mousePosition.y);
            }

            if (window == null || !windowRects.TryGetValue(window.Id, out rect) || !rect.Contains(pointer))
                return false;

            for (int i = drawList.Length - 1; i > index; i--)
            {
                ScenarioAuthoringShellWindowViewModel candidate = drawList[i];
                Rect candidateRect;
                if (candidate != null
                    && candidate.Visible
                    && !candidate.Collapsed
                    && windowRects.TryGetValue(candidate.Id, out candidateRect)
                    && candidateRect.Contains(pointer))
                {
                    return false;
                }
            }

            return true;
        }

        private static ScenarioAuthoringShellWindowViewModel FindWindow(ScenarioAuthoringShellWindowViewModel[] windows, string id)
        {
            for (int i = 0; windows != null && i < windows.Length; i++)
            {
                ScenarioAuthoringShellWindowViewModel window = windows[i];
                if (window != null && string.Equals(window.Id, id, StringComparison.OrdinalIgnoreCase))
                    return window;
            }

            return null;
        }

        private static float ResolveInspectorWidth(ScenarioAuthoringShellWindowViewModel window)
        {
            float width = window != null && window.Width > 0f ? window.Width : InspectorWidth;
            return Mathf.Clamp(width, ScenarioAuthoringShellLayout.InspectorMinWidth, ScenarioAuthoringShellLayout.InspectorMaxWidth);
        }

        private static int CountChromeActions(ScenarioAuthoringInspectorAction[] actions)
        {
            int count = 0;
            for (int i = 0; actions != null && i < actions.Length; i++)
            {
                ScenarioAuthoringInspectorAction action = actions[i];
                if (IsWindowHeaderChromeAction(action))
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
            _buttonContentStyle = BuildContentOnlyStyle(styles.Button);
            _activeButtonContentStyle = BuildContentOnlyStyle(styles.ButtonActive);
            _disabledButtonContentStyle = BuildContentOnlyStyle(styles.ButtonDisabled);
            _tabContentStyle = BuildContentOnlyStyle(styles.Tab);
            _activeTabContentStyle = BuildContentOnlyStyle(styles.TabActive);
            _disabledTabContentStyle = BuildContentOnlyStyle(styles.TabDisabled);
            GUI.skin.settings.cursorColor = styles.Theme.Palette.TextTitle;
            GUI.skin.settings.selectionColor = new Color(0.58f, 0.45f, 0.18f, 0.46f);
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
            _buttonContentStyle = null;
            _activeButtonContentStyle = null;
            _disabledButtonContentStyle = null;
            _tabStyle = null;
            _activeTabStyle = null;
            _tabContentStyle = null;
            _activeTabContentStyle = null;
            _disabledTabContentStyle = null;
            _sectionTitleStyle = null;
            _statusStyle = null;
            _styleOpacity = -1f;
        }

        private static GUIStyle BuildContentOnlyStyle(GUIStyle source)
        {
            GUIStyle style = new GUIStyle(source ?? GUI.skin.button);
            style.normal.background = null;
            style.hover.background = null;
            style.active.background = null;
            style.focused.background = null;
            style.onNormal.background = null;
            style.onHover.background = null;
            style.onActive.background = null;
            return style;
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

        private static bool IsAnyPlacementActive()
        {
            return IsBuildPlacementActive() || IsSceneSpritePlacementActive();
        }

        private static ScenarioAssetPlaceflowPhase ResolvePlaceflowPhase(bool toolWorkspaceOpen)
        {
            if (!toolWorkspaceOpen)
                return ScenarioAssetPlaceflowPhase.Hidden;

            return IsAnyPlacementActive()
                ? ScenarioAssetPlaceflowPhase.Placement
                : ScenarioAssetPlaceflowPhase.Browse;
        }

        private static bool IsBuildPlacementActive()
        {
            try
            {
                return ScenarioBuildPlacementAuthoringService.Instance.HasActivePlacement;
            }
            catch
            {
                return false;
            }
        }

        private static bool IsSceneSpritePlacementActive()
        {
            try
            {
                return ScenarioSceneSpritePlacementAuthoringService.Instance.HasActivePlacement;
            }
            catch
            {
                return false;
            }
        }

        private static bool IsWorkshopSurface(ScenarioAuthoringState state, string activeWorkspaceId)
        {
            if (string.IsNullOrEmpty(activeWorkspaceId))
                return false;

            return state == null
                || (state.ActiveStage != ScenarioStageKind.Bunker
                    && state.ActiveStage != ScenarioStageKind.BunkerBackground
                    && state.ActiveStage != ScenarioStageKind.BunkerSurface
                    && state.ActiveStage != ScenarioStageKind.BunkerInside);
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
