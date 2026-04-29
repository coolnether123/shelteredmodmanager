using System.Collections.Generic;
using ModAPI.Core;

namespace ShelteredAPI.Scenarios
{
    internal sealed class ScenarioTriggerScheduledActionProvider : IScenarioScheduledActionProvider
    {
        public void AddActions(ScenarioDefinition definition, IList<ScenarioScheduledActionDefinition> target)
        {
            if (definition == null || definition.TriggersAndEvents == null || definition.TriggersAndEvents.Triggers == null || target == null)
                return;

            for (int i = 0; i < definition.TriggersAndEvents.Triggers.Count; i++)
            {
                TriggerDef trigger = definition.TriggersAndEvents.Triggers[i];
                ScenarioScheduledActionDefinition action;
                string reason;
                if (ScenarioTriggerDefinitionCompiler.TryCreateAction(trigger, i, out action, out reason))
                {
                    target.Add(action);
                    continue;
                }

                if (!ScenarioTriggerDefinitionCompiler.IsManual(trigger) && !string.IsNullOrEmpty(reason))
                    MMLog.WriteWarning("[ScenarioTriggerRuntime] " + reason);
            }
        }
    }
}
