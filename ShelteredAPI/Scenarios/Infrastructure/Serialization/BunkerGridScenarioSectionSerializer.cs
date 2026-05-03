using System.Xml;

using ModAPI.Scenarios;

using ShelteredAPI.Scenarios;

namespace ShelteredAPI.Scenarios.Serialization
{
    internal sealed class BunkerGridScenarioSectionSerializer
    {
        public ScenarioBunkerGridDefinition Read(XmlElement element) { return ScenarioDefinitionSerializer.ReadBunkerGrid(element); }
        public void Write(XmlWriter writer, ScenarioBunkerGridDefinition value) { ScenarioDefinitionSerializer.WriteBunkerGrid(writer, value); }
    }
}
