using ModAPI.Core;
namespace ShelteredAPI.Scenarios.Shared{
    internal static class ScenarioFeatureToggles
    {
        public const string CustomScenarioEditorPatchToggleId = "ShelteredAPI.PatchCustomScenarioEditor";
        public const string CustomScenarioEditorPatchLabel = "Custom Scenario Editor";
        public const string CustomScenarioEditorPatchDescription =
            "Enables ShelteredAPI's custom scenario editor hooks and the Add New Scenario editor entry.";
        public const string DevActorAuthoringProviderToggleId = "ShelteredAPI.DevActorAuthoringProvider";
        public const string DevActorAuthoringProviderLabel = "Dev Actor Authoring Fields";
        public const string DevActorAuthoringProviderDescription =
            "Registers ShelteredAPI's internal test actor-authoring field provider for scenario editor verification.";

        public static void RegisterCustomScenarioEditorToggle()
        {
            ManagerBooleanOptions.RegisterBooleanOption(new ManagerBooleanOptionDefinition
            {
                Id = CustomScenarioEditorPatchToggleId,
                Owner = "ShelteredAPI",
                Label = CustomScenarioEditorPatchLabel,
                Description = CustomScenarioEditorPatchDescription,
                DefaultValue = true,
                RequiresRestart = true,
                SortOrder = 100
            });
            ManagerBooleanOptions.RegisterBooleanOption(new ManagerBooleanOptionDefinition
            {
                Id = DevActorAuthoringProviderToggleId,
                Owner = "ShelteredAPI",
                Label = DevActorAuthoringProviderLabel,
                Description = DevActorAuthoringProviderDescription,
                DefaultValue = false,
                RequiresRestart = true,
                SortOrder = 101
            });
        }

        public static bool IsCustomScenarioEditorEnabled()
        {
            return ManagerBooleanOptions.GetBool(CustomScenarioEditorPatchToggleId, true);
        }

        public static bool IsDevActorAuthoringProviderEnabled()
        {
            return ManagerBooleanOptions.GetBool(DevActorAuthoringProviderToggleId, false);
        }
    }
}
