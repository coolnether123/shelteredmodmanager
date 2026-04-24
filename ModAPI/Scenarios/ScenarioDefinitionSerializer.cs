using System;
using System.Globalization;
using System.IO;
using System.Xml;
using ModAPI.Scenarios.Serialization;

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
            FamilyScenarioSectionSerializer familySerializer = new FamilyScenarioSectionSerializer();
            InventoryScenarioSectionSerializer inventorySerializer = new InventoryScenarioSectionSerializer();
            BunkerEditsScenarioSectionSerializer bunkerEditsSerializer = new BunkerEditsScenarioSectionSerializer();
            TriggerEventScenarioSectionSerializer triggerSerializer = new TriggerEventScenarioSectionSerializer();
            QuestMapScenarioSectionSerializer questMapSerializer = new QuestMapScenarioSectionSerializer();
            WinLossScenarioSectionSerializer winLossSerializer = new WinLossScenarioSectionSerializer();
            AssetReferenceScenarioSectionSerializer assetSerializer = new AssetReferenceScenarioSectionSerializer();
            BunkerGridScenarioSectionSerializer bunkerGridSerializer = new BunkerGridScenarioSectionSerializer();
            GateConditionScenarioSectionSerializer gateSerializer = new GateConditionScenarioSectionSerializer();
            ScheduledActionScenarioSectionSerializer scheduledSerializer = new ScheduledActionScenarioSectionSerializer();

            XmlElement meta = Child(root, "Meta");
            definition.Id = ReadText(meta, "Id");
            definition.DisplayName = ReadText(meta, "DisplayName");
            definition.Description = ReadText(meta, "Description");
            definition.Author = ReadText(meta, "Author");
            definition.Version = ReadText(meta, "Version");

            XmlElement dependencies = Child(root, "Dependencies");
            if (dependencies != null)
            {
                ReadDependencyList(dependencies, definition.Dependencies);
                ReadModDependencyList(dependencies, definition.ModDependencies);
            }

            definition.BaseGameMode = ReadEnum(root, "BaseMode", ScenarioBaseGameMode.Survival);
            definition.SeedOverride = ReadNullableLong(root, "SeedOverride");
            definition.FamilySetup = familySerializer.Read(Child(root, "FamilySetup"));
            definition.StartingInventory = inventorySerializer.Read(Child(root, "StartingInventory"));
            definition.BunkerEdits = bunkerEditsSerializer.Read(Child(root, "BunkerEdits"));
            definition.TriggersAndEvents = triggerSerializer.Read(Child(root, "TriggersAndEvents"));
            definition.Quests = questMapSerializer.ReadQuests(Child(root, "Quests"));
            definition.Map = questMapSerializer.ReadMap(Child(root, "Map"));
            definition.WinLossConditions = winLossSerializer.Read(Child(root, "WinLossConditions"));
            definition.AssetReferences = assetSerializer.Read(Child(root, "AssetReferences"));
            definition.BunkerGrid = bunkerGridSerializer.Read(Child(root, "BunkerGrid"));
            gateSerializer.Read(Child(root, "Gates"), definition.Gates);
            scheduledSerializer.Read(Child(root, "ScheduledActions"), definition.ScheduledActions);
            return definition;
        }

        internal static FamilySetupDefinition ReadFamilySetup(XmlElement element)
        {
            FamilySetupDefinition setup = new FamilySetupDefinition();
            if (element == null)
                return setup;

            setup.OverrideVanillaFamily = ReadBool(element, "OverrideVanillaFamily", false);
            XmlElement members = Child(element, "Members");
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

            XmlElement future = Child(element, "FutureSurvivors");
            if (future != null)
            {
                XmlNodeList futureNodes = future.GetElementsByTagName("FutureSurvivor");
                for (int i = 0; i < futureNodes.Count; i++)
                {
                    XmlElement futureElement = futureNodes[i] as XmlElement;
                    if (futureElement == null)
                        continue;

                    FutureSurvivorDefinition survivor = new FutureSurvivorDefinition();
                    survivor.Id = AttributeOrChild(futureElement, "id", "Id");
                    survivor.AskToJoin = ReadBoolAttribute(futureElement, "askToJoin", true);
                    survivor.Arrival = ReadScheduleTime(Child(futureElement, "Arrival"));
                    XmlElement survivorElement = Child(futureElement, "Survivor");
                    if (survivorElement != null)
                    {
                        XmlElement nestedMember = Child(survivorElement, "Member");
                        survivor.Survivor = ReadFamilyMember(nestedMember ?? survivorElement);
                    }
                    setup.FutureSurvivors.Add(survivor);
                }
            }

            return setup;
        }

        private static FamilyMemberConfig ReadFamilyMember(XmlElement memberElement)
        {
            FamilyMemberConfig member = new FamilyMemberConfig();
            if (memberElement == null)
                return member;

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
            return member;
        }

        internal static StartingInventoryDefinition ReadStartingInventory(XmlElement element)
        {
            StartingInventoryDefinition inventory = new StartingInventoryDefinition();
            if (element == null)
                return inventory;

            inventory.OverrideRandomStart = ReadBool(element, "OverrideRandomStart", false);
            XmlElement items = Child(element, "Items");
            if (items != null)
            {
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
            }

            XmlElement scheduled = Child(element, "ScheduledChanges");
            if (scheduled != null)
            {
                XmlNodeList changeNodes = scheduled.GetElementsByTagName("Change");
                for (int i = 0; i < changeNodes.Count; i++)
                {
                    XmlElement changeElement = changeNodes[i] as XmlElement;
                    if (changeElement == null)
                        continue;

                    TimedInventoryChangeDefinition change = new TimedInventoryChangeDefinition();
                    change.Id = AttributeOrChild(changeElement, "id", "Id");
                    change.ItemId = AttributeOrChild(changeElement, "itemId", "ItemId");
                    change.Quantity = ReadIntAttribute(changeElement, "quantity", 0);
                    change.Kind = ReadEnumAttribute(changeElement, "kind", ScenarioInventoryChangeKind.Add);
                    change.When = ReadScheduleTime(Child(changeElement, "When"));
                    inventory.ScheduledChanges.Add(change);
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

        internal static BunkerEditsDefinition ReadBunkerEdits(XmlElement element)
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
                    placement.ScenarioObjectId = AttributeOrChild(placementElement, "scenarioObjectId", "ScenarioObjectId");
                    placement.RuntimeBindingKey = AttributeOrChild(placementElement, "runtimeBindingKey", "RuntimeBindingKey");
                    placement.PrefabReference = AttributeOrChild(placementElement, "prefab", "PrefabReference");
                    placement.DefinitionReference = AttributeOrChild(placementElement, "definition", "DefinitionReference");
                    placement.Position = ReadVector(Child(placementElement, "Position"));
                    placement.Rotation = ReadVector(Child(placementElement, "Rotation"));
                    placement.StartState = ReadEnumAttribute(placementElement, "startState", ScenarioObjectStartState.StartsEnabled);
                    placement.PlacementPhase = AttributeOrChild(placementElement, "placementPhase", "PlacementPhase");
                    placement.RequiredFoundationId = AttributeOrChild(placementElement, "requiredFoundationId", "RequiredFoundationId");
                    placement.RequiredBunkerExpansionId = AttributeOrChild(placementElement, "requiredExpansionId", "RequiredBunkerExpansionId");
                    placement.UnlockGateId = AttributeOrChild(placementElement, "unlockGateId", "UnlockGateId");
                    placement.ScheduledActivationId = AttributeOrChild(placementElement, "scheduledActivationId", "ScheduledActivationId");
                    ReadStringList(Child(placementElement, "Tags"), "Tag", placement.Tags);
                    ReadProperties(Child(placementElement, "CustomProperties"), placement.CustomProperties);
                    bunker.ObjectPlacements.Add(placement);
                }
            }

            return bunker;
        }

        internal static TriggersAndEventsDefinition ReadTriggersAndEvents(XmlElement element)
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

            XmlElement weatherEvents = Child(element, "WeatherEvents");
            if (weatherEvents != null)
            {
                XmlNodeList eventNodes = weatherEvents.GetElementsByTagName("WeatherEvent");
                for (int i = 0; i < eventNodes.Count; i++)
                {
                    XmlElement eventElement = eventNodes[i] as XmlElement;
                    if (eventElement == null)
                        continue;

                    WeatherEventDefinition weather = new WeatherEventDefinition();
                    weather.Id = AttributeOrChild(eventElement, "id", "Id");
                    weather.WeatherState = AttributeOrChild(eventElement, "state", "WeatherState");
                    weather.DurationHours = ReadIntAttribute(eventElement, "durationHours", 0);
                    weather.When = ReadScheduleTime(Child(eventElement, "When"));
                    result.WeatherEvents.Add(weather);
                }
            }

            return result;
        }

        internal static WinLossConditionsDefinition ReadWinLossConditions(XmlElement element)
        {
            WinLossConditionsDefinition result = new WinLossConditionsDefinition();
            if (element == null)
                return result;

            ReadConditions(Child(element, "WinConditions"), "Condition", result.WinConditions);
            ReadConditions(Child(element, "LossConditions"), "Condition", result.LossConditions);
            return result;
        }

        internal static QuestAuthoringDefinition ReadQuests(XmlElement element)
        {
            QuestAuthoringDefinition result = new QuestAuthoringDefinition();
            if (element == null)
                return result;

            XmlNodeList questNodes = element.GetElementsByTagName("Quest");
            for (int i = 0; i < questNodes.Count; i++)
            {
                XmlElement questElement = questNodes[i] as XmlElement;
                if (questElement == null)
                    continue;

                QuestDefinition quest = new QuestDefinition();
                quest.Id = AttributeOrChild(questElement, "id", "Id");
                quest.Title = AttributeOrChild(questElement, "title", "Title");
                quest.Description = ReadText(questElement, "Description");
                quest.StartTriggerId = AttributeOrChild(questElement, "startTriggerId", "StartTriggerId");
                quest.CompletionConditionId = AttributeOrChild(questElement, "completionConditionId", "CompletionConditionId");
                quest.ScheduledStart = ReadScheduleTime(Child(questElement, "ScheduledStart"));
                ReadProperties(Child(questElement, "Properties"), quest.Properties);
                result.Quests.Add(quest);
            }

            return result;
        }

        internal static MapAuthoringDefinition ReadMap(XmlElement element)
        {
            MapAuthoringDefinition result = new MapAuthoringDefinition();
            if (element == null)
                return result;

            result.StartLocationId = AttributeOrChild(element, "startLocationId", "StartLocationId");
            XmlNodeList locationNodes = element.GetElementsByTagName("Location");
            for (int i = 0; i < locationNodes.Count; i++)
            {
                XmlElement locationElement = locationNodes[i] as XmlElement;
                if (locationElement == null)
                    continue;

                MapLocationDefinition location = new MapLocationDefinition();
                location.Id = AttributeOrChild(locationElement, "id", "Id");
                location.DisplayName = AttributeOrChild(locationElement, "displayName", "DisplayName");
                location.X = ReadFloatAttribute(locationElement, "x", 0f);
                location.Y = ReadFloatAttribute(locationElement, "y", 0f);
                ReadProperties(Child(locationElement, "Properties"), location.Properties);
                result.Locations.Add(location);
            }

            return result;
        }

        internal static AssetReferencesDefinition ReadAssetReferences(XmlElement element)
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
                            RelativePath = AttributeOrChild(spriteElement, "path", "Path"),
                            PatchId = AttributeOrChild(spriteElement, "patchId", "PatchId")
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

            XmlElement spritePatches = Child(element, "SpritePatches");
            if (spritePatches != null)
            {
                XmlNodeList patchNodes = spritePatches.GetElementsByTagName("Patch");
                for (int i = 0; i < patchNodes.Count; i++)
                {
                    XmlElement patchElement = patchNodes[i] as XmlElement;
                    SpritePatchDefinition patch = SpritePatchSerializer.ReadPatch(
                        patchElement,
                        AttributeOrChild,
                        Child,
                        ReadIntAttribute);
                    if (patch != null)
                        result.SpritePatches.Add(patch);
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

                    SceneSpritePlacement placement = new SceneSpritePlacement();
                    placement.Id = AttributeOrChild(placementElement, "id", "Id");
                    placement.ScenarioObjectId = AttributeOrChild(placementElement, "scenarioObjectId", "ScenarioObjectId");
                    placement.RuntimeBindingKey = AttributeOrChild(placementElement, "runtimeBindingKey", "RuntimeBindingKey");
                    placement.SpriteId = AttributeOrChild(placementElement, "spriteId", "SpriteId");
                    placement.RelativePath = AttributeOrChild(placementElement, "path", "Path");
                    placement.RuntimeSpriteKey = AttributeOrChild(placementElement, "runtimeSpriteKey", "RuntimeSpriteKey");
                    placement.Position = ReadVector(Child(placementElement, "Position"));
                    placement.SnapToGrid = ReadBoolAttribute(placementElement, "snapToGrid", false);
                    placement.GridX = ReadNullableIntAttribute(placementElement, "gridX");
                    placement.GridY = ReadNullableIntAttribute(placementElement, "gridY");
                    placement.StartState = ReadEnumAttribute(placementElement, "startState", ScenarioObjectStartState.StartsEnabled);
                    placement.PlacementPhase = AttributeOrChild(placementElement, "placementPhase", "PlacementPhase");
                    placement.RequiredFoundationId = AttributeOrChild(placementElement, "requiredFoundationId", "RequiredFoundationId");
                    placement.RequiredBunkerExpansionId = AttributeOrChild(placementElement, "requiredExpansionId", "RequiredBunkerExpansionId");
                    placement.UnlockGateId = AttributeOrChild(placementElement, "unlockGateId", "UnlockGateId");
                    placement.ScheduledActivationId = AttributeOrChild(placementElement, "scheduledActivationId", "ScheduledActivationId");
                    ReadStringList(Child(placementElement, "Tags"), "Tag", placement.Tags);
                    placement.SortingLayerName = AttributeOrChild(placementElement, "sortingLayer", "SortingLayer");
                    placement.SortingOrder = ReadIntAttribute(placementElement, "sortingOrder", 0);
                    result.SceneSpritePlacements.Add(placement);
                }
            }

            return result;
        }

        private static void WriteDocument(ScenarioDefinition definition, XmlWriter writer)
        {
            writer.WriteStartDocument();
            writer.WriteStartElement("Scenario");
            FamilyScenarioSectionSerializer familySerializer = new FamilyScenarioSectionSerializer();
            InventoryScenarioSectionSerializer inventorySerializer = new InventoryScenarioSectionSerializer();
            BunkerEditsScenarioSectionSerializer bunkerEditsSerializer = new BunkerEditsScenarioSectionSerializer();
            TriggerEventScenarioSectionSerializer triggerSerializer = new TriggerEventScenarioSectionSerializer();
            QuestMapScenarioSectionSerializer questMapSerializer = new QuestMapScenarioSectionSerializer();
            WinLossScenarioSectionSerializer winLossSerializer = new WinLossScenarioSectionSerializer();
            AssetReferenceScenarioSectionSerializer assetSerializer = new AssetReferenceScenarioSectionSerializer();
            BunkerGridScenarioSectionSerializer bunkerGridSerializer = new BunkerGridScenarioSectionSerializer();
            GateConditionScenarioSectionSerializer gateSerializer = new GateConditionScenarioSectionSerializer();
            ScheduledActionScenarioSectionSerializer scheduledSerializer = new ScheduledActionScenarioSectionSerializer();

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
            if (definition.ModDependencies != null)
            {
                for (int i = 0; i < definition.ModDependencies.Count; i++)
                {
                    ScenarioModDependencyDefinition dependency = definition.ModDependencies[i];
                    if (dependency == null)
                        continue;
                    writer.WriteStartElement("ModDependency");
                    WriteAttribute(writer, "id", dependency.ModId);
                    WriteAttribute(writer, "version", dependency.Version);
                    writer.WriteAttributeString("kind", dependency.Kind.ToString());
                    writer.WriteAttributeString("manual", dependency.Manual ? "true" : "false");
                    writer.WriteEndElement();
                }
            }
            writer.WriteEndElement();

            WriteElement(writer, "BaseMode", definition.BaseGameMode.ToString());
            if (definition.SeedOverride.HasValue)
                WriteElement(writer, "SeedOverride", definition.SeedOverride.Value.ToString(CultureInfo.InvariantCulture));

            familySerializer.Write(writer, definition.FamilySetup);
            inventorySerializer.Write(writer, definition.StartingInventory);
            bunkerEditsSerializer.Write(writer, definition.BunkerEdits);
            triggerSerializer.Write(writer, definition.TriggersAndEvents);
            questMapSerializer.WriteQuests(writer, definition.Quests);
            questMapSerializer.WriteMap(writer, definition.Map);
            winLossSerializer.Write(writer, definition.WinLossConditions);
            assetSerializer.Write(writer, definition.AssetReferences);
            bunkerGridSerializer.Write(writer, definition.BunkerGrid);
            gateSerializer.Write(writer, definition.Gates);
            scheduledSerializer.Write(writer, definition.ScheduledActions);

            writer.WriteEndElement();
            writer.WriteEndDocument();
        }

        internal static void WriteFamilySetup(XmlWriter writer, FamilySetupDefinition setup)
        {
            if (setup == null)
                setup = new FamilySetupDefinition();

            writer.WriteStartElement("FamilySetup");
            WriteElement(writer, "OverrideVanillaFamily", setup.OverrideVanillaFamily.ToString());
            writer.WriteStartElement("Members");
            for (int i = 0; i < setup.Members.Count; i++)
            {
                WriteFamilyMember(writer, "Member", setup.Members[i]);
            }
            writer.WriteEndElement();

            writer.WriteStartElement("FutureSurvivors");
            for (int i = 0; i < setup.FutureSurvivors.Count; i++)
            {
                FutureSurvivorDefinition survivor = setup.FutureSurvivors[i];
                if (survivor == null)
                    continue;

                writer.WriteStartElement("FutureSurvivor");
                WriteAttribute(writer, "id", survivor.Id);
                writer.WriteAttributeString("askToJoin", survivor.AskToJoin.ToString());
                WriteScheduleTime(writer, "Arrival", survivor.Arrival);
                WriteFamilyMember(writer, "Survivor", survivor.Survivor);
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
            writer.WriteEndElement();
        }

        private static void WriteFamilyMember(XmlWriter writer, string elementName, FamilyMemberConfig member)
        {
            if (member == null)
                member = new FamilyMemberConfig();

            writer.WriteStartElement(elementName);
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

        internal static void WriteStartingInventory(XmlWriter writer, StartingInventoryDefinition inventory)
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
            writer.WriteStartElement("ScheduledChanges");
            for (int i = 0; i < inventory.ScheduledChanges.Count; i++)
            {
                TimedInventoryChangeDefinition change = inventory.ScheduledChanges[i];
                if (change == null)
                    continue;

                writer.WriteStartElement("Change");
                WriteAttribute(writer, "id", change.Id);
                WriteAttribute(writer, "itemId", change.ItemId);
                writer.WriteAttributeString("quantity", change.Quantity.ToString(CultureInfo.InvariantCulture));
                writer.WriteAttributeString("kind", change.Kind.ToString());
                WriteScheduleTime(writer, "When", change.When);
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

        internal static void WriteBunkerEdits(XmlWriter writer, BunkerEditsDefinition bunker)
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
                WriteAttribute(writer, "scenarioObjectId", placement.ScenarioObjectId);
                WriteAttribute(writer, "runtimeBindingKey", placement.RuntimeBindingKey);
                WriteAttribute(writer, "prefab", placement.PrefabReference);
                WriteAttribute(writer, "definition", placement.DefinitionReference);
                writer.WriteAttributeString("startState", placement.StartState.ToString());
                WriteAttribute(writer, "placementPhase", placement.PlacementPhase);
                WriteAttribute(writer, "requiredFoundationId", placement.RequiredFoundationId);
                WriteAttribute(writer, "requiredExpansionId", placement.RequiredBunkerExpansionId);
                WriteAttribute(writer, "unlockGateId", placement.UnlockGateId);
                WriteAttribute(writer, "scheduledActivationId", placement.ScheduledActivationId);
                WriteVector(writer, "Position", placement.Position);
                WriteVector(writer, "Rotation", placement.Rotation);
                WriteStringList(writer, "Tags", "Tag", placement.Tags);
                WriteProperties(writer, "CustomProperties", placement.CustomProperties);
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
            writer.WriteEndElement();
        }

        internal static void WriteTriggersAndEvents(XmlWriter writer, TriggersAndEventsDefinition value)
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

            writer.WriteStartElement("WeatherEvents");
            for (int i = 0; i < value.WeatherEvents.Count; i++)
            {
                WeatherEventDefinition weather = value.WeatherEvents[i];
                if (weather == null)
                    continue;

                writer.WriteStartElement("WeatherEvent");
                WriteAttribute(writer, "id", weather.Id);
                WriteAttribute(writer, "state", weather.WeatherState);
                writer.WriteAttributeString("durationHours", weather.DurationHours.ToString(CultureInfo.InvariantCulture));
                WriteScheduleTime(writer, "When", weather.When);
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
            writer.WriteEndElement();
        }

        internal static void WriteWinLossConditions(XmlWriter writer, WinLossConditionsDefinition value)
        {
            if (value == null)
                value = new WinLossConditionsDefinition();

            writer.WriteStartElement("WinLossConditions");
            WriteConditions(writer, "WinConditions", value.WinConditions);
            WriteConditions(writer, "LossConditions", value.LossConditions);
            writer.WriteEndElement();
        }

        internal static void WriteQuests(XmlWriter writer, QuestAuthoringDefinition value)
        {
            if (value == null)
                value = new QuestAuthoringDefinition();

            writer.WriteStartElement("Quests");
            for (int i = 0; i < value.Quests.Count; i++)
            {
                QuestDefinition quest = value.Quests[i];
                if (quest == null)
                    continue;

                writer.WriteStartElement("Quest");
                WriteAttribute(writer, "id", quest.Id);
                WriteAttribute(writer, "title", quest.Title);
                WriteAttribute(writer, "startTriggerId", quest.StartTriggerId);
                WriteAttribute(writer, "completionConditionId", quest.CompletionConditionId);
                WriteScheduleTime(writer, "ScheduledStart", quest.ScheduledStart);
                WriteElement(writer, "Description", quest.Description);
                WriteProperties(writer, "Properties", quest.Properties);
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
        }

        internal static void WriteMap(XmlWriter writer, MapAuthoringDefinition value)
        {
            if (value == null)
                value = new MapAuthoringDefinition();

            writer.WriteStartElement("Map");
            WriteAttribute(writer, "startLocationId", value.StartLocationId);
            for (int i = 0; i < value.Locations.Count; i++)
            {
                MapLocationDefinition location = value.Locations[i];
                if (location == null)
                    continue;

                writer.WriteStartElement("Location");
                WriteAttribute(writer, "id", location.Id);
                WriteAttribute(writer, "displayName", location.DisplayName);
                writer.WriteAttributeString("x", location.X.ToString(CultureInfo.InvariantCulture));
                writer.WriteAttributeString("y", location.Y.ToString(CultureInfo.InvariantCulture));
                WriteProperties(writer, "Properties", location.Properties);
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
        }

        internal static void WriteAssetReferences(XmlWriter writer, AssetReferencesDefinition value)
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
                WriteAttribute(writer, "patchId", value.CustomSprites[i].PatchId);
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

            writer.WriteStartElement("SpritePatches");
            for (int i = 0; i < value.SpritePatches.Count; i++)
                SpritePatchSerializer.WritePatch(writer, value.SpritePatches[i]);
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
                WriteAttribute(writer, "scenarioObjectId", placement.ScenarioObjectId);
                WriteAttribute(writer, "runtimeBindingKey", placement.RuntimeBindingKey);
                WriteAttribute(writer, "spriteId", placement.SpriteId);
                WriteAttribute(writer, "path", placement.RelativePath);
                WriteAttribute(writer, "runtimeSpriteKey", placement.RuntimeSpriteKey);
                writer.WriteAttributeString("startState", placement.StartState.ToString());
                WriteAttribute(writer, "placementPhase", placement.PlacementPhase);
                WriteAttribute(writer, "requiredFoundationId", placement.RequiredFoundationId);
                WriteAttribute(writer, "requiredExpansionId", placement.RequiredBunkerExpansionId);
                WriteAttribute(writer, "unlockGateId", placement.UnlockGateId);
                WriteAttribute(writer, "scheduledActivationId", placement.ScheduledActivationId);
                writer.WriteAttributeString("snapToGrid", placement.SnapToGrid.ToString());
                if (placement.GridX.HasValue)
                    writer.WriteAttributeString("gridX", placement.GridX.Value.ToString(CultureInfo.InvariantCulture));
                if (placement.GridY.HasValue)
                    writer.WriteAttributeString("gridY", placement.GridY.Value.ToString(CultureInfo.InvariantCulture));
                WriteAttribute(writer, "sortingLayer", placement.SortingLayerName);
                writer.WriteAttributeString("sortingOrder", placement.SortingOrder.ToString(CultureInfo.InvariantCulture));
                WriteVector(writer, "Position", placement.Position);
                WriteStringList(writer, "Tags", "Tag", placement.Tags);
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
            writer.WriteEndElement();
        }

        internal static ScenarioBunkerGridDefinition ReadBunkerGrid(XmlElement element)
        {
            ScenarioBunkerGridDefinition grid = new ScenarioBunkerGridDefinition();
            if (element == null)
                return grid;

            XmlElement cells = Child(element, "Cells");
            if (cells != null)
            {
                XmlNodeList nodes = cells.GetElementsByTagName("Cell");
                for (int i = 0; i < nodes.Count; i++)
                {
                    XmlElement node = nodes[i] as XmlElement;
                    if (node == null)
                        continue;

                    ScenarioBunkerCellDefinition cell = new ScenarioBunkerCellDefinition();
                    cell.Id = AttributeOrChild(node, "id", "Id");
                    cell.GridX = ReadIntAttribute(node, "gridX", 0);
                    cell.GridY = ReadIntAttribute(node, "gridY", 0);
                    cell.Zone = AttributeOrChild(node, "zone", "Zone");
                    cell.FoundationId = AttributeOrChild(node, "foundationId", "FoundationId");
                    cell.ExpansionId = AttributeOrChild(node, "expansionId", "ExpansionId");
                    cell.BuildPhase = ReadEnumAttribute(node, "buildPhase", ScenarioBunkerBuildPhase.Start);
                    cell.ActiveAtStart = ReadBoolAttribute(node, "activeAtStart", true);
                    cell.LockedAtStart = ReadBoolAttribute(node, "lockedAtStart", false);
                    cell.RequiredMaterialsId = AttributeOrChild(node, "requiredMaterialsId", "RequiredMaterialsId");
                    cell.RequiredTechId = AttributeOrChild(node, "requiredTechId", "RequiredTechId");
                    cell.RequiredTime = ReadOptionalScheduleTime(Child(node, "RequiredTime"));
                    cell.UnlockGateId = AttributeOrChild(node, "unlockGateId", "UnlockGateId");
                    grid.Cells.Add(cell);
                }
            }

            XmlElement foundations = Child(element, "Foundations");
            if (foundations != null)
            {
                XmlNodeList nodes = foundations.GetElementsByTagName("Foundation");
                for (int i = 0; i < nodes.Count; i++)
                {
                    XmlElement node = nodes[i] as XmlElement;
                    if (node == null)
                        continue;

                    ScenarioFoundationDefinition foundation = new ScenarioFoundationDefinition();
                    foundation.Id = AttributeOrChild(node, "id", "Id");
                    foundation.GridX = ReadIntAttribute(node, "gridX", 0);
                    foundation.GridY = ReadIntAttribute(node, "gridY", 0);
                    foundation.Width = ReadIntAttribute(node, "width", 1);
                    foundation.Height = ReadIntAttribute(node, "height", 1);
                    foundation.ExpansionId = AttributeOrChild(node, "expansionId", "ExpansionId");
                    foundation.BuildPhase = ReadEnumAttribute(node, "buildPhase", ScenarioBunkerBuildPhase.Start);
                    foundation.ActiveAtStart = ReadBoolAttribute(node, "activeAtStart", true);
                    foundation.LockedAtStart = ReadBoolAttribute(node, "lockedAtStart", false);
                    foundation.UnlockGateId = AttributeOrChild(node, "unlockGateId", "UnlockGateId");
                    grid.Foundations.Add(foundation);
                }
            }

            XmlElement expansions = Child(element, "Expansions");
            if (expansions != null)
            {
                XmlNodeList nodes = expansions.GetElementsByTagName("Expansion");
                for (int i = 0; i < nodes.Count; i++)
                {
                    XmlElement node = nodes[i] as XmlElement;
                    if (node == null)
                        continue;

                    ScenarioBunkerExpansionDefinition expansion = new ScenarioBunkerExpansionDefinition();
                    expansion.Id = AttributeOrChild(node, "id", "Id");
                    expansion.DisplayName = AttributeOrChild(node, "displayName", "DisplayName");
                    expansion.BoundaryId = AttributeOrChild(node, "boundaryId", "BoundaryId");
                    expansion.BuildPhase = ReadEnumAttribute(node, "buildPhase", ScenarioBunkerBuildPhase.Start);
                    expansion.ActiveAtStart = ReadBoolAttribute(node, "activeAtStart", true);
                    expansion.LockedAtStart = ReadBoolAttribute(node, "lockedAtStart", false);
                    expansion.RequiredMaterialsId = AttributeOrChild(node, "requiredMaterialsId", "RequiredMaterialsId");
                    expansion.RequiredTechId = AttributeOrChild(node, "requiredTechId", "RequiredTechId");
                    expansion.RequiredTime = ReadOptionalScheduleTime(Child(node, "RequiredTime"));
                    expansion.UnlockGateId = AttributeOrChild(node, "unlockGateId", "UnlockGateId");
                    ReadStringList(Child(node, "CellIds"), "CellId", expansion.CellIds);
                    grid.Expansions.Add(expansion);
                }
            }

            XmlElement boundaries = Child(element, "Boundaries");
            if (boundaries != null)
            {
                XmlNodeList nodes = boundaries.GetElementsByTagName("Boundary");
                for (int i = 0; i < nodes.Count; i++)
                {
                    XmlElement node = nodes[i] as XmlElement;
                    if (node == null)
                        continue;

                    grid.Boundaries.Add(new ScenarioBunkerBoundaryDefinition
                    {
                        Id = AttributeOrChild(node, "id", "Id"),
                        MinGridX = ReadIntAttribute(node, "minX", 0),
                        MinGridY = ReadIntAttribute(node, "minY", 0),
                        MaxGridX = ReadIntAttribute(node, "maxX", 0),
                        MaxGridY = ReadIntAttribute(node, "maxY", 0),
                        ExpansionId = AttributeOrChild(node, "expansionId", "ExpansionId")
                    });
                }
            }

            return grid;
        }

        internal static void WriteBunkerGrid(XmlWriter writer, ScenarioBunkerGridDefinition grid)
        {
            if (grid == null)
                grid = new ScenarioBunkerGridDefinition();

            writer.WriteStartElement("BunkerGrid");
            writer.WriteStartElement("Cells");
            for (int i = 0; i < grid.Cells.Count; i++)
            {
                ScenarioBunkerCellDefinition cell = grid.Cells[i];
                if (cell == null)
                    continue;
                writer.WriteStartElement("Cell");
                WriteAttribute(writer, "id", cell.Id);
                writer.WriteAttributeString("gridX", cell.GridX.ToString(CultureInfo.InvariantCulture));
                writer.WriteAttributeString("gridY", cell.GridY.ToString(CultureInfo.InvariantCulture));
                WriteAttribute(writer, "zone", cell.Zone);
                WriteAttribute(writer, "foundationId", cell.FoundationId);
                WriteAttribute(writer, "expansionId", cell.ExpansionId);
                writer.WriteAttributeString("buildPhase", cell.BuildPhase.ToString());
                writer.WriteAttributeString("activeAtStart", cell.ActiveAtStart.ToString());
                writer.WriteAttributeString("lockedAtStart", cell.LockedAtStart.ToString());
                WriteAttribute(writer, "requiredMaterialsId", cell.RequiredMaterialsId);
                WriteAttribute(writer, "requiredTechId", cell.RequiredTechId);
                WriteAttribute(writer, "unlockGateId", cell.UnlockGateId);
                WriteOptionalScheduleTime(writer, "RequiredTime", cell.RequiredTime);
                writer.WriteEndElement();
            }
            writer.WriteEndElement();

            writer.WriteStartElement("Foundations");
            for (int i = 0; i < grid.Foundations.Count; i++)
            {
                ScenarioFoundationDefinition foundation = grid.Foundations[i];
                if (foundation == null)
                    continue;
                writer.WriteStartElement("Foundation");
                WriteAttribute(writer, "id", foundation.Id);
                writer.WriteAttributeString("gridX", foundation.GridX.ToString(CultureInfo.InvariantCulture));
                writer.WriteAttributeString("gridY", foundation.GridY.ToString(CultureInfo.InvariantCulture));
                writer.WriteAttributeString("width", foundation.Width.ToString(CultureInfo.InvariantCulture));
                writer.WriteAttributeString("height", foundation.Height.ToString(CultureInfo.InvariantCulture));
                WriteAttribute(writer, "expansionId", foundation.ExpansionId);
                writer.WriteAttributeString("buildPhase", foundation.BuildPhase.ToString());
                writer.WriteAttributeString("activeAtStart", foundation.ActiveAtStart.ToString());
                writer.WriteAttributeString("lockedAtStart", foundation.LockedAtStart.ToString());
                WriteAttribute(writer, "unlockGateId", foundation.UnlockGateId);
                writer.WriteEndElement();
            }
            writer.WriteEndElement();

            writer.WriteStartElement("Expansions");
            for (int i = 0; i < grid.Expansions.Count; i++)
            {
                ScenarioBunkerExpansionDefinition expansion = grid.Expansions[i];
                if (expansion == null)
                    continue;
                writer.WriteStartElement("Expansion");
                WriteAttribute(writer, "id", expansion.Id);
                WriteAttribute(writer, "displayName", expansion.DisplayName);
                WriteAttribute(writer, "boundaryId", expansion.BoundaryId);
                writer.WriteAttributeString("buildPhase", expansion.BuildPhase.ToString());
                writer.WriteAttributeString("activeAtStart", expansion.ActiveAtStart.ToString());
                writer.WriteAttributeString("lockedAtStart", expansion.LockedAtStart.ToString());
                WriteAttribute(writer, "requiredMaterialsId", expansion.RequiredMaterialsId);
                WriteAttribute(writer, "requiredTechId", expansion.RequiredTechId);
                WriteAttribute(writer, "unlockGateId", expansion.UnlockGateId);
                WriteOptionalScheduleTime(writer, "RequiredTime", expansion.RequiredTime);
                WriteStringList(writer, "CellIds", "CellId", expansion.CellIds);
                writer.WriteEndElement();
            }
            writer.WriteEndElement();

            writer.WriteStartElement("Boundaries");
            for (int i = 0; i < grid.Boundaries.Count; i++)
            {
                ScenarioBunkerBoundaryDefinition boundary = grid.Boundaries[i];
                if (boundary == null)
                    continue;
                writer.WriteStartElement("Boundary");
                WriteAttribute(writer, "id", boundary.Id);
                writer.WriteAttributeString("minX", boundary.MinGridX.ToString(CultureInfo.InvariantCulture));
                writer.WriteAttributeString("minY", boundary.MinGridY.ToString(CultureInfo.InvariantCulture));
                writer.WriteAttributeString("maxX", boundary.MaxGridX.ToString(CultureInfo.InvariantCulture));
                writer.WriteAttributeString("maxY", boundary.MaxGridY.ToString(CultureInfo.InvariantCulture));
                WriteAttribute(writer, "expansionId", boundary.ExpansionId);
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
            writer.WriteEndElement();
        }

        internal static void ReadGates(XmlElement element, System.Collections.Generic.List<ScenarioGateDefinition> target)
        {
            if (element == null || target == null)
                return;

            XmlNodeList nodes = element.GetElementsByTagName("Gate");
            for (int i = 0; i < nodes.Count; i++)
            {
                XmlElement gateElement = nodes[i] as XmlElement;
                if (gateElement == null)
                    continue;

                ScenarioGateDefinition gate = new ScenarioGateDefinition();
                gate.Id = AttributeOrChild(gateElement, "id", "Id");
                gate.DisplayName = AttributeOrChild(gateElement, "displayName", "DisplayName");
                gate.Conditions = ReadConditionGroup(Child(gateElement, "Conditions"));
                target.Add(gate);
            }
        }

        internal static void WriteGates(XmlWriter writer, System.Collections.Generic.List<ScenarioGateDefinition> gates)
        {
            writer.WriteStartElement("Gates");
            if (gates != null)
            {
                for (int i = 0; i < gates.Count; i++)
                {
                    ScenarioGateDefinition gate = gates[i];
                    if (gate == null)
                        continue;
                    writer.WriteStartElement("Gate");
                    WriteAttribute(writer, "id", gate.Id);
                    WriteAttribute(writer, "displayName", gate.DisplayName);
                    WriteConditionGroup(writer, "Conditions", gate.Conditions);
                    writer.WriteEndElement();
                }
            }
            writer.WriteEndElement();
        }

        internal static void ReadScheduledActions(XmlElement element, System.Collections.Generic.List<ScenarioScheduledActionDefinition> target)
        {
            if (element == null || target == null)
                return;

            XmlNodeList nodes = element.GetElementsByTagName("Action");
            for (int i = 0; i < nodes.Count; i++)
            {
                XmlElement actionElement = nodes[i] as XmlElement;
                if (actionElement == null)
                    continue;

                ScenarioScheduledActionDefinition action = new ScenarioScheduledActionDefinition();
                action.Id = AttributeOrChild(actionElement, "id", "Id");
                action.ActionType = AttributeOrChild(actionElement, "type", "ActionType");
                action.GateId = AttributeOrChild(actionElement, "gateId", "GateId");
                action.DueTime = ReadScheduleTime(Child(actionElement, "DueTime"));
                XmlElement policy = Child(actionElement, "Policy");
                if (policy != null)
                {
                    action.Policy.Repeatable = ReadBoolAttribute(policy, "repeatable", false);
                    action.Policy.CooldownMinutes = ReadIntAttribute(policy, "cooldownMinutes", 0);
                }
                ReadConditionRefs(Child(actionElement, "Conditions"), action.ConditionRefs);
                ReadEffects(Child(actionElement, "Effects"), action.Effects);
                target.Add(action);
            }
        }

        internal static void WriteScheduledActions(XmlWriter writer, System.Collections.Generic.List<ScenarioScheduledActionDefinition> actions)
        {
            writer.WriteStartElement("ScheduledActions");
            if (actions != null)
            {
                for (int i = 0; i < actions.Count; i++)
                {
                    ScenarioScheduledActionDefinition action = actions[i];
                    if (action == null)
                        continue;
                    writer.WriteStartElement("Action");
                    WriteAttribute(writer, "id", action.Id);
                    WriteAttribute(writer, "type", action.ActionType);
                    WriteAttribute(writer, "gateId", action.GateId);
                    WriteScheduleTime(writer, "DueTime", action.DueTime);
                    writer.WriteStartElement("Policy");
                    writer.WriteAttributeString("repeatable", action.Policy != null && action.Policy.Repeatable ? "true" : "false");
                    writer.WriteAttributeString("cooldownMinutes", action.Policy != null ? action.Policy.CooldownMinutes.ToString(CultureInfo.InvariantCulture) : "0");
                    writer.WriteEndElement();
                    WriteConditionRefs(writer, "Conditions", action.ConditionRefs);
                    WriteEffects(writer, "Effects", action.Effects);
                    writer.WriteEndElement();
                }
            }
            writer.WriteEndElement();
        }

        private static ScenarioConditionGroup ReadConditionGroup(XmlElement element)
        {
            ScenarioConditionGroup group = new ScenarioConditionGroup();
            if (element == null)
                return group;

            group.Mode = ReadEnumAttribute(element, "mode", ScenarioConditionGroupMode.All);
            ReadConditionRefs(element, group.Conditions);
            XmlNodeList groupNodes = element.GetElementsByTagName("Group");
            for (int i = 0; i < groupNodes.Count; i++)
            {
                XmlElement child = groupNodes[i] as XmlElement;
                if (child != null && child.ParentNode == element)
                    group.Groups.Add(ReadConditionGroup(child));
            }
            return group;
        }

        private static void WriteConditionGroup(XmlWriter writer, string name, ScenarioConditionGroup group)
        {
            if (group == null)
                group = new ScenarioConditionGroup();

            writer.WriteStartElement(name);
            writer.WriteAttributeString("mode", group.Mode.ToString());
            WriteConditionRefs(writer, "Conditions", group.Conditions);
            for (int i = 0; group.Groups != null && i < group.Groups.Count; i++)
                WriteConditionGroup(writer, "Group", group.Groups[i]);
            writer.WriteEndElement();
        }

        private static void ReadConditionRefs(XmlElement element, System.Collections.Generic.List<ScenarioConditionRef> target)
        {
            if (element == null || target == null)
                return;

            XmlNodeList nodes = element.GetElementsByTagName("ConditionRef");
            for (int i = 0; i < nodes.Count; i++)
            {
                XmlElement node = nodes[i] as XmlElement;
                if (node == null)
                    continue;

                ScenarioConditionRef condition = new ScenarioConditionRef();
                condition.Id = AttributeOrChild(node, "id", "Id");
                condition.Kind = ReadEnumAttribute(node, "kind", ScenarioConditionKind.TimeReached);
                condition.TargetId = AttributeOrChild(node, "targetId", "TargetId");
                condition.Comparison = AttributeOrChild(node, "comparison", "Comparison");
                condition.Quantity = ReadIntAttribute(node, "quantity", 0);
                condition.StatId = AttributeOrChild(node, "statId", "StatId");
                condition.StatValue = ReadIntAttribute(node, "statValue", 0);
                condition.TraitId = AttributeOrChild(node, "traitId", "TraitId");
                condition.FlagId = AttributeOrChild(node, "flagId", "FlagId");
                condition.FlagValue = AttributeOrChild(node, "flagValue", "FlagValue");
                condition.Time = ReadOptionalScheduleTime(Child(node, "Time"));
                ReadProperties(Child(node, "Properties"), condition.Properties);
                target.Add(condition);
            }
        }

        private static void WriteConditionRefs(XmlWriter writer, string parentName, System.Collections.Generic.List<ScenarioConditionRef> conditions)
        {
            writer.WriteStartElement(parentName);
            for (int i = 0; conditions != null && i < conditions.Count; i++)
            {
                ScenarioConditionRef condition = conditions[i];
                if (condition == null)
                    continue;
                writer.WriteStartElement("ConditionRef");
                WriteAttribute(writer, "id", condition.Id);
                writer.WriteAttributeString("kind", condition.Kind.ToString());
                WriteAttribute(writer, "targetId", condition.TargetId);
                WriteAttribute(writer, "comparison", condition.Comparison);
                writer.WriteAttributeString("quantity", condition.Quantity.ToString(CultureInfo.InvariantCulture));
                WriteAttribute(writer, "statId", condition.StatId);
                writer.WriteAttributeString("statValue", condition.StatValue.ToString(CultureInfo.InvariantCulture));
                WriteAttribute(writer, "traitId", condition.TraitId);
                WriteAttribute(writer, "flagId", condition.FlagId);
                WriteAttribute(writer, "flagValue", condition.FlagValue);
                WriteOptionalScheduleTime(writer, "Time", condition.Time);
                WriteProperties(writer, "Properties", condition.Properties);
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
        }

        private static void ReadEffects(XmlElement element, System.Collections.Generic.List<ScenarioEffectDefinition> target)
        {
            if (element == null || target == null)
                return;

            XmlNodeList nodes = element.GetElementsByTagName("Effect");
            for (int i = 0; i < nodes.Count; i++)
            {
                XmlElement node = nodes[i] as XmlElement;
                if (node == null)
                    continue;

                ScenarioEffectDefinition effect = new ScenarioEffectDefinition();
                effect.Id = AttributeOrChild(node, "id", "Id");
                effect.Kind = ReadEnumAttribute(node, "kind", ScenarioEffectKind.SetScenarioFlag);
                effect.TargetId = AttributeOrChild(node, "targetId", "TargetId");
                effect.ItemId = AttributeOrChild(node, "itemId", "ItemId");
                effect.Quantity = ReadIntAttribute(node, "quantity", 0);
                effect.WeatherState = AttributeOrChild(node, "weatherState", "WeatherState");
                effect.DurationHours = ReadIntAttribute(node, "durationHours", 0);
                effect.SurvivorId = AttributeOrChild(node, "survivorId", "SurvivorId");
                effect.QuestId = AttributeOrChild(node, "questId", "QuestId");
                effect.ObjectId = AttributeOrChild(node, "objectId", "ObjectId");
                effect.BunkerExpansionId = AttributeOrChild(node, "bunkerExpansionId", "BunkerExpansionId");
                effect.FlagId = AttributeOrChild(node, "flagId", "FlagId");
                effect.FlagValue = AttributeOrChild(node, "flagValue", "FlagValue");
                ReadProperties(Child(node, "Properties"), effect.Properties);
                target.Add(effect);
            }
        }

        private static void WriteEffects(XmlWriter writer, string parentName, System.Collections.Generic.List<ScenarioEffectDefinition> effects)
        {
            writer.WriteStartElement(parentName);
            for (int i = 0; effects != null && i < effects.Count; i++)
            {
                ScenarioEffectDefinition effect = effects[i];
                if (effect == null)
                    continue;
                writer.WriteStartElement("Effect");
                WriteAttribute(writer, "id", effect.Id);
                writer.WriteAttributeString("kind", effect.Kind.ToString());
                WriteAttribute(writer, "targetId", effect.TargetId);
                WriteAttribute(writer, "itemId", effect.ItemId);
                writer.WriteAttributeString("quantity", effect.Quantity.ToString(CultureInfo.InvariantCulture));
                WriteAttribute(writer, "weatherState", effect.WeatherState);
                writer.WriteAttributeString("durationHours", effect.DurationHours.ToString(CultureInfo.InvariantCulture));
                WriteAttribute(writer, "survivorId", effect.SurvivorId);
                WriteAttribute(writer, "questId", effect.QuestId);
                WriteAttribute(writer, "objectId", effect.ObjectId);
                WriteAttribute(writer, "bunkerExpansionId", effect.BunkerExpansionId);
                WriteAttribute(writer, "flagId", effect.FlagId);
                WriteAttribute(writer, "flagValue", effect.FlagValue);
                WriteProperties(writer, "Properties", effect.Properties);
                writer.WriteEndElement();
            }
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

        private static ScenarioScheduleTime ReadScheduleTime(XmlElement element)
        {
            ScenarioScheduleTime time = new ScenarioScheduleTime();
            if (element == null)
                return time;

            time.Day = ReadIntAttribute(element, "day", time.Day);
            time.Hour = ReadIntAttribute(element, "hour", time.Hour);
            time.Minute = ReadIntAttribute(element, "minute", time.Minute);
            return time;
        }

        private static ScenarioScheduleTime ReadOptionalScheduleTime(XmlElement element)
        {
            return element != null ? ReadScheduleTime(element) : null;
        }

        private static void WriteScheduleTime(XmlWriter writer, string name, ScenarioScheduleTime time)
        {
            if (time == null)
                time = new ScenarioScheduleTime();

            writer.WriteStartElement(name);
            writer.WriteAttributeString("day", time.Day.ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("hour", time.Hour.ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("minute", time.Minute.ToString(CultureInfo.InvariantCulture));
            writer.WriteEndElement();
        }

        private static void WriteOptionalScheduleTime(XmlWriter writer, string name, ScenarioScheduleTime time)
        {
            if (time != null)
                WriteScheduleTime(writer, name, time);
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

        private static void WriteStringList(XmlWriter writer, string parentName, string elementName, System.Collections.Generic.List<string> values)
        {
            writer.WriteStartElement(parentName);
            for (int i = 0; values != null && i < values.Count; i++)
                WriteElement(writer, elementName, values[i]);
            writer.WriteEndElement();
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

        private static void ReadModDependencyList(XmlElement parent, System.Collections.Generic.List<ScenarioModDependencyDefinition> target)
        {
            if (parent == null || target == null)
                return;

            XmlNodeList nodes = parent.GetElementsByTagName("ModDependency");
            for (int i = 0; i < nodes.Count; i++)
            {
                XmlElement element = nodes[i] as XmlElement;
                if (element == null)
                    continue;

                ScenarioModDependencyDefinition dependency = new ScenarioModDependencyDefinition();
                dependency.ModId = AttributeOrChild(element, "id", "ModId");
                dependency.Version = AttributeOrChild(element, "version", "Version");
                dependency.Kind = ReadEnumAttribute(element, "kind", ScenarioModDependencyKind.Required);
                dependency.Manual = ReadBoolAttribute(element, "manual", true);
                if (!string.IsNullOrEmpty(dependency.ModId))
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

