using System;
using System.Collections.Generic;
using System.IO;
using Cortex.Chrome;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.Core.Services;
using Cortex.Presentation.Abstractions;
using Cortex.Presentation.Models;
using Cortex.Rendering.Abstractions;
using Cortex.Rendering;
using Cortex.Rendering.Models;
using Cortex.Services.Navigation;
using UnityEngine;
using Cortex.Services.Editor.Context;
using Cortex.Services.Onboarding;
using Cortex.Services.Search;
using Cortex.Shell;
using Cortex.Rendering.RuntimeUi;
using Cortex.Presentation.Services;

namespace Cortex
{
    public sealed partial class CortexShellController
    {
        private const string ToggleActionId = "cortex.toggle";
        private const int MainWindowId = 0xC07E;
        private const int LogsWindowId = 0xC07F;

        private readonly CortexShellState _state = new CortexShellState();
        private readonly CortexShellViewState _viewState = new CortexShellViewState();
        private readonly ShellBootstrapper _bootstrapper;
        private readonly ShellSessionCoordinator _sessionCoordinator;
        private readonly ShellLayoutCoordinator _layoutCoordinator;
        private readonly ShellOverlayCoordinator _overlayCoordinator;
        private readonly ShellCommandDispatcher _commandDispatcher;
        private readonly ShellStatusPresenter _statusPresenter;

        private readonly EditorSymbolInteractionService _editorSymbolInteractionService = new EditorSymbolInteractionService();
        private readonly WorkbenchSearchService _workbenchSearchService = new WorkbenchSearchService();
        private readonly CortexShellLifecycleCoordinator _lifecycleCoordinator = new CortexShellLifecycleCoordinator();
        private readonly CortexShellModuleContributionRegistry _moduleContributionRegistry = new CortexShellModuleContributionRegistry();
        private readonly CortexShellBuiltInModuleRegistrar _moduleRegistrar = new CortexShellBuiltInModuleRegistrar();
        private readonly WorkbenchExtensionRegistry _extensionRegistry = new WorkbenchExtensionRegistry();
        private readonly CortexShellCommandRouter _commandRouter = new CortexShellCommandRouter();
        private readonly CortexShellLayoutHostRouter _layoutHostRouter = new CortexShellLayoutHostRouter();
        private readonly CortexLanguageRuntimeService _languageRuntimeService;
        private readonly CortexOnboardingCoordinator _onboardingCoordinator = new CortexOnboardingCoordinator();
        private readonly CortexShellOnboardingLifecycle _onboardingLifecycle = new CortexShellOnboardingLifecycle();
        private readonly ShellOnboardingOverlayPresenter _onboardingOverlayPresenter = new ShellOnboardingOverlayPresenter();

        private IWorkbenchRuntime _workbenchRuntime;
        private ICortexSettingsStore _settingsStore;
        private IWorkbenchPersistenceService _workbenchPersistenceService;
        private IPathInteractionService _pathInteractionService;
        private IRuntimeSourceNavigationService _runtimeSourceNavigationService;
        private ICortexPlatformModule _platformModule;
        private ICortexHostEnvironment _hostEnvironment;
        private IWorkbenchFrameContext _hostFrameContext;
        private CortexShellLayoutContext _layoutContext;
        private CortexShellCommandContext _commandContext;
        private CortexShellModuleServices _moduleServices;
        private CortexShellModuleCompositionService _moduleCompositionService;
        private CortexShellModuleActivationService _moduleActivationService;
        private CortexShellModuleRenderService _moduleRenderService;
        private bool _moduleContributionsRegistered;
        private readonly WorkbenchRuntimeAccess _runtimeAccess;

        private GUIStyle _titleStyle;
        private GUIStyle _menuStyle;
        private GUIStyle _statusStyle;
        private GUIStyle _captionStyle;
        private GUIStyle _sectionStyle;
        private GUIStyle _windowStyle;
        private GUIStyle _tabStyle;
        private GUIStyle _activeTabStyle;
        private GUIStyle _tabCloseButtonStyle;
        private GUIStyle _collapsedWindowStyle;
        private Texture2D _windowBackground;
        private Texture2D _sectionBackground;
        private Texture2D _tabBackground;
        private Texture2D _tabActiveBackground;
        private Texture2D _collapsedWindowBackground;
        private string _appliedThemeId = string.Empty;
        private WorkbenchPresentationSnapshot _frameSnapshot;
        private readonly IWorkbenchPresenter _snapshotPresenter = new WorkbenchPresenter();
        private readonly Dictionary<string, Rect> _menuGroupRects = new Dictionary<string, Rect>(StringComparer.OrdinalIgnoreCase);

