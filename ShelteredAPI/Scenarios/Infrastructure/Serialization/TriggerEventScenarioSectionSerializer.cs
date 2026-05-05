using System.Xml;

using ModAPI.Scenarios;

using ShelteredAPI.Scenarios;

using ShelteredAPI.Scenarios.Definitions;
namespace ShelteredAPI.Scenarios.Infrastructure.Serialization{
    internal sealed class TriggerEventScenarioSectionSerializer : IScenarioSectionSerializer<TriggersAndEventsDefinition>
    {
        public TriggersAndEventsDefinition Read(XmlElement element) { return ScenarioDefinitionSerializer.ReadTriggersAndEvents(element); }
        public void Write(XmlWriter writer, TriggersAndEventsDefinition value) { ScenarioDefinitionSerializer.WriteTriggersAndEvents(writer, value); }
    }
}
