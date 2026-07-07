namespace ShelteredAPI.Scenarios.Application.Authoring{
    internal static class ScenarioAuthoringLocalActionIds
    {
        public const string ActionCaptureFamilyConfirm = "capture.family.confirm";
        public const string ActionCaptureInventoryConfirm = "capture.inventory.confirm";
        public const string ActionStartingSurvivorEditorOpenPrefix = "scenario.survivor_editor.start.";
        public const string ActionFutureSurvivorEditorOpenPrefix = "scenario.survivor_editor.future.";
        public const string ActionInventoryStartingPickerOpenPrefix = "scenario.inventory.start.picker.";
        public const string ActionInventorySchedulePickerOpenPrefix = "scenario.inventory.schedule.picker.";
        public const string ActionSurvivorOpenColorPickerPrefix = "scenario.survivor.color.open.";
        public const string ActionSurvivorApplyColorCommandPrefix = "color_apply.";

        public const string FocusedKindCaptureFamily = "capture_family_preview";
        public const string FocusedKindCaptureInventory = "capture_inventory_preview";
        public const string FocusedKindStartingSurvivor = "starting_survivor";
        public const string FocusedKindFutureSurvivor = "future_survivor";
        public const string FocusedKindInventoryStartingPicker = "inventory_starting_picker";
        public const string FocusedKindInventorySchedulePicker = "inventory_schedule_picker";
    }
}
