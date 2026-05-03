using System.Xml;

using ModAPI.Scenarios;

using ShelteredAPI.Scenarios;

namespace ShelteredAPI.Scenarios.Serialization
{
    internal sealed class AssetReferenceScenarioSectionSerializer
    {
        public AssetReferencesDefinition Read(XmlElement element) { return ScenarioDefinitionSerializer.ReadAssetReferences(element); }
        public void Write(XmlWriter writer, AssetReferencesDefinition value) { ScenarioDefinitionSerializer.WriteAssetReferences(writer, value); }
    }
}
