using System.Collections.Generic;
using ModAPI.Scenarios;

namespace ShelteredAPI.Scenarios
{
    internal interface IScenarioScheduledActionProvider
    {
        void AddActions(ScenarioDefinition definition, IList<ScenarioScheduledActionDefinition> target);
    }
}
