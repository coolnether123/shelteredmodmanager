using System;
using System.Collections.Generic;
using ModAPI.Scenarios;

using ShelteredAPI.Saves;
using ShelteredScenarioEditor.Application.Authoring;
using ShelteredScenarioEditor.Application.Commands;
using ShelteredScenarioEditor.Application.Stages;
using ShelteredScenarioEditor.Domain.Stages;
using ShelteredScenarioEditor.Presentation.Authoring.Windows;
namespace ShelteredScenarioEditor.Presentation.Authoring.Shell{
    internal sealed class StageNavigationViewModelBuilder
    {
        private readonly ScenarioStageRegistry _stageRegistry;
        private readonly ScenarioStageCoordinator _stageCoordinator;

        public StageNavigationViewModelBuilder(
            ScenarioStageRegistry stageRegistry,
            ScenarioStageCoordinator stageCoordinator)
        {
            _stageRegistry = stageRegistry;
            _stageCoordinator = stageCoordinator;
        }

        public ScenarioAuthoringInspectorAction[] BuildTabs(ScenarioAuthoringState state)
        {
            List<ScenarioAuthoringInspectorAction> actions = new List<ScenarioAuthoringInspectorAction>();
            ScenarioStageKind activeStageKind = ResolveActiveStageKind(state);
            actions.Add(CreateAction(
                ShellUxCommand.ToggleWindow(ScenarioAuthoringWindowIds.Scenario),
                "Home",
                "HOME",
                true,
                activeStageKind == ScenarioStageKind.None && HasWindowVisible(state, ScenarioAuthoringWindowIds.Scenario),
                "Open the scenario home dashboard."));
            AddTopLevelTab(actions, activeStageKind, ScenarioStageKind.Bunker);
            AddTopLevelTab(actions, activeStageKind, ScenarioStageKind.People);
            AddTopLevelTab(actions, activeStageKind, ScenarioStageKind.InventoryStorage);
            AddTopLevelTab(actions, activeStageKind, ScenarioStageKind.Events);
            AddTopLevelTab(actions, activeStageKind, ScenarioStageKind.Quests);
            AddTopLevelTab(actions, activeStageKind, ScenarioStageKind.Map);
            AddTopLevelTab(actions, activeStageKind, ScenarioStageKind.Assets);
            AddTopLevelTab(actions, activeStageKind, ScenarioStageKind.Test);
            AddTopLevelTab(actions, activeStageKind, ScenarioStageKind.Publish);

            return actions.ToArray();
        }

        public ScenarioAuthoringInspectorAction[] BuildWorldSubstageActions(ScenarioAuthoringState state)
        {
            ScenarioStageKind activeStageKind = ResolveActiveStageKind(state);
            List<ScenarioAuthoringInspectorAction> actions = new List<ScenarioAuthoringInspectorAction>();
            ScenarioStageDefinition[] children = _stageRegistry.GetChildren(ScenarioStageKind.Bunker);
            for (int i = 0; i < children.Length; i++)
            {
                ScenarioStageDefinition child = children[i];
                // Surface currently has no distinct selectable authoring target;
                // exposing it as a workspace implies functionality it cannot provide.
                if (child != null && child.Kind != ScenarioStageKind.BunkerSurface)
                    AddTab(actions, child, activeStageKind, true);
            }

            return actions.ToArray();
        }

        public ScenarioAuthoringInspectorAction[] BuildToolbarActions(ScenarioAuthoringState state)
        {
            return new[]
            {
                CreateAction(EditorLifecycleCommand.SaveDraft, "Save", "SAVE", true, true, "Validate and save the current scenario draft.")
            };
        }

        private void AddTopLevelTab(
            List<ScenarioAuthoringInspectorAction> actions,
            ScenarioStageKind activeStageKind,
            ScenarioStageKind stageKind)
        {
            ScenarioStageDefinition definition = _stageRegistry.Find(stageKind);
            if (definition != null)
                AddTab(actions, definition, activeStageKind, false);
        }

