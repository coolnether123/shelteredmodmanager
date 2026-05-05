using System.Collections.Generic;
using ModAPI.Scenarios;

using ShelteredAPI.Hooks;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Domain.Scheduling;
namespace ShelteredAPI.Scenarios.Application.Scheduling{
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
