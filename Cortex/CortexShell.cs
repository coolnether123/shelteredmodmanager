using System;
using System.Collections.Generic;
using System.IO;
using Cortex.Adapters;
using Cortex.Chrome;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.Core.Services;
using Cortex.Presentation.Abstractions;
using Cortex.Plugins.Abstractions;
using Cortex.Presentation.Models;
using Cortex.Shell;
using Cortex.Services;
using ModAPI.Core;
using ModAPI.InputActions;
using UnityEngine;

namespace Cortex
{
    public sealed partial class CortexShell : MonoBehaviour
    {
        private const string ToggleActionId = "cortex.toggle";
        private const KeyCode ToggleKey = KeyCode.F8;
        private const int MainWindowId = 0xC07E;
        private const int LogsWindowId = 0xC07F;
        private const int OnboardingWindowId = 0xC080;
        private const string OverlayInputCaptureOwnerId = "Cortex.Shell";

        private Rect _windowRect = new Rect(70f, 70f, 1180f, 760f);
        private Rect _logWindowRect = new Rect(100f, 100f, 980f, 620f);
        private bool _visible;
        private Vector2 _windowScroll = Vector2.zero;
        private string _draggingContainerId = string.Empty;
        private WorkbenchHostLocation _draggingContainerSourceHost = WorkbenchHostLocation.PrimarySideHost;

        private ICortexSettingsStore _settingsStore;
        private IWorkbenchPersistenceService _workbenchPersistenceService;
        private IProjectCatalog _projectCatalog;
        private ILoadedModCatalog _loadedModCatalog;
        private IProjectWorkspaceService _projectWorkspaceService;
        private IPathInteractionService _pathInteractionService;
        private IWorkspaceBrowserService _workspaceBrowserService;
        private IDecompilerExplorerService _decompilerExplorerService;
        private IDocumentService _documentService;
        private IBuildCommandResolver _buildCommandResolver;
        private IBuildExecutor _buildExecutor;
        private IReferenceCatalogService _referenceCatalogService;
        private ISourceLookupIndex _sourceLookupIndex;
        private ISourcePathResolver _sourcePathResolver;
        private ISourceReferenceService _sourceReferenceService;
        private IRuntimeLogFeed _runtimeLogFeed;
        private IRuntimeSourceNavigationService _runtimeSourceNavigationService;
        private IRuntimeToolBridge _runtimeToolBridge;
        private IRestartCoordinator _restartCoordinator;
        private IOverlayInputCaptureService _overlayInputCaptureService;
        private ITextSearchService _textSearchService;
        private CortexNavigationService _navigationService;
        private IWorkbenchRuntime _workbenchRuntime;
        private IWorkbenchRuntimeFactory _workbenchRuntimeFactory;
        private readonly EditorSymbolInteractionService _editorSymbolInteractionService = new EditorSymbolInteractionService();
        private readonly WorkbenchSearchService _workbenchSearchService = new WorkbenchSearchService();
        private readonly CortexShellLifecycleCoordinator _lifecycleCoordinator = new CortexShellLifecycleCoordinator();
        private readonly CortexShellModuleContributionRegistry _moduleContributionRegistry = new CortexShellModuleContributionRegistry();
        private readonly CortexShellBuiltInModuleRegistrar _moduleRegistrar = new CortexShellBuiltInModuleRegistrar();
        private readonly CortexShellCommandRouter _commandRouter = new CortexShellCommandRouter();
        private readonly CortexShellLayoutHostRouter _layoutHostRouter = new CortexShellLayoutHostRouter();
        private readonly CortexShellLanguageCoordinator _languageCoordinator = new CortexShellLanguageCoordinator();
        private readonly CortexShellLanguageRequestDispatcher _languageRequestDispatcher = new CortexShellLanguageRequestDispatcher();
        private readonly CortexShellLanguageResponseProcessor _languageResponseProcessor = new CortexShellLanguageResponseProcessor();
        private readonly CortexShellLanguageRuntimeState _languageRuntime = new CortexShellLanguageRuntimeState();
        private readonly CortexOnboardingCoordinator _onboardingCoordinator = new CortexOnboardingCoordinator();
        private readonly CortexShellOnboardingLifecycle _onboardingLifecycle = new CortexShellOnboardingLifecycle();

        private readonly CortexShellState _state = new CortexShellState();
        private ExternalWorkbenchPluginLoader _externalPluginLoader;
        private CortexShellLayoutContext _layoutContext;
        private CortexShellCommandContext _commandContext;
        private CortexShellModuleServices _moduleServices;
        private CortexShellModuleCompositionService _moduleCompositionService;
        private CortexShellModuleActivationService _moduleActivationService;
        private CortexShellModuleRenderService _moduleRenderService;
        private CortexShellLanguageRuntimeContext _languageRuntimeContext;
        private bool _moduleContributionsRegistered;

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
        private readonly Dictionary<string, Rect> _menuGroupRects = new Dictionary<string, Rect>(StringComparer.OrdinalIgnoreCase);
        private bool _lastOverlayMouseCapture;
        private bool _lastOverlayKeyboardCapture;

        private void Awake()
        {
            _lifecycleCoordinator.Awake(this);
        }