        private ShellServiceMap _services;
        private IList<ILanguageProviderFactory> _hostLanguageProviderFactories = new List<ILanguageProviderFactory>();

        private IProjectCatalog ProjectCatalog
        {
            get { return _services != null ? _services.ProjectCatalog : null; }
        }

        private ILoadedModCatalog LoadedModCatalog
        {
            get { return _services != null ? _services.LoadedModCatalog : null; }
        }

        private ISourceLookupIndex SourceLookupIndex
        {
            get { return _services != null ? _services.SourceLookupIndex : null; }
        }

        private IProjectWorkspaceService ProjectWorkspaceService
        {
            get { return _services != null ? _services.ProjectWorkspaceService : null; }
        }

        private IDocumentService DocumentService
        {
            get { return _services != null ? _services.DocumentService : null; }
        }

        private IOverlayInputCaptureService OverlayInputCaptureService
        {
            get { return _services != null ? _services.OverlayInputCaptureService : null; }
        }

        private ITextSearchService TextSearchService
        {
            get { return _services != null ? _services.TextSearchService : null; }
        }

        private ICortexNavigationService NavigationService
        {
            get { return _services != null ? _services.NavigationService : null; }
        }

        private IPathInteractionService PathInteractionService
        {
            get
            {
                return _services != null && _services.PathInteractionService != null
                    ? _services.PathInteractionService
                    : _pathInteractionService;
            }
        }

        private ILanguageRuntimeControl LanguageRuntimeControl
        {
            get { return _services != null ? _services.LanguageRuntimeControl : null; }
        }

        public CortexShellController()
        {
            _services = ShellServiceMap.Empty;
            _runtimeAccess = new WorkbenchRuntimeAccess(_state, () => _moduleCompositionService ?? GetModuleCompositionService());
            _languageRuntimeService = new CortexLanguageRuntimeService(
                _state,
                delegate { return _services; },
                delegate { return _completionAugmentationInFlight; },
                ProcessCompletionAugmentationResponses,
                DispatchDeferredCompletionAugmentation,
                BuildCompletionAugmentationRequest,
                TryQueueCompletionAugmentation,
                delegate { return _hostLanguageProviderFactories; },
                null);
            _bootstrapper = new ShellBootstrapper(
                _state,
                _viewState,
                _moduleContributionRegistry,
                null,
                _moduleRegistrar,
                _extensionRegistry,
                _runtimeAccess,
                _languageRuntimeService,
                _languageRuntimeService,
                _languageRuntimeService);
            _sessionCoordinator = new ShellSessionCoordinator(
                _state,
                _viewState,
                () => NavigationService,
                () => LoadedModCatalog,
                () => ProjectCatalog,
                () => _workbenchPersistenceService,
                () => _settingsStore);
            _layoutCoordinator = new ShellLayoutCoordinator(
                _state,
                _viewState,
                _layoutHostRouter,
                () => _workbenchRuntime,
                () => GetModuleRenderService(),
                () => GetCurrentFrameInputSnapshot());
            _overlayCoordinator = new ShellOverlayCoordinator(
                _state,
                _viewState,
                _onboardingCoordinator,
                _onboardingLifecycle,
                _onboardingOverlayPresenter,
                () => OverlayInputCaptureService,
                () => GetCurrentFrameInputSnapshot(),
                ConsumeCurrentInputEvent);
            _commandDispatcher = new ShellCommandDispatcher(
                _state,
                _viewState,
                _commandRouter,
                () => _workbenchRuntime,
                () => DocumentService,
                () => _sessionCoordinator.Visible,
                value => _sessionCoordinator.Visible = value,
                PersistWorkbenchSession,
                PersistWindowSettings,
                FitMainWindowToScreen,
                ActivateContainer,
                ResolveHostLocation,
                HideContainer,
                OpenSettingsWindow,
                OpenOnboarding,
                OpenFind,
                ExecuteSearchOrAdvance,
                CloseFind,
                _editorSymbolInteractionService);
            _statusPresenter = new ShellStatusPresenter(_state, ExecuteCommand);
        }