        public ScenarioAuthoringInspectorAction[] BuildLayoutActions(ScenarioAuthoringState state)
        {
            return new[]
            {
                CreateAction(ShellUxCommand.Simple(ShellUxCommandKind.ToggleShell, ScenarioAuthoringActionIds.ActionShellToggle), "Shell", "SHOW", true, state != null && state.ShellVisible, "Toggle the authoring shell."),
                CreateAction(ShellUxCommand.Simple(ShellUxCommandKind.FocusSelection, ScenarioAuthoringActionIds.ActionShellFocusSelection), "Focus Selection", "FOCUS", true, state != null && state.FocusSelectionMode, "Focus the layout on the active selection."),
                CreateAction(ShellUxCommand.Simple(ShellUxCommandKind.ResetLayout, ScenarioAuthoringActionIds.ActionShellResetLayout), "Reset Layout", "RESET", true, false, "Reset the authoring layout."),
                CreateAction(ShellUxCommand.Simple(ShellUxCommandKind.ToggleWindowMenu, ScenarioAuthoringActionIds.ActionShellToggleWindowMenu), "Windows", "PANEL", true, false, "Choose visible editor panels.")
            };
        }

        public ScenarioAuthoringToolButtonViewModel[] BuildToolButtons(ScenarioAuthoringState state)
        {
            return new[]
            {
                CreateToolButton(state, ScenarioAuthoringTool.Objects, ToolCommand.Select(ScenarioAuthoringTool.Objects), "Build", "BLD", "Open the world-context asset browser for rooms, objects, walls, wiring, and scene art.")
            };
        }

        public ScenarioAuthoringInspectorAction[] BuildWindowMenuActions(ScenarioAuthoringState state, ScenarioAuthoringWindowRegistry windowRegistry)
        {
            ScenarioAuthoringWindowDefinition[] definitions = windowRegistry != null ? windowRegistry.GetDefinitions() : new ScenarioAuthoringWindowDefinition[0];
            List<ScenarioAuthoringInspectorAction> actions = new List<ScenarioAuthoringInspectorAction>();
            AddWindowMenuGroup(actions, "Tools");
            AddWindowMenuDefinitions(actions, state, definitions, WindowMenuGroup.Tools);
            actions.Add(CreateAction(ScenarioDraftHistoryCommand.Show(), "Draft History", "HIST", true, state != null && state.HistoryWindowOpen, "Open protected versions and recovery snapshots."));
            AddWindowMenuGroup(actions, "Panels");
            AddWindowMenuDefinitions(actions, state, definitions, WindowMenuGroup.Panels);
            AddWindowMenuGroup(actions, "Help & Settings");
            actions.Add(CreateAction(ShellUxCommand.Simple(ShellUxCommandKind.OpenHelp, ScenarioAuthoringActionIds.ActionShellOpenHelp), FormatWindowMenuLabel("Help", state != null && state.HelpWindowOpen && !state.HelpShortcutsView), "HELP", true, state != null && state.HelpWindowOpen && !state.HelpShortcutsView, "Open the workshop help pages."));
            actions.Add(CreateAction(ShellUxCommand.Simple(ShellUxCommandKind.OpenShortcuts, ScenarioAuthoringActionIds.ActionShellOpenShortcuts), FormatWindowMenuLabel("Shortcuts", state != null && state.HelpWindowOpen && state.HelpShortcutsView), "?", true, state != null && state.HelpWindowOpen && state.HelpShortcutsView, "Open the keyboard shortcuts reference (F1)."));
            actions.Add(CreateAction(ShellUxCommand.Simple(ShellUxCommandKind.OpenSettings, ScenarioAuthoringActionIds.ActionShellOpenSettings), FormatWindowMenuLabel("Settings", state != null && state.SettingsWindowOpen), "SET", true, state != null && state.SettingsWindowOpen, "Open authoring settings."));
            actions.Add(CreateAction(EditorLifecycleCommand.ExitToMainMenu, "Exit to Scenario Book", "EXIT", true, false, "Save or discard pending changes, then close the editor."));

            return actions.ToArray();
        }

