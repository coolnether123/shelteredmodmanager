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
            Register(ScenarioAuthoringActionIds.UndoKey, "Undo Authoring Change", "Scenario Authoring", KeyCode.Z, KeyCode.None, "Undo the last scenario authoring change. Hold Ctrl to trigger.");
            Register(ScenarioAuthoringActionIds.RedoKey, "Redo Authoring Change", "Scenario Authoring", KeyCode.Y, KeyCode.None, "Redo the last undone authoring change. Hold Ctrl to trigger.");
            Register(ScenarioAuthoringActionIds.CopyKey, "Copy Active Sprite Swap", "Scenario Authoring", KeyCode.C, KeyCode.None, "Copy the selected target's sprite swap to the clipboard. Hold Ctrl to trigger.");
            Register(ScenarioAuthoringActionIds.PasteKey, "Paste Sprite Swap", "Scenario Authoring", KeyCode.V, KeyCode.None, "Paste the clipboard sprite swap onto the selected target. Hold Ctrl to trigger.");
            Register(ScenarioAuthoringActionIds.RevertKey, "Revert Selected Sprite", "Scenario Authoring", KeyCode.R, KeyCode.None, "Revert the selected target back to its original sprite.");
        }

        public static bool IsUndoDown()
        {
            return IsSelectionModifierHeld() && InputActionRegistry.IsDown(ScenarioAuthoringActionIds.UndoKey);
        }

        public static bool IsRedoDown()
        {
            return IsSelectionModifierHeld() && InputActionRegistry.IsDown(ScenarioAuthoringActionIds.RedoKey);
        }

        public static bool IsCopyDown()
        {
            return IsSelectionModifierHeld() && InputActionRegistry.IsDown(ScenarioAuthoringActionIds.CopyKey);
        }

        public static bool IsPasteDown()
        {
            return IsSelectionModifierHeld() && InputActionRegistry.IsDown(ScenarioAuthoringActionIds.PasteKey);
        }

        public static bool IsRevertDown()
        {
            return IsSelectionModifierHeld() && InputActionRegistry.IsDown(ScenarioAuthoringActionIds.RevertKey);
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
