using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security;
using System.Text;
using System.Xml;
using ModAPI.Scenarios;

using ShelteredAPI.Scenarios.Application.Authoring;
using ShelteredAPI.Scenarios.Application.Stages;
using ShelteredAPI.Scenarios.Domain.Stages;
using ShelteredAPI.Scenarios.Infrastructure.Persistence;
using ShelteredAPI.Scenarios.Presentation.Authoring.Windows;
namespace ShelteredAPI.Scenarios.Presentation.Authoring.Shell{
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
            state.ActiveStage = ScenarioStageKind.None;
            state.ActiveShellTab = ScenarioAuthoringShellTab.Shell;
            if (state.ActiveBunkerStage == ScenarioStageKind.None)
                state.ActiveBunkerStage = ScenarioStageKind.BunkerInside;
            state.Settings = state.Settings != null ? state.Settings.Copy() : _settingsService.Load();
            _settingsService.ApplyDefinitionDefaults(state.Settings);
            EnsureWindowStates(state);
            bool layoutLoaded = LoadLayout(state);
            if (!layoutLoaded)
                HideStartupUtilityWindows(state);
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

        public bool ToggleWindowVisibility(ScenarioAuthoringState state, string windowId)
        {
            ScenarioAuthoringWindowState window = FindWindow(state, windowId);
            if (window == null)
                return false;

            if (string.Equals(windowId, ScenarioAuthoringWindowIds.Scenario, StringComparison.OrdinalIgnoreCase))
                return ShowHome(state, window);

            ScenarioAuthoringWindowDefinition definition = _windowRegistry.Find(windowId);
            if (definition != null && definition.IsWorkspaceStageWindow)
            {
                if (!window.Visible || window.Collapsed)
                {
                    _stageCoordinator.SelectStage(state, definition.WorkspaceStage);
                    ApplyStageWorkspace(state);
                    window.Visible = true;
                    window.Collapsed = false;
                    BringWindowToFront(state, window.Id);
                }
                else
                {
                    window.Visible = false;
                    window.Collapsed = false;
                }

                state.MinimalMode = false;
                state.FocusSelectionMode = false;
                PersistIfEnabled(state);
                return true;
            }

            window.Visible = !window.Visible;
            if (window.Visible)
            {
                window.Collapsed = false;
                BringWindowToFront(state, window.Id);
            }
            else
            {
                window.Collapsed = false;
            }

            state.MinimalMode = false;
            state.FocusSelectionMode = false;
            PersistIfEnabled(state);
            return true;
        }

        public bool SetWindowOpen(ScenarioAuthoringState state, string windowId, bool open)
        {
            ScenarioAuthoringWindowState window = FindWindow(state, windowId);
            if (window == null)
                return false;

            if (string.Equals(windowId, ScenarioAuthoringWindowIds.Scenario, StringComparison.OrdinalIgnoreCase))
                return open && ShowHome(state, window);

            ScenarioAuthoringWindowDefinition definition = _windowRegistry.Find(windowId);
            if (definition != null && definition.IsWorkspaceStageWindow)
            {
                bool workspaceChanged = window.Visible != open || (open && window.Collapsed);
                if (open)
                {
                    _stageCoordinator.SelectStage(state, definition.WorkspaceStage);
                    ApplyStageWorkspace(state);
                    window.Visible = true;
                    window.Collapsed = false;
                    BringWindowToFront(state, window.Id);
                }
                else
                {
                    window.Visible = false;
                    window.Collapsed = false;
                }

                state.MinimalMode = false;
                state.FocusSelectionMode = false;
                PersistIfEnabled(state);
                return workspaceChanged;
            }

            bool changed = window.Visible != open || (open && window.Collapsed);
            window.Visible = open;
            if (open)
            {
                window.Collapsed = false;
                BringWindowToFront(state, window.Id);
            }

            state.MinimalMode = false;
            state.FocusSelectionMode = false;
            ApplyStageWorkspace(state);
            PersistIfEnabled(state);
            return changed;
        }

