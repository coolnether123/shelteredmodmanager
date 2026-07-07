using ShelteredAPI.Scenarios.Definitions;

namespace ShelteredAPI.Scenarios.Application.Runtime{
    internal sealed class ScenarioPlayStartReadiness
    {
        public const string EmptyCastWarning = "No starting survivors.";
        public const string EmptyCastDisabledReason = "No starting survivors. Add a starting survivor in Cast before playtest can begin.";
        public const string UnsavedDraftDisabledReason = "Save draft before testing.";
        public const string ValidationUnavailableDisabledReason = "Validation is unavailable. Open Publish and refresh checks before playtest.";

        public static int CountStartingSurvivors(ScenarioDefinition definition)
        {
            if (definition == null || definition.FamilySetup == null || definition.FamilySetup.Members == null)
                return 0;

            return definition.FamilySetup.Members.Count;
        }

        public static bool HasStartingSurvivor(ScenarioDefinition definition)
        {
            return CountStartingSurvivors(definition) > 0;
        }

        public bool CanStartPlay(ScenarioDefinition definition, out string reason)
        {
            reason = null;
            if (HasStartingSurvivor(definition))
                return true;

            reason = EmptyCastDisabledReason;
            return false;
        }
    }
}
