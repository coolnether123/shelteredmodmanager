using System.Xml;

using ModAPI.Scenarios;

using ShelteredAPI.Scenarios;

using ShelteredAPI.Scenarios.Domain.Bunker;
namespace ShelteredAPI.Scenarios.Infrastructure.Serialization{
    internal sealed class BunkerGridScenarioSectionSerializer : IScenarioSectionSerializer<ScenarioBunkerGridDefinition>
    {
        public ScenarioBunkerGridDefinition Read(XmlElement element) { return ScenarioDefinitionSerializer.ReadBunkerGrid(element); }
        public void Write(XmlWriter writer, ScenarioBunkerGridDefinition value) { ScenarioDefinitionSerializer.WriteBunkerGrid(writer, value); }
    }
}
