using System.Collections.Generic;
using System.Xml;

using ModAPI.Scenarios;

using ShelteredAPI.Scenarios;

namespace ShelteredAPI.Scenarios.Serialization
{
    internal sealed class ScheduledActionScenarioSectionSerializer
    {
        public void Read(XmlElement element, List<ScenarioScheduledActionDefinition> target) { ScenarioDefinitionSerializer.ReadScheduledActions(element, target); }
        public void Write(XmlWriter writer, List<ScenarioScheduledActionDefinition> actions) { ScenarioDefinitionSerializer.WriteScheduledActions(writer, actions); }
    }
}
