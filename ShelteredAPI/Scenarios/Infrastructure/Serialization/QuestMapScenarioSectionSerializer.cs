using System.Xml;

using ModAPI.Scenarios;

using ShelteredAPI.Scenarios;

using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Domain.Map;
namespace ShelteredAPI.Scenarios.Infrastructure.Serialization{
    internal sealed class QuestMapScenarioSectionSerializer
    {
        private readonly ScenarioMapXmlSerializer _mapSerializer = new ScenarioMapXmlSerializer();

        public QuestAuthoringDefinition ReadQuests(XmlElement element) { return ScenarioDefinitionSerializer.ReadQuests(element); }
        public MapAuthoringDefinition ReadMap(XmlElement element) { return _mapSerializer.Read(element); }
        public void WriteQuests(XmlWriter writer, QuestAuthoringDefinition value) { ScenarioDefinitionSerializer.WriteQuests(writer, value); }
        public void WriteMap(XmlWriter writer, MapAuthoringDefinition value) { _mapSerializer.Write(writer, value); }
    }
}
