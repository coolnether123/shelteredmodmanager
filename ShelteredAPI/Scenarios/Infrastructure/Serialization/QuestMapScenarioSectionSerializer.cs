using System.Xml;

using ModAPI.Scenarios;

using ShelteredAPI.Scenarios;

namespace ShelteredAPI.Scenarios.Serialization
{
    internal sealed class QuestMapScenarioSectionSerializer
    {
        private readonly ScenarioMapXmlSerializer _mapSerializer = new ScenarioMapXmlSerializer();

        public QuestAuthoringDefinition ReadQuests(XmlElement element) { return ScenarioDefinitionSerializer.ReadQuests(element); }
        public MapAuthoringDefinition ReadMap(XmlElement element) { return _mapSerializer.Read(element); }
        public void WriteQuests(XmlWriter writer, QuestAuthoringDefinition value) { ScenarioDefinitionSerializer.WriteQuests(writer, value); }
        public void WriteMap(XmlWriter writer, MapAuthoringDefinition value) { _mapSerializer.Write(writer, value); }
    }
}
