using System;
using System.Collections.Generic;
using System.IO;
using Cortex.Adapters;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.Core.Services;
using ModAPI.Core;
using ModAPI.InputActions;
using UnityEngine;

namespace Cortex
{
    public sealed class CortexShell : MonoBehaviour
    {
        private const string ToggleActionId = "cortex.toggle";
        private const KeyCode ToggleKey = KeyCode.F8;
        private const int MainWindowId = 0xC07E;
        private const int LogsWindowId = 0xC07F;

        private Rect _windowRect = new Rect(70f, 70f, 1180f, 760f);
        private Rect _logWindowRect = new Rect(100f, 100f, 980f, 620f);
        private bool _visible;
        private int _selectedTab;

        private ICortexSettingsStore _settingsStore;
        private IProjectCatalog _projectCatalog;
        private IDocumentService _documentService;
        private IBuildCommandResolver _buildCommandResolver;
        private IBuildExecutor _buildExecutor;
        private ISourceReferenceService _sourceReferenceService;
        private IRuntimeLogFeed _runtimeLogFeed;
        private IRuntimeSourceNavigationService _runtimeSourceNavigationService;
        private IRuntimeToolBridge _runtimeToolBridge;
        private IRestartCoordinator _restartCoordinator;

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
        private GUIStyle _sectionStyle;
        private GUIStyle _windowStyle;
        private Texture2D _windowBackground;
        private Texture2D _sectionBackground;

        private void Awake()
        {
            try
            {
                MMLog.WriteInfo("[Cortex] Awake starting.");
                gameObject.name = "Cortex.Shell";
                DontDestroyOnLoad(gameObject);
                InitializeSettingsAndServices();
                InitializeModules();
                RegisterToggleAction();
                MMLog.WriteInfo("[Cortex] Awake complete. Toggle=" + ToggleKey + ", projects=" + _projectCatalog.GetProjects().Count + ".");
            }
            catch (Exception ex)
            {
                MMLog.WriteError("[Cortex] Awake failed: " + ex);
                throw;
            }
        }

        private void OnDestroy()
        {
            MMLog.WriteDebug("[Cortex] OnDestroy invoked. Persisting window settings.");
            PersistWindowSettings();
        }

