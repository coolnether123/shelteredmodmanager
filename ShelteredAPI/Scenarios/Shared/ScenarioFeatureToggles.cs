using ModAPI.Core;
namespace ShelteredAPI.Scenarios.Shared{
    internal static class ScenarioFeatureToggles
    {
        public const string CustomScenarioEditorPatchToggleId = "ShelteredAPI.PatchCustomScenarioEditor";
        public const string CustomScenarioEditorPatchLabel = "Custom Scenario Editor";
        public const string CustomScenarioEditorPatchDescription =
            "Enables ShelteredAPI's custom scenario editor hooks and the Add New Scenario editor entry.";

        public static void RegisterCustomScenarioEditorToggle()
        {
            ManagerBooleanOptions.RegisterBooleanOption(new ManagerBooleanOptionDefinition
            {
                Id = CustomScenarioEditorPatchToggleId,
                Owner = "ShelteredAPI",
                Label = CustomScenarioEditorPatchLabel,
                Description = CustomScenarioEditorPatchDescription,
                DefaultValue = false,
                RequiresRestart = true,
                SortOrder = 100
            });
        }

        public static bool IsCustomScenarioEditorEnabled()
        {
            return ManagerBooleanOptions.GetBool(CustomScenarioEditorPatchToggleId, false);
        }
    }
}
