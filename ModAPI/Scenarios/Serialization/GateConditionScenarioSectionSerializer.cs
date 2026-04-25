using System.Collections.Generic;
using System.Xml;

namespace ModAPI.Scenarios.Serialization
{
    internal sealed class GateConditionScenarioSectionSerializer
    {
        public void Read(XmlElement element, List<ScenarioGateDefinition> target) { ScenarioDefinitionSerializer.ReadGates(element, target); }
        public void Write(XmlWriter writer, List<ScenarioGateDefinition> gates) { ScenarioDefinitionSerializer.WriteGates(writer, gates); }
    }
}
