using System.Globalization;
using System.Xml;
using ShelteredAPI.Scenarios.Definitions;

namespace ShelteredAPI.Scenarios.Infrastructure.Serialization
{
    internal sealed class LaunchSetupScenarioSectionSerializer : IScenarioSectionSerializer<ScenarioLaunchSetupDefinition>
    {
        public ScenarioLaunchSetupDefinition Read(XmlElement element)
        {
            ScenarioLaunchSetupDefinition setup = ScenarioLaunchSetupDefinition.CreateDefault();
            if (element == null)
                return setup;

            setup.Mode = ScenarioXmlSerializerUtil.ReadEnum(element, "Mode", ScenarioLaunchSetupMode.FullSetup);
            XmlElement categories = ScenarioXmlSerializerUtil.Child(element, "Categories");
            if (categories == null)
                return setup;

            setup.Categories.Clear();
            XmlNodeList nodes = categories.GetElementsByTagName("Category");
            for (int i = 0; i < nodes.Count; i++)
            {
                XmlElement category = nodes[i] as XmlElement;
                if (category == null)
                    continue;
                setup.Categories.Add(new ScenarioDifficultyCategoryDefinition
                {
                    Id = ScenarioXmlSerializerUtil.AttributeOrChild(category, "id", "Id"),
                    AuthoredValue = ScenarioXmlSerializerUtil.ReadIntAttribute(category, "value", 0),
                    PlayerSelectable = ScenarioXmlSerializerUtil.ReadBoolAttribute(category, "playerSelectable", true)
                });
            }
            return setup;
        }

        public void Write(XmlWriter writer, ScenarioLaunchSetupDefinition value)
        {
            if (value == null)
                value = ScenarioLaunchSetupDefinition.CreateDefault();
            writer.WriteStartElement("LaunchSetup");
            ScenarioXmlSerializerUtil.WriteElement(writer, "Mode", value.Mode.ToString());
            writer.WriteStartElement("Categories");
            for (int i = 0; value.Categories != null && i < value.Categories.Count; i++)
            {
                ScenarioDifficultyCategoryDefinition category = value.Categories[i];
                if (category == null)
                    continue;
                writer.WriteStartElement("Category");
                ScenarioXmlSerializerUtil.WriteAttribute(writer, "id", category.Id);
                writer.WriteAttributeString("value", category.AuthoredValue.ToString(CultureInfo.InvariantCulture));
                writer.WriteAttributeString("playerSelectable", category.PlayerSelectable ? "true" : "false");
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
            writer.WriteEndElement();
        }
    }
}
