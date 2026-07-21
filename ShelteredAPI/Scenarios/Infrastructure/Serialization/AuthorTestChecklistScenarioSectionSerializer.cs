using System;
using System.Globalization;
using System.Xml;
using ShelteredAPI.Scenarios.Definitions;

namespace ShelteredAPI.Scenarios.Infrastructure.Serialization
{
    internal sealed class AuthorTestChecklistScenarioSectionSerializer : IScenarioSectionSerializer<ScenarioAuthorTestChecklist>
    {
        public ScenarioAuthorTestChecklist Read(XmlElement element)
        {
            ScenarioAuthorTestChecklist checklist = new ScenarioAuthorTestChecklist();
            if (element == null)
                return checklist;

            for (XmlNode node = element.FirstChild; node != null; node = node.NextSibling)
            {
                XmlElement itemElement = node as XmlElement;
                if (itemElement == null || itemElement.Name != "Item")
                    continue;
                string id = itemElement.GetAttribute("id");
                if (string.IsNullOrEmpty(id))
                    continue;

                ScenarioAuthorTestChecklistItem item = checklist.GetOrCreate(id);
                item.Checked = ReadBoolAttribute(itemElement, "checked");
                item.Note = ScenarioXmlSerializerUtil.ReadText(itemElement, "Note");
                item.Source = item.Checked
                    ? ReadSource(itemElement.GetAttribute("source"))
                    : ScenarioAuthorTestVerificationSource.None;
                item.CheckedUtc = ReadDate(itemElement.GetAttribute("checkedUtc"));
            }

            return checklist;
        }

        public void Write(XmlWriter writer, ScenarioAuthorTestChecklist checklist)
        {
            if (!HasAuthoredContent(checklist))
                return;

            writer.WriteStartElement("AuthorTestChecklist");
            for (int i = 0; checklist != null && checklist.Items != null && i < checklist.Items.Count; i++)
            {
                ScenarioAuthorTestChecklistItem item = checklist.Items[i];
                if (item == null || string.IsNullOrEmpty(item.Id) || (!item.Checked && string.IsNullOrEmpty(item.Note)))
                    continue;
                writer.WriteStartElement("Item");
                writer.WriteAttributeString("id", item.Id);
                writer.WriteAttributeString("checked", item.Checked ? "true" : "false");
                if (item.CheckedUtc.HasValue)
                    writer.WriteAttributeString("checkedUtc", item.CheckedUtc.Value.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture));
                if (item.Source != ScenarioAuthorTestVerificationSource.None)
                    writer.WriteAttributeString("source", item.Source.ToString().ToLowerInvariant());
                if (!string.IsNullOrEmpty(item.Note))
                    ScenarioXmlSerializerUtil.WriteElement(writer, "Note", item.Note);
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
        }

        private static bool HasAuthoredContent(ScenarioAuthorTestChecklist checklist)
        {
            for (int i = 0; checklist != null && checklist.Items != null && i < checklist.Items.Count; i++)
            {
                ScenarioAuthorTestChecklistItem item = checklist.Items[i];
                if (item != null && (item.Checked || !string.IsNullOrEmpty(item.Note)))
                    return true;
            }
            return false;
        }

        private static bool ReadBoolAttribute(XmlElement element, string name)
        {
            bool value;
            return element != null && bool.TryParse(element.GetAttribute(name), out value) && value;
        }

        private static DateTime? ReadDate(string value)
        {
            DateTime parsed;
            if (!string.IsNullOrEmpty(value) && DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out parsed))
                return parsed.ToUniversalTime();
            return null;
        }

        private static ScenarioAuthorTestVerificationSource ReadSource(string value)
        {
            try
            {
                return string.IsNullOrEmpty(value)
                    ? ScenarioAuthorTestVerificationSource.Manual
                    : (ScenarioAuthorTestVerificationSource)Enum.Parse(typeof(ScenarioAuthorTestVerificationSource), value, true);
            }
            catch
            {
                return ScenarioAuthorTestVerificationSource.Manual;
            }
        }
    }
}
