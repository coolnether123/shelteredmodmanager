using System.Collections.Generic;
using ModAPI.Scenarios;

namespace ShelteredAPI.Scenarios
{
    internal sealed class ScenarioDefinitionScheduledActionProvider : IScenarioScheduledActionProvider
    {
        public void AddActions(ScenarioDefinition definition, IList<ScenarioScheduledActionDefinition> target)
        {
            if (definition == null || target == null)
                return;

            for (int i = 0; definition.ScheduledActions != null && i < definition.ScheduledActions.Count; i++)
                target.Add(definition.ScheduledActions[i]);
        }
    }
}
