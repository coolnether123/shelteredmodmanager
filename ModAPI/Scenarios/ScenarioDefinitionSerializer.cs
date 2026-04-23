using System;
using System.Globalization;
using System.IO;
using System.Xml;

namespace ModAPI.Scenarios
{
    /// <summary>
    /// XML serializer for persistent scenario definitions. It uses System.Xml only so it
    /// works under the .NET 3.5 runtime used by the Sheltered mod stack.
    /// </summary>
    public class ScenarioDefinitionSerializer
    {
        public const string DefaultFileName = "scenario.xml";

        public ScenarioDefinition Load(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentException("Scenario file path is required.", "filePath");

            XmlDocument document = new XmlDocument();
            document.Load(filePath);
            return ReadDocument(document);
        }

        public ScenarioDefinition FromXml(string xml)
        {
            XmlDocument document = new XmlDocument();
            document.LoadXml(xml);
            return ReadDocument(document);
        }

        public void Save(ScenarioDefinition definition, string filePath)
        {
            if (definition == null)
                throw new ArgumentNullException("definition");
            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentException("Scenario file path is required.", "filePath");

            string directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            XmlWriterSettings settings = new XmlWriterSettings();
            settings.Indent = true;
            settings.Encoding = System.Text.Encoding.UTF8;

            using (XmlWriter writer = XmlWriter.Create(filePath, settings))
            {
                WriteDocument(definition, writer);
            }
        }

        public string ToXml(ScenarioDefinition definition)
        {
            if (definition == null)
                throw new ArgumentNullException("definition");

            XmlWriterSettings settings = new XmlWriterSettings();
            settings.Indent = true;
            settings.OmitXmlDeclaration = true;

            using (StringWriter stringWriter = new StringWriter(CultureInfo.InvariantCulture))
            {
                using (XmlWriter writer = XmlWriter.Create(stringWriter, settings))
                {
                    WriteDocument(definition, writer);
                }

                return stringWriter.ToString();
            }
        }

        public ScenarioInfo LoadInfo(string filePath, string ownerModId)
        {
            ScenarioDefinition definition = Load(filePath);
            return new ScenarioInfo(
                definition.Id,
                definition.DisplayName,
                definition.Author,
                definition.Version,
                filePath,
                ownerModId);
        }

        private static ScenarioDefinition ReadDocument(XmlDocument document)
        {
            if (document == null || document.DocumentElement == null || document.DocumentElement.Name != "Scenario")
                throw new FormatException("Scenario XML must have a <Scenario> root element.");

            XmlElement root = document.DocumentElement;
            ScenarioDefinition definition = new ScenarioDefinition();

            XmlElement meta = Child(root, "Meta");
            definition.Id = ReadText(meta, "Id");
            definition.DisplayName = ReadText(meta, "DisplayName");
            definition.Description = ReadText(meta, "Description");
            definition.Author = ReadText(meta, "Author");
            definition.Version = ReadText(meta, "Version");

            XmlElement dependencies = Child(root, "Dependencies");
            if (dependencies != null)
                ReadDependencyList(dependencies, definition.Dependencies);

            definition.BaseGameMode = ReadEnum(root, "BaseMode", ScenarioBaseGameMode.Survival);
            definition.SeedOverride = ReadNullableLong(root, "SeedOverride");
            definition.FamilySetup = ReadFamilySetup(Child(root, "FamilySetup"));
            definition.StartingInventory = ReadStartingInventory(Child(root, "StartingInventory"));
            definition.BunkerEdits = ReadBunkerEdits(Child(root, "BunkerEdits"));
            definition.TriggersAndEvents = ReadTriggersAndEvents(Child(root, "TriggersAndEvents"));
            definition.WinLossConditions = ReadWinLossConditions(Child(root, "WinLossConditions"));
            definition.AssetReferences = ReadAssetReferences(Child(root, "AssetReferences"));
            return definition;
        }