        public void StartShell()
        {
            _lifecycleCoordinator.Start(this);
        }

        public void ShutdownShell()
        {
            _lifecycleCoordinator.Destroy(this);
            DisposeWorkbenchRuntime();
        }

        public void UpdateShell()
        {
            if (!_sessionCoordinator.Visible)
            {
                ReleaseOverlayInputCapture();
            }

            _lifecycleCoordinator.Update(this);
        }

        public void RenderShell()
        {
            _lifecycleCoordinator.OnGui(this);
        }

        private void RenderVisibleShell()
        {
            if (_state.OpenOnboardingRequested)
            {
                _state.OpenOnboardingRequested = false;
                OpenOnboarding(true);
            }

            if (!_sessionCoordinator.Visible)
            {
                ReleaseOverlayInputCapture();
                return;
            }

            if (_state.Onboarding.IsActive)
            {
                PreviewOnboardingSelections();
            }

            _frameSnapshot = BuildPresentationSnapshot();
            _layoutCoordinator.SynchronizeRuntimeLayoutState();
            CortexIdeLayout.ApplyTheme(_frameSnapshot.ThemeTokens, _frameSnapshot.ActiveThemeId);
            EnsureStyles(_frameSnapshot.ThemeTokens, _frameSnapshot.ActiveThemeId);
            var previousSkin = GUI.skin;
            GUI.skin = CortexIdeLayout.GetWorkbenchSkin(previousSkin);
            ClampWindowsToScreen();
            UpdateOverlayInputCapture();
            if (_viewState.MainWindow.IsCollapsed)
            {
                DrawCollapsedWindowButton(_viewState.MainWindow, ">", "Cortex");
            }
            else
            {
                _viewState.MainWindow.CurrentRect = ToRenderRect(GUI.Window(MainWindowId, ToRect(_viewState.MainWindow.CurrentRect), DrawWindow, "Cortex IDE", _windowStyle));
                _viewState.MainWindow.ExpandedRect = _viewState.MainWindow.CurrentRect;
            }

            if (_viewState.ShowDetachedLogsWindow && !_state.Onboarding.IsActive)
            {
                if (_viewState.LogsWindow.IsCollapsed)
                {
                    DrawCollapsedWindowButton(_viewState.LogsWindow, ">", "Logs");
                }
                else
                {
                    _viewState.LogsWindow.CurrentRect = ToRenderRect(GUI.Window(LogsWindowId, ToRect(_viewState.LogsWindow.CurrentRect), DrawLogsWindow, "Cortex Logs", _windowStyle));
                    _viewState.LogsWindow.ExpandedRect = _viewState.LogsWindow.CurrentRect;
                }
            }

            if (_state.Onboarding.IsActive)
            {
                DrawOnboardingOverlay();
            }

            _frameSnapshot = null;
            GUI.skin = previousSkin;
        }

