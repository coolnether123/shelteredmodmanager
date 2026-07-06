using ShelteredAPI.Scenarios.Definitions;

namespace ShelteredAPI.Scenarios.Application.Runtime{
    internal sealed class ScenarioPlayStartReadiness
    {
        public const string EmptyCastWarning = "This scenario starts with no survivors; playtest and live start are disabled until at least one survivor is present at the start.";
        public const string EmptyCastDisabledReason = EmptyCastWarning + " Add a starting survivor in Cast. Future arrivals are scheduled after play begins and cannot safely prevent Sheltered's initial empty-family game over.";

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
