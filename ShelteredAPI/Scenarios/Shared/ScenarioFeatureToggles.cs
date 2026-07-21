using ModAPI.Core;
namespace ShelteredAPI.Scenarios.Shared{
    internal static class ScenarioFeatureToggles
    {
        public const string CustomScenarioEditorPatchToggleId = "ShelteredAPI.PatchCustomScenarioEditor";
        public const string CustomScenarioEditorPatchLabel = "Custom Scenario Authoring (Preview)";
        public const string CustomScenarioEditorPatchDescription =
            "Enables the advanced scenario authoring workspace and Add New Scenario entry. Installed custom scenarios remain available while this preview is disabled.";
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
                DefaultValue = false,
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
            return ManagerBooleanOptions.GetBool(CustomScenarioEditorPatchToggleId, false);
        }

        public static bool IsDevActorAuthoringProviderEnabled()
        {
            return ManagerBooleanOptions.GetBool(DevActorAuthoringProviderToggleId, false);
        }
    }
}