        public void BeginPixelEditorFocus(ScenarioAuthoringState state)
        {
            if (state == null)
                return;

            ScenarioAuthoringWindowState buildTools = FindWindow(state, ScenarioAuthoringWindowIds.BuildTools);
            ScenarioAuthoringWindowState inspector = FindWindow(state, ScenarioAuthoringWindowIds.Inspector);
            if (!state.PixelEditorChromeSuppressed)
            {
                state.PixelEditorRestoreBuildToolsVisible = buildTools != null && buildTools.Visible;
                state.PixelEditorRestoreBuildToolsCollapsed = buildTools != null && buildTools.Collapsed;
                state.PixelEditorRestoreInspectorVisible = inspector != null && inspector.Visible;
                state.PixelEditorRestoreInspectorCollapsed = inspector != null && inspector.Collapsed;
            }

            state.PixelEditorChromeSuppressed = true;
            if (buildTools != null)
            {
                buildTools.Visible = false;
                buildTools.Collapsed = false;
            }

            if (inspector != null)
            {
                inspector.Visible = false;
                inspector.Collapsed = false;
            }

            state.WindowMenuOpen = false;
            PersistIfEnabled(state);
        }

        public void EndPixelEditorFocus(ScenarioAuthoringState state)
        {
            if (state == null || !state.PixelEditorChromeSuppressed)
                return;

            ScenarioAuthoringWindowState buildTools = FindWindow(state, ScenarioAuthoringWindowIds.BuildTools);
            ScenarioAuthoringWindowState inspector = FindWindow(state, ScenarioAuthoringWindowIds.Inspector);
            state.PixelEditorChromeSuppressed = false;
            if (buildTools != null)
            {
                buildTools.Visible = state.PixelEditorRestoreBuildToolsVisible;
                buildTools.Collapsed = state.PixelEditorRestoreBuildToolsCollapsed && !buildTools.Visible;
            }

            if (inspector != null)
            {
                inspector.Visible = state.PixelEditorRestoreInspectorVisible;
                inspector.Collapsed = state.PixelEditorRestoreInspectorCollapsed && !inspector.Visible;
            }

            state.PixelEditorRestoreBuildToolsVisible = false;
            state.PixelEditorRestoreInspectorVisible = false;
            state.PixelEditorRestoreBuildToolsCollapsed = false;
            state.PixelEditorRestoreInspectorCollapsed = false;
            PersistIfEnabled(state);
        }

        public bool ToggleWindowCollapsed(ScenarioAuthoringState state, string windowId)
        {
            ScenarioAuthoringWindowState window = FindWindow(state, windowId);
            if (window == null)
                return false;

            bool shouldCollapse = window.Visible && !window.Collapsed;
            window.Collapsed = shouldCollapse;
            window.Visible = !shouldCollapse;
            PersistIfEnabled(state);
            return true;
        }

