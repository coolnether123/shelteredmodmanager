using System.Xml;

namespace ModAPI.Scenarios.Serialization
{
    internal sealed class AssetReferenceScenarioSectionSerializer
    {
        public AssetReferencesDefinition Read(XmlElement element) { return ScenarioDefinitionSerializer.ReadAssetReferences(element); }
        public void Write(XmlWriter writer, AssetReferencesDefinition value) { ScenarioDefinitionSerializer.WriteAssetReferences(writer, value); }
    }
}
