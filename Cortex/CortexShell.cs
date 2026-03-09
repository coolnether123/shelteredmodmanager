using System;
using System.Collections.Generic;
using System.IO;
using Cortex.Adapters;
using Cortex.Chrome;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.Core.Services;
using Cortex.Host.Unity.Runtime;
using Cortex.Modules.Build;
using Cortex.Modules.Editor;
using Cortex.Modules.Logs;
using Cortex.Modules.Projects;
using Cortex.Modules.Reference;
using Cortex.Modules.Runtime;
using Cortex.Modules.Settings;
using Cortex.Presentation.Models;
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

        private Rect _windowRect = new Rect(70f, 70f, 1180f, 760f);
        private Rect _logWindowRect = new Rect(100f, 100f, 980f, 620f);
        private bool _visible;
        private Vector2 _windowScroll = Vector2.zero;

        private ICortexSettingsStore _settingsStore;
        private IWorkbenchPersistenceService _workbenchPersistenceService;
        private IProjectCatalog _projectCatalog;
        private IDocumentService _documentService;
        private IBuildCommandResolver _buildCommandResolver;
        private IBuildExecutor _buildExecutor;
        private ISourceReferenceService _sourceReferenceService;
        private IRuntimeLogFeed _runtimeLogFeed;
        private IRuntimeSourceNavigationService _runtimeSourceNavigationService;
        private IRuntimeToolBridge _runtimeToolBridge;
        private IRestartCoordinator _restartCoordinator;
        private UnityWorkbenchRuntime _workbenchRuntime;
        private readonly HashSet<string> _activatedContainers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private readonly CortexShellState _state = new CortexShellState();
        private LogsModule _logsModule;
        private ProjectsModule _projectsModule;
        private EditorModule _editorModule;
        private BuildModule _buildModule;
        private ReferenceModule _referenceModule;
        private RuntimeToolsModule _runtimeToolsModule;
        private SettingsModule _settingsModule;

        private GUIStyle _titleStyle;
        private GUIStyle _statusStyle;
        private GUIStyle _captionStyle;
        private GUIStyle _sectionStyle;
        private GUIStyle _windowStyle;
        private GUIStyle _tabStyle;
        private GUIStyle _activeTabStyle;
        private GUIStyle _collapsedWindowStyle;
        private Texture2D _windowBackground;
        private Texture2D _sectionBackground;
        private Texture2D _tabBackground;
        private Texture2D _tabActiveBackground;
        private Texture2D _collapsedWindowBackground;

        private void Awake()
        {
            try
            {
                gameObject.name = "Cortex.Shell";
                DontDestroyOnLoad(gameObject);
                InitializeSettingsAndServices();
                RestoreWorkbenchSession();
                InitializeWorkbenchRuntime();
                RegisterCommandHandlers();
                RegisterToggleAction();
            }
            catch (Exception ex)
            {
                MMLog.WriteError("[Cortex] Awake failed: " + ex);
                throw;
            }
        }

        private void OnDestroy()
        {
            DisableMmLogRuntimeIntegration();
            PersistWorkbenchSession();
            PersistWindowSettings();
        }

        private void Update()
        {
            if (InputActionRegistry.IsDown(ToggleActionId))
            {
                ExecuteCommand("cortex.shell.toggle", null);
            }

            if (_state.ReloadSettingsRequested)
            {
                ApplySettingsChanges();
            }

            if (!string.IsNullOrEmpty(_state.Workbench.RequestedContainerId))
            {
                ActivateContainer(_state.Workbench.RequestedContainerId);
                _state.Workbench.RequestedContainerId = string.Empty;
            }
            else if (_state.Workbench.RequestedTabIndex >= 0)
            {
                ActivateContainer(MapLegacyTabIndex(_state.Workbench.RequestedTabIndex));
                _state.Workbench.RequestedTabIndex = -1;
            }
        }

        private void OnGUI()
        {
            if (!_visible)
            {
                return;
            }

            EnsureStyles();
            ClampWindowsToScreen();
            if (_state.Chrome.Main.IsCollapsed)
            {
                DrawCollapsedWindowButton(_state.Chrome.Main, ">", "Cortex");
            }
            else
            {
                _windowRect = GUI.Window(MainWindowId, _windowRect, DrawWindow, "Cortex IDE", _windowStyle);
                _state.Chrome.Main.ExpandedRect = _windowRect;
            }

            if (_state.Logs.ShowDetachedWindow)
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
        }

        private void DrawWindow(int windowId)
        {
            var snapshot = _workbenchRuntime != null ? _workbenchRuntime.CreateSnapshot() : new WorkbenchPresentationSnapshot();
            GUILayout.BeginVertical();
            _windowScroll = GUILayout.BeginScrollView(_windowScroll, false, true, GUIStyle.none, GUI.skin.verticalScrollbar);
            DrawHeader(snapshot);
            DrawWorkbenchSelectors(snapshot);
            GUILayout.Space(6f);
            DrawWorkbenchSurface(snapshot);

            if (!_state.Logs.ShowDetachedWindow)
            {
                GUILayout.Space(6f);
                DrawPanelHost(snapshot);
            }

            GUILayout.Space(6f);
            GUILayout.BeginVertical(_sectionStyle);
            GUILayout.Label(string.IsNullOrEmpty(_state.StatusMessage) ? "Status: Ready" : "Status: " + _state.StatusMessage, _statusStyle);
            GUILayout.Label(BuildStatusLine(snapshot), _captionStyle);
            GUILayout.EndVertical();
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
            GUI.DragWindow(new Rect(0f, 0f, 10000f, 28f));
        }

        private void DrawLogsWindow(int windowId)
        {
            EnsureModuleActivated(CortexWorkbenchIds.LogsContainer);
            GUILayout.BeginVertical();
            DrawLogsWindowHeaderActions();
            _logsModule.Draw(_runtimeLogFeed, _runtimeSourceNavigationService, _documentService, _state, true);
            GUILayout.EndVertical();
            var localRect = new Rect(0f, 0f, _logWindowRect.width, _logWindowRect.height);
            localRect = CortexWindowChromeController.DrawResizeHandle(windowId, localRect, 760f, 420f, Math.Max(760f, Screen.width - 12f), Math.Max(420f, Screen.height - 12f));
            _logWindowRect.width = localRect.width;
            _logWindowRect.height = localRect.height;
            GUI.DragWindow(new Rect(0f, 0f, 10000f, 24f));
        }

        private void DrawActiveModule(string containerId, bool detachedWindow)
        {
            if (!CanActivateContainer(containerId))
            {
                GUILayout.BeginVertical(GUI.skin.box, GUILayout.ExpandHeight(true));
                GUILayout.Label(BuildActivationBlockedMessage(containerId));
                GUILayout.EndVertical();
                return;
            }

            EnsureModuleActivated(containerId);

            switch (containerId)
            {
                case CortexWorkbenchIds.LogsContainer:
                    _logsModule.Draw(_runtimeLogFeed, _runtimeSourceNavigationService, _documentService, _state, detachedWindow);
                    break;
                case CortexWorkbenchIds.ProjectsContainer:
                    _projectsModule.Draw(_projectCatalog, _state);
                    break;
                case CortexWorkbenchIds.EditorContainer:
                    _editorModule.Draw(_documentService, _state);
                    break;
                case CortexWorkbenchIds.BuildContainer:
                    _buildModule.Draw(_buildCommandResolver, _buildExecutor, _restartCoordinator, _documentService, _state);
                    break;
                case CortexWorkbenchIds.ReferenceContainer:
                    _referenceModule.Draw(_sourceReferenceService, _documentService, _state);
                    break;
                case CortexWorkbenchIds.RuntimeContainer:
                    _runtimeToolsModule.Draw(_runtimeToolBridge);
                    break;
                case CortexWorkbenchIds.SettingsContainer:
                    _settingsModule.Draw(_settingsStore, _state);
                    break;
                default:
                    _projectsModule.Draw(_projectCatalog, _state);
                    break;
            }
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
            _workbenchRuntime = new UnityWorkbenchRuntime();
            _workbenchRuntime.LayoutState.PrimarySideWidth = _state.Settings != null ? _state.Settings.ProjectsPaneWidth : 360f;
            _workbenchRuntime.LayoutState.PanelSize = _state.Settings != null ? _state.Settings.LogsPaneWidth : 520f;
            ActivateContainer(_state.Workbench.SideContainerId);
            ActivateContainer(_state.Workbench.EditorContainerId);
            ActivateContainer(_state.Workbench.PanelContainerId);
        }

        private void InitializeServices(string gameRoot, string smmRoot, string smmBin, CortexSettings settings)
        {
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

            _projectCatalog = new ProjectCatalog(new JsonProjectConfigurationStore(projectCatalogPath));
            _documentService = new FileDocumentService();
            _buildCommandResolver = new CsprojBuildCommandResolver();
            _buildExecutor = new ProcessBuildExecutor();
            _sourceReferenceService = new SourceReferenceService(new DecompilerCliClient(decompilerPath, settings.DecompilerCachePath, 15000));
            var runtimeLogFeed = new MmLogRuntimeLogFeed();
            runtimeLogFeed.Attach();
            _runtimeLogFeed = runtimeLogFeed;
            _runtimeSourceNavigationService = new RuntimeSourceNavigationService(new ModApiRuntimeSymbolResolver());
            _runtimeToolBridge = new ModApiRuntimeToolBridge();
            _restartCoordinator = new ModApiRestartCoordinator(new RestartRequestWriter());
            EnableMmLogRuntimeIntegration();
        }

        private void ApplySettingsChanges()
        {
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
                _workbenchRuntime.LayoutState.PanelSize = _state.Settings.LogsPaneWidth;
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
                effective.EditorFilePaneWidth = 320f;
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
            MMLog.ConfigureRuntimeIntegration(MMLogRuntimeOptions.CortexDefaults());
        }

        private void DisableMmLogRuntimeIntegration()
        {
            var runtimeLogFeed = _runtimeLogFeed as MmLogRuntimeLogFeed;
            if (runtimeLogFeed != null)
            {
                runtimeLogFeed.Detach();
            }

            MMLog.ConfigureRuntimeIntegration(MMLogRuntimeOptions.Disabled());
        }

        private void EnsureStyles()
        {
            if (_titleStyle == null)
            {
                _titleStyle = new GUIStyle(GUI.skin.label);
                _titleStyle.fontSize = 16;
                _titleStyle.fontStyle = FontStyle.Bold;
                GuiStyleUtil.ApplyTextColorToAllStates(_titleStyle, new Color(0.96f, 0.96f, 0.96f, 1f));
            }

            if (_statusStyle == null)
            {
                _statusStyle = new GUIStyle(GUI.skin.label);
                _statusStyle.wordWrap = true;
                GuiStyleUtil.ApplyTextColorToAllStates(_statusStyle, new Color(0.88f, 0.88f, 0.9f, 1f));
            }

            if (_captionStyle == null)
            {
                _captionStyle = new GUIStyle(GUI.skin.label);
                _captionStyle.wordWrap = true;
                GuiStyleUtil.ApplyTextColorToAllStates(_captionStyle, new Color(0.72f, 0.76f, 0.82f, 1f));
            }

            if (_sectionStyle == null)
            {
                _sectionStyle = new GUIStyle(GUI.skin.box);
                _sectionBackground = MakeTex(new Color(0.1f, 0.1f, 0.12f, 0.96f));
                GuiStyleUtil.ApplyBackgroundToAllStates(_sectionStyle, _sectionBackground);
                _sectionStyle.padding = new RectOffset(10, 10, 8, 8);
                _sectionStyle.margin = new RectOffset(4, 4, 4, 4);
            }

            if (_windowStyle == null)
            {
                _windowBackground = MakeTex(new Color(0.05f, 0.05f, 0.07f, 0.97f));
                _windowStyle = new GUIStyle(GUI.skin.window);
                GuiStyleUtil.ApplyBackgroundToAllStates(_windowStyle, _windowBackground);
                GuiStyleUtil.ApplyTextColorToAllStates(_windowStyle, new Color(0.96f, 0.96f, 0.96f, 1f));
                _windowStyle.padding = new RectOffset(8, 8, 24, 8);
            }

            if (_tabStyle == null)
            {
                _tabBackground = MakeTex(new Color(0.16f, 0.17f, 0.2f, 1f));
                _tabStyle = new GUIStyle(GUI.skin.button);
                GuiStyleUtil.ApplyBackgroundToAllStates(_tabStyle, _tabBackground);
                GuiStyleUtil.ApplyTextColorToAllStates(_tabStyle, new Color(0.86f, 0.88f, 0.92f, 1f));
                _tabStyle.alignment = TextAnchor.MiddleCenter;
                _tabStyle.padding = new RectOffset(10, 10, 4, 4);
                _tabStyle.margin = new RectOffset(0, 4, 0, 0);
            }

            if (_activeTabStyle == null)
            {
                _tabActiveBackground = MakeTex(new Color(0.22f, 0.3f, 0.4f, 1f));
                _activeTabStyle = new GUIStyle(_tabStyle);
                GuiStyleUtil.ApplyBackgroundToAllStates(_activeTabStyle, _tabActiveBackground);
                GuiStyleUtil.ApplyTextColorToAllStates(_activeTabStyle, Color.white);
                _activeTabStyle.fontStyle = FontStyle.Bold;
            }

            if (_collapsedWindowStyle == null)
            {
                _collapsedWindowBackground = MakeTex(new Color(0.09f, 0.11f, 0.15f, 0.98f));
                _collapsedWindowStyle = new GUIStyle(GUI.skin.button);
                GuiStyleUtil.ApplyBackgroundToAllStates(_collapsedWindowStyle, _collapsedWindowBackground);
                _collapsedWindowStyle.alignment = TextAnchor.MiddleLeft;
                _collapsedWindowStyle.padding = new RectOffset(10, 10, 4, 4);
                _collapsedWindowStyle.fontStyle = FontStyle.Bold;
                GuiStyleUtil.ApplyTextColorToAllStates(_collapsedWindowStyle, new Color(0.9f, 0.93f, 0.98f, 1f));
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
            actions.Add(new CortexWindowAction
            {
                ActionId = "logs.collapse",
                Label = "_",
                Width = 28f,
                Execute = delegate
                {
                    _logWindowRect = CortexWindowChromeController.ToggleCollapsed(_state.Chrome.Logs, _logWindowRect, 110f, 26f);
                }
            });
            actions.Add(new CortexWindowAction
            {
                ActionId = "logs.close",
                Label = "X",
                Width = 28f,
                Execute = delegate
                {
                    _state.Logs.ShowDetachedWindow = false;
                    _state.Chrome.Logs.IsCollapsed = false;
                }
            });
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
            if (string.IsNullOrEmpty(containerId))
            {
                return;
            }

            var hostLocation = ResolveHostLocation(containerId);
            _state.Workbench.FocusedContainerId = containerId;
            if (hostLocation == WorkbenchHostLocation.PanelHost)
            {
                _state.Workbench.PanelContainerId = containerId;
            }
            else if (hostLocation == WorkbenchHostLocation.DocumentHost)
            {
                _state.Workbench.EditorContainerId = containerId;
            }
            else
            {
                _state.Workbench.SideContainerId = containerId;
            }

            if (_workbenchRuntime != null)
            {
                if (hostLocation == WorkbenchHostLocation.PanelHost)
                {
                    _workbenchRuntime.WorkbenchState.ActivePanelId = containerId;
                }
                else if (hostLocation == WorkbenchHostLocation.DocumentHost)
                {
                    _workbenchRuntime.WorkbenchState.ActiveEditorGroupId = containerId;
                    _workbenchRuntime.WorkbenchState.ActiveContainerId = containerId;
                }
                else
                {
                    _workbenchRuntime.WorkbenchState.ActiveContainerId = containerId;
                }

                _workbenchRuntime.FocusState.FocusedRegionId = containerId;
            }
        }

        private WorkbenchHostLocation ResolveHostLocation(string containerId)
        {
            if (_workbenchRuntime != null)
            {
                var containers = _workbenchRuntime.ContributionRegistry.GetViewContainers();
                for (var i = 0; i < containers.Count; i++)
                {
                    if (string.Equals(containers[i].ContainerId, containerId, StringComparison.OrdinalIgnoreCase))
                    {
                        return containers[i].DefaultHostLocation;
                    }
                }
            }

            if (string.Equals(containerId, CortexWorkbenchIds.LogsContainer, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(containerId, CortexWorkbenchIds.BuildContainer, StringComparison.OrdinalIgnoreCase))
            {
                return WorkbenchHostLocation.PanelHost;
            }

            if (string.Equals(containerId, CortexWorkbenchIds.EditorContainer, StringComparison.OrdinalIgnoreCase))
            {
                return WorkbenchHostLocation.DocumentHost;
            }

            return WorkbenchHostLocation.PrimarySideHost;
        }

        private static string MapLegacyTabIndex(int index)
        {
            switch (index)
            {
                case 0: return CortexWorkbenchIds.LogsContainer;
                case 1: return CortexWorkbenchIds.ProjectsContainer;
                case 2: return CortexWorkbenchIds.EditorContainer;
                case 3: return CortexWorkbenchIds.BuildContainer;
                case 4: return CortexWorkbenchIds.ReferenceContainer;
                case 5: return CortexWorkbenchIds.RuntimeContainer;
                case 6: return CortexWorkbenchIds.SettingsContainer;
                default: return CortexWorkbenchIds.LogsContainer;
            }
        }

    }
}