        public bool RestoreWindow(ScenarioAuthoringState state, string windowId)
        {
            ScenarioAuthoringWindowState window = FindWindow(state, windowId);
            if (window == null)
                return false;

            bool changed = !window.Visible || window.Collapsed;
            window.Visible = true;
            window.Collapsed = false;
            state.MinimalMode = false;
            BringWindowToFront(state, window.Id);
            PersistIfEnabled(state);
            return changed;
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
            state.HelpWindowOpen = false;
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
            state.HelpWindowOpen = false;
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

        public ScenarioAuthoringWorkflowTransition SelectTool(ScenarioAuthoringState state, ScenarioAuthoringTool tool)
        {
            if (state == null)
                return new ScenarioAuthoringWorkflowTransition();

            ScenarioAuthoringWorkflowTransition transition = _stageCoordinator.SelectTool(state, tool);
            state.MinimalMode = false;
            state.FocusSelectionMode = false;
            ApplyStageWorkspace(state);
            PersistIfEnabled(state);
            return transition;
        }

        private bool ShowHome(ScenarioAuthoringState state, ScenarioAuthoringWindowState window)
        {
            if (state == null || window == null)
                return false;

            bool changed = state.ActiveStage != ScenarioStageKind.None || !window.Visible || window.Collapsed;
            state.ActiveStage = ScenarioStageKind.None;
            state.ActiveShellTab = ScenarioAuthoringShellTab.Shell;
            state.MinimalMode = false;
            state.FocusSelectionMode = false;
            window.Visible = true;
            window.Collapsed = false;
            ApplyStageWorkspace(state);
            PersistIfEnabled(state);
            return changed;
        }

        public void ApplyStageWorkspace(ScenarioAuthoringState state)
        {
            if (state == null || state.WindowStates == null)
                return;

            ScenarioStageKind activeStage = ResolveActiveStage(state);
            bool showBuild = ScenarioAuthoringWorkflowRules.ShouldShowToolWorkspace(state);
            bool showWorldInspector = IsWorldSurfaceStage(activeStage);

            for (int i = 0; i < state.WindowStates.Count; i++)
            {
                ScenarioAuthoringWindowState window = state.WindowStates[i];
                if (window == null)
                    continue;

                ScenarioAuthoringWindowDefinition definition = _windowRegistry.Find(window.Id);
                if (definition != null && definition.IsWorkspaceStageWindow)
                {
                    window.Visible = string.Equals(window.Id, ScenarioAuthoringWindowIds.Scenario, StringComparison.OrdinalIgnoreCase)
                        ? activeStage == ScenarioStageKind.None || activeStage == ScenarioStageKind.Test
                        : definition.WorkspaceStage == activeStage;
                    window.Collapsed = !window.Visible && window.Collapsed;
                    continue;
                }

                if (state.PixelEditorChromeSuppressed
                    && (string.Equals(window.Id, ScenarioAuthoringWindowIds.BuildTools, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(window.Id, ScenarioAuthoringWindowIds.Inspector, StringComparison.OrdinalIgnoreCase)))
                {
                    window.Visible = false;
                    window.Collapsed = false;
                }
                else if (string.Equals(window.Id, ScenarioAuthoringWindowIds.BuildTools, StringComparison.OrdinalIgnoreCase))
                    window.Visible = showBuild && !window.Collapsed;
                else if (string.Equals(window.Id, ScenarioAuthoringWindowIds.Inspector, StringComparison.OrdinalIgnoreCase))
                    window.Visible = showWorldInspector && !window.Collapsed;

                if (window.Visible)
                    window.Collapsed = false;
            }
        }

        private static bool IsWorldSurfaceStage(ScenarioStageKind stage)
        {
            return stage == ScenarioStageKind.Bunker
                || stage == ScenarioStageKind.BunkerBackground
                || stage == ScenarioStageKind.BunkerSurface
                || stage == ScenarioStageKind.BunkerInside;
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

        public bool SetWindowFrame(
            ScenarioAuthoringState state,
            string windowId,
            float x,
            float y,
            float width,
            float height,
            bool persist)
        {
            ScenarioAuthoringWindowState window = FindWindow(state, windowId);
            if (window == null)
                return false;

            ScenarioAuthoringWindowDefinition definition = _windowRegistry.Find(windowId);
            bool dockedInspector = definition != null
                && definition.Dock == ScenarioAuthoringShellDock.Right
                && string.Equals(windowId, ScenarioAuthoringWindowIds.Inspector, StringComparison.OrdinalIgnoreCase);
            if (definition == null || (definition.Dock != ScenarioAuthoringShellDock.Floating && !dockedInspector))
                return false;

            float minWidth = dockedInspector ? ScenarioAuthoringShellLayout.InspectorMinWidth : definition.MinWidth;
            float maxWidth = dockedInspector ? ScenarioAuthoringShellLayout.InspectorMaxWidth : float.MaxValue;
            float clampedWidth = Math.Min(maxWidth, Math.Max(minWidth, width));
            float clampedHeight = Math.Max(definition.MinHeight, height);
            bool changed = !window.HasCustomBounds
                || Math.Abs(window.X - x) > 0.01f
                || Math.Abs(window.Y - y) > 0.01f
                || Math.Abs(window.Width - clampedWidth) > 0.01f
                || Math.Abs(window.Height - clampedHeight) > 0.01f;

            window.HasCustomBounds = true;
            window.X = x;
            window.Y = y;
            window.Width = clampedWidth;
            window.Height = clampedHeight;
            changed |= BringWindowToFrontInternal(state, window);

            if (persist)
                PersistIfEnabled(state);

            return changed;
        }

        public bool BringWindowToFront(ScenarioAuthoringState state, string windowId)
        {
            ScenarioAuthoringWindowState window = FindWindow(state, windowId);
            if (window == null)
                return false;

            bool changed = BringWindowToFrontInternal(state, window);
            if (changed)
                PersistIfEnabled(state);
            return changed;
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

        private bool LoadLayout(ScenarioAuthoringState state)
        {
            string path = ScenarioAuthoringStoragePaths.GetLayoutFilePath();
            if (!File.Exists(path))
                return false;

            try
            {
                XmlDocument document = new XmlDocument();
                document.Load(path);
                XmlElement root = document.DocumentElement;
                if (root == null)
                    return false;

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
                    window.HasCustomBounds = ReadBool(element, "customFrame", window.HasCustomBounds);
                    window.X = ReadFloat(element, "x", window.X);
                    window.Y = ReadFloat(element, "y", window.Y);
                    window.Width = ReadFloat(element, "width", window.Width);
                    window.Height = ReadFloat(element, "height", window.Height);
                    window.ZIndex = ReadInt(element, "z", window.ZIndex);
                    NormalizeWindowState(window);
                }

                ApplyStageWorkspace(state);
                return true;
            }
            catch (Exception ex)
            {
                ModAPI.Core.MMLog.WriteWarning("[ScenarioAuthoringLayout] Failed to load layout: " + ex.Message);
                return false;
            }
        }

        private static void HideStartupUtilityWindows(ScenarioAuthoringState state)
        {
            SetStartupUtilityWindowHidden(state, ScenarioAuthoringWindowIds.Hierarchy);
            SetStartupUtilityWindowHidden(state, ScenarioAuthoringWindowIds.SelectionStack);
        }

        private static void SetStartupUtilityWindowHidden(ScenarioAuthoringState state, string windowId)
        {
            if (state == null || string.IsNullOrEmpty(windowId))
                return;

            for (int i = 0; i < state.WindowStates.Count; i++)
            {
                ScenarioAuthoringWindowState window = state.WindowStates[i];
                if (window == null || !string.Equals(window.Id, windowId, StringComparison.OrdinalIgnoreCase))
                    continue;

                window.Visible = false;
                window.Collapsed = false;
                return;
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
                        .Append("\" customFrame=\"")
                        .Append(window.HasCustomBounds ? "true" : "false")
                        .Append("\" x=\"")
                        .Append(FormatFloat(window.X))
                        .Append("\" y=\"")
                        .Append(FormatFloat(window.Y))
                        .Append("\" width=\"")
                        .Append(FormatFloat(window.Width))
                        .Append("\" height=\"")
                        .Append(FormatFloat(window.Height))
                        .Append("\" z=\"")
                        .Append(window.ZIndex.ToString(CultureInfo.InvariantCulture))
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
                HasCustomBounds = false,
                X = 0f,
                Y = 0f,
                Width = definition.DefaultWidth,
                Height = definition.DefaultHeight,
                ZIndex = definition.Order
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

            if (state.Width < 1f)
                state.Width = 1f;
            if (state.Height < 1f)
                state.Height = 1f;
        }

        private ScenarioStageKind ResolveActiveStage(ScenarioAuthoringState state)
        {
            if (state == null)
                return ScenarioStageKind.None;

            if (state.ActiveStage != ScenarioStageKind.None)
                return state.ActiveStage;

            ScenarioStageDefinition activeStage = _stageCoordinator.Resolve(state);
            return activeStage != null ? activeStage.Kind : ScenarioStageKind.None;
        }

        private static bool BringWindowToFrontInternal(ScenarioAuthoringState state, ScenarioAuthoringWindowState window)
        {
            if (state == null || state.WindowStates == null || window == null)
                return false;

            int top = 0;
            for (int i = 0; i < state.WindowStates.Count; i++)
            {
                ScenarioAuthoringWindowState candidate = state.WindowStates[i];
                if (candidate != null && candidate.ZIndex > top)
                    top = candidate.ZIndex;
            }

            if (window.ZIndex >= top)
                return false;

            window.ZIndex = top + 1;
            return true;
        }

        private static bool ReadBool(XmlElement element, string name, bool fallback)
        {
            bool parsed;
            return bool.TryParse(ReadAttribute(element, name, fallback ? "true" : "false"), out parsed) ? parsed : fallback;
        }

        private static int ReadInt(XmlElement element, string name, int fallback)
        {
            int parsed;
            return int.TryParse(
                ReadAttribute(element, name, fallback.ToString(CultureInfo.InvariantCulture)),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out parsed)
                ? parsed
                : fallback;
        }

        private static float ReadFloat(XmlElement element, string name, float fallback)
        {
            float parsed;
            return float.TryParse(
                ReadAttribute(element, name, fallback.ToString(CultureInfo.InvariantCulture)),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out parsed)
                ? parsed
                : fallback;
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

        private static string FormatFloat(float value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }
    }
}
