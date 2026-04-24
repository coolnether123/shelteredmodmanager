using ModAPI.Scenarios;

namespace ShelteredAPI.Scenarios
{
    internal sealed class TriggerRuntimeAdapter
    {
        public void Apply(ScenarioDefinition definition, ScenarioApplyResult result)
        {
            if (definition == null || definition.TriggersAndEvents == null)
                return;

            if (definition.TriggersAndEvents.Triggers != null && definition.TriggersAndEvents.Triggers.Count > 0)
                result.AddMessage("Trigger runtime application remains deferred to the existing safe adapter boundary.");

            result.TriggerChanges += definition.TriggersAndEvents.Triggers != null ? definition.TriggersAndEvents.Triggers.Count : 0;
            result.ConditionChanges += definition.WinLossConditions != null
                ? (definition.WinLossConditions.WinConditions.Count + definition.WinLossConditions.LossConditions.Count)
                : 0;
        }
    }
}