        private static FamilySetupDefinition ReadFamilySetup(XmlElement element)
        {
            FamilySetupDefinition setup = new FamilySetupDefinition();
            if (element == null)
                return setup;

            setup.OverrideVanillaFamily = ReadBool(element, "OverrideVanillaFamily", false);
            XmlElement members = Child(element, "Members");
            if (members == null)
                return setup;

            XmlNodeList memberNodes = members.GetElementsByTagName("Member");
            for (int i = 0; i < memberNodes.Count; i++)
            {
                XmlElement memberElement = memberNodes[i] as XmlElement;
                if (memberElement == null)
                    continue;

                FamilyMemberConfig member = new FamilyMemberConfig();
                member.Name = ReadText(memberElement, "Name");
                member.Gender = ReadEnum(memberElement, "Gender", ScenarioGender.Any);
                XmlElement age = Child(memberElement, "Age");
                if (age != null)
                {
                    member.ExactAge = ReadNullableInt(age, "Exact");
                    member.MinAge = ReadNullableInt(age, "Min");
                    member.MaxAge = ReadNullableInt(age, "Max");
                }

                XmlElement stats = Child(memberElement, "Stats");
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
                                StatId = AttributeOrChild(statElement, "id", "Id"),
                                Value = ReadIntAttribute(statElement, "value", 0)
                            });
                        }
                    }
                }

                XmlElement traits = Child(memberElement, "Traits");
                if (traits != null)
                    ReadStringList(traits, "Trait", member.Traits);

                XmlElement skills = Child(memberElement, "Skills");
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
                                SkillId = AttributeOrChild(skillElement, "id", "Id"),
                                Level = ReadIntAttribute(skillElement, "level", 0)
                            });
                        }
                    }
                }

                member.Appearance = ReadFamilyAppearance(Child(memberElement, "Appearance"));

                setup.Members.Add(member);
            }

            return setup;
        }

        private static StartingInventoryDefinition ReadStartingInventory(XmlElement element)
        {
            StartingInventoryDefinition inventory = new StartingInventoryDefinition();
            if (element == null)
                return inventory;

            inventory.OverrideRandomStart = ReadBool(element, "OverrideRandomStart", false);
            XmlElement items = Child(element, "Items");
            if (items == null)
                return inventory;

            XmlNodeList itemNodes = items.GetElementsByTagName("Item");
            for (int i = 0; i < itemNodes.Count; i++)
            {
                XmlElement itemElement = itemNodes[i] as XmlElement;
                if (itemElement != null)
                {
                    inventory.Items.Add(new ItemEntry
                    {
                        ItemId = AttributeOrChild(itemElement, "id", "Id"),
                        Quantity = ReadIntAttribute(itemElement, "quantity", 0)
                    });
                }
            }

            return inventory;
        }

        private static FamilyMemberAppearanceConfig ReadFamilyAppearance(XmlElement element)
        {
            FamilyMemberAppearanceConfig appearance = new FamilyMemberAppearanceConfig();
            if (element == null)
                return appearance;

            string textureId;
            string texturePath;

            ReadFamilyAppearancePart(Child(element, "Head"), out textureId, out texturePath);
            appearance.HeadTextureId = textureId;
            appearance.HeadTexturePath = texturePath;

            ReadFamilyAppearancePart(Child(element, "Torso"), out textureId, out texturePath);
            appearance.TorsoTextureId = textureId;
            appearance.TorsoTexturePath = texturePath;

            ReadFamilyAppearancePart(Child(element, "Legs"), out textureId, out texturePath);
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

            textureId = AttributeOrChild(element, "id", "Id");
            texturePath = AttributeOrChild(element, "path", "Path");
        }

        private static BunkerEditsDefinition ReadBunkerEdits(XmlElement element)
        {
            BunkerEditsDefinition bunker = new BunkerEditsDefinition();
            if (element == null)
                return bunker;

            XmlElement rooms = Child(element, "RoomChanges");
            if (rooms != null)
            {
                XmlNodeList roomNodes = rooms.GetElementsByTagName("RoomEdit");
                for (int i = 0; i < roomNodes.Count; i++)
                {
                    XmlElement roomElement = roomNodes[i] as XmlElement;
                    if (roomElement != null)
                    {
                        bunker.RoomChanges.Add(new RoomEdit
                        {
                            GridX = ReadIntAttribute(roomElement, "gridX", 0),
                            GridY = ReadIntAttribute(roomElement, "gridY", 0),
                            WallSpriteIndex = ReadNullableIntAttribute(roomElement, "wallSpriteIndex"),
                            WireSpriteIndex = ReadNullableIntAttribute(roomElement, "wireSpriteIndex")
                        });
                    }
                }
            }

            XmlElement placements = Child(element, "ObjectPlacements");
            if (placements != null)
            {
                XmlNodeList placementNodes = placements.GetElementsByTagName("ObjectPlacement");
                for (int i = 0; i < placementNodes.Count; i++)
                {
                    XmlElement placementElement = placementNodes[i] as XmlElement;
                    if (placementElement == null)
                        continue;

                    ObjectPlacement placement = new ObjectPlacement();
                    placement.PrefabReference = AttributeOrChild(placementElement, "prefab", "PrefabReference");
                    placement.DefinitionReference = AttributeOrChild(placementElement, "definition", "DefinitionReference");
                    placement.Position = ReadVector(Child(placementElement, "Position"));
                    placement.Rotation = ReadVector(Child(placementElement, "Rotation"));
                    ReadProperties(Child(placementElement, "CustomProperties"), placement.CustomProperties);
                    bunker.ObjectPlacements.Add(placement);
                }
            }

            return bunker;
        }

        private static TriggersAndEventsDefinition ReadTriggersAndEvents(XmlElement element)
        {
            TriggersAndEventsDefinition result = new TriggersAndEventsDefinition();
            if (element == null)
                return result;

            XmlElement triggers = Child(element, "Triggers");
            if (triggers != null)
            {
                XmlNodeList triggerNodes = triggers.GetElementsByTagName("Trigger");
                for (int i = 0; i < triggerNodes.Count; i++)
                {
                    XmlElement triggerElement = triggerNodes[i] as XmlElement;
                    if (triggerElement == null)
                        continue;

                    TriggerDef trigger = new TriggerDef();
                    trigger.Id = AttributeOrChild(triggerElement, "id", "Id");
                    trigger.Type = AttributeOrChild(triggerElement, "type", "Type");
                    ReadProperties(Child(triggerElement, "Properties"), trigger.Properties);
                    result.Triggers.Add(trigger);
                }
            }

            XmlElement chains = Child(element, "DialogueChains");
            if (chains != null)
            {
                XmlNodeList chainNodes = chains.GetElementsByTagName("DialogueChain");
                for (int i = 0; i < chainNodes.Count; i++)
                {
                    XmlElement chainElement = chainNodes[i] as XmlElement;
                    if (chainElement == null)
                        continue;

                    DialogueChain chain = new DialogueChain();
                    chain.Id = AttributeOrChild(chainElement, "id", "Id");
                    ReadStringList(chainElement, "Line", chain.Lines);
                    result.DialogueChains.Add(chain);
                }
            }

            return result;
        }

        private static WinLossConditionsDefinition ReadWinLossConditions(XmlElement element)
        {
            WinLossConditionsDefinition result = new WinLossConditionsDefinition();
            if (element == null)
                return result;

            ReadConditions(Child(element, "WinConditions"), "Condition", result.WinConditions);
            ReadConditions(Child(element, "LossConditions"), "Condition", result.LossConditions);
            return result;
        }

        private static AssetReferencesDefinition ReadAssetReferences(XmlElement element)
        {
            AssetReferencesDefinition result = new AssetReferencesDefinition();
            if (element == null)
                return result;

            XmlElement sprites = Child(element, "CustomSprites");
            if (sprites != null)
            {
                XmlNodeList spriteNodes = sprites.GetElementsByTagName("Sprite");
                for (int i = 0; i < spriteNodes.Count; i++)
                {
                    XmlElement spriteElement = spriteNodes[i] as XmlElement;
                    if (spriteElement != null)
                    {
                        result.CustomSprites.Add(new SpriteRef
                        {
                            Id = AttributeOrChild(spriteElement, "id", "Id"),
                            RelativePath = AttributeOrChild(spriteElement, "path", "Path")
                        });
                    }
                }
            }

            XmlElement icons = Child(element, "CustomIcons");
            if (icons != null)
            {
                XmlNodeList iconNodes = icons.GetElementsByTagName("Icon");
                for (int i = 0; i < iconNodes.Count; i++)
                {
                    XmlElement iconElement = iconNodes[i] as XmlElement;
                    if (iconElement != null)
                    {
                        result.CustomIcons.Add(new IconRef
                        {
                            Id = AttributeOrChild(iconElement, "id", "Id"),
                            RelativePath = AttributeOrChild(iconElement, "path", "Path")
                        });
                    }
                }
            }

            XmlElement spriteSwaps = Child(element, "SpriteSwaps");
            if (spriteSwaps != null)
            {
                XmlNodeList swapNodes = spriteSwaps.GetElementsByTagName("Swap");
                for (int i = 0; i < swapNodes.Count; i++)
                {
                    XmlElement swapElement = swapNodes[i] as XmlElement;
                    if (swapElement == null)
                        continue;

                    result.SpriteSwaps.Add(new SpriteSwapRule
                    {
                        Id = AttributeOrChild(swapElement, "id", "Id"),
                        TargetPath = AttributeOrChild(swapElement, "targetPath", "TargetPath"),
                        SpriteId = AttributeOrChild(swapElement, "spriteId", "SpriteId"),
                        RelativePath = AttributeOrChild(swapElement, "path", "Path"),
                        RuntimeSpriteKey = AttributeOrChild(swapElement, "runtimeSpriteKey", "RuntimeSpriteKey"),
                        Day = ReadNullableIntAttribute(swapElement, "day"),
                        TargetComponent = ReadEnumAttribute(swapElement, "targetComponent", ScenarioSpriteTargetComponentKind.Auto)
                    });
                }
            }

            XmlElement scenePlacements = Child(element, "SceneSpritePlacements");
            if (scenePlacements != null)
            {
                XmlNodeList placementNodes = scenePlacements.GetElementsByTagName("Placement");
                for (int i = 0; i < placementNodes.Count; i++)
                {
                    XmlElement placementElement = placementNodes[i] as XmlElement;
                    if (placementElement == null)
                        continue;

                    result.SceneSpritePlacements.Add(new SceneSpritePlacement
                    {
                        Id = AttributeOrChild(placementElement, "id", "Id"),
                        SpriteId = AttributeOrChild(placementElement, "spriteId", "SpriteId"),
                        RelativePath = AttributeOrChild(placementElement, "path", "Path"),
                        RuntimeSpriteKey = AttributeOrChild(placementElement, "runtimeSpriteKey", "RuntimeSpriteKey"),
                        Position = ReadVector(Child(placementElement, "Position")),
                        SnapToGrid = ReadBoolAttribute(placementElement, "snapToGrid", false),
                        GridX = ReadNullableIntAttribute(placementElement, "gridX"),
                        GridY = ReadNullableIntAttribute(placementElement, "gridY"),
                        SortingLayerName = AttributeOrChild(placementElement, "sortingLayer", "SortingLayer"),
                        SortingOrder = ReadIntAttribute(placementElement, "sortingOrder", 0)
                    });
                }
            }

            return result;
        }

        private static void WriteDocument(ScenarioDefinition definition, XmlWriter writer)
        {
            writer.WriteStartDocument();
            writer.WriteStartElement("Scenario");

            writer.WriteStartElement("Meta");
            WriteElement(writer, "Id", definition.Id);
            WriteElement(writer, "DisplayName", definition.DisplayName);
            WriteElement(writer, "Description", definition.Description);
            WriteElement(writer, "Author", definition.Author);
            WriteElement(writer, "Version", definition.Version);
            writer.WriteEndElement();

            writer.WriteStartElement("Dependencies");
            if (definition.Dependencies != null)
            {
                for (int i = 0; i < definition.Dependencies.Count; i++)
                    WriteElement(writer, "Requires", definition.Dependencies[i]);
            }
            writer.WriteEndElement();

            WriteElement(writer, "BaseMode", definition.BaseGameMode.ToString());
            if (definition.SeedOverride.HasValue)
                WriteElement(writer, "SeedOverride", definition.SeedOverride.Value.ToString(CultureInfo.InvariantCulture));

            WriteFamilySetup(writer, definition.FamilySetup);
            WriteStartingInventory(writer, definition.StartingInventory);
            WriteBunkerEdits(writer, definition.BunkerEdits);
            WriteTriggersAndEvents(writer, definition.TriggersAndEvents);
            WriteWinLossConditions(writer, definition.WinLossConditions);
            WriteAssetReferences(writer, definition.AssetReferences);

            writer.WriteEndElement();
            writer.WriteEndDocument();
        }

        private static void WriteFamilySetup(XmlWriter writer, FamilySetupDefinition setup)
        {
            if (setup == null)
                setup = new FamilySetupDefinition();

            writer.WriteStartElement("FamilySetup");
            WriteElement(writer, "OverrideVanillaFamily", setup.OverrideVanillaFamily.ToString());
            writer.WriteStartElement("Members");
            for (int i = 0; i < setup.Members.Count; i++)
            {
                FamilyMemberConfig member = setup.Members[i];
                writer.WriteStartElement("Member");
                WriteElement(writer, "Name", member.Name);
                WriteElement(writer, "Gender", member.Gender.ToString());
                writer.WriteStartElement("Age");
                WriteNullableElement(writer, "Exact", member.ExactAge);
                WriteNullableElement(writer, "Min", member.MinAge);
                WriteNullableElement(writer, "Max", member.MaxAge);
                writer.WriteEndElement();

                writer.WriteStartElement("Stats");
                for (int j = 0; j < member.Stats.Count; j++)
                {
                    writer.WriteStartElement("Stat");
                    WriteAttribute(writer, "id", member.Stats[j].StatId);
                    writer.WriteAttributeString("value", member.Stats[j].Value.ToString(CultureInfo.InvariantCulture));
                    writer.WriteEndElement();
                }
                writer.WriteEndElement();

                writer.WriteStartElement("Traits");
                for (int j = 0; j < member.Traits.Count; j++)
                    WriteElement(writer, "Trait", member.Traits[j]);
                writer.WriteEndElement();

                writer.WriteStartElement("Skills");
                for (int j = 0; j < member.Skills.Count; j++)
                {
                    writer.WriteStartElement("Skill");
                    WriteAttribute(writer, "id", member.Skills[j].SkillId);
                    writer.WriteAttributeString("level", member.Skills[j].Level.ToString(CultureInfo.InvariantCulture));
                    writer.WriteEndElement();
                }
                writer.WriteEndElement();
                WriteFamilyAppearance(writer, member.Appearance);
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
            writer.WriteEndElement();
        }

        private static void WriteStartingInventory(XmlWriter writer, StartingInventoryDefinition inventory)
        {
            if (inventory == null)
                inventory = new StartingInventoryDefinition();

            writer.WriteStartElement("StartingInventory");
            WriteElement(writer, "OverrideRandomStart", inventory.OverrideRandomStart.ToString());
            writer.WriteStartElement("Items");
            for (int i = 0; i < inventory.Items.Count; i++)
            {
                writer.WriteStartElement("Item");
                WriteAttribute(writer, "id", inventory.Items[i].ItemId);
                writer.WriteAttributeString("quantity", inventory.Items[i].Quantity.ToString(CultureInfo.InvariantCulture));
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
            writer.WriteEndElement();
        }

        private static void WriteFamilyAppearance(XmlWriter writer, FamilyMemberAppearanceConfig appearance)
        {
            if (appearance == null)
                appearance = new FamilyMemberAppearanceConfig();

            writer.WriteStartElement("Appearance");
            WriteFamilyAppearancePart(writer, "Head", appearance.HeadTextureId, appearance.HeadTexturePath);
            WriteFamilyAppearancePart(writer, "Torso", appearance.TorsoTextureId, appearance.TorsoTexturePath);
            WriteFamilyAppearancePart(writer, "Legs", appearance.LegTextureId, appearance.LegTexturePath);
            writer.WriteEndElement();
        }

        private static void WriteFamilyAppearancePart(XmlWriter writer, string name, string textureId, string texturePath)
        {
            writer.WriteStartElement(name);
            WriteAttribute(writer, "id", textureId);
            WriteAttribute(writer, "path", texturePath);
            writer.WriteEndElement();
        }

        private static void WriteBunkerEdits(XmlWriter writer, BunkerEditsDefinition bunker)
        {
            if (bunker == null)
                bunker = new BunkerEditsDefinition();

            writer.WriteStartElement("BunkerEdits");
            writer.WriteStartElement("RoomChanges");
            for (int i = 0; i < bunker.RoomChanges.Count; i++)
            {
                RoomEdit room = bunker.RoomChanges[i];
                writer.WriteStartElement("RoomEdit");
                writer.WriteAttributeString("gridX", room.GridX.ToString(CultureInfo.InvariantCulture));
                writer.WriteAttributeString("gridY", room.GridY.ToString(CultureInfo.InvariantCulture));
                if (room.WallSpriteIndex.HasValue)
                    writer.WriteAttributeString("wallSpriteIndex", room.WallSpriteIndex.Value.ToString(CultureInfo.InvariantCulture));
                if (room.WireSpriteIndex.HasValue)
                    writer.WriteAttributeString("wireSpriteIndex", room.WireSpriteIndex.Value.ToString(CultureInfo.InvariantCulture));
                writer.WriteEndElement();
            }
            writer.WriteEndElement();

            writer.WriteStartElement("ObjectPlacements");
            for (int i = 0; i < bunker.ObjectPlacements.Count; i++)
            {
                ObjectPlacement placement = bunker.ObjectPlacements[i];
                writer.WriteStartElement("ObjectPlacement");
                WriteAttribute(writer, "prefab", placement.PrefabReference);
                WriteAttribute(writer, "definition", placement.DefinitionReference);
                WriteVector(writer, "Position", placement.Position);
                WriteVector(writer, "Rotation", placement.Rotation);
                WriteProperties(writer, "CustomProperties", placement.CustomProperties);
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
            writer.WriteEndElement();
        }

        private static void WriteTriggersAndEvents(XmlWriter writer, TriggersAndEventsDefinition value)
        {
            if (value == null)
                value = new TriggersAndEventsDefinition();

            writer.WriteStartElement("TriggersAndEvents");
            writer.WriteStartElement("Triggers");
            for (int i = 0; i < value.Triggers.Count; i++)
            {
                TriggerDef trigger = value.Triggers[i];
                writer.WriteStartElement("Trigger");
                WriteAttribute(writer, "id", trigger.Id);
                WriteAttribute(writer, "type", trigger.Type);
                WriteProperties(writer, "Properties", trigger.Properties);
                writer.WriteEndElement();
            }
            writer.WriteEndElement();

            writer.WriteStartElement("DialogueChains");
            for (int i = 0; i < value.DialogueChains.Count; i++)
            {
                DialogueChain chain = value.DialogueChains[i];
                writer.WriteStartElement("DialogueChain");
                WriteAttribute(writer, "id", chain.Id);
                for (int j = 0; j < chain.Lines.Count; j++)
                    WriteElement(writer, "Line", chain.Lines[j]);
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
            writer.WriteEndElement();
        }

        private static void WriteWinLossConditions(XmlWriter writer, WinLossConditionsDefinition value)
        {
            if (value == null)
                value = new WinLossConditionsDefinition();

            writer.WriteStartElement("WinLossConditions");
            WriteConditions(writer, "WinConditions", value.WinConditions);
            WriteConditions(writer, "LossConditions", value.LossConditions);
            writer.WriteEndElement();
        }

        private static void WriteAssetReferences(XmlWriter writer, AssetReferencesDefinition value)
        {
            if (value == null)
                value = new AssetReferencesDefinition();

            writer.WriteStartElement("AssetReferences");
            writer.WriteStartElement("CustomSprites");
            for (int i = 0; i < value.CustomSprites.Count; i++)
            {
                writer.WriteStartElement("Sprite");
                WriteAttribute(writer, "id", value.CustomSprites[i].Id);
                WriteAttribute(writer, "path", value.CustomSprites[i].RelativePath);
                writer.WriteEndElement();
            }
            writer.WriteEndElement();

            writer.WriteStartElement("CustomIcons");
            for (int i = 0; i < value.CustomIcons.Count; i++)
            {
                writer.WriteStartElement("Icon");
                WriteAttribute(writer, "id", value.CustomIcons[i].Id);
                WriteAttribute(writer, "path", value.CustomIcons[i].RelativePath);
                writer.WriteEndElement();
            }
            writer.WriteEndElement();

            writer.WriteStartElement("SpriteSwaps");
            for (int i = 0; i < value.SpriteSwaps.Count; i++)
            {
                SpriteSwapRule swap = value.SpriteSwaps[i];
                writer.WriteStartElement("Swap");
                WriteAttribute(writer, "id", swap.Id);
                WriteAttribute(writer, "targetPath", swap.TargetPath);
                WriteAttribute(writer, "spriteId", swap.SpriteId);
                WriteAttribute(writer, "path", swap.RelativePath);
                WriteAttribute(writer, "runtimeSpriteKey", swap.RuntimeSpriteKey);
                if (swap.Day.HasValue)
                    writer.WriteAttributeString("day", swap.Day.Value.ToString(CultureInfo.InvariantCulture));
                writer.WriteAttributeString("targetComponent", swap.TargetComponent.ToString());
                writer.WriteEndElement();
            }
            writer.WriteEndElement();

            writer.WriteStartElement("SceneSpritePlacements");
            for (int i = 0; i < value.SceneSpritePlacements.Count; i++)
            {
                SceneSpritePlacement placement = value.SceneSpritePlacements[i];
                if (placement == null)
                    continue;

                writer.WriteStartElement("Placement");
                WriteAttribute(writer, "id", placement.Id);
                WriteAttribute(writer, "spriteId", placement.SpriteId);
                WriteAttribute(writer, "path", placement.RelativePath);
                WriteAttribute(writer, "runtimeSpriteKey", placement.RuntimeSpriteKey);
                writer.WriteAttributeString("snapToGrid", placement.SnapToGrid.ToString());
                if (placement.GridX.HasValue)
                    writer.WriteAttributeString("gridX", placement.GridX.Value.ToString(CultureInfo.InvariantCulture));
                if (placement.GridY.HasValue)
                    writer.WriteAttributeString("gridY", placement.GridY.Value.ToString(CultureInfo.InvariantCulture));
                WriteAttribute(writer, "sortingLayer", placement.SortingLayerName);
                writer.WriteAttributeString("sortingOrder", placement.SortingOrder.ToString(CultureInfo.InvariantCulture));
                WriteVector(writer, "Position", placement.Position);
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
            writer.WriteEndElement();
        }

        private static void ReadConditions(XmlElement parent, string elementName, System.Collections.Generic.List<ConditionDef> target)
        {
            if (parent == null)
                return;

            XmlNodeList nodes = parent.GetElementsByTagName(elementName);
            for (int i = 0; i < nodes.Count; i++)
            {
                XmlElement conditionElement = nodes[i] as XmlElement;
                if (conditionElement == null)
                    continue;

                ConditionDef condition = new ConditionDef();
                condition.Id = AttributeOrChild(conditionElement, "id", "Id");
                condition.Type = AttributeOrChild(conditionElement, "type", "Type");
                ReadProperties(Child(conditionElement, "Properties"), condition.Properties);
                target.Add(condition);
            }
        }

        private static void WriteConditions(XmlWriter writer, string parentName, System.Collections.Generic.List<ConditionDef> conditions)
        {
            writer.WriteStartElement(parentName);
            if (conditions != null)
            {
                for (int i = 0; i < conditions.Count; i++)
                {
                    ConditionDef condition = conditions[i];
                    writer.WriteStartElement("Condition");
                    WriteAttribute(writer, "id", condition.Id);
                    WriteAttribute(writer, "type", condition.Type);
                    WriteProperties(writer, "Properties", condition.Properties);
                    writer.WriteEndElement();
                }
            }
            writer.WriteEndElement();
        }

        private static void ReadProperties(XmlElement parent, System.Collections.Generic.List<ScenarioProperty> target)
        {
            if (parent == null || target == null)
                return;

            XmlNodeList nodes = parent.GetElementsByTagName("Property");
            for (int i = 0; i < nodes.Count; i++)
            {
                XmlElement propertyElement = nodes[i] as XmlElement;
                if (propertyElement != null)
                {
                    target.Add(new ScenarioProperty
                    {
                        Key = AttributeOrChild(propertyElement, "key", "Key"),
                        Value = AttributeOrChild(propertyElement, "value", "Value")
                    });
                }
            }
        }

        private static void WriteProperties(XmlWriter writer, string parentName, System.Collections.Generic.List<ScenarioProperty> properties)
        {
            writer.WriteStartElement(parentName);
            if (properties != null)
            {
                for (int i = 0; i < properties.Count; i++)
                {
                    writer.WriteStartElement("Property");
                    WriteAttribute(writer, "key", properties[i].Key);
                    WriteAttribute(writer, "value", properties[i].Value);
                    writer.WriteEndElement();
                }
            }
            writer.WriteEndElement();
        }

        private static ScenarioVector3 ReadVector(XmlElement element)
        {
            ScenarioVector3 vector = new ScenarioVector3();
            if (element == null)
                return vector;

            vector.X = ReadFloatAttribute(element, "x", 0f);
            vector.Y = ReadFloatAttribute(element, "y", 0f);
            vector.Z = ReadFloatAttribute(element, "z", 0f);
            return vector;
        }

        private static void WriteVector(XmlWriter writer, string name, ScenarioVector3 vector)
        {
            if (vector == null)
                vector = new ScenarioVector3();

            writer.WriteStartElement(name);
            writer.WriteAttributeString("x", vector.X.ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("y", vector.Y.ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("z", vector.Z.ToString(CultureInfo.InvariantCulture));
            writer.WriteEndElement();
        }

        private static XmlElement Child(XmlElement parent, string name)
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

        private static string ReadText(XmlElement parent, string name)
        {
            XmlElement child = Child(parent, name);
            return child != null ? child.InnerText : null;
        }

        private static void ReadStringList(XmlElement parent, string elementName, System.Collections.Generic.List<string> target)
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

        private static void ReadDependencyList(XmlElement parent, System.Collections.Generic.List<string> target)
        {
            if (parent == null || target == null)
                return;

            XmlNodeList nodes = parent.GetElementsByTagName("Requires");
            for (int i = 0; i < nodes.Count; i++)
            {
                XmlElement element = nodes[i] as XmlElement;
                if (element == null)
                    continue;

                string id = AttributeOrChild(element, "id", "Id");
                if (string.IsNullOrEmpty(id))
                    id = AttributeOrChild(element, "modId", "ModId");

                string version = AttributeOrChild(element, "version", "Version");
                string dependency = !string.IsNullOrEmpty(id)
                    ? string.IsNullOrEmpty(version) ? id : id + "@" + version
                    : element.InnerText;

                if (!string.IsNullOrEmpty(dependency))
                    target.Add(dependency);
            }
        }

        private static T ReadEnum<T>(XmlElement parent, string name, T fallback)
        {
            string raw = ReadText(parent, name);
            if (string.IsNullOrEmpty(raw))
                return fallback;

            try { return (T)Enum.Parse(typeof(T), raw, true); }
            catch { return fallback; }
        }

        private static T ReadEnumAttribute<T>(XmlElement element, string attributeName, T fallback)
        {
            if (element == null || !element.HasAttribute(attributeName))
                return fallback;

            try { return (T)Enum.Parse(typeof(T), element.GetAttribute(attributeName), true); }
            catch { return fallback; }
        }

        private static bool ReadBool(XmlElement parent, string name, bool fallback)
        {
            string raw = ReadText(parent, name);
            if (string.IsNullOrEmpty(raw))
                return fallback;

            bool parsed;
            return bool.TryParse(raw, out parsed) ? parsed : fallback;
        }

        private static int? ReadNullableInt(XmlElement parent, string name)
        {
            string raw = ReadText(parent, name);
            return ParseNullableInt(raw);
        }

        private static long? ReadNullableLong(XmlElement parent, string name)
        {
            string raw = ReadText(parent, name);
            long parsed;
            if (!string.IsNullOrEmpty(raw) && long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed))
                return parsed;
            return null;
        }

        private static int ReadIntAttribute(XmlElement element, string attributeName, int fallback)
        {
            int? parsed = ReadNullableIntAttribute(element, attributeName);
            return parsed.HasValue ? parsed.Value : fallback;
        }

        private static bool ReadBoolAttribute(XmlElement element, string attributeName, bool fallback)
        {
            if (element == null || !element.HasAttribute(attributeName))
                return fallback;

            bool parsed;
            return bool.TryParse(element.GetAttribute(attributeName), out parsed) ? parsed : fallback;
        }

        private static int? ReadNullableIntAttribute(XmlElement element, string attributeName)
        {
            if (element == null || !element.HasAttribute(attributeName))
                return null;
            return ParseNullableInt(element.GetAttribute(attributeName));
        }

        private static float ReadFloatAttribute(XmlElement element, string attributeName, float fallback)
        {
            if (element == null || !element.HasAttribute(attributeName))
                return fallback;

            float parsed;
            return float.TryParse(element.GetAttribute(attributeName), NumberStyles.Float, CultureInfo.InvariantCulture, out parsed)
                ? parsed
                : fallback;
        }

        private static int? ParseNullableInt(string raw)
        {
            int parsed;
            if (!string.IsNullOrEmpty(raw) && int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed))
                return parsed;
            return null;
        }

        private static string AttributeOrChild(XmlElement element, string attributeName, string childName)
        {
            if (element == null)
                return null;
            if (!string.IsNullOrEmpty(attributeName) && element.HasAttribute(attributeName))
                return element.GetAttribute(attributeName);
            return ReadText(element, childName);
        }

        private static void WriteElement(XmlWriter writer, string name, string value)
        {
            writer.WriteStartElement(name);
            writer.WriteString(value ?? string.Empty);
            writer.WriteEndElement();
        }

        private static void WriteNullableElement(XmlWriter writer, string name, int? value)
        {
            if (!value.HasValue)
                return;
            WriteElement(writer, name, value.Value.ToString(CultureInfo.InvariantCulture));
        }

        private static void WriteAttribute(XmlWriter writer, string name, string value)
        {
            if (!string.IsNullOrEmpty(value))
                writer.WriteAttributeString(name, value);
        }
    }
}
