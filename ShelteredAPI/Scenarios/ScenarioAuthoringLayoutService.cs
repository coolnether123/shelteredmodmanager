using System;
using System.Collections.Generic;
using System.IO;
using System.Security;
using System.Text;
using System.Xml;
using ModAPI.Scenarios;

namespace ShelteredAPI.Scenarios
{
    internal sealed class ScenarioAuthoringLayoutService
    {
        private readonly ScenarioAuthoringWindowRegistry _windowRegistry;
        private readonly ScenarioAuthoringSettingsService _settingsService;
        private readonly ScenarioStageCoordinator _stageCoordinator;

        public ScenarioAuthoringLayoutService(
            ScenarioAuthoringWindowRegistry windowRegistry,
            ScenarioAuthoringSettingsService settingsService,
            ScenarioStageCoordinator stageCoordinator)
        {
            _windowRegistry = windowRegistry;
            _settingsService = settingsService;
            _stageCoordinator = stageCoordinator;
        }

        public void InitializeState(ScenarioAuthoringState state)
        {
            if (state == null)
                return;

            state.ActiveLayoutPreset = string.IsNullOrEmpty(state.ActiveLayoutPreset) ? "default" : state.ActiveLayoutPreset;
            if (state.ActiveBunkerStage == ScenarioStageKind.None)
                state.ActiveBunkerStage = ScenarioStageKind.BunkerInside;
            state.Settings = state.Settings != null ? state.Settings.Copy() : _settingsService.Load();
            _settingsService.ApplyDefinitionDefaults(state.Settings);
            EnsureWindowStates(state);
            LoadLayout(state);
            ApplyStageWorkspace(state);
        }

        public void EnsureWindowStates(ScenarioAuthoringState state)
        {
            if (state == null)
                return;

            ScenarioAuthoringWindowDefinition[] definitions = _windowRegistry.GetDefinitions();
            for (int i = 0; i < definitions.Length; i++)
            {
                ScenarioAuthoringWindowDefinition definition = definitions[i];
                if (definition == null || FindWindow(state, definition.Id) != null)
                    continue;

                state.WindowStates.Add(CreateState(definition));
            }

            ApplyStageWorkspace(state);
        }

        private static readonly string[] WorkspaceWindowIds = new[]
        {
            ScenarioAuthoringWindowIds.Triggers,
            ScenarioAuthoringWindowIds.Survivors,
            ScenarioAuthoringWindowIds.Stockpile,
            ScenarioAuthoringWindowIds.Quests,
            ScenarioAuthoringWindowIds.Map,
            ScenarioAuthoringWindowIds.Publish
        };

