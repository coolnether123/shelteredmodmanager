using System.Collections.Generic;
using ModAPI.Scenarios;

using ShelteredAPI.Hooks;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Domain.Scheduling;
namespace ShelteredAPI.Scenarios.Application.Scheduling{
    internal interface IScenarioScheduledActionProvider
    {
        void AddActions(ScenarioDefinition definition, IList<ScenarioScheduledActionDefinition> target);
    }
}
