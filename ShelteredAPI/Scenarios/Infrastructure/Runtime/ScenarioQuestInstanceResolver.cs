using System;

namespace ShelteredAPI.Scenarios
{
    internal sealed class ScenarioQuestInstanceResolver : IScenarioQuestInstanceResolver
    {
        private readonly IVanillaScenarioRuntime _vanillaRuntime;

        public ScenarioQuestInstanceResolver(IVanillaScenarioRuntime vanillaRuntime)
        {
            _vanillaRuntime = vanillaRuntime;
        }

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
                instance = FindScenarioInstance(binding.ScenarioId);
                if (instance == null)
                {
                    reason = "Active scenario binding has no spawned QuestInstance id.";
                    return false;
                }

                return true;
            }

            if (!_vanillaRuntime.TryGetQuestInstance(binding.ScenarioQuestInstanceId.Value, out instance, out reason))
                return false;

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

        private QuestInstance FindScenarioInstance(string scenarioId)
        {
            if (string.IsNullOrEmpty(scenarioId))
                return null;

            System.Collections.Generic.List<QuestInstance> quests = _vanillaRuntime.GetCurrentQuests();
            for (int i = 0; quests != null && i < quests.Count; i++)
            {
                QuestInstance quest = quests[i];
                if (quest != null
                    && quest.definition != null
                    && quest.definition.IsScenario()
                    && string.Equals(quest.definition.id, scenarioId, StringComparison.OrdinalIgnoreCase))
                {
                    return quest;
                }
            }

            return null;
        }
    }
}
