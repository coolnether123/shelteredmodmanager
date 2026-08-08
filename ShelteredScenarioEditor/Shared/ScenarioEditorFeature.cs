using ModAPI.Core;
using ShelteredModManager.Shared.ScenarioEditor;

namespace ShelteredScenarioEditor.Shared
{
    internal static class ScenarioEditorFeature
    {
        public const string EnabledOptionId = ScenarioEditorBooleanOptionDescriptor.Id;
        public const string EnabledOptionLabel = ScenarioEditorBooleanOptionDescriptor.Label;
        public const string EnabledOptionDescription = ScenarioEditorBooleanOptionDescriptor.Description;

        public const string DevActorProviderOptionId = "ShelteredScenarioEditor.DevActorAuthoringProvider";
        public const string DevActorProviderOptionLabel = "Developer Actor Authoring Fields";
        public const string DevActorProviderOptionDescription =
            "Registers the editor's verification-only actor field provider.";

        public static bool Enabled
        {
            get { return ManagerBooleanOptions.GetBool(EnabledOptionId, ScenarioEditorBooleanOptionDescriptor.DefaultValue); }
        }

        public static bool DevActorProviderEnabled
        {
            get { return ManagerBooleanOptions.GetBool(DevActorProviderOptionId, false); }
        }

        public static void RegisterOptions()
        {
            ManagerBooleanOptions.RegisterBooleanOption(new ManagerBooleanOptionDefinition
            {
                Id = EnabledOptionId,
                Owner = ScenarioEditorBooleanOptionDescriptor.Owner,
                Label = EnabledOptionLabel,
                Description = EnabledOptionDescription,
                DefaultValue = ScenarioEditorBooleanOptionDescriptor.DefaultValue,
                RequiresRestart = ScenarioEditorBooleanOptionDescriptor.RequiresRestart,
                SortOrder = ScenarioEditorBooleanOptionDescriptor.SortOrder
            });
            ManagerBooleanOptions.RegisterBooleanOption(new ManagerBooleanOptionDefinition
            {
                Id = DevActorProviderOptionId,
                Owner = "ShelteredScenarioEditor",
                Label = DevActorProviderOptionLabel,
                Description = DevActorProviderOptionDescription,
                DefaultValue = false,
                RequiresRestart = true,
                SortOrder = 101
            });
        }
    }
}
