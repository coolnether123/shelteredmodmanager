using System.Xml;

namespace ModAPI.Scenarios.Serialization
{
    internal sealed class WinLossScenarioSectionSerializer
    {
        public WinLossConditionsDefinition Read(XmlElement element) { return ScenarioDefinitionSerializer.ReadWinLossConditions(element); }
        public void Write(XmlWriter writer, WinLossConditionsDefinition value) { ScenarioDefinitionSerializer.WriteWinLossConditions(writer, value); }
    }
}