        private void Start()
        {
            _lifecycleCoordinator.Start(this);
        }

        private void OnDestroy()
        {
            _lifecycleCoordinator.Destroy(this);
        }

        private void Update()
        {
            if (!_visible)
            {
                ReleaseOverlayInputCapture();
            }

            _lifecycleCoordinator.Update(this);
        }

        private void OnGUI()
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

            if (!_visible)
            {
                ReleaseOverlayInputCapture();
                return;
            }

            if (_state.Onboarding.IsActive)
            {
                PreviewOnboardingSelections();
            }

            _frameSnapshot = _workbenchRuntime != null ? _workbenchRuntime.CreateSnapshot() : new WorkbenchPresentationSnapshot();
            CortexIdeLayout.ApplyTheme(_frameSnapshot.ThemeTokens, _frameSnapshot.ActiveThemeId);
            EnsureStyles(_frameSnapshot.ThemeTokens, _frameSnapshot.ActiveThemeId);
            var previousSkin = GUI.skin;
            GUI.skin = CortexIdeLayout.GetWorkbenchSkin(previousSkin);
            ClampWindowsToScreen();
            UpdateOverlayInputCapture(Event.current);
            if (_state.Chrome.Main.IsCollapsed)
            {
                DrawCollapsedWindowButton(_state.Chrome.Main, ">", "Cortex");
            }
            else
            {
                _windowRect = GUI.Window(MainWindowId, _windowRect, DrawWindow, "Cortex IDE", _windowStyle);
                _state.Chrome.Main.ExpandedRect = _windowRect;
            }

