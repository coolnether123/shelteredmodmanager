using System;
using System.Collections.Generic;
using ModAPI.Scenarios;

using ShelteredAPI.Saves;
using ShelteredAPI.Scenarios.Application.Authoring;
using ShelteredAPI.Scenarios.Application.Stages;
using ShelteredAPI.Scenarios.Domain.Stages;
using ShelteredAPI.Scenarios.Presentation.Authoring.Windows;
namespace ShelteredAPI.Scenarios.Presentation.Authoring.Shell{
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
            ScenarioStageDefinition[] topLevel = _stageRegistry.GetTopLevel();
            for (int i = 0; i < topLevel.Length; i++)
            {
                ScenarioStageDefinition definition = topLevel[i];
                if (definition == null)
                    continue;

                AddTab(actions, definition, activeStageKind, false);
                if (definition.Kind == ScenarioStageKind.Bunker)
                {
                    ScenarioStageDefinition[] children = _stageRegistry.GetChildren(ScenarioStageKind.Bunker);
                    for (int childIndex = 0; childIndex < children.Length; childIndex++)
                    {
                        ScenarioStageDefinition child = children[childIndex];
                        if (child != null)
                            AddTab(actions, child, activeStageKind, true);
                    }
                }
            }

            return actions.ToArray();
        }

        public ScenarioAuthoringInspectorAction[] BuildToolbarActions(ScenarioAuthoringState state)
        {
            return new[]
            {
                CreateAction(ScenarioAuthoringActionIds.ActionSave, "Save Draft", "SAVE", true, true, "Validate and save the current scenario draft."),
                CreateAction(ScenarioAuthoringActionIds.ActionShellOpenCalendar, "Schedule", "TIME", true, HasWindowVisible(state, ScenarioAuthoringWindowIds.Calendar), "Open the scenario schedule."),
                CreateAction(ScenarioAuthoringActionIds.ActionShellOpenSettings, "Settings", "SET", true, false, "Open authoring settings.")
            };
        }

        public ScenarioAuthoringInspectorAction[] BuildLayoutActions(ScenarioAuthoringState state)
        {
            return new[]
            {
                CreateAction(ScenarioAuthoringActionIds.ActionShellToggle, "Shell", "SHOW", true, state != null && state.ShellVisible, "Toggle the authoring shell."),
                CreateAction(ScenarioAuthoringActionIds.ActionShellFocusSelection, "Focus Selection", "FOCUS", true, state != null && state.FocusSelectionMode, "Focus the layout on the active selection."),
                CreateAction(ScenarioAuthoringActionIds.ActionShellResetLayout, "Reset Layout", "RESET", true, false, "Reset the authoring layout."),
                CreateAction(ScenarioAuthoringActionIds.ActionShellToggleWindowMenu, "Windows", "PANEL", true, false, "Choose visible editor panels.")
            };
        }

        public ScenarioAuthoringToolButtonViewModel[] BuildToolButtons(ScenarioAuthoringState state)
        {
            bool hasSelection = state != null && state.SelectedTarget != null;
            return new[]
            {
                CreateToolButton(state, ScenarioAuthoringTool.Select, ScenarioAuthoringActionIds.ActionToolSelect, "Select", "PICK", "Pick and inspect shelter objects."),
                CreateToolButton(state, ScenarioAuthoringTool.Objects, ScenarioAuthoringActionIds.ActionToolObjects, "Objects", "OBJ", "Place or capture shelter objects."),
                CreateToolButton(state, ScenarioAuthoringTool.Shelter, ScenarioAuthoringActionIds.ActionToolShelter, "Rooms", "ROOM", "Author rooms, ladders, lights, and structure."),
                CreateToolButton(state, ScenarioAuthoringTool.Wiring, ScenarioAuthoringActionIds.ActionToolWiring, "Walls", "WALL", "Edit wall and wiring layers."),
                CreateToolButton(state, ScenarioAuthoringTool.Assets, ScenarioAuthoringActionIds.ActionToolAssets, "Art", "ART", hasSelection ? "Edit or place scenario art for the selection." : "Place snapped scene art."),
                CreateToolButton(state, ScenarioAuthoringTool.WinLoss, ScenarioAuthoringActionIds.ActionToolWinLoss, "Victory", "WIN", "Define win and loss conditions.")
            };
        }

        public ScenarioAuthoringInspectorAction[] BuildWindowMenuActions(ScenarioAuthoringState state, ScenarioAuthoringWindowRegistry windowRegistry)
        {
            ScenarioAuthoringWindowDefinition[] definitions = windowRegistry != null ? windowRegistry.GetDefinitions() : new ScenarioAuthoringWindowDefinition[0];
            List<ScenarioAuthoringInspectorAction> actions = new List<ScenarioAuthoringInspectorAction>();
            for (int i = 0; i < definitions.Length; i++)
            {
                ScenarioAuthoringWindowDefinition definition = definitions[i];
                if (definition == null)
                    continue;
                if (!definition.MenuVisible)
                    continue;

                bool emphasized = HasWindowVisible(state, definition.Id);
                actions.Add(CreateAction(
                    ScenarioAuthoringActionIds.ActionWindowTogglePrefix + definition.Id,
                    definition.Title,
                    "PANEL",
                    true,
                    emphasized,
                    "Toggle the '" + definition.Title + "' panel."));
            }

            return actions.ToArray();
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
                ScenarioAuthoringActionIds.ActionStageSelectPrefix + definition.Kind,
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

        private static ScenarioAuthoringToolButtonViewModel CreateToolButton(
            ScenarioAuthoringState state,
            ScenarioAuthoringTool tool,
            string actionId,
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
                    Id = actionId,
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
                    return "Interior";
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
                case ScenarioStageKind.Test:
                    return "Test";
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
                case ScenarioStageKind.Test:
                    return "TEST";
                case ScenarioStageKind.Publish:
                    return "PUB";
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
                case ScenarioStageKind.Test:
                    return "Apply the draft into the live shelter and test it.";
                case ScenarioStageKind.Publish:
                    return "Validate the scenario and prepare it for distribution.";
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
