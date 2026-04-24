using System.Xml;

namespace ModAPI.Scenarios.Serialization
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
