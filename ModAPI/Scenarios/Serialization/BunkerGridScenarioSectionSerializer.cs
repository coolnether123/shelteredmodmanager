using System.Xml;

namespace ModAPI.Scenarios.Serialization
{
    internal sealed class BunkerGridScenarioSectionSerializer
    {
        public ScenarioBunkerGridDefinition Read(XmlElement element) { return ScenarioDefinitionSerializer.ReadBunkerGrid(element); }
        public void Write(XmlWriter writer, ScenarioBunkerGridDefinition value) { ScenarioDefinitionSerializer.WriteBunkerGrid(writer, value); }
    }
}
