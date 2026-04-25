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
                || kind == ScenarioConditionKind.QuestFailed
                || kind == ScenarioConditionKind.CustomTrigger;
        }

        public bool IsSatisfied(ScenarioDefinition definition, ScenarioConditionRef condition, ScenarioRuntimeState state, out string reason)
        {
            reason = null;
            if (condition == null || QuestManager.instance == null)
            {
                reason = "QuestManager is not ready.";
                return false;
            }

            if (condition.Kind == ScenarioConditionKind.CustomTrigger)
                return HasTrigger(definition, condition.TargetId);

            List<QuestInstance> quests = QuestManager.instance.GetCurrentQuests(true, true, true);
            for (int i = 0; quests != null && i < quests.Count; i++)
            {
                QuestInstance quest = quests[i];
                if (quest != null && quest.definition != null && string.Equals(quest.definition.id, condition.TargetId, StringComparison.OrdinalIgnoreCase))
                    return condition.Kind == ScenarioConditionKind.QuestActive;
            }

            reason = "Quest condition not satisfied: " + condition.TargetId;
            return false;
        }

        private static bool HasTrigger(ScenarioDefinition definition, string id)
        {
            for (int i = 0; definition != null && definition.TriggersAndEvents != null && definition.TriggersAndEvents.Triggers != null && i < definition.TriggersAndEvents.Triggers.Count; i++)
            {
                TriggerDef trigger = definition.TriggersAndEvents.Triggers[i];
                if (trigger != null && string.Equals(trigger.Id, id, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }
    }
}