        private static void AddWindowMenuDefinitions(
            List<ScenarioAuthoringInspectorAction> actions,
            ScenarioAuthoringState state,
            ScenarioAuthoringWindowDefinition[] definitions,
            WindowMenuGroup group)
        {
            for (int i = 0; i < definitions.Length; i++)
            {
                ScenarioAuthoringWindowDefinition definition = definitions[i];
                if (definition == null)
                    continue;
                if (!definition.MenuVisible)
                    continue;
                if (ResolveWindowMenuGroup(definition) != group)
                    continue;

                bool emphasized = HasWindowVisible(state, definition.Id);
                actions.Add(CreateAction(
                    ShellUxCommand.ToggleWindow(definition.Id),
                    FormatWindowMenuLabel(definition.Title, emphasized),
                    emphasized ? "OPEN" : "OFF",
                    true,
                    emphasized,
                    "Toggle the '" + definition.Title + "' panel."));
            }
        }

        private static void AddWindowMenuGroup(List<ScenarioAuthoringInspectorAction> actions, string label)
        {
            actions.Add(CreateAction("window.menu.group." + label.Replace(" ", "_").Replace("&", "and").ToLowerInvariant(), label, "GROUP", false, false, label));
        }

        private static WindowMenuGroup ResolveWindowMenuGroup(ScenarioAuthoringWindowDefinition definition)
        {
            if (definition == null)
                return WindowMenuGroup.Panels;

            if (string.Equals(definition.Id, ScenarioAuthoringWindowIds.Triggers, StringComparison.OrdinalIgnoreCase)
                || string.Equals(definition.Id, ScenarioAuthoringWindowIds.Survivors, StringComparison.OrdinalIgnoreCase)
                || string.Equals(definition.Id, ScenarioAuthoringWindowIds.Stockpile, StringComparison.OrdinalIgnoreCase)
                || string.Equals(definition.Id, ScenarioAuthoringWindowIds.Quests, StringComparison.OrdinalIgnoreCase)
                || string.Equals(definition.Id, ScenarioAuthoringWindowIds.Map, StringComparison.OrdinalIgnoreCase)
                || string.Equals(definition.Id, ScenarioAuthoringWindowIds.AssetBrowser, StringComparison.OrdinalIgnoreCase)
                || string.Equals(definition.Id, ScenarioAuthoringWindowIds.Publish, StringComparison.OrdinalIgnoreCase)
                || string.Equals(definition.Id, ScenarioAuthoringWindowIds.Scenario, StringComparison.OrdinalIgnoreCase))
            {
                return WindowMenuGroup.Tools;
            }

            return WindowMenuGroup.Panels;
        }

        private static string FormatWindowMenuLabel(string title, bool open)
        {
            return (open ? "Open - " : "Closed - ") + title;
        }

        private enum WindowMenuGroup
        {
            Tools,
            Panels
        }

        public string BuildStageLabel(ScenarioAuthoringState state)
        {
            ScenarioStageDefinition stage = ResolveStage(state);
            return stage != null ? ScenarioAuthoringWorkflowLabels.GetStageLabel(stage.Kind, false) : "Workshop";
        }

        private ScenarioStageDefinition ResolveStage(ScenarioAuthoringState state)
        {
            if (state == null)
                return null;

            return _stageCoordinator.Resolve(state);
        }

        private ScenarioStageKind ResolveActiveStageKind(ScenarioAuthoringState state)
        {
            ScenarioStageDefinition activeStage = ResolveStage(state);
            return activeStage != null ? activeStage.Kind : ScenarioStageKind.None;
        }