        private void DrawWindow(int windowId)
        {
            var snapshot = _frameSnapshot ?? BuildPresentationSnapshot();
            var onboardingActive = IsOnboardingActive();
            const float headerHeight = 30f;
            const float statusHeight = 24f;
            var currentRect = ToRect(_viewState.MainWindow.CurrentRect);
            var contentRect = new Rect(6f, 24f, Mathf.Max(0f, currentRect.width - 12f), Mathf.Max(0f, currentRect.height - 30f));
            var headerRect = new Rect(contentRect.x, contentRect.y, contentRect.width, headerHeight);
            var statusRect = new Rect(contentRect.x, Mathf.Max(contentRect.y + headerHeight + 8f, contentRect.yMax - statusHeight), contentRect.width, statusHeight);
            var workbenchTop = headerRect.yMax + 2f;
            var workbenchRect = new Rect(
                contentRect.x,
                workbenchTop,
                contentRect.width,
                Mathf.Max(0f, statusRect.y - workbenchTop - 3f));

            _menuGroupRects.Clear();
            var previousEnabled = GUI.enabled;
            if (onboardingActive)
            {
                GUI.enabled = false;
                _openMenuGroup = string.Empty;
            }

            GUILayout.BeginArea(headerRect);
            DrawHeader(snapshot);
            GUILayout.EndArea();

            GUILayout.BeginArea(workbenchRect);
            DrawWorkbenchSurface(snapshot, new Rect(0f, 0f, workbenchRect.width, workbenchRect.height));
            GUILayout.EndArea();

            GUILayout.BeginArea(statusRect);
            DrawStatusStrip(snapshot);
            GUILayout.EndArea();
            GUI.enabled = previousEnabled;
            if (!onboardingActive)
            {
                DrawOpenMenuPanel(snapshot, headerRect);
                ApplyWindowResize(windowId, ref _viewState.MainWindow.CurrentRect, _viewState.MainWindow.MinWidth, _viewState.MainWindow.MinHeight);
                GUI.DragWindow(new Rect(0f, 0f, 10000f, 28f));
            }
        }

        private void DrawLogsWindow(int windowId)
        {
            EnsureModuleActivated(CortexWorkbenchIds.LogsContainer);
            GUILayout.BeginVertical();
            DrawLogsWindowHeaderActions();
            DrawActiveModule(_frameSnapshot, CortexWorkbenchIds.LogsContainer, true);
            GUILayout.EndVertical();
            ApplyWindowResize(windowId, ref _viewState.LogsWindow.CurrentRect, _viewState.LogsWindow.MinWidth, _viewState.LogsWindow.MinHeight);
            GUI.DragWindow(new Rect(0f, 0f, 10000f, 24f));
        }

        private void InitializeSettingsAndServices()
        {
            _bootstrapper.InitializeSettings();
            var hostEnvironment = _bootstrapper.HostEnvironment ?? _hostEnvironment ?? NullCortexHostServices.Instance.Environment;
            var settings = _state.Settings;
            _settingsStore = _bootstrapper.SettingsStore;
            _workbenchPersistenceService = _bootstrapper.PersistenceService;

            InitializeServices(hostEnvironment, settings);
        }

        private void InitializeWorkbenchRuntime()
        {
            _workbenchRuntime = _bootstrapper.InitializeRuntime();
            if (_workbenchRuntime == null)
            {
                return;
            }
            ActivateContainer(_state.Workbench.SideContainerId);
            if (!string.IsNullOrEmpty(_state.Workbench.SecondarySideContainerId))
            {
                ActivateContainer(_state.Workbench.SecondarySideContainerId);
            }
            ActivateContainer(_state.Workbench.EditorContainerId);
            ActivateContainer(_state.Workbench.PanelContainerId);
            if (_state.Settings != null && !_state.Settings.HasCompletedOnboarding)
            {
                OpenOnboarding(false);
            }
        }

        public void ConfigureHostServices(ICortexHostServices hostServices)
        {
            var resolvedHostServices = hostServices ?? NullCortexHostServices.Instance;
            _pathInteractionService = resolvedHostServices.PathInteractionService;
            _platformModule = resolvedHostServices.PlatformModule ?? NullCortexPlatformModule.Instance;
            _hostEnvironment = resolvedHostServices.Environment ?? NullCortexHostServices.Instance.Environment;
            _hostFrameContext = resolvedHostServices.FrameContext ?? NullWorkbenchFrameContext.Instance;
            _hostLanguageProviderFactories = resolvedHostServices.LanguageProviderFactories ?? new List<ILanguageProviderFactory>();
            _bootstrapper.ConfigureHostServices(resolvedHostServices);
        }

        private void InitializeServices(ICortexHostEnvironment hostEnvironment, CortexSettings settings)
        {
            ApplyServiceMap(_bootstrapper.InitializeServices(settings));
            ApplyLanguageRuntimeConfiguration(hostEnvironment, settings, false);
            EnableRuntimeLogIntegration();
        }

