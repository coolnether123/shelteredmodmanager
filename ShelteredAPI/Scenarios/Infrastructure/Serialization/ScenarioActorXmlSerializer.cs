using System.Globalization;
using System.Xml;

using ShelteredAPI.Scenarios.Definitions;

namespace ShelteredAPI.Scenarios.Infrastructure.Serialization
{
    internal static class ScenarioActorXmlSerializer
    {
        public static ScenarioActorRef ReadActorRef(XmlElement parent)
        {
            XmlElement element = ScenarioXmlSerializerUtil.Child(parent, "Actor");
            if (element == null)
                return null;

            ScenarioActorRef actorRef = new ScenarioActorRef();
            actorRef.Kind = ScenarioXmlSerializerUtil.AttributeOrChild(element, "kind", "Kind");
            actorRef.LocalId = ScenarioXmlSerializerUtil.ReadIntAttribute(element, "localId", 0);
            actorRef.Domain = ScenarioXmlSerializerUtil.AttributeOrChild(element, "domain", "Domain");
            actorRef.BindingType = ScenarioXmlSerializerUtil.AttributeOrChild(element, "bindingType", "BindingType");
            actorRef.BindingKey = ScenarioXmlSerializerUtil.AttributeOrChild(element, "bindingKey", "BindingKey");
            actorRef.DisplayNameFallback = ScenarioXmlSerializerUtil.AttributeOrChild(element, "displayNameFallback", "DisplayNameFallback");
            actorRef.RequiredModId = ScenarioXmlSerializerUtil.AttributeOrChild(element, "requiredModId", "RequiredModId");
            return actorRef;
        }

        public static void WriteActorRef(XmlWriter writer, ScenarioActorRef actorRef)
        {
            if (writer == null || actorRef == null)
                return;

            writer.WriteStartElement("Actor");
            ScenarioXmlSerializerUtil.WriteAttribute(writer, "kind", actorRef.Kind);
            writer.WriteAttributeString("localId", actorRef.LocalId.ToString(CultureInfo.InvariantCulture));
            ScenarioXmlSerializerUtil.WriteAttribute(writer, "domain", actorRef.Domain);
            ScenarioXmlSerializerUtil.WriteAttribute(writer, "bindingType", actorRef.BindingType);
            ScenarioXmlSerializerUtil.WriteAttribute(writer, "bindingKey", actorRef.BindingKey);
            ScenarioXmlSerializerUtil.WriteAttribute(writer, "displayNameFallback", actorRef.DisplayNameFallback);
            ScenarioXmlSerializerUtil.WriteAttribute(writer, "requiredModId", actorRef.RequiredModId);
            writer.WriteEndElement();
        }

        public static void ReadActorComponents(XmlElement parent, System.Collections.Generic.List<ScenarioActorComponentDefinition> target)
        {
            if (parent == null || target == null)
                return;

            XmlElement componentsElement = ScenarioXmlSerializerUtil.Child(parent, "ActorComponents");
            if (componentsElement == null)
                return;

            XmlNodeList nodes = componentsElement.GetElementsByTagName("Component");
            for (int i = 0; i < nodes.Count; i++)
            {
                XmlElement componentElement = nodes[i] as XmlElement;
                if (componentElement == null)
                    continue;

                ScenarioActorComponentDefinition component = new ScenarioActorComponentDefinition();
                component.ComponentId = ScenarioXmlSerializerUtil.AttributeOrChild(componentElement, "id", "ComponentId");
                component.OwnerModId = ScenarioXmlSerializerUtil.AttributeOrChild(componentElement, "ownerModId", "OwnerModId");
                component.Version = ScenarioXmlSerializerUtil.ReadIntAttribute(componentElement, "version", 1);

                XmlElement payload = ScenarioXmlSerializerUtil.Child(componentElement, "PayloadJson");
                component.PayloadJson = payload != null ? payload.InnerText : ScenarioXmlSerializerUtil.AttributeOrChild(componentElement, "payloadJson", "PayloadJson");
                target.Add(component);
            }
        }

        public static void WriteActorComponents(XmlWriter writer, System.Collections.Generic.List<ScenarioActorComponentDefinition> components)
        {
            if (writer == null || components == null || components.Count == 0)
                return;

            writer.WriteStartElement("ActorComponents");
            for (int i = 0; i < components.Count; i++)
            {
                ScenarioActorComponentDefinition component = components[i];
                if (component == null)
                    continue;

                writer.WriteStartElement("Component");
                ScenarioXmlSerializerUtil.WriteAttribute(writer, "id", component.ComponentId);
                ScenarioXmlSerializerUtil.WriteAttribute(writer, "ownerModId", component.OwnerModId);
                writer.WriteAttributeString("version", component.Version.ToString(CultureInfo.InvariantCulture));
                ScenarioXmlSerializerUtil.WriteElement(writer, "PayloadJson", component.PayloadJson);
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
        }
    }
}
