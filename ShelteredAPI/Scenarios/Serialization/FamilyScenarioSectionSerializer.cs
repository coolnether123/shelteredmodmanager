using System.Xml;

using ModAPI.Scenarios;

using ShelteredAPI.Scenarios;

namespace ShelteredAPI.Scenarios.Serialization
{
    internal sealed class FamilyScenarioSectionSerializer
    {
        public FamilySetupDefinition Read(XmlElement element) { return ScenarioDefinitionSerializer.ReadFamilySetup(element); }
        public void Write(XmlWriter writer, FamilySetupDefinition value) { ScenarioDefinitionSerializer.WriteFamilySetup(writer, value); }
    }
}