        private void Update()
        {
            if (InputActionRegistry.IsDown(ToggleActionId))
            {
                _visible = !_visible;
                MMLog.WriteInfo("[Cortex] Shell " + (_visible ? "opened" : "closed") + " via " + ToggleKey + ".");
                if (!_visible)
                {
                    PersistWindowSettings();
                }
            }

            if (_state.ReloadSettingsRequested)
            {
                MMLog.WriteDebug("[Cortex] Applying requested settings reload.");
                ApplySettingsChanges();
            }

            if (_state.RequestedTabIndex >= 0)
            {
                _selectedTab = _state.RequestedTabIndex;
                _state.RequestedTabIndex = -1;
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
            _windowRect = GUI.Window(MainWindowId, _windowRect, DrawWindow, "Cortex IDE", _windowStyle);

            if (_state.ShowDetachedLogWindow)
            {
                _logWindowRect = GUI.Window(LogsWindowId, _logWindowRect, DrawLogsWindow, "Cortex Logs", _windowStyle);
            }
        }

        private void DrawWindow(int windowId)
        {
            GUILayout.BeginVertical();
            DrawHeader();
            DrawTabStrip();
            GUILayout.Space(6f);

            switch (_selectedTab)
            {
                case 0:
                    _logsModule.Draw(_runtimeLogFeed, _runtimeSourceNavigationService, _documentService, _state, false);
                    break;
                case 1:
                    _projectsModule.Draw(_projectCatalog, _state);
                    break;
                case 2:
                    _editorModule.Draw(_documentService, _state);
                    break;
                case 3:
                    _buildModule.Draw(_buildCommandResolver, _buildExecutor, _restartCoordinator, _documentService, _state);
                    break;
                case 4:
                    _referenceModule.Draw(_sourceReferenceService, _documentService, _state);
                    break;
                case 5:
                    _runtimeToolsModule.Draw(_runtimeToolBridge);
                    break;
                case 6:
                    _settingsModule.Draw(_settingsStore, _state);
                    break;
            }

            GUILayout.FlexibleSpace();
            GUILayout.BeginVertical(_sectionStyle);
            GUILayout.Label(string.IsNullOrEmpty(_state.StatusMessage) ? "Status: Ready" : "Status: " + _state.StatusMessage, _statusStyle);
            GUILayout.Label("Open docs: " + _state.OpenDocuments.Count + " | Projects: " + _projectCatalog.GetProjects().Count + " | Logs window: " + (_state.ShowDetachedLogWindow ? "Open" : "Docked"));
            GUILayout.EndVertical();
            GUILayout.EndVertical();
            GUI.DragWindow(new Rect(0f, 0f, 10000f, 28f));
        }

        private void DrawLogsWindow(int windowId)
        {
            GUILayout.BeginVertical();
            _logsModule.Draw(_runtimeLogFeed, _runtimeSourceNavigationService, _documentService, _state, true);
            GUILayout.EndVertical();
            GUI.DragWindow(new Rect(0f, 0f, 10000f, 24f));
        }

        private void DrawHeader()
        {
            GUILayout.BeginVertical(_sectionStyle);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Cortex In-Game IDE", _titleStyle);
            GUILayout.FlexibleSpace();
            if (_state.SelectedProject != null)
            {
                GUILayout.Label("Active Project: " + _state.SelectedProject.GetDisplayName());
            }
            GUILayout.Space(8f);
            if (GUILayout.Button(_state.ShowDetachedLogWindow ? "Hide Logs Window" : "Show Logs Window", GUILayout.Width(140f)))
            {
                _state.ShowDetachedLogWindow = !_state.ShowDetachedLogWindow;
            }
            if (GUILayout.Button("Fit Screen", GUILayout.Width(90f)))
            {
                FitMainWindowToScreen();
            }
            if (GUILayout.Button("Close", GUILayout.Width(80f)))
            {
                PersistWindowSettings();
                _visible = false;
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(4f);
            GUILayout.Label("Toggle: F8 | Detached logs stay available while you work in Projects, Editor, Build, and Settings.");
            GUILayout.EndVertical();
        }

        private void DrawTabStrip()
        {
            GUILayout.BeginHorizontal(_sectionStyle);
            DrawTabButton(0, "Logs");
            DrawTabButton(1, "Projects");
            DrawTabButton(2, "Editor");
            DrawTabButton(3, "Build");
            DrawTabButton(4, "Reference");
            DrawTabButton(5, "Runtime");
            DrawTabButton(6, "Settings");
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        private void DrawTabButton(int index, string label)
        {
            if (GUILayout.Toggle(_selectedTab == index, label, "button", GUILayout.Width(100f)))
            {
                _selectedTab = index;
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
            var settings = BuildEffectiveSettings(_settingsStore.Load(), gameRoot, smmRoot, smmBin);
            _state.Settings = settings;
            _windowRect = new Rect(settings.WindowX, settings.WindowY, settings.WindowWidth, settings.WindowHeight);
            _logWindowRect = new Rect(settings.WindowX + 30f, settings.WindowY + 30f, Math.Max(760f, settings.WindowWidth - 120f), Math.Max(460f, settings.WindowHeight - 140f));

            MMLog.WriteDebug("[Cortex] Settings initialized. gameRoot='" + gameRoot + "', smmRoot='" + smmRoot + "', smmBin='" + smmBin + "'.");

            InitializeServices(gameRoot, smmRoot, smmBin, settings);
        }

        private void InitializeServices(string gameRoot, string smmRoot, string smmBin, CortexSettings settings)
        {
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
            _runtimeLogFeed = new MmLogRuntimeLogFeed();
            _runtimeSourceNavigationService = new RuntimeSourceNavigationService(new ModApiRuntimeSymbolResolver());
            _runtimeToolBridge = new ModApiRuntimeToolBridge();
            _restartCoordinator = new ModApiRestartCoordinator(new RestartRequestWriter());

            MMLog.WriteDebug("[Cortex] Services initialized. Catalog='" + projectCatalogPath + "', Decompiler='" + decompilerPath + "', Cache='" + settings.DecompilerCachePath + "'.");
        }

        private void InitializeModules()
        {
            if (_logsModule == null) _logsModule = new LogsModule();
            if (_projectsModule == null) _projectsModule = new ProjectsModule();
            if (_editorModule == null) _editorModule = new EditorModule();
            if (_buildModule == null) _buildModule = new BuildModule();
            if (_referenceModule == null) _referenceModule = new ReferenceModule();
            if (_runtimeToolsModule == null) _runtimeToolsModule = new RuntimeToolsModule();
            if (_settingsModule == null) _settingsModule = new SettingsModule();
        }

        private void ApplySettingsChanges()
        {
            PersistWindowSettings();

            var gameRoot = Directory.GetParent(Application.dataPath).FullName;
            var smmRoot = Path.Combine(gameRoot, "SMM");
            var smmBin = Path.Combine(smmRoot, "bin");
            var activeProjectId = _state.SelectedProject != null ? _state.SelectedProject.ModId : string.Empty;
            var activeDocumentPath = _state.ActiveDocument != null ? _state.ActiveDocument.FilePath : string.Empty;

            _state.Settings = BuildEffectiveSettings(_state.Settings, gameRoot, smmRoot, smmBin);
            InitializeServices(gameRoot, smmRoot, smmBin, _state.Settings);
            _settingsStore.Save(_state.Settings);

            if (!string.IsNullOrEmpty(activeProjectId))
            {
                _state.SelectedProject = _projectCatalog.GetProject(activeProjectId) ?? _state.SelectedProject;
            }

            if (!string.IsNullOrEmpty(activeDocumentPath))
            {
                for (var i = 0; i < _state.OpenDocuments.Count; i++)
                {
                    if (string.Equals(_state.OpenDocuments[i].FilePath, activeDocumentPath, StringComparison.OrdinalIgnoreCase))
                    {
                        _state.ActiveDocument = _state.OpenDocuments[i];
                        _state.ActiveDocumentPath = activeDocumentPath;
                        break;
                    }
                }
            }

            _windowRect = new Rect(_state.Settings.WindowX, _state.Settings.WindowY, _state.Settings.WindowWidth, _state.Settings.WindowHeight);
            ClampWindowsToScreen();
            _state.ReloadSettingsRequested = false;
            _state.StatusMessage = "Cortex settings applied.";
            MMLog.WriteInfo("[Cortex] Settings reload applied.");
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

            _state.Settings.WindowX = _windowRect.x;
            _state.Settings.WindowY = _windowRect.y;
            _state.Settings.WindowWidth = _windowRect.width;
            _state.Settings.WindowHeight = _windowRect.height;
            _settingsStore.Save(_state.Settings);
        }

        private void ClampWindowsToScreen()
        {
            _windowRect = ClampRectToScreen(_windowRect, 920f, 580f);
            _logWindowRect = ClampRectToScreen(_logWindowRect, 760f, 420f);
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
        }

        private void EnsureStyles()
        {
            if (_titleStyle == null)
            {
                _titleStyle = new GUIStyle(GUI.skin.label);
                _titleStyle.fontSize = 16;
                _titleStyle.fontStyle = FontStyle.Bold;
                _titleStyle.normal.textColor = new Color(0.96f, 0.96f, 0.96f, 1f);
            }

            if (_statusStyle == null)
            {
                _statusStyle = new GUIStyle(GUI.skin.label);
                _statusStyle.wordWrap = true;
                _statusStyle.normal.textColor = new Color(0.88f, 0.88f, 0.9f, 1f);
            }

            if (_sectionStyle == null)
            {
                _sectionStyle = new GUIStyle(GUI.skin.box);
                _sectionBackground = MakeTex(new Color(0.1f, 0.1f, 0.12f, 0.96f));
                _sectionStyle.normal.background = _sectionBackground;
                _sectionStyle.padding = new RectOffset(10, 10, 8, 8);
            }

            if (_windowStyle == null)
            {
                _windowBackground = MakeTex(new Color(0.05f, 0.05f, 0.07f, 0.97f));
                _windowStyle = new GUIStyle(GUI.skin.window);
                _windowStyle.normal.background = _windowBackground;
                _windowStyle.padding = new RectOffset(8, 8, 24, 8);
            }
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
                MMLog.WriteInfo("[Cortex] Registered toggle action '" + ToggleActionId + "' on " + ToggleKey + ".");
            }
            else
            {
                MMLog.WriteDebug("[Cortex] Toggle action '" + ToggleActionId + "' already registered.");
            }
        }
    }

    public sealed class CortexShellState
    {
        public CortexProjectDefinition SelectedProject;
        public DocumentSession ActiveDocument;
        public string ActiveDocumentPath;
        public BuildResult LastBuildResult;
        public DecompilerResponse LastReferenceResult;
        public RuntimeLogEntry SelectedLogEntry;
        public int SelectedLogFrameIndex = -1;
        public CortexSettings Settings;
        public string StatusMessage;
        public bool ShowDetachedLogWindow;
        public bool ReloadSettingsRequested;
        public bool EditorUnlocked;
        public int RequestedTabIndex = -1;
        public readonly List<DocumentSession> OpenDocuments = new List<DocumentSession>();
    }
}
