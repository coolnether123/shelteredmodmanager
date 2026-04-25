using System.Xml;

namespace ModAPI.Scenarios.Serialization
{
    internal sealed class QuestMapScenarioSectionSerializer
    {
        public QuestAuthoringDefinition ReadQuests(XmlElement element) { return ScenarioDefinitionSerializer.ReadQuests(element); }
        public MapAuthoringDefinition ReadMap(XmlElement element) { return ScenarioDefinitionSerializer.ReadMap(element); }
        public void WriteQuests(XmlWriter writer, QuestAuthoringDefinition value) { ScenarioDefinitionSerializer.WriteQuests(writer, value); }
        public void WriteMap(XmlWriter writer, MapAuthoringDefinition value) { ScenarioDefinitionSerializer.WriteMap(writer, value); }
    }
}
