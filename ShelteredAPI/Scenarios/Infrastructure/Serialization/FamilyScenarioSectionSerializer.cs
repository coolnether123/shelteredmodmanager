using System.Globalization;
using System.Xml;

using ModAPI.Scenarios;

using ShelteredAPI.Scenarios;

using ShelteredAPI.Scenarios.Definitions;
namespace ShelteredAPI.Scenarios.Infrastructure.Serialization{
    internal sealed class FamilyScenarioSectionSerializer : IScenarioSectionSerializer<FamilySetupDefinition>
    {
        public FamilySetupDefinition Read(XmlElement element)
        {
            FamilySetupDefinition setup = new FamilySetupDefinition();
            if (element == null)
                return setup;

            setup.OverrideVanillaFamily = ScenarioXmlSerializerUtil.ReadBool(element, "OverrideVanillaFamily", false);
            XmlElement members = ScenarioXmlSerializerUtil.Child(element, "Members");
            if (members != null)
            {
                XmlNodeList memberNodes = members.GetElementsByTagName("Member");
                for (int i = 0; i < memberNodes.Count; i++)
                {
                    XmlElement memberElement = memberNodes[i] as XmlElement;
                    if (memberElement != null)
                        setup.Members.Add(ReadFamilyMember(memberElement));
                }
            }

            XmlElement future = ScenarioXmlSerializerUtil.Child(element, "FutureSurvivors");
            if (future != null)
            {
                XmlNodeList futureNodes = future.GetElementsByTagName("FutureSurvivor");
                for (int i = 0; i < futureNodes.Count; i++)
                {
                    XmlElement futureElement = futureNodes[i] as XmlElement;
                    if (futureElement == null)
                        continue;

                    FutureSurvivorDefinition survivor = new FutureSurvivorDefinition();
                    survivor.Id = ScenarioXmlSerializerUtil.AttributeOrChild(futureElement, "id", "Id");
                    survivor.AskToJoin = ScenarioXmlSerializerUtil.ReadBoolAttribute(futureElement, "askToJoin", true);
                    survivor.Arrival = ScenarioXmlSerializerUtil.ReadScheduleTime(ScenarioXmlSerializerUtil.Child(futureElement, "Arrival"));
                    XmlElement survivorElement = ScenarioXmlSerializerUtil.Child(futureElement, "Survivor");
                    if (survivorElement != null)
                    {
                        XmlElement nestedMember = ScenarioXmlSerializerUtil.Child(survivorElement, "Member");
                        survivor.Survivor = ReadFamilyMember(nestedMember ?? survivorElement);
                    }
                    setup.FutureSurvivors.Add(survivor);
                }
            }

            return setup;
        }

