using System.Xml;

using ModAPI.Scenarios;

using ShelteredAPI.Scenarios;

using ShelteredAPI.Scenarios.Definitions;
namespace ShelteredAPI.Scenarios.Infrastructure.Serialization{
    internal sealed class AssetReferenceScenarioSectionSerializer : IScenarioSectionSerializer<AssetReferencesDefinition>
    {
        public AssetReferencesDefinition Read(XmlElement element) { return ScenarioDefinitionSerializer.ReadAssetReferences(element); }
        public void Write(XmlWriter writer, AssetReferencesDefinition value) { ScenarioDefinitionSerializer.WriteAssetReferences(writer, value); }
    }
}
