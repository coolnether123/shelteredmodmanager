using ModAPI.InputActions;
using UnityEngine;

namespace ShelteredAPI.Scenarios
{
    public static class ScenarioAuthoringInputActions
    {
        private static bool _registered;

        public static void EnsureRegistered()
        {
            if (_registered)
                return;

            _registered = true;
            Register(ScenarioAuthoringActionIds.ToggleShell, "Toggle Authoring Shell", "Scenario Authoring", KeyCode.F6, KeyCode.None, "Open or close the scenario authoring shell.");
            Register(ScenarioAuthoringActionIds.SelectionModifier, "Selection Modifier", "Scenario Authoring", KeyCode.LeftControl, KeyCode.RightControl, "Hold to enable scenario target selection.");
            Register(ScenarioAuthoringActionIds.ConfirmSelection, "Confirm Selection", "Scenario Authoring", KeyCode.Mouse0, KeyCode.None, "Select the currently hovered scenario target.");
            Register(ScenarioAuthoringActionIds.ClearSelection, "Clear Selection", "Scenario Authoring", KeyCode.Mouse1, KeyCode.None, "Clear the current scenario target selection.");
            Register(ScenarioAuthoringActionIds.SaveDraft, "Save Draft", "Scenario Authoring", KeyCode.F5, KeyCode.None, "Persist the current scenario draft XML.");
            Register(ScenarioAuthoringActionIds.TogglePlaytest, "Toggle Playtest", "Scenario Authoring", KeyCode.F7, KeyCode.None, "Toggle between paused authoring mode and scenario playtest mode.");
        }

        public static bool IsSelectionModifierHeld()
        {
            return InputActionRegistry.IsHeld(ScenarioAuthoringActionIds.SelectionModifier);
        }

        public static bool IsConfirmSelectionDown()
        {
            return InputActionRegistry.IsDown(ScenarioAuthoringActionIds.ConfirmSelection);
        }

        public static bool IsClearSelectionDown()
        {
            return InputActionRegistry.IsDown(ScenarioAuthoringActionIds.ClearSelection);
        }

        private static void Register(string id, string label, string category, KeyCode primary, KeyCode secondary, string description)
        {
            InputActionRegistry.Register(new ModInputAction(
                id,
                label,
                category,
                new InputBinding(primary, secondary),
                description));
        }
    }
}