        private static void AddTab(
            List<ScenarioAuthoringInspectorAction> actions,
            ScenarioStageDefinition definition,
            ScenarioStageKind activeStageKind,
            bool child)
        {
            string label = ScenarioAuthoringWorkflowLabels.GetStageLabel(definition.Kind, child);
            actions.Add(CreateAction(
                ShellUxCommand.SelectStage(definition.Kind),
                child ? ("- " + label) : label,
                ScenarioAuthoringWorkflowLabels.GetStageBadge(definition.Kind),
                true,
                IsActiveStage(definition, activeStageKind),
                ScenarioAuthoringWorkflowLabels.GetStageHint(definition.Kind, child)));
        }

        private static bool IsActiveStage(ScenarioStageDefinition definition, ScenarioStageKind activeStageKind)
        {
            if (definition == null)
                return false;

            if (definition.Kind == activeStageKind)
                return true;

            return definition.Kind == ScenarioStageKind.Bunker
                && (activeStageKind == ScenarioStageKind.BunkerBackground
                    || activeStageKind == ScenarioStageKind.BunkerSurface
                    || activeStageKind == ScenarioStageKind.BunkerInside);
        }

        private static ScenarioAuthoringInspectorAction CreateAction(string id, string label, string badge, bool enabled, bool emphasized, string detail)
        {
            return new ScenarioAuthoringInspectorAction
            {
                Id = id,
                Label = label,
                Badge = badge,
                Enabled = enabled,
                Emphasized = emphasized,
                Detail = detail
            };
        }

        private static ScenarioAuthoringInspectorAction CreateAction(ShellUxCommand command, string label, string badge, bool enabled, bool emphasized, string detail)
        {
            ScenarioAuthoringInspectorAction action = CreateAction(command != null ? command.AutomationId : string.Empty, label, badge, enabled, emphasized, detail);
            action.Command = command;
            return action;
        }

        private static ScenarioAuthoringInspectorAction CreateAction(ScenarioAuthoringCommand command, string label, string badge, bool enabled, bool emphasized, string detail)
        {
            ScenarioAuthoringInspectorAction action = CreateAction(command != null ? command.AutomationId : string.Empty, label, badge, enabled, emphasized, detail);
            action.Command = command;
            return action;
        }

        private static ScenarioAuthoringToolButtonViewModel CreateToolButton(
            ScenarioAuthoringState state,
            ScenarioAuthoringTool tool,
            ToolCommand command,
            string label,
            string iconText,
            string hint)
        {
            bool active = state != null && state.ActiveTool == tool;
            return new ScenarioAuthoringToolButtonViewModel
            {
                Tool = tool,
                Label = label,
                IconText = iconText,
                Action = new ScenarioAuthoringInspectorAction
                {
                    Id = command != null ? command.AutomationId : string.Empty,
                    Command = command,
                    Label = label,
                    IconText = iconText,
                    Hint = hint,
                    Detail = hint,
                    Enabled = true,
                    Emphasized = active
                }
            };
        }

        private static bool HasWindowVisible(ScenarioAuthoringState state, string windowId)
        {
            for (int i = 0; state != null && state.WindowStates != null && i < state.WindowStates.Count; i++)
            {
                ScenarioAuthoringWindowState window = state.WindowStates[i];
                if (window != null && string.Equals(window.Id, windowId, System.StringComparison.OrdinalIgnoreCase))
                    return window.Visible;
            }

            return false;
        }

    }

