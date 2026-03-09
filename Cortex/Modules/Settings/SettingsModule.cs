using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using UnityEngine;

namespace Cortex.Modules.Settings
{
    public sealed class SettingsModule
    {
        private bool _loaded;
        private string _workspaceRootPath = string.Empty;
        private string _modsRootPath = string.Empty;
        private string _managedAssemblyRootPath = string.Empty;
        private string _additionalSourceRoots = string.Empty;
        private string _logFilePath = string.Empty;
        private string _projectCatalogPath = string.Empty;
        private string _decompilerPath = string.Empty;
        private string _decompilerCachePath = string.Empty;
        private string _defaultBuildConfiguration = string.Empty;
        private string _buildTimeoutMs = string.Empty;
        private string _maxRecentLogs = string.Empty;
        private string _logsPaneWidth = string.Empty;
        private string _projectsPaneWidth = string.Empty;
        private string _editorFilePaneWidth = string.Empty;
        private string _windowX = string.Empty;
        private string _windowY = string.Empty;
        private string _windowWidth = string.Empty;
        private string _windowHeight = string.Empty;
        private bool _autoScrollLogs;
        private bool _showLogBacklog;

        public void Draw(ICortexSettingsStore settingsStore, CortexShellState state)
        {
            EnsureLoaded(state);

            GUILayout.BeginVertical();
            CortexIdeLayout.DrawTwoPane(
                520f,
                420f,
                delegate
                {
                    CortexIdeLayout.DrawGroup("Workspace Discovery", delegate
                    {
                        GUILayout.Label("Tell Cortex where to scan for editable source, loaded mod folders, and game assemblies.");
                        GUILayout.Label("Workspace Root is your development workspace. Mods Root is the live in-game mod folder Cortex can use for loaded-mod assistance.");
                        DrawField("Workspace Scan Root", ref _workspaceRootPath);
                        DrawField("Loaded Mods Root", ref _modsRootPath);
                        DrawField("Game Managed DLLs", ref _managedAssemblyRootPath);
                        DrawField("Extra Source Roots", ref _additionalSourceRoots);
                        DrawField("Live Log File", ref _logFilePath);
                        DrawField("Project Catalog", ref _projectCatalogPath);
                        DrawField("Decompiler Override", ref _decompilerPath);
                        DrawField("Decompiler Cache", ref _decompilerCachePath);
                    });
                },
                delegate
                {
                    CortexIdeLayout.DrawGroup("Behavior and Layout", delegate
                    {
                        DrawField("Default Config", ref _defaultBuildConfiguration);
                        DrawField("Build Timeout (ms)", ref _buildTimeoutMs);
                        DrawField("Max Recent Logs", ref _maxRecentLogs);
                        _autoScrollLogs = GUILayout.Toggle(_autoScrollLogs, "Auto-scroll log list");
                        _showLogBacklog = GUILayout.Toggle(_showLogBacklog, "Show file backlog under live logs");
                        GUILayout.Space(6f);
                        DrawField("Logs Pane Width", ref _logsPaneWidth);
                        DrawField("Projects Pane Width", ref _projectsPaneWidth);
                        DrawField("Explorer Width", ref _editorFilePaneWidth);
                        DrawField("Window X", ref _windowX);
                        DrawField("Window Y", ref _windowY);
                        DrawField("Window Width", ref _windowWidth);
                        DrawField("Window Height", ref _windowHeight);
                        GUILayout.Space(8f);
                        GUILayout.BeginHorizontal();
                        if (GUILayout.Button("Save Settings", GUILayout.Width(120f)))
                        {
                            Apply(state);
                            settingsStore.Save(state.Settings);
                            state.ReloadSettingsRequested = true;
                            _loaded = false;
                            state.StatusMessage = "Saved Cortex settings.";
                        }
                        if (GUILayout.Button("Reset Defaults", GUILayout.Width(120f)))
                        {
                            state.Settings = new CortexSettings();
                            _loaded = false;
                            EnsureLoaded(state);
                            state.StatusMessage = "Reset settings fields to defaults.";
                        }
                        if (GUILayout.Button("Show Logs Window", GUILayout.Width(140f)))
                        {
                            state.Logs.ShowDetachedWindow = true;
                        }
                        GUILayout.EndHorizontal();
                    });
                });
            GUILayout.EndVertical();
        }

