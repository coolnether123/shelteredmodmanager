using System.Xml;

using ModAPI.Scenarios;

using ShelteredAPI.Scenarios;

using ShelteredAPI.Scenarios.Definitions;
namespace ShelteredAPI.Scenarios.Infrastructure.Serialization{
    internal sealed class BunkerEditsScenarioSectionSerializer : IScenarioSectionSerializer<BunkerEditsDefinition>
    {
        public BunkerEditsDefinition Read(XmlElement element) { return ScenarioDefinitionSerializer.ReadBunkerEdits(element); }
        public void Write(XmlWriter writer, BunkerEditsDefinition value) { ScenarioDefinitionSerializer.WriteBunkerEdits(writer, value); }
    }
}
