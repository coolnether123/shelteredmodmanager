using System.Xml;

using ModAPI.Scenarios;

using ShelteredAPI.Scenarios;

using ShelteredAPI.Scenarios.Definitions;
namespace ShelteredAPI.Scenarios.Infrastructure.Serialization{
    internal sealed class InventoryScenarioSectionSerializer : IScenarioSectionSerializer<StartingInventoryDefinition>
    {
        public StartingInventoryDefinition Read(XmlElement element) { return ScenarioDefinitionSerializer.ReadStartingInventory(element); }
        public void Write(XmlWriter writer, StartingInventoryDefinition value) { ScenarioDefinitionSerializer.WriteStartingInventory(writer, value); }
    }
}
