using System;
using System.Collections.Generic;
using ModAPI.Scenarios;

namespace ShelteredAPI.Scenarios
{
    internal sealed class ScheduledQuestRuntimeService : IScenarioEffectHandler, IScenarioConditionEvaluator
    {
        public bool CanHandle(ScenarioEffectKind kind)
        {
            return kind == ScenarioEffectKind.StartQuest;
        }

        public bool Handle(ScenarioDefinition definition, ScenarioEffectDefinition effect, ScenarioRuntimeState state, out string message)
        {
            message = null;
            string questId = effect != null ? (effect.QuestId ?? effect.TargetId) : null;
            if (QuestManager.instance == null || string.IsNullOrEmpty(questId))
            {
                message = "QuestManager is not ready or quest id is missing.";
                return false;
            }

            QuestManager.instance.SpawnQuestWithId(questId);
            return true;
        }

        public bool CanEvaluate(ScenarioConditionKind kind)
        {
            return kind == ScenarioConditionKind.QuestActive
                || kind == ScenarioConditionKind.QuestCompleted
                || kind == ScenarioConditionKind.QuestFailed;
        }

        public bool IsSatisfied(ScenarioDefinition definition, ScenarioConditionRef condition, ScenarioRuntimeState state, out string reason)
        {
            reason = null;
            if (condition == null || QuestManager.instance == null)
            {
                reason = "QuestManager is not ready.";
                return false;
            }

            List<QuestInstance> quests = QuestManager.instance.GetCurrentQuests(true, true, true);
            for (int i = 0; quests != null && i < quests.Count; i++)
            {
                QuestInstance quest = quests[i];
                if (quest != null && quest.definition != null && string.Equals(quest.definition.id, condition.TargetId, StringComparison.OrdinalIgnoreCase))
                {
                    if (condition.Kind == ScenarioConditionKind.QuestActive)
                        return quest.state == QuestInstance.QuestState.Active;
                    if (condition.Kind == ScenarioConditionKind.QuestCompleted)
                        return quest.state == QuestInstance.QuestState.Completed;
                    if (condition.Kind == ScenarioConditionKind.QuestFailed)
                        return quest.state == QuestInstance.QuestState.Failed;
                }
            }

            reason = "Quest condition not satisfied: " + condition.TargetId;
            return false;
        }
    }
}
