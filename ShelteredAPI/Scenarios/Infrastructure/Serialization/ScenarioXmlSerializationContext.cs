using System.Xml;

using ModAPI.Scenarios;

using ShelteredAPI.Scenarios;
namespace ShelteredAPI.Scenarios.Infrastructure.Serialization{
    internal sealed class ScenarioXmlSerializationContext
    {
        public ScenarioXmlSerializationContext(XmlElement root)
        {
            Root = root;
        }

        public XmlElement Root { get; private set; }
    }
}
