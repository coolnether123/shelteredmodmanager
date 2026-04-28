using System.Xml;

using ModAPI.Scenarios;

using ShelteredAPI.Scenarios;

namespace ShelteredAPI.Scenarios.Serialization
{
    internal sealed class TriggerEventScenarioSectionSerializer
    {
        public TriggersAndEventsDefinition Read(XmlElement element) { return ScenarioDefinitionSerializer.ReadTriggersAndEvents(element); }
        public void Write(XmlWriter writer, TriggersAndEventsDefinition value) { ScenarioDefinitionSerializer.WriteTriggersAndEvents(writer, value); }
    }
}
