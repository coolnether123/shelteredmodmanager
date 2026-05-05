using System.Xml;

using ModAPI.Scenarios;

using ShelteredAPI.Scenarios;

using ShelteredAPI.Scenarios.Definitions;
namespace ShelteredAPI.Scenarios.Infrastructure.Serialization{
    internal sealed class WinLossScenarioSectionSerializer : IScenarioSectionSerializer<WinLossConditionsDefinition>
    {
        public WinLossConditionsDefinition Read(XmlElement element) { return ScenarioDefinitionSerializer.ReadWinLossConditions(element); }
        public void Write(XmlWriter writer, WinLossConditionsDefinition value) { ScenarioDefinitionSerializer.WriteWinLossConditions(writer, value); }
    }
}
