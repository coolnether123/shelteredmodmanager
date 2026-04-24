using System.Xml;

namespace ModAPI.Scenarios.Serialization
{
    internal sealed class BunkerEditsScenarioSectionSerializer
    {
        public BunkerEditsDefinition Read(XmlElement element) { return ScenarioDefinitionSerializer.ReadBunkerEdits(element); }
        public void Write(XmlWriter writer, BunkerEditsDefinition value) { ScenarioDefinitionSerializer.WriteBunkerEdits(writer, value); }
    }
}