        private void UpdateOverlayInputCapture() => _overlayCoordinator.UpdateOverlayInputCapture(_sessionCoordinator.Visible, ToRect(_viewState.MainWindow.CurrentRect), ToRect(_viewState.LogsWindow.CurrentRect));
        private void ReleaseOverlayInputCapture() => _overlayCoordinator.ReleaseOverlayInputCapture();

        private int GetScreenWidth()
        {
            return (int)GetCurrentFrameInputSnapshot().ViewportSize.Width;
        }

        private int GetScreenHeight()
        {
            return (int)GetCurrentFrameInputSnapshot().ViewportSize.Height;
        }

        private void ConsumeCurrentInputEvent()
        {
            GetWorkbenchFrameContext().ConsumeCurrentInput();
        }

        private WorkbenchFrameInputSnapshot GetCurrentFrameInputSnapshot()
        {
            return GetWorkbenchFrameContext().Snapshot;
        }

        private void ApplySettingsChanges()
        {
            _sessionCoordinator.PersistSession();
            _sessionCoordinator.PersistWindowSettings();

            var hostEnv = _bootstrapper.HostEnvironment;
            _state.Settings = _bootstrapper.BuildEffectiveSettings(_state.Settings, hostEnv);
            ApplyServiceMap(_bootstrapper.InitializeServices(_state.Settings));
            ApplyLanguageRuntimeConfiguration(hostEnv, _state.Settings, true);
            if (_settingsStore != null)
            {
                _settingsStore.Save(_state.Settings);
            }

            _bootstrapper.ApplyShellWindowSettings(_state.Settings);
            ClampWindowsToScreen();
            _state.ReloadSettingsRequested = false;
            _state.StatusMessage = "Cortex settings applied.";
            if (_workbenchRuntime != null)
            {
                _workbenchRuntime.LayoutState.PrimarySideWidth = _state.Settings.ProjectsPaneWidth;
                _workbenchRuntime.LayoutState.SecondarySideWidth = _state.Settings.EditorFilePaneWidth;
                _workbenchRuntime.LayoutState.PanelSize = _state.Settings.PanelPaneSize;
                _workbenchRuntime.ThemeState.ThemeId = string.IsNullOrEmpty(_state.Settings.ThemeId) ? _workbenchRuntime.ThemeState.ThemeId : _state.Settings.ThemeId;
            }
        }

        private void PersistWindowSettings() => _sessionCoordinator.PersistWindowSettings();

        private void ClampWindowsToScreen()
        {
            var sw = GetScreenWidth(); var sh = GetScreenHeight();
            ClampWindowToScreen(_viewState.MainWindow, sw, sh);
            ClampWindowToScreen(_viewState.LogsWindow, sw, sh);
        }

        private RenderRect ClampRectToScreen(RenderRect rect, float minWidth, float minHeight, int sw, int sh)
        {
            var width = Mathf.Clamp(rect.Width, minWidth, Math.Max(minWidth, sw - 20f));
            var height = Mathf.Clamp(rect.Height, minHeight, Math.Max(minHeight, sh - 20f));
            var x = Mathf.Clamp(rect.X, 0f, Math.Max(0f, sw - width));
            var y = Mathf.Clamp(rect.Y, 0f, Math.Max(0f, sh - height));
            return new RenderRect(x, y, width, height);
        }

        private void FitMainWindowToScreen()
        {
            var sw = GetScreenWidth(); var sh = GetScreenHeight();
            var fittedRect = new RenderRect(Mathf.Max(10f, sw * 0.05f), Mathf.Max(10f, sh * 0.05f), Mathf.Max(980f, sw * 0.9f), Mathf.Max(620f, sh * 0.88f));
            _viewState.MainWindow.CurrentRect = fittedRect;
            _viewState.MainWindow.ExpandedRect = fittedRect;
            _viewState.MainWindow.CollapsedRect = CortexWindowChromeController.BuildCollapsedRect(fittedRect, _viewState.MainWindow.CollapsedWidth, _viewState.MainWindow.CollapsedHeight);
        }

        private void EnableRuntimeLogIntegration() => (_bootstrapper.PlatformModule ?? NullCortexPlatformModule.Instance).ConfigureRuntimeLogging(true);
        private void DisableRuntimeLogIntegration() => (_bootstrapper.PlatformModule ?? NullCortexPlatformModule.Instance).ConfigureRuntimeLogging(false);