        private void EnsureLoaded(CortexShellState state)
        {
            if (_loaded)
            {
                return;
            }

            var settings = state.Settings ?? new CortexSettings();
            _workspaceRootPath = settings.WorkspaceRootPath ?? string.Empty;
            _modsRootPath = settings.ModsRootPath ?? string.Empty;
            _managedAssemblyRootPath = settings.ManagedAssemblyRootPath ?? string.Empty;
            _additionalSourceRoots = settings.AdditionalSourceRoots ?? string.Empty;
            _logFilePath = settings.LogFilePath ?? string.Empty;
            _projectCatalogPath = settings.ProjectCatalogPath ?? string.Empty;
            _decompilerPath = settings.DecompilerPathOverride ?? string.Empty;
            _decompilerCachePath = settings.DecompilerCachePath ?? string.Empty;
            _defaultBuildConfiguration = settings.DefaultBuildConfiguration ?? "Debug";
            _buildTimeoutMs = settings.BuildTimeoutMs.ToString();
            _maxRecentLogs = settings.MaxRecentLogs.ToString();
            _logsPaneWidth = settings.LogsPaneWidth.ToString("F0");
            _projectsPaneWidth = settings.ProjectsPaneWidth.ToString("F0");
            _editorFilePaneWidth = settings.EditorFilePaneWidth.ToString("F0");
            _windowX = settings.WindowX.ToString("F0");
            _windowY = settings.WindowY.ToString("F0");
            _windowWidth = settings.WindowWidth.ToString("F0");
            _windowHeight = settings.WindowHeight.ToString("F0");
            _autoScrollLogs = settings.AutoScrollLogs;
            _showLogBacklog = settings.ShowLogBacklog;
            _loaded = true;
        }

        private void Apply(CortexShellState state)
        {
            if (state.Settings == null)
            {
                state.Settings = new CortexSettings();
            }

            state.Settings.WorkspaceRootPath = _workspaceRootPath;
            state.Settings.ModsRootPath = _modsRootPath;
            state.Settings.ManagedAssemblyRootPath = _managedAssemblyRootPath;
            state.Settings.AdditionalSourceRoots = _additionalSourceRoots;
            state.Settings.LogFilePath = _logFilePath;
            state.Settings.ProjectCatalogPath = _projectCatalogPath;
            state.Settings.DecompilerPathOverride = _decompilerPath;
            state.Settings.DecompilerCachePath = _decompilerCachePath;
            state.Settings.DefaultBuildConfiguration = string.IsNullOrEmpty(_defaultBuildConfiguration) ? "Debug" : _defaultBuildConfiguration;
            state.Settings.BuildTimeoutMs = ParseInt(_buildTimeoutMs, 300000);
            state.Settings.MaxRecentLogs = ParseInt(_maxRecentLogs, 300);
            state.Settings.LogsPaneWidth = ParseFloat(_logsPaneWidth, state.Settings.LogsPaneWidth);
            state.Settings.ProjectsPaneWidth = ParseFloat(_projectsPaneWidth, state.Settings.ProjectsPaneWidth);
            state.Settings.EditorFilePaneWidth = ParseFloat(_editorFilePaneWidth, state.Settings.EditorFilePaneWidth);
            state.Settings.WindowX = ParseFloat(_windowX, state.Settings.WindowX);
            state.Settings.WindowY = ParseFloat(_windowY, state.Settings.WindowY);
            state.Settings.WindowWidth = ParseFloat(_windowWidth, state.Settings.WindowWidth);
            state.Settings.WindowHeight = ParseFloat(_windowHeight, state.Settings.WindowHeight);
            state.Settings.AutoScrollLogs = _autoScrollLogs;
            state.Settings.ShowLogBacklog = _showLogBacklog;
        }

        private static void DrawField(string label, ref string value)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(130f));
            value = GUILayout.TextField(value ?? string.Empty, GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();
        }

        private static int ParseInt(string raw, int fallback)
        {
            int value;
            return int.TryParse(raw, out value) ? value : fallback;
        }

        private static float ParseFloat(string raw, float fallback)
        {
            float value;
            return float.TryParse(raw, out value) ? value : fallback;
        }
    }
}
