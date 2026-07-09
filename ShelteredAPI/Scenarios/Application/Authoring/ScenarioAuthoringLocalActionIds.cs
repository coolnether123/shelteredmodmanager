namespace ShelteredAPI.Scenarios.Application.Authoring{
    internal static class ScenarioAuthoringLocalActionIds
    {
        public const string ActionCaptureFamilyConfirm = "capture.family.confirm";
        public const string ActionStartingSurvivorEditorOpenPrefix = "scenario.survivor_editor.start.";
        public const string ActionFutureSurvivorEditorOpenPrefix = "scenario.survivor_editor.future.";
        public const string ActionInventoryStartingPickerOpenPrefix = "scenario.inventory.start.picker.";
        public const string ActionInventorySchedulePickerOpenPrefix = "scenario.inventory.schedule.picker.";
        public const string ActionInventoryStartingAddAndPick = "scenario.inventory.start.add_pick";
        public const string ActionInventoryScheduleAddAndPick = "scenario.inventory.schedule.add_pick";
        public const string ActionInventoryScheduleRemoveAndPick = "scenario.inventory.schedule.remove_pick";
        public const string ActionSuppliesPresetPreviewPrefix = "scenario.inventory.preset.preview.";
        public const string ActionSuppliesPresetApplyPrefix = "scenario.inventory.preset.apply.";
        public const string ActionSuppliesMergeDuplicates = "scenario.inventory.merge_duplicates";
        public const string ActionWorldEventEditorOpenPrefix = "scenario.world_event.editor.";
        public const string ActionWorldEventItemPickerOpenPrefix = "scenario.world_event.item_picker.";
        public const string ActionSurvivorOpenColorPickerPrefix = "scenario.survivor.color.open.";
        public const string ActionSurvivorApplyColorCommandPrefix = "color_apply.";

        public const string FocusedKindCaptureFamily = "capture_family_preview";
        public const string FocusedKindStartingSurvivor = "starting_survivor";
        public const string FocusedKindFutureSurvivor = "future_survivor";
        public const string FocusedKindInventoryStartingPicker = "inventory_starting_picker";
        public const string FocusedKindInventorySchedulePicker = "inventory_schedule_picker";
        public const string FocusedKindWorldEvent = "world_event";
        public const string FocusedKindWorldEventItemPickerPrefix = "world_event_item_picker:";
        public const string FocusedKindSuppliesPreset = "supplies_preset_preview";
    }
}
