using System.Xml;

namespace ModAPI.Scenarios.Serialization
{
    internal sealed class FamilyScenarioSectionSerializer
    {
        public FamilySetupDefinition Read(XmlElement element) { return ScenarioDefinitionSerializer.ReadFamilySetup(element); }
        public void Write(XmlWriter writer, FamilySetupDefinition value) { ScenarioDefinitionSerializer.WriteFamilySetup(writer, value); }
    }
}