        private static bool IsWorkspaceWindow(string windowId)
        {
            if (string.IsNullOrEmpty(windowId))
                return false;
            for (int i = 0; i < WorkspaceWindowIds.Length; i++)
            {
                if (string.Equals(WorkspaceWindowIds[i], windowId, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        public bool ToggleWindowVisibility(ScenarioAuthoringState state, string windowId)
        {
            ScenarioAuthoringWindowState window = FindWindow(state, windowId);
            if (window == null)
                return false;

            if (IsWorkspaceWindow(windowId))
            {
                ScenarioStageKind workspaceStage = ResolveWorkspaceStage(windowId);
                if (workspaceStage == ScenarioStageKind.None)
                    return false;

                _stageCoordinator.SelectStage(state, workspaceStage);
                ApplyStageWorkspace(state);
                state.MinimalMode = false;
                state.FocusSelectionMode = false;
                PersistIfEnabled(state);
                return true;
            }

            window.Visible = !window.Visible;
            if (window.Visible)
                window.Collapsed = false;
            else
                window.Collapsed = false;

            state.MinimalMode = false;
            state.FocusSelectionMode = false;
            PersistIfEnabled(state);
            return true;
        }

        public bool ToggleWindowCollapsed(ScenarioAuthoringState state, string windowId)
        {
            ScenarioAuthoringWindowState window = FindWindow(state, windowId);
            if (window == null)
                return false;

            bool shouldOpen = window.Collapsed || !window.Visible;
            window.Collapsed = !shouldOpen;
            window.Visible = shouldOpen;
            PersistIfEnabled(state);
            return true;
        }

        public bool ResetLayout(ScenarioAuthoringState state)
        {
            if (state == null)
                return false;

            state.WindowStates.Clear();
            ScenarioAuthoringWindowDefinition[] definitions = _windowRegistry.GetDefinitions();
            for (int i = 0; i < definitions.Length; i++)
                state.WindowStates.Add(CreateState(definitions[i]));

            state.MinimalMode = false;
            state.FocusSelectionMode = false;
            state.SettingsWindowOpen = false;
            ApplyStageWorkspace(state);
            PersistIfEnabled(state);
            return true;
        }

        public bool HideAll(ScenarioAuthoringState state)
        {
            if (state == null)
                return false;

            for (int i = 0; i < state.WindowStates.Count; i++)
            {
                ScenarioAuthoringWindowState window = state.WindowStates[i];
                if (window != null && !string.Equals(window.Id, ScenarioAuthoringWindowIds.Settings, StringComparison.OrdinalIgnoreCase))
                    window.Visible = false;
            }

            state.MinimalMode = true;
            state.FocusSelectionMode = false;
            state.SettingsWindowOpen = false;
            PersistIfEnabled(state);
            return true;
        }

        public bool FocusSelection(ScenarioAuthoringState state)
        {
            if (state == null)
                return false;

            for (int i = 0; i < state.WindowStates.Count; i++)
            {
                ScenarioAuthoringWindowState window = state.WindowStates[i];
                if (window == null)
                    continue;

                bool visible = string.Equals(window.Id, ScenarioAuthoringWindowIds.Inspector, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(window.Id, ScenarioAuthoringWindowIds.BuildTools, StringComparison.OrdinalIgnoreCase);
                window.Visible = visible;
                if (visible)
                    window.Collapsed = false;
            }

            state.MinimalMode = false;
            state.FocusSelectionMode = true;
            ApplyStageWorkspace(state);
            PersistIfEnabled(state);
            return true;
        }

        public bool SelectStage(ScenarioAuthoringState state, ScenarioStageKind stageKind)
        {
            if (state == null)
                return false;

            _stageCoordinator.SelectStage(state, stageKind);
            state.MinimalMode = false;
            state.FocusSelectionMode = false;
            ApplyStageWorkspace(state);
            PersistIfEnabled(state);
            return true;
        }

        public void ApplyStageWorkspace(ScenarioAuthoringState state)
        {
            if (state == null || state.WindowStates == null)
                return;

            ScenarioStageKind activeStage = state.ActiveStage != ScenarioStageKind.None
                ? state.ActiveStage
                : _stageCoordinator.Resolve(state) != null ? _stageCoordinator.Resolve(state).Kind : ScenarioStageKind.None;

            bool showBuild = activeStage == ScenarioStageKind.BunkerBackground
                || activeStage == ScenarioStageKind.BunkerSurface
                || activeStage == ScenarioStageKind.BunkerInside;

            for (int i = 0; i < state.WindowStates.Count; i++)
            {
                ScenarioAuthoringWindowState window = state.WindowStates[i];
                if (window == null)
                    continue;

                if (IsWorkspaceWindow(window.Id))
                {
                    window.Visible = string.Equals(window.Id, ResolveWorkspaceWindowId(activeStage), StringComparison.OrdinalIgnoreCase);
                    window.Collapsed = !window.Visible && window.Collapsed;
                    continue;
                }

                if (string.Equals(window.Id, ScenarioAuthoringWindowIds.BuildTools, StringComparison.OrdinalIgnoreCase))
                    window.Visible = showBuild;
                else if (string.Equals(window.Id, ScenarioAuthoringWindowIds.TilesPalette, StringComparison.OrdinalIgnoreCase))
                    window.Visible = false;
                else if (string.Equals(window.Id, ScenarioAuthoringWindowIds.Layers, StringComparison.OrdinalIgnoreCase))
                    window.Visible = false;
                else if (string.Equals(window.Id, ScenarioAuthoringWindowIds.Scenario, StringComparison.OrdinalIgnoreCase))
                    window.Visible = false;

                if (window.Visible)
                    window.Collapsed = false;
            }
        }

        public bool SetSettingsWindowOpen(ScenarioAuthoringState state, bool open)
        {
            if (state == null)
                return false;

            bool changed = state.SettingsWindowOpen != open;
            state.SettingsWindowOpen = open;

            ScenarioAuthoringWindowState window = FindWindow(state, ScenarioAuthoringWindowIds.Settings);
            if (window != null && window.Visible != open)
            {
                window.Visible = open;
                if (open)
                    window.Collapsed = false;
                changed = true;
            }

            return changed;
        }

        public void PersistIfEnabled(ScenarioAuthoringState state)
        {
            if (state == null || state.Settings == null)
                return;

            if (!state.Settings.GetBool("layout.remember_windows", true))
                return;

            SaveLayout(state);
        }

        public ScenarioAuthoringWindowState FindWindow(ScenarioAuthoringState state, string windowId)
        {
            if (state == null || string.IsNullOrEmpty(windowId))
                return null;

            for (int i = 0; i < state.WindowStates.Count; i++)
            {
                ScenarioAuthoringWindowState window = state.WindowStates[i];
                if (window != null && string.Equals(window.Id, windowId, StringComparison.OrdinalIgnoreCase))
                    return window;
            }

            return null;
        }

        private void LoadLayout(ScenarioAuthoringState state)
        {
            string path = ScenarioAuthoringStoragePaths.GetLayoutFilePath();
            if (!File.Exists(path))
                return;

            try
            {
                XmlDocument document = new XmlDocument();
                document.Load(path);
                XmlElement root = document.DocumentElement;
                if (root == null)
                    return;

                state.ActiveLayoutPreset = ReadAttribute(root, "preset", "default");
                state.MinimalMode = ReadBool(root, "minimalMode", false);
                state.FocusSelectionMode = ReadBool(root, "focusSelection", false);

                XmlNodeList nodes = root.SelectNodes("Window");
                for (int i = 0; nodes != null && i < nodes.Count; i++)
                {
                    XmlElement element = nodes[i] as XmlElement;
                    if (element == null)
                        continue;

                    ScenarioAuthoringWindowState window = FindWindow(state, element.GetAttribute("id"));
                    if (window == null)
                        continue;

                    window.Visible = ReadBool(element, "visible", window.Visible);
                    window.Collapsed = ReadBool(element, "collapsed", window.Collapsed);
                    window.Pinned = ReadBool(element, "pinned", window.Pinned);
                    NormalizeWindowState(window);
                }

                ApplyStageWorkspace(state);
            }
            catch (Exception ex)
            {
                ModAPI.Core.MMLog.WriteWarning("[ScenarioAuthoringLayout] Failed to load layout: " + ex.Message);
            }
        }

        private static ScenarioStageKind ResolveWorkspaceStage(string windowId)
        {
            if (string.Equals(windowId, ScenarioAuthoringWindowIds.Survivors, StringComparison.OrdinalIgnoreCase))
                return ScenarioStageKind.People;
            if (string.Equals(windowId, ScenarioAuthoringWindowIds.Stockpile, StringComparison.OrdinalIgnoreCase))
                return ScenarioStageKind.InventoryStorage;
            if (string.Equals(windowId, ScenarioAuthoringWindowIds.Triggers, StringComparison.OrdinalIgnoreCase))
                return ScenarioStageKind.Events;
            if (string.Equals(windowId, ScenarioAuthoringWindowIds.Quests, StringComparison.OrdinalIgnoreCase))
                return ScenarioStageKind.Quests;
            if (string.Equals(windowId, ScenarioAuthoringWindowIds.Map, StringComparison.OrdinalIgnoreCase))
                return ScenarioStageKind.Map;
            if (string.Equals(windowId, ScenarioAuthoringWindowIds.Publish, StringComparison.OrdinalIgnoreCase))
                return ScenarioStageKind.Publish;
            return ScenarioStageKind.None;
        }

        private static string ResolveWorkspaceWindowId(ScenarioStageKind stageKind)
        {
            switch (stageKind)
            {
                case ScenarioStageKind.InventoryStorage:
                    return ScenarioAuthoringWindowIds.Stockpile;
                case ScenarioStageKind.People:
                    return ScenarioAuthoringWindowIds.Survivors;
                case ScenarioStageKind.Events:
                    return ScenarioAuthoringWindowIds.Triggers;
                case ScenarioStageKind.Quests:
                    return ScenarioAuthoringWindowIds.Quests;
                case ScenarioStageKind.Map:
                    return ScenarioAuthoringWindowIds.Map;
                case ScenarioStageKind.Publish:
                    return ScenarioAuthoringWindowIds.Publish;
                default:
                    return null;
            }
        }

        private void SaveLayout(ScenarioAuthoringState state)
        {
            try
            {
                StringBuilder builder = new StringBuilder();
                builder.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
                builder.Append("<ScenarioAuthoringLayout preset=\"")
                    .Append(Escape(state.ActiveLayoutPreset ?? "default"))
                    .Append("\" minimalMode=\"")
                    .Append(state.MinimalMode ? "true" : "false")
                    .Append("\" focusSelection=\"")
                    .Append(state.FocusSelectionMode ? "true" : "false")
                    .AppendLine("\">");

                for (int i = 0; i < state.WindowStates.Count; i++)
                {
                    ScenarioAuthoringWindowState window = state.WindowStates[i];
                    if (window == null || string.IsNullOrEmpty(window.Id))
                        continue;

                    builder.Append("  <Window id=\"")
                        .Append(Escape(window.Id))
                        .Append("\" visible=\"")
                        .Append(window.Visible ? "true" : "false")
                        .Append("\" collapsed=\"")
                        .Append(window.Collapsed ? "true" : "false")
                        .Append("\" pinned=\"")
                        .Append(window.Pinned ? "true" : "false")
                        .AppendLine("\" />");
                }

                builder.AppendLine("</ScenarioAuthoringLayout>");
                File.WriteAllText(ScenarioAuthoringStoragePaths.GetLayoutFilePath(), builder.ToString());
            }
            catch (Exception ex)
            {
                ModAPI.Core.MMLog.WriteWarning("[ScenarioAuthoringLayout] Failed to save layout: " + ex.Message);
            }
        }

        private static ScenarioAuthoringWindowState CreateState(ScenarioAuthoringWindowDefinition definition)
        {
            ScenarioAuthoringWindowState state = new ScenarioAuthoringWindowState
            {
                Id = definition.Id,
                Visible = definition.DefaultVisible,
                Collapsed = definition.DefaultCollapsed,
                Pinned = definition.DefaultPinned,
                Order = definition.Order,
                Width = definition.DefaultWidth,
                Height = definition.DefaultHeight
            };

            NormalizeWindowState(state);
            return state;
        }

        private static void NormalizeWindowState(ScenarioAuthoringWindowState state)
        {
            if (state == null)
                return;

            if (state.Collapsed)
                state.Visible = false;
        }

        private static bool ReadBool(XmlElement element, string name, bool fallback)
        {
            bool parsed;
            return bool.TryParse(ReadAttribute(element, name, fallback ? "true" : "false"), out parsed) ? parsed : fallback;
        }

        private static string ReadAttribute(XmlElement element, string name, string fallback)
        {
            if (element == null || string.IsNullOrEmpty(name))
                return fallback;

            string value = element.GetAttribute(name);
            return string.IsNullOrEmpty(value) ? fallback : value;
        }

        private static string Escape(string value)
        {
            return string.IsNullOrEmpty(value)
                ? string.Empty
                : SecurityElement.Escape(value);
        }
    }
}
