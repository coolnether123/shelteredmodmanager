using System.Collections.Generic;
using ModAPI.Scenarios;

namespace ShelteredAPI.Scenarios
{
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

                AddTab(actions, definition, activeStageKind, BuildBadge(definition.Kind), false);
                if (definition.Kind == ScenarioStageKind.Bunker)
                {
                    ScenarioStageDefinition[] children = _stageRegistry.GetChildren(ScenarioStageKind.Bunker);
                    for (int childIndex = 0; childIndex < children.Length; childIndex++)
                    {
                        ScenarioStageDefinition child = children[childIndex];
                        if (child != null)
                            AddTab(actions, child, activeStageKind, "B" + (childIndex + 1), true);
                    }
                }
            }

            return actions.ToArray();
        }

        public ScenarioAuthoringInspectorAction[] BuildToolbarActions(ScenarioAuthoringState state)
        {
            return new[]
            {
                CreateAction(ScenarioAuthoringActionIds.ActionSave, "Save", "SV", true, true, "Validate and save the current draft."),
                CreateAction(ScenarioAuthoringActionIds.ActionShellOpenCalendar, "Calendar", "CL", true, HasWindowVisible(state, ScenarioAuthoringWindowIds.Calendar), "Open the scheduled event calendar."),
                CreateAction(ScenarioAuthoringActionIds.ActionShellOpenSettings, "Settings", "ST", true, false, "Open authoring settings.")
            };
        }

        public ScenarioAuthoringInspectorAction[] BuildLayoutActions(ScenarioAuthoringState state)
        {
            return new[]
            {
                CreateAction(ScenarioAuthoringActionIds.ActionShellToggle, "Shell", "SH", true, state != null && state.ShellVisible, "Toggle the authoring shell."),
                CreateAction(ScenarioAuthoringActionIds.ActionShellFocusSelection, "Focus", "FC", true, state != null && state.FocusSelectionMode, "Focus the layout on the active stage and selection."),
                CreateAction(ScenarioAuthoringActionIds.ActionShellResetLayout, "Reset", "RS", true, false, "Reset the authoring layout."),
                CreateAction(ScenarioAuthoringActionIds.ActionShellToggleWindowMenu, "Windows", "WN", true, false, "Choose visible editor panels.")
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
                if (IsLegacyDockWindow(definition.Id))
                    continue;

                bool emphasized = HasWindowVisible(state, definition.Id);
                actions.Add(CreateAction(
                    ScenarioAuthoringActionIds.ActionWindowTogglePrefix + definition.Id,
                    definition.Title,
                    "WN",
                    true,
                    emphasized,
                    "Toggle the '" + definition.Title + "' panel."));
            }

            return actions.ToArray();
        }

        private static bool IsLegacyDockWindow(string windowId)
        {
            return string.Equals(windowId, ScenarioAuthoringWindowIds.Scenario, System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(windowId, ScenarioAuthoringWindowIds.Layers, System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(windowId, ScenarioAuthoringWindowIds.TilesPalette, System.StringComparison.OrdinalIgnoreCase);
        }

        public string BuildStageLabel(ScenarioAuthoringState state)
        {
            ScenarioStageDefinition stage = ResolveStage(state);
            return stage != null ? stage.DisplayName : "Shell";
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
            string badge,
            bool child)
        {
            actions.Add(CreateAction(
                ScenarioAuthoringActionIds.ActionStageSelectPrefix + definition.Kind,
                child ? ("- " + definition.DisplayName) : definition.DisplayName,
                badge,
                true,
                IsActiveStage(definition, activeStageKind),
                "Switch to the " + definition.DisplayName + " stage."));
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

        private static string BuildBadge(ScenarioStageKind stageKind)
        {
            switch (stageKind)
            {
                case ScenarioStageKind.Bunker:
                    return "BK";
                case ScenarioStageKind.InventoryStorage:
                    return "IV";
                case ScenarioStageKind.People:
                    return "PP";
                case ScenarioStageKind.Events:
                    return "EV";
                case ScenarioStageKind.Quests:
                    return "QT";
                case ScenarioStageKind.Map:
                    return "MP";
                case ScenarioStageKind.Test:
                    return "TS";
                case ScenarioStageKind.Publish:
                    return "PB";
                default:
                    return "ST";
            }
        }
    }
}