        private void ApplyLanguageRuntimeConfiguration(ICortexHostEnvironment hostEnvironment, CortexSettings settings, bool reload)
        {
            var control = LanguageRuntimeControl;
            if (control == null)
            {
                return;
            }

            var configuration = _bootstrapper.BuildLanguageRuntimeConfiguration(hostEnvironment, settings);

            if (reload)
            {
                control.Reload(configuration);
            }
            else
            {
                control.Start(configuration);
            }
        }

        private void RegisterToggleAction() => (_bootstrapper.PlatformModule ?? NullCortexPlatformModule.Instance).EnsureShellToggleRegistered(ToggleActionId);

        private void ActivateContainer(string containerId) => _layoutCoordinator.ActivateContainer(containerId);
        private WorkbenchHostLocation ResolveHostLocation(string containerId) => _layoutCoordinator.ResolveHostLocation(containerId);
        private void DockContainer(string containerId, WorkbenchHostLocation hostLocation) => _layoutCoordinator.DockContainer(containerId, hostLocation);
        private void HideContainer(string containerId) => _layoutCoordinator.HideContainer(containerId);
        private string FindFirstContainerForHost(WorkbenchHostLocation hostLocation, string excludedContainerId) => _layoutHostRouter.FindFirstContainerForHost(GetLayoutContext(), hostLocation, excludedContainerId);
        private string MapLegacyTabIndex(int index) => _layoutHostRouter.MapLegacyTabIndex(index);

