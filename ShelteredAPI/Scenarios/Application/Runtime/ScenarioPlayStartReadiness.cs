using ShelteredAPI.Scenarios.Definitions;

namespace ShelteredAPI.Scenarios.Application.Runtime{
    internal sealed class ScenarioPlayStartReadiness
    {
        public const string EmptyCastWarning = "No starting survivors.";
        public const string EmptyCastDisabledReason = "No starting survivors. Add a starting survivor in Cast before playtest can begin.";
        public const string UnsavedDraftDisabledReason = "Save draft before testing.";
        public const string ValidationUnavailableDisabledReason = "Validation is unavailable. Open Publish and refresh checks before playtest.";

        public bool CanStartPlay(ScenarioDefinition definition, out string reason)
        {
            reason = null;
            if (HasStartingSurvivor(definition))
                return true;

            reason = EmptyCastDisabledReason;
            return false;
        }

        private static bool HasStartingSurvivor(ScenarioDefinition definition)
        {
            if (definition == null || definition.FamilySetup == null || definition.FamilySetup.Members == null)
                return false;

            for (int i = 0; i < definition.FamilySetup.Members.Count; i++)
            {
                if (definition.FamilySetup.Members[i] != null)
                    return true;
            }

            return false;
        }
    }
}