        public void Write(XmlWriter writer, FamilySetupDefinition value)
        {
            if (value == null)
                value = new FamilySetupDefinition();

            writer.WriteStartElement("FamilySetup");
            ScenarioXmlSerializerUtil.WriteElement(writer, "OverrideVanillaFamily", value.OverrideVanillaFamily.ToString());
            writer.WriteStartElement("Members");
            for (int i = 0; i < value.Members.Count; i++)
            {
                WriteFamilyMember(writer, "Member", value.Members[i]);
            }
            writer.WriteEndElement();

            writer.WriteStartElement("FutureSurvivors");
            for (int i = 0; i < value.FutureSurvivors.Count; i++)
            {
                FutureSurvivorDefinition survivor = value.FutureSurvivors[i];
                if (survivor == null)
                    continue;

                writer.WriteStartElement("FutureSurvivor");
                ScenarioXmlSerializerUtil.WriteAttribute(writer, "id", survivor.Id);
                writer.WriteAttributeString("askToJoin", survivor.AskToJoin.ToString());
                ScenarioXmlSerializerUtil.WriteScheduleTime(writer, "Arrival", survivor.Arrival);
                WriteFamilyMember(writer, "Survivor", survivor.Survivor);
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
            writer.WriteEndElement();
        }

        private static FamilyMemberConfig ReadFamilyMember(XmlElement memberElement)
        {
            FamilyMemberConfig member = new FamilyMemberConfig();
            if (memberElement == null)
                return member;

            member.Name = ScenarioXmlSerializerUtil.ReadText(memberElement, "Name");
            member.Gender = ScenarioXmlSerializerUtil.ReadEnum(memberElement, "Gender", ScenarioGender.Any);
            XmlElement age = ScenarioXmlSerializerUtil.Child(memberElement, "Age");
            if (age != null)
            {
                member.ExactAge = ScenarioXmlSerializerUtil.ReadNullableInt(age, "Exact");
                member.MinAge = ScenarioXmlSerializerUtil.ReadNullableInt(age, "Min");
                member.MaxAge = ScenarioXmlSerializerUtil.ReadNullableInt(age, "Max");
            }

            XmlElement stats = ScenarioXmlSerializerUtil.Child(memberElement, "Stats");
            if (stats != null)
            {
                XmlNodeList statNodes = stats.GetElementsByTagName("Stat");
                for (int j = 0; j < statNodes.Count; j++)
                {
                    XmlElement statElement = statNodes[j] as XmlElement;
                    if (statElement != null)
                    {
                        member.Stats.Add(new StatOverride
                        {
                            StatId = ScenarioXmlSerializerUtil.AttributeOrChild(statElement, "id", "Id"),
                            Value = ScenarioXmlSerializerUtil.ReadIntAttribute(statElement, "value", 0)
                        });
                    }
                }
            }

            XmlElement traits = ScenarioXmlSerializerUtil.Child(memberElement, "Traits");
            if (traits != null)
                ScenarioXmlSerializerUtil.ReadStringList(traits, "Trait", member.Traits);

            XmlElement skills = ScenarioXmlSerializerUtil.Child(memberElement, "Skills");
            if (skills != null)
            {
                XmlNodeList skillNodes = skills.GetElementsByTagName("Skill");
                for (int j = 0; j < skillNodes.Count; j++)
                {
                    XmlElement skillElement = skillNodes[j] as XmlElement;
                    if (skillElement != null)
                    {
                        member.Skills.Add(new SkillOverride
                        {
                            SkillId = ScenarioXmlSerializerUtil.AttributeOrChild(skillElement, "id", "Id"),
                            Level = ScenarioXmlSerializerUtil.ReadIntAttribute(skillElement, "level", 0)
                        });
                    }
                }
            }

            member.Appearance = ReadFamilyAppearance(ScenarioXmlSerializerUtil.Child(memberElement, "Appearance"));
            return member;
        }

        private static FamilyMemberAppearanceConfig ReadFamilyAppearance(XmlElement element)
        {
            FamilyMemberAppearanceConfig appearance = new FamilyMemberAppearanceConfig();
            if (element == null)
                return appearance;

            appearance.MeshId = ScenarioXmlSerializerUtil.AttributeOrChild(element, "meshId", "MeshId");
            if (element.HasAttribute("adult"))
                appearance.IsAdult = ScenarioXmlSerializerUtil.ReadBoolAttribute(element, "adult", true);
            else
            {
                string adult = ScenarioXmlSerializerUtil.AttributeOrChild(element, "isAdult", "IsAdult");
                bool parsed;
                if (!string.IsNullOrEmpty(adult) && bool.TryParse(adult, out parsed))
                    appearance.IsAdult = parsed;
            }

            appearance.HairColorHex = ScenarioXmlSerializerUtil.AttributeOrChild(element, "hairColor", "HairColor");
            appearance.SkinColorHex = ScenarioXmlSerializerUtil.AttributeOrChild(element, "skinColor", "SkinColor");
            appearance.ShirtColorHex = ScenarioXmlSerializerUtil.AttributeOrChild(element, "shirtColor", "ShirtColor");
            appearance.PantsColorHex = ScenarioXmlSerializerUtil.AttributeOrChild(element, "pantsColor", "PantsColor");

            string textureId;
            string texturePath;
            ReadFamilyAppearancePart(ScenarioXmlSerializerUtil.Child(element, "Head"), out textureId, out texturePath);
            appearance.HeadTextureId = textureId;
            appearance.HeadTexturePath = texturePath;
            ReadFamilyAppearancePart(ScenarioXmlSerializerUtil.Child(element, "Torso"), out textureId, out texturePath);
            appearance.TorsoTextureId = textureId;
            appearance.TorsoTexturePath = texturePath;
            ReadFamilyAppearancePart(ScenarioXmlSerializerUtil.Child(element, "Legs"), out textureId, out texturePath);
            appearance.LegTextureId = textureId;
            appearance.LegTexturePath = texturePath;
            return appearance;
        }

        private static void ReadFamilyAppearancePart(XmlElement element, out string textureId, out string texturePath)
        {
            textureId = null;
            texturePath = null;
            if (element == null)
                return;

            textureId = ScenarioXmlSerializerUtil.AttributeOrChild(element, "id", "Id");
            texturePath = ScenarioXmlSerializerUtil.AttributeOrChild(element, "path", "Path");
        }

        private static void WriteFamilyMember(XmlWriter writer, string elementName, FamilyMemberConfig member)
        {
            if (member == null)
                member = new FamilyMemberConfig();

            writer.WriteStartElement(elementName);
            ScenarioXmlSerializerUtil.WriteElement(writer, "Name", member.Name);
            ScenarioXmlSerializerUtil.WriteElement(writer, "Gender", member.Gender.ToString());
            writer.WriteStartElement("Age");
            ScenarioXmlSerializerUtil.WriteNullableElement(writer, "Exact", member.ExactAge);
            ScenarioXmlSerializerUtil.WriteNullableElement(writer, "Min", member.MinAge);
            ScenarioXmlSerializerUtil.WriteNullableElement(writer, "Max", member.MaxAge);
            writer.WriteEndElement();

            writer.WriteStartElement("Stats");
            for (int j = 0; j < member.Stats.Count; j++)
            {
                writer.WriteStartElement("Stat");
                ScenarioXmlSerializerUtil.WriteAttribute(writer, "id", member.Stats[j].StatId);
                writer.WriteAttributeString("value", member.Stats[j].Value.ToString(CultureInfo.InvariantCulture));
                writer.WriteEndElement();
            }
            writer.WriteEndElement();

            writer.WriteStartElement("Traits");
            for (int j = 0; j < member.Traits.Count; j++)
                ScenarioXmlSerializerUtil.WriteElement(writer, "Trait", member.Traits[j]);
            writer.WriteEndElement();

            writer.WriteStartElement("Skills");
            for (int j = 0; j < member.Skills.Count; j++)
            {
                writer.WriteStartElement("Skill");
                ScenarioXmlSerializerUtil.WriteAttribute(writer, "id", member.Skills[j].SkillId);
                writer.WriteAttributeString("level", member.Skills[j].Level.ToString(CultureInfo.InvariantCulture));
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
            WriteFamilyAppearance(writer, member.Appearance);
            writer.WriteEndElement();
        }

        private static void WriteFamilyAppearance(XmlWriter writer, FamilyMemberAppearanceConfig appearance)
        {
            if (appearance == null)
                appearance = new FamilyMemberAppearanceConfig();

            writer.WriteStartElement("Appearance");
            ScenarioXmlSerializerUtil.WriteAttribute(writer, "meshId", appearance.MeshId);
            if (appearance.IsAdult.HasValue)
                writer.WriteAttributeString("adult", appearance.IsAdult.Value.ToString());
            ScenarioXmlSerializerUtil.WriteAttribute(writer, "hairColor", appearance.HairColorHex);
            ScenarioXmlSerializerUtil.WriteAttribute(writer, "skinColor", appearance.SkinColorHex);
            ScenarioXmlSerializerUtil.WriteAttribute(writer, "shirtColor", appearance.ShirtColorHex);
            ScenarioXmlSerializerUtil.WriteAttribute(writer, "pantsColor", appearance.PantsColorHex);
            WriteFamilyAppearancePart(writer, "Head", appearance.HeadTextureId, appearance.HeadTexturePath);
            WriteFamilyAppearancePart(writer, "Torso", appearance.TorsoTextureId, appearance.TorsoTexturePath);
            WriteFamilyAppearancePart(writer, "Legs", appearance.LegTextureId, appearance.LegTexturePath);
            writer.WriteEndElement();
        }

        private static void WriteFamilyAppearancePart(XmlWriter writer, string name, string textureId, string texturePath)
        {
            writer.WriteStartElement(name);
            ScenarioXmlSerializerUtil.WriteAttribute(writer, "id", textureId);
            ScenarioXmlSerializerUtil.WriteAttribute(writer, "path", texturePath);
            writer.WriteEndElement();
        }
    }
}