        private void EnsureStyles(ThemeTokenSet themeTokens, string themeId)
        {
            var effectiveThemeId = string.IsNullOrEmpty(themeId) ? "cortex.vs-dark" : themeId;
            if (!string.Equals(_appliedThemeId, effectiveThemeId, StringComparison.OrdinalIgnoreCase))
            {
                _titleStyle = null; _menuStyle = null; _statusStyle = null; _captionStyle = null; _sectionStyle = null; _windowStyle = null;
                _tabStyle = null; _activeTabStyle = null; _tabCloseButtonStyle = null; _collapsedWindowStyle = null;
                _windowBackground = null; _sectionBackground = null; _tabBackground = null; _tabActiveBackground = null; _collapsedWindowBackground = null;
                _appliedThemeId = effectiveThemeId;
            }

            var textColor = CortexIdeLayout.ParseColor(themeTokens != null ? themeTokens.TextColor : string.Empty, new Color(0.96f, 0.96f, 0.96f, 1f));
            var mutedTextColor = CortexIdeLayout.ParseColor(themeTokens != null ? themeTokens.MutedTextColor : string.Empty, new Color(0.72f, 0.76f, 0.82f, 1f));
            var surfaceColor = CortexIdeLayout.ParseColor(themeTokens != null ? themeTokens.SurfaceColor : string.Empty, new Color(0.1f, 0.1f, 0.12f, 0.96f));
            var backgroundColor = CortexIdeLayout.ParseColor(themeTokens != null ? themeTokens.BackgroundColor : string.Empty, new Color(0.05f, 0.05f, 0.07f, 0.97f));
            var headerColor = CortexIdeLayout.ParseColor(themeTokens != null ? themeTokens.HeaderColor : string.Empty, new Color(0.16f, 0.17f, 0.2f, 1f));
            var accentColor = CortexIdeLayout.ParseColor(themeTokens != null ? themeTokens.AccentColor : string.Empty, new Color(0.22f, 0.3f, 0.4f, 1f));

            if (_titleStyle == null) { _titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold }; GuiStyleUtil.ApplyTextColorToAllStates(_titleStyle, textColor); }
            if (_menuStyle == null) { _menuStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Normal, padding = new RectOffset(6, 6, 3, 3) }; GuiStyleUtil.ApplyTextColorToAllStates(_menuStyle, textColor); }
            if (_statusStyle == null) { _statusStyle = new GUIStyle(GUI.skin.label) { wordWrap = true }; GuiStyleUtil.ApplyTextColorToAllStates(_statusStyle, CortexIdeLayout.ParseColor(themeTokens != null ? themeTokens.TextColor : string.Empty, new Color(0.88f, 0.88f, 0.9f, 1f))); }
            if (_captionStyle == null) { _captionStyle = new GUIStyle(GUI.skin.label) { wordWrap = true }; GuiStyleUtil.ApplyTextColorToAllStates(_captionStyle, mutedTextColor); }
            if (_sectionStyle == null) { _sectionStyle = new GUIStyle(GUI.skin.box); _sectionBackground = MakeTex(surfaceColor); GuiStyleUtil.ApplyBackgroundToAllStates(_sectionStyle, _sectionBackground); _sectionStyle.padding = new RectOffset(8, 8, 6, 6); _sectionStyle.margin = new RectOffset(0, 0, 0, 0); }
            if (_windowStyle == null) { _windowBackground = MakeTex(surfaceColor); _windowStyle = new GUIStyle(GUI.skin.window); GuiStyleUtil.ApplyBackgroundToAllStates(_windowStyle, _windowBackground); GuiStyleUtil.ApplyTextColorToAllStates(_windowStyle, textColor); _windowStyle.padding = new RectOffset(6, 6, 24, 6); _windowStyle.margin = new RectOffset(0, 0, 0, 0); }
            if (_tabStyle == null) { _tabBackground = MakeTex(headerColor); _tabStyle = new GUIStyle(GUI.skin.button); GuiStyleUtil.ApplyBackgroundToAllStates(_tabStyle, _tabBackground); GuiStyleUtil.ApplyTextColorToAllStates(_tabStyle, mutedTextColor); _tabStyle.alignment = TextAnchor.MiddleCenter; _tabStyle.padding = new RectOffset(10, 10, 5, 5); _tabStyle.margin = new RectOffset(0, 2, 0, 0); }
            if (_activeTabStyle == null) { _tabActiveBackground = MakeTex(accentColor); _activeTabStyle = new GUIStyle(_tabStyle); GuiStyleUtil.ApplyBackgroundToAllStates(_activeTabStyle, _tabActiveBackground); GuiStyleUtil.ApplyTextColorToAllStates(_activeTabStyle, Color.white); _activeTabStyle.fontStyle = FontStyle.Bold; }
            if (_tabCloseButtonStyle == null) { _tabCloseButtonStyle = new GUIStyle(GUI.skin.button) { alignment = TextAnchor.MiddleCenter, fontSize = 10, padding = new RectOffset(0, 0, 0, 0), margin = new RectOffset(0, 0, 0, 0) }; GuiStyleUtil.ApplyBackgroundToAllStates(_tabCloseButtonStyle, MakeTex(CortexIdeLayout.Blend(headerColor, backgroundColor, 0.45f))); GuiStyleUtil.ApplyTextColorToAllStates(_tabCloseButtonStyle, textColor); }
            if (_collapsedWindowStyle == null) { _collapsedWindowBackground = MakeTex(CortexIdeLayout.ParseColor(themeTokens != null ? themeTokens.HeaderColor : string.Empty, new Color(0.09f, 0.11f, 0.15f, 0.98f))); _collapsedWindowStyle = new GUIStyle(GUI.skin.button) { alignment = TextAnchor.MiddleLeft, padding = new RectOffset(10, 10, 4, 4), fontStyle = FontStyle.Bold }; GuiStyleUtil.ApplyBackgroundToAllStates(_collapsedWindowStyle, _collapsedWindowBackground); GuiStyleUtil.ApplyTextColorToAllStates(_collapsedWindowStyle, textColor); }
        }

        private void DrawCollapsedWindowButton(CortexShellWindowViewState chromeState, string glyph, string title)
        {
            if (chromeState != null && CortexWindowChromeController.DrawCollapsedButton(ToRect(chromeState.CollapsedRect), glyph + " " + title, _collapsedWindowStyle))
            {
                chromeState.IsCollapsed = false;
                chromeState.CurrentRect = chromeState.ExpandedRect;
            }
        }

        private void DrawLogsWindowHeaderActions()
        {
            GUILayout.BeginHorizontal(); GUILayout.FlexibleSpace();
            var actions = new List<CortexWindowAction>();
            actions.Add(BuildGlyphWindowAction("logs.collapse", "_", "Minimize logs window", delegate
            {
                _viewState.LogsWindow.CurrentRect = CortexWindowChromeController.ToggleCollapsed(_viewState.LogsWindow, _viewState.LogsWindow.CurrentRect, _viewState.LogsWindow.CollapsedWidth, _viewState.LogsWindow.CollapsedHeight);
            }));
            actions.Add(BuildGlyphWindowAction("logs.close", "X", "Close detached logs window", delegate
            {
                _viewState.ShowDetachedLogsWindow = false;
                _viewState.LogsWindow.IsCollapsed = false;
                _viewState.LogsWindow.CurrentRect = _viewState.LogsWindow.ExpandedRect;
            }));
            CortexWindowChromeController.DrawActions(actions);
            GUILayout.EndHorizontal();
        }

        private static Texture2D MakeTex(Color color) { var texture = new Texture2D(1, 1); texture.SetPixel(0, 0, color); texture.Apply(); return texture; }
        private static CortexWindowAction BuildGlyphWindowAction(string actionId, string label, string toolTip, Action execute) => new CortexWindowAction { ActionId = actionId, Label = label, ToolTip = toolTip, Width = 24f, Height = 20f, Execute = execute };

        private void ApplyWindowResize(int windowId, ref RenderRect windowRect, float minWidth, float minHeight)
        {
            var sw = GetScreenWidth(); var sh = GetScreenHeight(); var localRect = new Rect(0f, 0f, windowRect.Width, windowRect.Height);
            localRect = CortexWindowChromeController.DrawResizeHandle(windowId, localRect, minWidth, minHeight, Math.Max(minWidth, sw - 12f), Math.Max(minHeight, sh - 12f));
            windowRect = new RenderRect(windowRect.X, windowRect.Y, localRect.width, localRect.height);
        }

        private void ClampWindowToScreen(CortexShellWindowViewState windowState, int screenWidth, int screenHeight)
        {
            if (windowState == null)
            {
                return;
            }

            var currentRect = windowState.CurrentRect.Width > 0f ? windowState.CurrentRect : windowState.ExpandedRect;
            var expandedRect = windowState.ExpandedRect.Width > 0f ? windowState.ExpandedRect : currentRect;
            var collapsedRect = windowState.CollapsedRect.Width > 0f
                ? windowState.CollapsedRect
                : CortexWindowChromeController.BuildCollapsedRect(currentRect, windowState.CollapsedWidth, windowState.CollapsedHeight);

            windowState.CurrentRect = ClampRectToScreen(currentRect, windowState.IsCollapsed ? windowState.CollapsedWidth : windowState.MinWidth, windowState.IsCollapsed ? windowState.CollapsedHeight : windowState.MinHeight, screenWidth, screenHeight);
            windowState.ExpandedRect = ClampRectToScreen(expandedRect, windowState.MinWidth, windowState.MinHeight, screenWidth, screenHeight);
            windowState.CollapsedRect = ClampRectToScreen(collapsedRect, windowState.CollapsedWidth, windowState.CollapsedHeight, screenWidth, screenHeight);

            if (windowState.IsCollapsed)
            {
                windowState.CurrentRect = windowState.CollapsedRect;
            }
            else
            {
                windowState.CurrentRect = windowState.ExpandedRect;
            }
        }

        private void ApplyServiceMap(ShellServiceMap services)
        {
            _services = services ?? ShellServiceMap.Empty;
            ResetModuleRuntime();
        }

        private void DrawOnboardingOverlay()
        {
            _overlayCoordinator.DrawOnboardingOverlay(
                ProjectCatalog,
                ProjectWorkspaceService,
                _workbenchRuntime,
                PathInteractionService,
                ActivateContainer,
                PersistWorkbenchSession,
                PersistWindowSettings);
        }

        private CortexShellLayoutContext GetLayoutContext()
        {
            if (_layoutContext == null)
            {
                _layoutContext = new CortexShellLayoutContext(_state, delegate { return _workbenchRuntime; });
            }

            return _layoutContext;
        }

    }
}
