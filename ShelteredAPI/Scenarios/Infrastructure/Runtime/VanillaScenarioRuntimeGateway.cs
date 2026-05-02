using System;
using System.Collections.Generic;

namespace ShelteredAPI.Scenarios
{
    internal sealed class VanillaScenarioRuntimeGateway : IVanillaScenarioRuntime
    {
        public bool IsWorldReady(out string blockingReason)
        {
            return ScenarioWorldReady.Evaluate(out blockingReason);
        }

        public bool TrySpawnScenario(ScenarioDef definition, out QuestInstance instance, out string reason)
        {
            instance = null;
            reason = null;

            if (definition == null)
            {
                reason = "ScenarioDef is null.";
                return false;
            }

            if (QuestManager.instance == null)
            {
                reason = "QuestManager is not ready.";
                return false;
            }

            try
            {
                instance = QuestManager.instance.SpawnQuestOrScenario(definition);
            }
            catch (Exception ex)
            {
                reason = "QuestManager failed while spawning scenario '" + (definition.id ?? string.Empty) + "': " + ex.Message;
                return false;
            }

            if (instance == null)
            {
                reason = "QuestManager rejected scenario '" + (definition.id ?? string.Empty) + "'.";
                return false;
            }

            return true;
        }

        public bool TryStartQuest(string questId, out string reason)
        {
            reason = null;
            if (QuestManager.instance == null)
            {
                reason = "QuestManager is not ready.";
                return false;
            }

            if (string.IsNullOrEmpty(questId))
            {
                reason = "Quest id is missing.";
                return false;
            }

            try
            {
                if (QuestManager.instance.SpawnQuestWithId(questId))
                    return true;

                reason = "QuestManager rejected quest '" + questId + "'.";
                return false;
            }
            catch (Exception ex)
            {
                reason = "QuestManager failed while starting quest '" + questId + "': " + ex.Message;
                return false;
            }
        }

        public bool TryGetQuestInstance(int instanceId, out QuestInstance instance, out string reason)
        {
            instance = null;
            reason = null;

            if (QuestManager.instance == null)
            {
                reason = "QuestManager is not ready.";
                return false;
            }

            instance = QuestManager.instance.GetQuestInstance(instanceId);
            if (instance == null)
            {
                reason = "QuestInstance was not found: " + instanceId.ToString();
                return false;
            }

            return true;
        }

        public List<QuestInstance> GetCurrentQuests()
        {
            if (QuestManager.instance == null)
                return new List<QuestInstance>();

            List<QuestInstance> quests = QuestManager.instance.GetCurrentQuests(true, true, true);
            return quests ?? new List<QuestInstance>();
        }

        public bool TryFinishQuest(QuestInstance instance, bool success, out string reason)
        {
            reason = null;

            if (instance == null)
            {
                reason = "QuestInstance is null.";
                return false;
            }

            if (QuestManager.instance == null)
            {
                reason = "QuestManager is not ready.";
                return false;
            }

            try
            {
                QuestManager.instance.FinishQuest(instance.id, success);
                return true;
            }
            catch (Exception ex)
            {
                reason = "QuestManager failed while finishing QuestInstance " + instance.id.ToString() + ": " + ex.Message;
                return false;
            }
        }
    }
}
