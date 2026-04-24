using System.Xml;

namespace ModAPI.Scenarios.Serialization
{
    internal sealed class TriggerEventScenarioSectionSerializer
    {
        public TriggersAndEventsDefinition Read(XmlElement element) { return ScenarioDefinitionSerializer.ReadTriggersAndEvents(element); }
        public void Write(XmlWriter writer, TriggersAndEventsDefinition value) { ScenarioDefinitionSerializer.WriteTriggersAndEvents(writer, value); }
    }
}
