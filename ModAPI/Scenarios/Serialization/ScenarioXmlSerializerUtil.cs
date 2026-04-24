using System.Xml;

namespace ModAPI.Scenarios.Serialization
{
    internal static class ScenarioXmlSerializerUtil
    {
        public static XmlElement Child(XmlElement parent, string name)
        {
            if (parent == null || string.IsNullOrEmpty(name))
                return null;
            for (XmlNode node = parent.FirstChild; node != null; node = node.NextSibling)
            {
                XmlElement element = node as XmlElement;
                if (element != null && element.Name == name)
                    return element;
            }
            return null;
        }
    }
}
