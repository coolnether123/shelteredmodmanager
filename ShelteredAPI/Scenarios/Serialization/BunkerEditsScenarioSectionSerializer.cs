using System.Xml;

using ModAPI.Scenarios;

using ShelteredAPI.Scenarios;

namespace ShelteredAPI.Scenarios.Serialization
{
    internal sealed class BunkerEditsScenarioSectionSerializer
    {
        public BunkerEditsDefinition Read(XmlElement element) { return ScenarioDefinitionSerializer.ReadBunkerEdits(element); }
        public void Write(XmlWriter writer, BunkerEditsDefinition value) { ScenarioDefinitionSerializer.WriteBunkerEdits(writer, value); }
    }
}
