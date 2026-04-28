using System.Collections.Generic;
using System.Xml;

using ModAPI.Scenarios;

using ShelteredAPI.Scenarios;

namespace ShelteredAPI.Scenarios.Serialization
{
    internal sealed class GateConditionScenarioSectionSerializer
    {
        public void Read(XmlElement element, List<ScenarioGateDefinition> target) { ScenarioDefinitionSerializer.ReadGates(element, target); }
        public void Write(XmlWriter writer, List<ScenarioGateDefinition> gates) { ScenarioDefinitionSerializer.WriteGates(writer, gates); }
    }
}
