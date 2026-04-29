using System;

namespace ShelteredAPI.Scenarios
{
    internal sealed class ScenarioQuestInstanceResolver : IScenarioQuestInstanceResolver
    {
        public bool TryResolve(ScenarioRuntimeBinding binding, out QuestInstance instance, out string reason)
        {
            instance = null;
            reason = null;

            if (binding == null || !binding.IsActive || binding.IsConvertedToNormalSave)
            {
                reason = "No active scenario binding is available.";
                return false;
            }

            if (!binding.ScenarioQuestInstanceId.HasValue)
            {
                reason = "Active scenario binding has no spawned QuestInstance id.";
                return false;
            }

            if (QuestManager.instance == null)
            {
                reason = "QuestManager is not ready.";
                return false;
            }

            instance = QuestManager.instance.GetQuestInstance(binding.ScenarioQuestInstanceId.Value);
            if (instance == null)
            {
                reason = "Bound scenario QuestInstance was not found: " + binding.ScenarioQuestInstanceId.Value.ToString();
                return false;
            }

            if (instance.definition == null || !instance.definition.IsScenario())
            {
                instance = null;
                reason = "Bound QuestInstance is not a scenario.";
                return false;
            }

            if (!string.IsNullOrEmpty(binding.ScenarioId)
                && !string.Equals(instance.definition.id, binding.ScenarioId, StringComparison.OrdinalIgnoreCase))
            {
                reason = "Bound QuestInstance definition '" + instance.definition.id
                    + "' does not match scenario binding '" + binding.ScenarioId + "'.";
                instance = null;
                return false;
            }

            return true;
        }
    }
}
