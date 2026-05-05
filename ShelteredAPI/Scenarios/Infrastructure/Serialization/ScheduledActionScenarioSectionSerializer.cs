using System.Collections.Generic;
using System.Xml;

using ModAPI.Scenarios;

using ShelteredAPI.Scenarios;

using ShelteredAPI.Hooks;
using ShelteredAPI.Scenarios.Domain.Scheduling;
namespace ShelteredAPI.Scenarios.Infrastructure.Serialization{
    internal sealed class ScheduledActionScenarioSectionSerializer : IScenarioSectionSerializer<List<ScenarioScheduledActionDefinition>>
    {
        public List<ScenarioScheduledActionDefinition> Read(XmlElement element)
        {
            List<ScenarioScheduledActionDefinition> actions = new List<ScenarioScheduledActionDefinition>();
            Read(element, actions);
            return actions;
        }

        public void Read(XmlElement element, List<ScenarioScheduledActionDefinition> target) { ScenarioDefinitionSerializer.ReadScheduledActions(element, target); }
        public void Write(XmlWriter writer, List<ScenarioScheduledActionDefinition> actions) { ScenarioDefinitionSerializer.WriteScheduledActions(writer, actions); }
    }
}
