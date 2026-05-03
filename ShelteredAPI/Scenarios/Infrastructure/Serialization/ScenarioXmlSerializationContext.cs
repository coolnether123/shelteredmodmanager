using System.Xml;

using ModAPI.Scenarios;

using ShelteredAPI.Scenarios;

namespace ShelteredAPI.Scenarios.Serialization
{
    internal sealed class ScenarioXmlSerializationContext
    {
        public ScenarioXmlSerializationContext(XmlElement root)
        {
            Root = root;
        }

        public XmlElement Root { get; private set; }
    }
}
