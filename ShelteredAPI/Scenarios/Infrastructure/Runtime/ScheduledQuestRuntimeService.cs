using System;
using System.Collections.Generic;
using ModAPI.Scenarios;

using ShelteredAPI.Content;
using ShelteredAPI.Scenarios.Application.Conditions;
using ShelteredAPI.Scenarios.Application.Effects;
using ShelteredAPI.Scenarios.Application.Runtime;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Domain.Conditions;
using ShelteredAPI.Scenarios.Domain.Effects;
using ShelteredAPI.Scenarios.Domain.Runtime;
namespace ShelteredAPI.Scenarios.Infrastructure.Runtime{
    internal sealed class ScheduledQuestRuntimeService : IScenarioEffectHandler, IScenarioConditionEvaluator
    {
        private readonly IVanillaScenarioRuntime _vanillaRuntime;

        public ScheduledQuestRuntimeService(IVanillaScenarioRuntime vanillaRuntime)
        {
            _vanillaRuntime = vanillaRuntime;
        }

        public bool CanHandle(ScenarioEffectKind kind)
        {
            return kind == ScenarioEffectKind.StartQuest;
        }

        public bool Handle(ScenarioDefinition definition, ScenarioEffectDefinition effect, ScenarioRuntimeState state, out string message)
        {
            message = null;
            string questId = effect != null ? (effect.QuestId ?? effect.TargetId) : null;
            return _vanillaRuntime.TryStartQuest(questId, out message);
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
            if (condition == null)
            {
                reason = "Quest condition is missing.";
                return false;
            }

            List<QuestInstance> quests = _vanillaRuntime.GetCurrentQuests();
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