            if (_state.Logs.ShowDetachedWindow && !_state.Onboarding.IsActive)
            {
                if (_state.Chrome.Logs.IsCollapsed)
                {
                    DrawCollapsedWindowButton(_state.Chrome.Logs, ">", "Logs");
                }
                else
                {
                    _logWindowRect = GUI.Window(LogsWindowId, _logWindowRect, DrawLogsWindow, "Cortex Logs", _windowStyle);
                    _state.Chrome.Logs.ExpandedRect = _logWindowRect;
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
            var snapshot = _frameSnapshot ?? (_workbenchRuntime != null ? _workbenchRuntime.CreateSnapshot() : new WorkbenchPresentationSnapshot());
            var onboardingActive = IsOnboardingActive();
            const float headerHeight = 30f;
            const float statusHeight = 24f;
            var contentRect = new Rect(6f, 24f, Mathf.Max(0f, _windowRect.width - 12f), Mathf.Max(0f, _windowRect.height - 30f));
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
                ApplyWindowResize(windowId, ref _windowRect, 920f, 580f);
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
            ApplyWindowResize(windowId, ref _logWindowRect, 760f, 420f);
            GUI.DragWindow(new Rect(0f, 0f, 10000f, 24f));
        }

        private void DrawActiveModule(WorkbenchPresentationSnapshot snapshot, string containerId, bool detachedWindow)
        {
            GetModuleRenderService().DrawActiveModule(snapshot, containerId, detachedWindow);
        }

        private void InitializeSettingsAndServices()
        {
            var gameRoot = Directory.GetParent(Application.dataPath).FullName;
            var smmRoot = Path.Combine(gameRoot, "SMM");
            var smmBin = Path.Combine(smmRoot, "bin");
            if (!Directory.Exists(smmBin))
            {
                Directory.CreateDirectory(smmBin);
            }

            _settingsStore = new JsonCortexSettingsStore(Path.Combine(smmBin, "cortex_settings.json"));
            _workbenchPersistenceService = new JsonWorkbenchPersistenceService(Path.Combine(smmBin, "cortex_workbench.json"));
            var settings = BuildEffectiveSettings(_settingsStore.Load(), gameRoot, smmRoot, smmBin);
            _state.Settings = settings;
            _windowRect = new Rect(settings.WindowX, settings.WindowY, settings.WindowWidth, settings.WindowHeight);
            _logWindowRect = new Rect(settings.WindowX + 30f, settings.WindowY + 30f, Math.Max(760f, settings.WindowWidth - 120f), Math.Max(460f, settings.WindowHeight - 140f));
            _state.Chrome.Main.ExpandedRect = _windowRect;
            _state.Chrome.Main.CollapsedRect = CortexWindowChromeController.BuildCollapsedRect(_windowRect, 126f, 28f);
            _state.Chrome.Logs.ExpandedRect = _logWindowRect;
            _state.Chrome.Logs.CollapsedRect = CortexWindowChromeController.BuildCollapsedRect(_logWindowRect, 110f, 26f);

            InitializeServices(gameRoot, smmRoot, smmBin, settings);
        }

        private void InitializeWorkbenchRuntime()
        {
            if (_workbenchRuntimeFactory == null)
            {
                MMLog.WriteWarning("[Cortex] Workbench runtime initialization skipped because no host runtime factory was configured.");
                return;
            }

            _workbenchRuntime = _workbenchRuntimeFactory.Create();
            if (_workbenchRuntime == null)
            {
                MMLog.WriteWarning("[Cortex] Workbench runtime initialization skipped because the host runtime factory returned null.");
                return;
            }

            if (_externalPluginLoader == null)
            {
                _externalPluginLoader = new ExternalWorkbenchPluginLoader();
            }

            RegisterExternalWorkbenchPlugins();
            _workbenchRuntime.LayoutState.PrimarySideWidth = _state.Settings != null ? _state.Settings.ProjectsPaneWidth : 360f;
            _workbenchRuntime.LayoutState.SecondarySideWidth = _state.Settings != null ? _state.Settings.EditorFilePaneWidth : 320f;
            _workbenchRuntime.LayoutState.PanelSize = _state.Settings != null ? _state.Settings.PanelPaneSize : 280f;
            _workbenchRuntime.ThemeState.ThemeId = _state.Settings != null && !string.IsNullOrEmpty(_state.Settings.ThemeId)
                ? _state.Settings.ThemeId
                : _workbenchRuntime.ThemeState.ThemeId;
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

        public void ConfigureHostServices(IPathInteractionService pathInteractionService, IWorkbenchRuntimeFactory workbenchRuntimeFactory)
        {
            _pathInteractionService = pathInteractionService;
            _workbenchRuntimeFactory = workbenchRuntimeFactory;
        }

        private void RegisterExternalWorkbenchPlugins()
        {
            if (_workbenchRuntime == null || _externalPluginLoader == null)
            {
                return;
            }

            var results = _externalPluginLoader.LoadPlugins(
                _state.Settings,
                _workbenchRuntime.CommandRegistry,
                _workbenchRuntime.ContributionRegistry,
                _moduleContributionRegistry);

            for (var i = 0; i < results.Count; i++)
            {
                var result = results[i];
                if (result == null)
                {
                    continue;
                }

                if (result.Loaded)
                {
                    _state.Diagnostics.Add("Loaded Cortex plugin " + result.DisplayName + " from " + result.AssemblyPath + ".");
                }
                else if (!string.IsNullOrEmpty(result.StatusMessage))
                {
                    _state.Diagnostics.Add("Cortex plugin skip: " + result.StatusMessage);
                }
            }
        }

        private void InitializeServices(string gameRoot, string smmRoot, string smmBin, CortexSettings settings)
        {
            MMLog.WriteInfo("[Cortex] Initializing runtime. WorkspaceRoot=" +
                (settings != null ? settings.WorkspaceRootPath ?? string.Empty : string.Empty) +
                ", ManagedRoot=" + (settings != null ? settings.ManagedAssemblyRootPath ?? string.Empty : string.Empty) +
                ", LanguageServiceEnabled=" + (settings != null && settings.EnableRoslynLanguageService) +
                ", CompletionProvider=" + (settings != null ? settings.CompletionAugmentationProviderId ?? string.Empty : string.Empty) + ".");

            var existingFeed = _runtimeLogFeed as MmLogRuntimeLogFeed;
            if (existingFeed != null)
            {
                existingFeed.Detach();
            }

            var projectCatalogPath = string.IsNullOrEmpty(settings.ProjectCatalogPath)
                ? Path.Combine(smmBin, "cortex_projects.json")
                : settings.ProjectCatalogPath;

            var decompilerPath = string.IsNullOrEmpty(settings.DecompilerPathOverride)
                ? new ModAPI.Inspector.ExternalProcessManager().ResolveDecompilerPath()
                : settings.DecompilerPathOverride;

            MMLog.WriteDebug("[Cortex] Service inputs resolved. ProjectCatalogPath=" + projectCatalogPath +
                ", DecompilerPath=" + decompilerPath +
                ", DecompilerCachePath=" + (settings != null ? settings.DecompilerCachePath ?? string.Empty : string.Empty) + ".");

            _projectCatalog = new ProjectCatalog(new JsonProjectConfigurationStore(projectCatalogPath));
            _loadedModCatalog = new ModApiLoadedModCatalog();
            _sourceLookupIndex = new SourceLookupIndexService();
            _projectWorkspaceService = new ProjectWorkspaceService(_sourceLookupIndex);
            _workspaceBrowserService = new WorkspaceBrowserService(_sourceLookupIndex);
            _referenceCatalogService = new ReferenceCatalogService();
            _decompilerExplorerService = new DecompilerExplorerService(_referenceCatalogService);
            _documentService = new FileDocumentService();
            _buildCommandResolver = new CsprojBuildCommandResolver();
            _buildExecutor = new ProcessBuildExecutor();
            _sourcePathResolver = new SourcePathResolver(_sourceLookupIndex);
            _sourceReferenceService = new SourceReferenceService(new DecompilerCliClient(decompilerPath, settings.DecompilerCachePath, 15000));
            var runtimeLogFeed = new MmLogRuntimeLogFeed();
            runtimeLogFeed.Attach();
            _runtimeLogFeed = runtimeLogFeed;
            _runtimeSourceNavigationService = new RuntimeSourceNavigationService(new ModApiRuntimeSymbolResolver(_sourcePathResolver), _sourcePathResolver);
            _runtimeToolBridge = new ModApiRuntimeToolBridge();
            _restartCoordinator = new ModApiRestartCoordinator(new RestartRequestWriter());
            _textSearchService = new TextSearchService();
            _navigationService = new CortexNavigationService(_documentService, _sourceReferenceService, _runtimeSourceNavigationService, _sourceLookupIndex);
            _overlayInputCaptureService = null;
            InitializeLanguageService(smmBin, settings);
            EnableMmLogRuntimeIntegration();
        }

        private void UpdateOverlayInputCapture(Event currentEvent)
        {
            var captureMouse = false;
            var captureKeyboard = GUIUtility.hotControl != 0 || GUIUtility.keyboardControl != 0;

            if (_state.Onboarding.IsActive)
            {
                ApplyOverlayInputCapture(true, true);
                return;
            }

            if (_visible)
            {
                Vector2 guiMouse;
                if (currentEvent != null)
                {
                    guiMouse = currentEvent.mousePosition;
                }
                else
                {
                    var screenMouse = Input.mousePosition;
                    guiMouse = new Vector2(screenMouse.x, Screen.height - screenMouse.y);
                }

                captureMouse = GUIUtility.hotControl != 0 || IsPointWithinVisibleChrome(guiMouse);
            }

            ApplyOverlayInputCapture(captureMouse, captureKeyboard);
        }

        private void ReleaseOverlayInputCapture()
        {
            ApplyOverlayInputCapture(false, false);
        }

        private void ApplyOverlayInputCapture(bool captureMouse, bool captureKeyboard)
        {
            var hasChanged = _lastOverlayMouseCapture != captureMouse || _lastOverlayKeyboardCapture != captureKeyboard;
            var captureService = ResolveOverlayInputCaptureService();
            if (captureService == null)
            {
                return;
            }

            if (!hasChanged)
            {
                return;
            }

            if (captureMouse || captureKeyboard)
            {
                captureService.ReportCapture(OverlayInputCaptureOwnerId, captureMouse, captureKeyboard);
            }
            else
            {
                captureService.ReleaseCapture(OverlayInputCaptureOwnerId);
            }

            _lastOverlayMouseCapture = captureMouse;
            _lastOverlayKeyboardCapture = captureKeyboard;
            MMLog.WriteDebug("[Cortex.InputCapture] Reported overlay capture. Mouse=" + captureMouse + ", Keyboard=" + captureKeyboard + ".");
        }

        private IOverlayInputCaptureService ResolveOverlayInputCaptureService()
        {
            if (_overlayInputCaptureService != null)
            {
                return _overlayInputCaptureService;
            }

            if (!ModAPIRegistry.IsAPIRegistered(OverlayInputCaptureApi.Name))
            {
                return null;
            }

            IOverlayInputCaptureService captureService;
            if (!ModAPIRegistry.TryGetAPI<IOverlayInputCaptureService>(OverlayInputCaptureApi.Name, out captureService))
            {
                return null;
            }

            _overlayInputCaptureService = captureService;
            return _overlayInputCaptureService;
        }

        private bool IsPointWithinVisibleChrome(Vector2 guiPoint)
        {
            if (_state.Chrome.Main.IsCollapsed)
            {
                if (_state.Chrome.Main.CollapsedRect.Contains(guiPoint))
                {
                    return true;
                }
            }
            else if (_windowRect.Contains(guiPoint))
            {
                return true;
            }

            if (_state.Logs.ShowDetachedWindow)
            {
                if (_state.Chrome.Logs.IsCollapsed)
                {
                    if (_state.Chrome.Logs.CollapsedRect.Contains(guiPoint))
                    {
                        return true;
                    }
                }
                else if (_logWindowRect.Contains(guiPoint))
                {
                    return true;
                }
            }

            return false;
        }

        private void ApplySettingsChanges()
        {
            MMLog.WriteInfo("[Cortex] Applying settings changes. ActiveProject=" +
                (_state.SelectedProject != null ? _state.SelectedProject.ModId ?? string.Empty : string.Empty) +
                ", ActiveDocument=" + (_state.Documents.ActiveDocument != null ? _state.Documents.ActiveDocument.FilePath ?? string.Empty : string.Empty) +
                ", ReloadRequested=" + _state.ReloadSettingsRequested + ".");
            PersistWorkbenchSession();
            PersistWindowSettings();

            var gameRoot = Directory.GetParent(Application.dataPath).FullName;
            var smmRoot = Path.Combine(gameRoot, "SMM");
            var smmBin = Path.Combine(smmRoot, "bin");
            var activeProjectId = _state.SelectedProject != null ? _state.SelectedProject.ModId : string.Empty;
            var activeDocumentPath = _state.Documents.ActiveDocument != null ? _state.Documents.ActiveDocument.FilePath : string.Empty;

            _state.Settings = BuildEffectiveSettings(_state.Settings, gameRoot, smmRoot, smmBin);
            InitializeServices(gameRoot, smmRoot, smmBin, _state.Settings);
            _settingsStore.Save(_state.Settings);

            if (!string.IsNullOrEmpty(activeProjectId))
            {
                _state.SelectedProject = _projectCatalog.GetProject(activeProjectId) ?? _state.SelectedProject;
            }

            if (!string.IsNullOrEmpty(activeDocumentPath))
            {
                for (var i = 0; i < _state.Documents.OpenDocuments.Count; i++)
                {
                    if (string.Equals(_state.Documents.OpenDocuments[i].FilePath, activeDocumentPath, StringComparison.OrdinalIgnoreCase))
                    {
                        _state.Documents.ActiveDocument = _state.Documents.OpenDocuments[i];
                        _state.Documents.ActiveDocumentPath = activeDocumentPath;
                        break;
                    }
                }
            }

            _windowRect = new Rect(_state.Settings.WindowX, _state.Settings.WindowY, _state.Settings.WindowWidth, _state.Settings.WindowHeight);
            ClampWindowsToScreen();
            _state.ReloadSettingsRequested = false;
            _state.StatusMessage = "Cortex settings applied.";
            if (_workbenchRuntime != null)
            {
                _workbenchRuntime.LayoutState.PrimarySideWidth = _state.Settings.ProjectsPaneWidth;
                _workbenchRuntime.LayoutState.SecondarySideWidth = _state.Settings.EditorFilePaneWidth;
                _workbenchRuntime.LayoutState.PanelSize = _state.Settings.PanelPaneSize;
                _workbenchRuntime.ThemeState.ThemeId = string.IsNullOrEmpty(_state.Settings.ThemeId)
                    ? _workbenchRuntime.ThemeState.ThemeId
                    : _state.Settings.ThemeId;
            }
        }

        private CortexSettings BuildEffectiveSettings(CortexSettings settings, string gameRoot, string smmRoot, string smmBin)
        {
            var effective = settings ?? new CortexSettings();

            if (string.IsNullOrEmpty(effective.ModsRootPath))
            {
                effective.ModsRootPath = Path.Combine(smmRoot, "mods");
            }

            if (string.IsNullOrEmpty(effective.WorkspaceRootPath))
            {
                effective.WorkspaceRootPath = effective.ModsRootPath;
            }

            if (string.IsNullOrEmpty(effective.ManagedAssemblyRootPath))
            {
                effective.ManagedAssemblyRootPath = Path.Combine(Path.Combine(gameRoot, "Sheltered_Data"), "Managed");
            }

            if (string.IsNullOrEmpty(effective.AdditionalSourceRoots))
            {
                effective.AdditionalSourceRoots = effective.WorkspaceRootPath;
            }

            if (string.IsNullOrEmpty(effective.CortexPluginSearchRoots))
            {
                effective.CortexPluginSearchRoots = effective.ModsRootPath;
            }

            if (string.IsNullOrEmpty(effective.LogFilePath))
            {
                effective.LogFilePath = Path.Combine(smmRoot, "mod_manager.log");
            }

            if (string.IsNullOrEmpty(effective.ProjectCatalogPath))
            {
                effective.ProjectCatalogPath = Path.Combine(smmBin, "cortex_projects.json");
            }

            if (string.IsNullOrEmpty(effective.DecompilerCachePath))
            {
                effective.DecompilerCachePath = Path.Combine(smmBin, "cortex_cache");
            }

            if (string.IsNullOrEmpty(effective.DefaultBuildConfiguration))
            {
                effective.DefaultBuildConfiguration = "Debug";
            }

            if (effective.BuildTimeoutMs <= 0)
            {
                effective.BuildTimeoutMs = 300000;
            }

            if (effective.RoslynServiceTimeoutMs <= 0)
            {
                effective.RoslynServiceTimeoutMs = 15000;
            }

            if (string.IsNullOrEmpty(effective.CompletionAugmentationProviderId))
            {
                effective.CompletionAugmentationProviderId = CompletionAugmentationProviderIds.Tabby;
            }

            if (effective.EnableTabbyCompletion && !effective.EnableCompletionAugmentation)
            {
                effective.EnableCompletionAugmentation = true;
                effective.CompletionAugmentationProviderId = CompletionAugmentationProviderIds.Tabby;
            }

            if (effective.CompletionAugmentationSnippetDocumentLimit < 0)
            {
                effective.CompletionAugmentationSnippetDocumentLimit = 3;
            }

            if (effective.CompletionAugmentationSnippetCharacterLimit <= 0)
            {
                effective.CompletionAugmentationSnippetCharacterLimit = 800;
            }

            if (effective.TabbyRequestTimeoutMs <= 0)
            {
                effective.TabbyRequestTimeoutMs = 8000;
            }

            if (string.IsNullOrEmpty(effective.OllamaServerUrl))
            {
                effective.OllamaServerUrl = "http://localhost:11434";
            }

            if (string.IsNullOrEmpty(effective.OllamaSystemPrompt))
            {
                effective.OllamaSystemPrompt = CompletionAugmentationPromptDefaults.OllamaSystemPrompt;
            }

            if (effective.OllamaRequestTimeoutMs <= 0)
            {
                effective.OllamaRequestTimeoutMs = 8000;
            }

            if (string.IsNullOrEmpty(effective.OpenRouterBaseUrl))
            {
                effective.OpenRouterBaseUrl = "https://openrouter.ai/api/v1";
            }

            if (string.IsNullOrEmpty(effective.OpenRouterPromptPreamble))
            {
                effective.OpenRouterPromptPreamble = CompletionAugmentationPromptDefaults.OpenRouterPromptPreamble;
            }

            if (string.IsNullOrEmpty(effective.OpenRouterAppTitle))
            {
                effective.OpenRouterAppTitle = "Cortex";
            }

            if (effective.OpenRouterRequestTimeoutMs <= 0)
            {
                effective.OpenRouterRequestTimeoutMs = 10000;
            }

            if (string.IsNullOrEmpty(effective.DefaultOnboardingProfileId))
            {
                effective.DefaultOnboardingProfileId = "cortex.onboarding.profile.ide";
            }

            if (string.IsNullOrEmpty(effective.DefaultOnboardingLayoutPresetId))
            {
                effective.DefaultOnboardingLayoutPresetId = "cortex.onboarding.layout.visual-studio";
            }

            if (string.IsNullOrEmpty(effective.DefaultOnboardingThemeId))
            {
                effective.DefaultOnboardingThemeId = "cortex.vs-dark";
            }

            if (effective.MaxRecentLogs <= 0)
            {
                effective.MaxRecentLogs = 300;
            }

            if (effective.LogsPaneWidth < 360f)
            {
                effective.LogsPaneWidth = 520f;
            }

            if (effective.ProjectsPaneWidth < 280f)
            {
                effective.ProjectsPaneWidth = 360f;
            }

            if (effective.EditorFilePaneWidth < 240f)
            {
                effective.EditorFilePaneWidth = 420f;
            }

            if (effective.PanelPaneSize < 150f)
            {
                effective.PanelPaneSize = 280f;
            }

            if (effective.WindowWidth < 980f || effective.WindowHeight < 620f)
            {
                effective.WindowWidth = Math.Max(980f, Screen.width * 0.82f);
                effective.WindowHeight = Math.Max(620f, Screen.height * 0.82f);
            }

            if (effective.WindowX < 0f)
            {
                effective.WindowX = 40f;
            }

            if (effective.WindowY < 0f)
            {
                effective.WindowY = 40f;
            }

            return effective;
        }

        private void PersistWindowSettings()
        {
            if (_state.Settings == null || _settingsStore == null)
            {
                return;
            }

            var persistedRect = _state.Chrome.Main.IsCollapsed ? _state.Chrome.Main.ExpandedRect : _windowRect;
            _state.Settings.WindowX = persistedRect.x;
            _state.Settings.WindowY = persistedRect.y;
            _state.Settings.WindowWidth = persistedRect.width;
            _state.Settings.WindowHeight = persistedRect.height;
            _settingsStore.Save(_state.Settings);
        }

        private void ClampWindowsToScreen()
        {
            _windowRect = ClampRectToScreen(_windowRect, 920f, 580f);
            _logWindowRect = ClampRectToScreen(_logWindowRect, 760f, 420f);
            _state.Chrome.Main.ExpandedRect = ClampRectToScreen(_state.Chrome.Main.ExpandedRect.width > 0f ? _state.Chrome.Main.ExpandedRect : _windowRect, 920f, 580f);
            _state.Chrome.Logs.ExpandedRect = ClampRectToScreen(_state.Chrome.Logs.ExpandedRect.width > 0f ? _state.Chrome.Logs.ExpandedRect : _logWindowRect, 760f, 420f);
            _state.Chrome.Main.CollapsedRect = ClampRectToScreen(
                _state.Chrome.Main.CollapsedRect.width > 0f ? _state.Chrome.Main.CollapsedRect : CortexWindowChromeController.BuildCollapsedRect(_windowRect, 126f, 28f),
                126f,
                28f);
            _state.Chrome.Logs.CollapsedRect = ClampRectToScreen(
                _state.Chrome.Logs.CollapsedRect.width > 0f ? _state.Chrome.Logs.CollapsedRect : CortexWindowChromeController.BuildCollapsedRect(_logWindowRect, 110f, 26f),
                110f,
                26f);
        }

        private static Rect ClampRectToScreen(Rect rect, float minWidth, float minHeight)
        {
            var width = Mathf.Clamp(rect.width, minWidth, Math.Max(minWidth, Screen.width - 20f));
            var height = Mathf.Clamp(rect.height, minHeight, Math.Max(minHeight, Screen.height - 20f));
            var x = Mathf.Clamp(rect.x, 0f, Math.Max(0f, Screen.width - width));
            var y = Mathf.Clamp(rect.y, 0f, Math.Max(0f, Screen.height - height));
            return new Rect(x, y, width, height);
        }

        private void FitMainWindowToScreen()
        {
            _windowRect = new Rect(
                Mathf.Max(10f, Screen.width * 0.05f),
                Mathf.Max(10f, Screen.height * 0.05f),
                Mathf.Max(980f, Screen.width * 0.9f),
                Mathf.Max(620f, Screen.height * 0.88f));
            _state.Chrome.Main.ExpandedRect = _windowRect;
        }

        private static void EnableMmLogRuntimeIntegration()
        {
            ModAPI.Core.MMLog.ConfigureRuntimeIntegration(MMLogRuntimeOptions.CortexDefaults());
        }

        private void DisableMmLogRuntimeIntegration()
        {
            var runtimeLogFeed = _runtimeLogFeed as MmLogRuntimeLogFeed;
            if (runtimeLogFeed != null)
            {
                runtimeLogFeed.Detach();
            }

            ModAPI.Core.MMLog.ConfigureRuntimeIntegration(MMLogRuntimeOptions.Disabled());
        }

        private void EnsureStyles(ThemeTokenSet themeTokens, string themeId)
        {
            var effectiveThemeId = string.IsNullOrEmpty(themeId) ? "cortex.vs-dark" : themeId;
            if (!string.Equals(_appliedThemeId, effectiveThemeId, StringComparison.OrdinalIgnoreCase))
            {
                _titleStyle = null;
                _menuStyle = null;
                _statusStyle = null;
                _captionStyle = null;
                _sectionStyle = null;
                _windowStyle = null;
                _tabStyle = null;
                _activeTabStyle = null;
                _tabCloseButtonStyle = null;
                _collapsedWindowStyle = null;
                _windowBackground = null;
                _sectionBackground = null;
                _tabBackground = null;
                _tabActiveBackground = null;
                _collapsedWindowBackground = null;
                _appliedThemeId = effectiveThemeId;
            }

            var textColor = CortexIdeLayout.ParseColor(themeTokens != null ? themeTokens.TextColor : string.Empty, new Color(0.96f, 0.96f, 0.96f, 1f));
            var mutedTextColor = CortexIdeLayout.ParseColor(themeTokens != null ? themeTokens.MutedTextColor : string.Empty, new Color(0.72f, 0.76f, 0.82f, 1f));
            var surfaceColor = CortexIdeLayout.ParseColor(themeTokens != null ? themeTokens.SurfaceColor : string.Empty, new Color(0.1f, 0.1f, 0.12f, 0.96f));
            var backgroundColor = CortexIdeLayout.ParseColor(themeTokens != null ? themeTokens.BackgroundColor : string.Empty, new Color(0.05f, 0.05f, 0.07f, 0.97f));
            var headerColor = CortexIdeLayout.ParseColor(themeTokens != null ? themeTokens.HeaderColor : string.Empty, new Color(0.16f, 0.17f, 0.2f, 1f));
            var accentColor = CortexIdeLayout.ParseColor(themeTokens != null ? themeTokens.AccentColor : string.Empty, new Color(0.22f, 0.3f, 0.4f, 1f));

            if (_titleStyle == null)
            {
                _titleStyle = new GUIStyle(GUI.skin.label);
                _titleStyle.fontSize = 13;
                _titleStyle.fontStyle = FontStyle.Bold;
                GuiStyleUtil.ApplyTextColorToAllStates(_titleStyle, textColor);
            }

            if (_menuStyle == null)
            {
                _menuStyle = new GUIStyle(GUI.skin.label);
                _menuStyle.fontSize = 12;
                _menuStyle.fontStyle = FontStyle.Normal;
                _menuStyle.padding = new RectOffset(6, 6, 3, 3);
                GuiStyleUtil.ApplyTextColorToAllStates(_menuStyle, textColor);
            }

            if (_statusStyle == null)
            {
                _statusStyle = new GUIStyle(GUI.skin.label);
                _statusStyle.wordWrap = true;
                GuiStyleUtil.ApplyTextColorToAllStates(_statusStyle, CortexIdeLayout.ParseColor(themeTokens != null ? themeTokens.TextColor : string.Empty, new Color(0.88f, 0.88f, 0.9f, 1f)));
            }

            if (_captionStyle == null)
            {
                _captionStyle = new GUIStyle(GUI.skin.label);
                _captionStyle.wordWrap = true;
                GuiStyleUtil.ApplyTextColorToAllStates(_captionStyle, mutedTextColor);
            }

            if (_sectionStyle == null)
            {
                _sectionStyle = new GUIStyle(GUI.skin.box);
                _sectionBackground = MakeTex(surfaceColor);
                GuiStyleUtil.ApplyBackgroundToAllStates(_sectionStyle, _sectionBackground);
                _sectionStyle.padding = new RectOffset(8, 8, 6, 6);
                _sectionStyle.margin = new RectOffset(0, 0, 0, 0);
            }

            if (_windowStyle == null)
            {
                _windowBackground = MakeTex(surfaceColor);
                _windowStyle = new GUIStyle(GUI.skin.window);
                GuiStyleUtil.ApplyBackgroundToAllStates(_windowStyle, _windowBackground);
                GuiStyleUtil.ApplyTextColorToAllStates(_windowStyle, textColor);
                _windowStyle.padding = new RectOffset(6, 6, 24, 6);
                _windowStyle.margin = new RectOffset(0, 0, 0, 0);
            }

            if (_tabStyle == null)
            {
                _tabBackground = MakeTex(headerColor);
                _tabStyle = new GUIStyle(GUI.skin.button);
                GuiStyleUtil.ApplyBackgroundToAllStates(_tabStyle, _tabBackground);
                GuiStyleUtil.ApplyTextColorToAllStates(_tabStyle, mutedTextColor);
                _tabStyle.alignment = TextAnchor.MiddleCenter;
                _tabStyle.padding = new RectOffset(10, 10, 5, 5);
                _tabStyle.margin = new RectOffset(0, 2, 0, 0);
            }

            if (_activeTabStyle == null)
            {
                _tabActiveBackground = MakeTex(accentColor);
                _activeTabStyle = new GUIStyle(_tabStyle);
                GuiStyleUtil.ApplyBackgroundToAllStates(_activeTabStyle, _tabActiveBackground);
                GuiStyleUtil.ApplyTextColorToAllStates(_activeTabStyle, Color.white);
                _activeTabStyle.fontStyle = FontStyle.Bold;
            }

            if (_tabCloseButtonStyle == null)
            {
                _tabCloseButtonStyle = new GUIStyle(GUI.skin.button);
                _tabCloseButtonStyle.alignment = TextAnchor.MiddleCenter;
                _tabCloseButtonStyle.fontSize = 10;
                _tabCloseButtonStyle.padding = new RectOffset(0, 0, 0, 0);
                _tabCloseButtonStyle.margin = new RectOffset(0, 0, 0, 0);
                GuiStyleUtil.ApplyBackgroundToAllStates(_tabCloseButtonStyle, MakeTex(CortexIdeLayout.Blend(headerColor, backgroundColor, 0.45f)));
                GuiStyleUtil.ApplyTextColorToAllStates(_tabCloseButtonStyle, textColor);
            }

            if (_collapsedWindowStyle == null)
            {
                _collapsedWindowBackground = MakeTex(CortexIdeLayout.ParseColor(themeTokens != null ? themeTokens.HeaderColor : string.Empty, new Color(0.09f, 0.11f, 0.15f, 0.98f)));
                _collapsedWindowStyle = new GUIStyle(GUI.skin.button);
                GuiStyleUtil.ApplyBackgroundToAllStates(_collapsedWindowStyle, _collapsedWindowBackground);
                _collapsedWindowStyle.alignment = TextAnchor.MiddleLeft;
                _collapsedWindowStyle.padding = new RectOffset(10, 10, 4, 4);
                _collapsedWindowStyle.fontStyle = FontStyle.Bold;
                GuiStyleUtil.ApplyTextColorToAllStates(_collapsedWindowStyle, textColor);
            }
        }

        private void DrawCollapsedWindowButton(CortexWindowChromeState chromeState, string glyph, string title)
        {
            if (chromeState == null)
            {
                return;
            }

            if (CortexWindowChromeController.DrawCollapsedButton(chromeState.CollapsedRect, glyph + " " + title, _collapsedWindowStyle))
            {
                chromeState.IsCollapsed = false;
            }
        }

        private void DrawLogsWindowHeaderActions()
        {
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            var actions = new List<CortexWindowAction>();
            actions.Add(BuildGlyphWindowAction(
                "logs.collapse",
                "_",
                "Minimize logs window",
                delegate
                {
                    _logWindowRect = CortexWindowChromeController.ToggleCollapsed(_state.Chrome.Logs, _logWindowRect, 110f, 26f);
                }));
            actions.Add(BuildGlyphWindowAction(
                "logs.close",
                "X",
                "Close detached logs window",
                delegate
                {
                    _state.Logs.ShowDetachedWindow = false;
                    _state.Chrome.Logs.IsCollapsed = false;
                }));
            CortexWindowChromeController.DrawActions(actions);
            GUILayout.EndHorizontal();
        }

        private static Texture2D MakeTex(Color color)
        {
            var texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }

        private static CortexWindowAction BuildGlyphWindowAction(string actionId, string label, string toolTip, Action execute)
        {
            return new CortexWindowAction
            {
                ActionId = actionId,
                Label = label,
                ToolTip = toolTip,
                Width = 24f,
                Height = 20f,
                Execute = execute
            };
        }

        private static void ApplyWindowResize(int windowId, ref Rect windowRect, float minWidth, float minHeight)
        {
            var localRect = new Rect(0f, 0f, windowRect.width, windowRect.height);
            localRect = CortexWindowChromeController.DrawResizeHandle(
                windowId,
                localRect,
                minWidth,
                minHeight,
                Math.Max(minWidth, Screen.width - 12f),
                Math.Max(minHeight, Screen.height - 12f));
            windowRect.width = localRect.width;
            windowRect.height = localRect.height;
        }

        private static void RegisterToggleAction()
        {
            if (!InputActionRegistry.IsRegistered(ToggleActionId))
            {
                InputActionRegistry.Register(new ModInputAction(
                    ToggleActionId,
                    "Toggle Cortex",
                    "Cortex",
                    new InputBinding(ToggleKey, KeyCode.None),
                    "Open or close the Cortex IDE shell."));
            }
        }

        private void ActivateContainer(string containerId)
        {
            _layoutHostRouter.ActivateContainer(GetLayoutContext(), containerId);
        }

        private WorkbenchHostLocation ResolveHostLocation(string containerId)
        {
            return _layoutHostRouter.ResolveHostLocation(GetLayoutContext(), containerId);
        }

        private void DockContainer(string containerId, WorkbenchHostLocation hostLocation)
        {
            _layoutHostRouter.DockContainer(GetLayoutContext(), containerId, hostLocation);
        }

        private void HideContainer(string containerId)
        {
            _layoutHostRouter.HideContainer(GetLayoutContext(), containerId);
        }

        private string FindFirstContainerForHost(WorkbenchHostLocation hostLocation, string excludedContainerId)
        {
            return _layoutHostRouter.FindFirstContainerForHost(GetLayoutContext(), hostLocation, excludedContainerId);
        }

        private string MapLegacyTabIndex(int index)
        {
            return _layoutHostRouter.MapLegacyTabIndex(index);
        }

    }
}
