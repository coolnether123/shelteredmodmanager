using System.Collections.Generic;

using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Domain.Conditions;
using ShelteredAPI.Scenarios.Domain.Effects;
using ShelteredAPI.Scenarios.Domain.Scheduling;

namespace ShelteredAPI.Scenarios.Application.Scheduling
{
    internal sealed class ScenarioConversationScheduledActionProvider : IScenarioScheduledActionProvider
    {
        public void AddActions(ScenarioDefinition definition, IList<ScenarioScheduledActionDefinition> target)
        {
            ScenarioConversationAuthoringDefinition authoring = definition != null ? definition.Conversations : null;
            if (authoring == null || authoring.Conversations == null || target == null)
                return;

            for (int i = 0; i < authoring.Conversations.Count; i++)
            {
                ScenarioConversationDefinition conversation = authoring.Conversations[i];
                ScenarioConversationTriggerDefinition trigger = conversation != null ? conversation.Trigger : null;
                if (conversation == null || trigger == null)
                    continue;
                if (trigger.Source != ScenarioConversationTriggerSource.Event && trigger.Source != ScenarioConversationTriggerSource.Timeline)
                    continue;

                ScenarioScheduledActionDefinition action = new ScenarioScheduledActionDefinition();
                action.Id = "conversation." + (conversation.Id ?? i.ToString());
                action.ActionType = "StartConversation";
                action.DueTime = trigger.Source == ScenarioConversationTriggerSource.Timeline && trigger.Time != null
                    ? trigger.Time
                    : new ScenarioScheduleTime();
                action.Policy.Repeatable = false;
                action.Effects.Add(new ScenarioEffectDefinition
                {
                    Id = action.Id + ".start",
                    Kind = ScenarioEffectKind.StartConversation,
                    ConversationId = conversation.Id,
                    TargetId = conversation.Id
                });

                if (trigger.Source == ScenarioConversationTriggerSource.Event && !string.IsNullOrEmpty(trigger.TriggerId))
                {
                    action.ConditionRefs.Add(new ScenarioConditionRef
                    {
                        Id = "conversation_trigger_" + (conversation.Id ?? i.ToString()),
                        Kind = ScenarioConditionKind.CustomTrigger,
                        TargetId = trigger.TriggerId
                    });
                }

                target.Add(action);
            }
        }
    }
}