    internal static class ScenarioAuthoringWorkflowLabels
    {
        public static string GetStageLabel(ScenarioStageKind stageKind, bool child)
        {
            switch (stageKind)
            {
                case ScenarioStageKind.Bunker:
                    return "World";
                case ScenarioStageKind.BunkerBackground:
                    return "Backdrop";
                case ScenarioStageKind.BunkerSurface:
                    return "Surface";
                case ScenarioStageKind.BunkerInside:
                    return "Inside";
                case ScenarioStageKind.InventoryStorage:
                    return "Supplies";
                case ScenarioStageKind.People:
                    return "Cast";
                case ScenarioStageKind.Events:
                    return "Timeline";
                case ScenarioStageKind.Quests:
                    return "Story";
                case ScenarioStageKind.Map:
                    return "Map";
                case ScenarioStageKind.Assets:
                    return "Assets";
                case ScenarioStageKind.Test:
                    return "Test Console";
                case ScenarioStageKind.Publish:
                    return "Publish";
                default:
                    return child ? "Layer" : "Workshop";
            }
        }

        public static string GetStageBadge(ScenarioStageKind stageKind)
        {
            switch (stageKind)
            {
                case ScenarioStageKind.Bunker:
                    return "WORLD";
                case ScenarioStageKind.BunkerBackground:
                    return "BACK";
                case ScenarioStageKind.BunkerSurface:
                    return "SURF";
                case ScenarioStageKind.BunkerInside:
                    return "IN";
                case ScenarioStageKind.InventoryStorage:
                    return "SUP";
                case ScenarioStageKind.People:
                    return "CAST";
                case ScenarioStageKind.Events:
                    return "TIME";
                case ScenarioStageKind.Quests:
                    return "STORY";
                case ScenarioStageKind.Map:
                    return "MAP";
                case ScenarioStageKind.Assets:
                    return "AST";
                case ScenarioStageKind.Test:
                    return "TEST";
                case ScenarioStageKind.Publish:
                    return "EXPORT";
                default:
                    return "WORK";
            }
        }

        public static string GetStageHint(ScenarioStageKind stageKind, bool child)
        {
            switch (stageKind)
            {
                case ScenarioStageKind.Bunker:
                    return "Shape the shelter world: rooms, live objects, and visual layers.";
                case ScenarioStageKind.BunkerBackground:
                    return "Edit the far backdrop and back-layer scenery.";
                case ScenarioStageKind.BunkerSurface:
                    return "Edit the ground-level shelter surface.";
                case ScenarioStageKind.BunkerInside:
                    return "Edit interior rooms, shelter objects, and placed scenario art.";
                case ScenarioStageKind.InventoryStorage:
                    return "Set starting supplies and scheduled stockpile changes.";
                case ScenarioStageKind.People:
                    return "Build the starting cast and future survivors.";
                case ScenarioStageKind.Events:
                    return "Sequence triggers, gates, and scheduled scenario changes.";
                case ScenarioStageKind.Quests:
                    return "Author story beats and quest entries.";
                case ScenarioStageKind.Map:
                    return "Author map-facing scenario setup.";
                case ScenarioStageKind.Assets:
                    return "Browse all placeable and editable art assets in the workshop.";
                case ScenarioStageKind.Test:
                    return "Apply the draft into the live shelter and test it.";
                case ScenarioStageKind.Publish:
                    return "Validate the scenario and create a local package for sharing.";
                default:
                    return child ? "Switch world layer." : "Switch scenario workspace.";
            }
        }

        public static string GetToolLabel(ScenarioAuthoringTool tool)
        {
            switch (tool)
            {
                case ScenarioAuthoringTool.Select:
                    return "Select";
                case ScenarioAuthoringTool.Objects:
                    return "Objects";
                case ScenarioAuthoringTool.Shelter:
                    return "Rooms";
                case ScenarioAuthoringTool.Wiring:
                    return "Walls";
                case ScenarioAuthoringTool.Assets:
                    return "Art";
                case ScenarioAuthoringTool.WinLoss:
                    return "Victory";
                case ScenarioAuthoringTool.Family:
                    return "Cast";
                case ScenarioAuthoringTool.Inventory:
                    return "Supplies";
                default:
                    return tool.ToString();
            }
        }
    }
}
