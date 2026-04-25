using System.Xml;

namespace ModAPI.Scenarios.Serialization
{
    internal sealed class InventoryScenarioSectionSerializer
    {
        public StartingInventoryDefinition Read(XmlElement element) { return ScenarioDefinitionSerializer.ReadStartingInventory(element); }
        public void Write(XmlWriter writer, StartingInventoryDefinition value) { ScenarioDefinitionSerializer.WriteStartingInventory(writer, value); }
    }
}
