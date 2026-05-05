using System;
using System.Globalization;
using System.Xml;

using ModAPI.Scenarios;

using ShelteredAPI.Scenarios;

using ShelteredAPI.Hooks;
using ShelteredAPI.Scenarios.Domain.Scheduling;
namespace ShelteredAPI.Scenarios.Infrastructure.Serialization{
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

        public static string ReadText(XmlElement parent, string name)
        {
            XmlElement child = Child(parent, name);
            return child != null ? child.InnerText : null;
        }

        public static bool ReadBool(XmlElement parent, string name, bool fallback)
        {
            string raw = ReadText(parent, name);
            if (string.IsNullOrEmpty(raw))
                return fallback;

            bool parsed;
            return bool.TryParse(raw, out parsed) ? parsed : fallback;
        }

        public static int? ReadNullableInt(XmlElement parent, string name)
        {
            return ParseNullableInt(ReadText(parent, name));
        }

        public static int ReadIntAttribute(XmlElement element, string attributeName, int fallback)
        {
            int? parsed = ReadNullableIntAttribute(element, attributeName);
            return parsed.HasValue ? parsed.Value : fallback;
        }

        public static bool ReadBoolAttribute(XmlElement element, string attributeName, bool fallback)
        {
            if (element == null || !element.HasAttribute(attributeName))
                return fallback;

            bool parsed;
            return bool.TryParse(element.GetAttribute(attributeName), out parsed) ? parsed : fallback;
        }

        public static T ReadEnum<T>(XmlElement parent, string name, T fallback)
        {
            string raw = ReadText(parent, name);
            if (string.IsNullOrEmpty(raw))
                return fallback;

            try { return (T)Enum.Parse(typeof(T), raw, true); }
            catch { return fallback; }
        }

        public static string AttributeOrChild(XmlElement element, string attributeName, string childName)
        {
            if (element == null)
                return null;
            if (!string.IsNullOrEmpty(attributeName) && element.HasAttribute(attributeName))
                return element.GetAttribute(attributeName);
            return ReadText(element, childName);
        }

        public static void ReadStringList(XmlElement parent, string elementName, System.Collections.Generic.List<string> target)
        {
            if (parent == null || target == null)
                return;

            XmlNodeList nodes = parent.GetElementsByTagName(elementName);
            for (int i = 0; i < nodes.Count; i++)
            {
                XmlElement element = nodes[i] as XmlElement;
                if (element != null)
                    target.Add(element.InnerText);
            }
        }

        public static ScenarioScheduleTime ReadScheduleTime(XmlElement element)
        {
            ScenarioScheduleTime time = new ScenarioScheduleTime();
            if (element == null)
                return time;

            time.Day = ReadIntAttribute(element, "day", time.Day);
            time.Hour = ReadIntAttribute(element, "hour", time.Hour);
            time.Minute = ReadIntAttribute(element, "minute", time.Minute);
            return time;
        }

        public static void WriteElement(XmlWriter writer, string name, string value)
        {
            writer.WriteStartElement(name);
            writer.WriteString(value ?? string.Empty);
            writer.WriteEndElement();
        }

        public static void WriteNullableElement(XmlWriter writer, string name, int? value)
        {
            if (!value.HasValue)
                return;

            WriteElement(writer, name, value.Value.ToString(CultureInfo.InvariantCulture));
        }

        public static void WriteAttribute(XmlWriter writer, string name, string value)
        {
            if (!string.IsNullOrEmpty(value))
                writer.WriteAttributeString(name, value);
        }

        public static void WriteScheduleTime(XmlWriter writer, string name, ScenarioScheduleTime time)
        {
            if (time == null)
                time = new ScenarioScheduleTime();

            writer.WriteStartElement(name);
            writer.WriteAttributeString("day", time.Day.ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("hour", time.Hour.ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("minute", time.Minute.ToString(CultureInfo.InvariantCulture));
            writer.WriteEndElement();
        }

        private static int? ReadNullableIntAttribute(XmlElement element, string attributeName)
        {
            if (element == null || !element.HasAttribute(attributeName))
                return null;
            return ParseNullableInt(element.GetAttribute(attributeName));
        }

        private static int? ParseNullableInt(string raw)
        {
            int parsed;
            if (!string.IsNullOrEmpty(raw) && int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed))
                return parsed;
            return null;
        }
    }
}
