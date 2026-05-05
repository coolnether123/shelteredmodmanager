using System.Collections.Generic;
using System.Xml;

using ModAPI.Scenarios;

using ShelteredAPI.Scenarios;

using ShelteredAPI.Hooks;
using ShelteredAPI.Scenarios.Domain.Conditions;
namespace ShelteredAPI.Scenarios.Infrastructure.Serialization{
    internal sealed class GateConditionScenarioSectionSerializer : IScenarioSectionSerializer<List<ScenarioGateDefinition>>
    {
        public List<ScenarioGateDefinition> Read(XmlElement element)
        {
            List<ScenarioGateDefinition> gates = new List<ScenarioGateDefinition>();
            Read(element, gates);
            return gates;
        }

        public void Read(XmlElement element, List<ScenarioGateDefinition> target) { ScenarioDefinitionSerializer.ReadGates(element, target); }
        public void Write(XmlWriter writer, List<ScenarioGateDefinition> gates) { ScenarioDefinitionSerializer.WriteGates(writer, gates); }
    }
}
