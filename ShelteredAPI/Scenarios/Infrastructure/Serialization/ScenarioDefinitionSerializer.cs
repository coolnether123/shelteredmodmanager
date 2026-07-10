using System;
using System.Globalization;
using System.IO;
using System.Xml;
using ModAPI.Scenarios;

using ShelteredAPI.Content;
using ShelteredAPI.Hooks;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Domain.Assets;
using ShelteredAPI.Scenarios.Domain.Bunker;
using ShelteredAPI.Scenarios.Domain.Compatibility;
using ShelteredAPI.Scenarios.Domain.Conditions;
using ShelteredAPI.Scenarios.Domain.Effects;
using ShelteredAPI.Scenarios.Domain.Journal;
using ShelteredAPI.Scenarios.Domain.Map;
using ShelteredAPI.Scenarios.Domain.Objects;
using ShelteredAPI.Scenarios.Domain.Scheduling;
namespace ShelteredAPI.Scenarios.Infrastructure.Serialization{
    /// <summary>
    /// XML serializer for persistent scenario definitions. It uses System.Xml only so it
    /// works under the .NET 3.5 runtime used by the Sheltered mod stack.
    /// </summary>
    internal class ScenarioDefinitionSerializer
    {
        public const string DefaultFileName = "scenario.xml";

        public ScenarioDefinition Load(string filePath)
        {
            return LoadUncached(filePath);
        }

        public bool TryLoadWithRecovery(string filePath, out ScenarioDefinition definition, out string recoveryMessage, out bool recovered)
        {
            definition = null;
            recoveryMessage = null;
            recovered = false;

            if (string.IsNullOrEmpty(filePath))
            {
                recoveryMessage = "Scenario file path is required.";
                return false;
            }

            try
            {
                definition = LoadUncached(filePath);
                return true;
            }
            catch (Exception primaryError)
            {
                string backupPath = filePath + ".bak";
                if (!File.Exists(backupPath))
                {
                    recoveryMessage = "Scenario XML could not be loaded and no recovery copy exists: " + primaryError.Message;
                    return false;
                }

                try
                {
                    ScenarioDefinition backupDefinition = LoadUncached(backupPath);
                    string corruptPath;
                    RestoreBackupOverUnreadablePrimary(filePath, backupPath, out corruptPath);
                    definition = backupDefinition;
                    recovered = true;
                    recoveryMessage = "Recovered the scenario draft from the last good backup. The unreadable XML was kept at " + corruptPath + ".";
                    return true;
                }
                catch (Exception recoveryError)
                {
                    recoveryMessage = "Scenario XML and its recovery copy could not be loaded. Primary error: "
                        + primaryError.Message + " Recovery error: " + recoveryError.Message;
                    return false;
                }
            }
        }

        internal ScenarioDefinition LoadUncached(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentException("Scenario file path is required.", "filePath");

            try
            {
                XmlDocument document = CreateDocument();
                using (XmlReader reader = XmlReader.Create(filePath, CreateReaderSettings()))
                {
                    document.Load(reader);
                }
                return ReadDocument(document);
            }
            catch (Exception ex)
            {
                string backupPath = filePath + ".bak";
                if (File.Exists(backupPath))
                {
                    throw new IOException(
                        "Scenario XML could not be loaded from '" + filePath + "'. A backup is available at '" + backupPath + "'. Restore it manually or fix the XML before retrying. " + ex.Message,
                        ex);
                }

                throw;
            }
        }

        public ScenarioDefinition FromXml(string xml)
        {
            if (xml == null)
                throw new ArgumentNullException("xml");

            XmlDocument document = CreateDocument();
            using (StringReader stringReader = new StringReader(xml))
            {
                using (XmlReader reader = XmlReader.Create(stringReader, CreateReaderSettings()))
                {
                    document.Load(reader);
                }
            }
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

            string tempPath = BuildTempPath(filePath);
            XmlWriterSettings settings = new XmlWriterSettings();
            settings.Indent = true;
            settings.Encoding = System.Text.Encoding.UTF8;

            try
            {
                using (FileStream stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    using (XmlWriter writer = XmlWriter.Create(stream, settings))
                    {
                        WriteDocument(definition, writer);
                    }

                    stream.Flush();
                }

                Load(tempPath);
                ReplaceValidatedTempFile(tempPath, filePath);
                tempPath = null;
            }
            finally
            {
                if (!string.IsNullOrEmpty(tempPath))
                    TryDeleteFile(tempPath);
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
            ScenarioDefinitionMetadata metadata;
            if (ScenarioDefinitionMetadataCache.TryLoad(this, filePath, ownerModId, out metadata) && metadata != null)
                return metadata.Info;

            ScenarioDefinition definition = LoadUncached(filePath);
            return new ScenarioInfo(
                definition.Id,
                definition.DisplayName,
                definition.Author,
                definition.Version,
                filePath,
                ownerModId);
        }

        private static XmlDocument CreateDocument()
        {
            XmlDocument document = new XmlDocument();
            document.XmlResolver = null;
            return document;
        }

        private static string BuildTempPath(string filePath)
        {
            string directory = Path.GetDirectoryName(filePath);
            if (string.IsNullOrEmpty(directory))
                directory = Directory.GetCurrentDirectory();

            string name = Path.GetFileName(filePath);
            return Path.Combine(directory, name + "." + Guid.NewGuid().ToString("N") + ".tmp");
        }

        private void RestoreBackupOverUnreadablePrimary(string filePath, string backupPath, out string corruptPath)
        {
            corruptPath = BuildCorruptPath(filePath);
            string tempPath = BuildTempPath(filePath);
            try
            {
                File.Copy(backupPath, tempPath, true);
                LoadUncached(tempPath);
                if (File.Exists(filePath))
                {
                    try
                    {
                        File.Replace(tempPath, filePath, corruptPath, false);
                        tempPath = null;
                        return;
                    }
                    catch (PlatformNotSupportedException)
                    {
                    }
                    catch (NotSupportedException)
                    {
                    }
                }

                RestoreBackupWithCopyFallback(tempPath, filePath, backupPath, corruptPath);
                tempPath = null;
            }
            finally
            {
                if (!string.IsNullOrEmpty(tempPath))
                    TryDeleteFile(tempPath);
            }
        }

        private static void RestoreBackupWithCopyFallback(string tempPath, string filePath, string backupPath, string corruptPath)
        {
            if (File.Exists(filePath))
                File.Copy(filePath, corruptPath, true);

            try
            {
                if (File.Exists(filePath))
                    File.Delete(filePath);
                File.Move(tempPath, filePath);
            }
            catch
            {
                if (File.Exists(backupPath))
                    File.Copy(backupPath, filePath, true);
                throw;
            }
        }

        private static string BuildCorruptPath(string filePath)
        {
            string directory = Path.GetDirectoryName(filePath);
            if (string.IsNullOrEmpty(directory))
                directory = Directory.GetCurrentDirectory();

            string name = Path.GetFileName(filePath);
            string stamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
            string path = Path.Combine(directory, name + ".corrupt_" + stamp);
            while (File.Exists(path))
                path = Path.Combine(directory, name + ".corrupt_" + stamp + "_" + Guid.NewGuid().ToString("N").Substring(0, 6));
            return path;
        }

        private static void ReplaceValidatedTempFile(string tempPath, string filePath)
        {
            if (File.Exists(filePath))
            {
                string backupPath = filePath + ".bak";
                try
                {
                    File.Replace(tempPath, filePath, backupPath, false);
                    return;
                }
                catch (PlatformNotSupportedException)
                {
                    ReplaceWithBackupFallback(tempPath, filePath, backupPath);
                    return;
                }
                catch (NotSupportedException)
                {
                    ReplaceWithBackupFallback(tempPath, filePath, backupPath);
                    return;
                }
            }

            File.Move(tempPath, filePath);
        }

        private static void ReplaceWithBackupFallback(string tempPath, string filePath, string backupPath)
        {
            bool backupCreated = false;
            if (File.Exists(filePath))
            {
                File.Copy(filePath, backupPath, true);
                backupCreated = true;
                File.Delete(filePath);
            }

            try
            {
                File.Move(tempPath, filePath);
            }
            catch
            {
                if (backupCreated && File.Exists(backupPath) && !File.Exists(filePath))
                    File.Copy(backupPath, filePath, true);
                throw;
            }
        }

        private static void TryDeleteFile(string path)
        {
            try
            {
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                    File.Delete(path);
            }
            catch
            {
            }
        }

        private static XmlReaderSettings CreateReaderSettings()
        {
            XmlReaderSettings settings = new XmlReaderSettings();
            settings.ProhibitDtd = true;
            settings.XmlResolver = null;
            return settings;
        }

        private static ScenarioDefinition ReadDocument(XmlDocument document)
        {
            if (document == null || document.DocumentElement == null || document.DocumentElement.Name != "Scenario")
                throw new FormatException("Scenario XML must have a <Scenario> root element.");

            XmlElement root = document.DocumentElement;
            ScenarioDefinition definition = new ScenarioDefinition();
            IScenarioSectionSerializer<FamilySetupDefinition> familySerializer = new FamilyScenarioSectionSerializer();
            IScenarioSectionSerializer<StartingInventoryDefinition> inventorySerializer = new InventoryScenarioSectionSerializer();
            IScenarioSectionSerializer<BunkerEditsDefinition> bunkerEditsSerializer = new BunkerEditsScenarioSectionSerializer();
            IScenarioSectionSerializer<TriggersAndEventsDefinition> triggerSerializer = new TriggerEventScenarioSectionSerializer();
            QuestMapScenarioSectionSerializer questMapSerializer = new QuestMapScenarioSectionSerializer();
            IScenarioSectionSerializer<WinLossConditionsDefinition> winLossSerializer = new WinLossScenarioSectionSerializer();
            IScenarioSectionSerializer<AssetReferencesDefinition> assetSerializer = new AssetReferenceScenarioSectionSerializer();
            IScenarioSectionSerializer<ScenarioBunkerGridDefinition> bunkerGridSerializer = new BunkerGridScenarioSectionSerializer();
            GateConditionScenarioSectionSerializer gateSerializer = new GateConditionScenarioSectionSerializer();
            ScheduledActionScenarioSectionSerializer scheduledSerializer = new ScheduledActionScenarioSectionSerializer();

            XmlElement meta = Child(root, "Meta");
            definition.Id = ReadText(meta, "Id");
            definition.DisplayName = ScenarioMetadataDefaults.ForLoad(ReadText(meta, "DisplayName"), ScenarioMetadataDefaults.DefaultTitle);
            definition.Description = ReadText(meta, "Description");
            definition.Goal = ReadText(meta, "Goal");
            definition.Author = ScenarioMetadataDefaults.ForLoad(ReadText(meta, "Author"), ScenarioMetadataDefaults.DefaultAuthor);
            definition.Version = ScenarioMetadataDefaults.ForLoad(ReadText(meta, "Version"), ScenarioMetadataDefaults.DefaultVersion);
            definition.Credits = ReadText(meta, "Credits");
            ReadStringList(Child(meta, "Tags"), "Tag", definition.Tags);

            XmlElement dependencies = Child(root, "Dependencies");
            if (dependencies != null)
            {
                ReadDependencyList(dependencies, definition.Dependencies);
                ReadModDependencyList(dependencies, definition.ModDependencies);
            }

            definition.BaseGameMode = ReadEnum(root, "BaseMode", ScenarioBaseGameMode.Survival);
            definition.BaseFamilyChoice = ReadText(root, "BaseFamilyChoice");
            definition.SeedOverride = ReadNullableLong(root, "SeedOverride");
            XmlElement selectionRules = Child(root, "SelectionRules");
            definition.SelectionRules = selectionRules != null
                ? ReadSelectionRules(selectionRules)
                : ScenarioSelectionRulesDefinition.ForBaseMode(definition.BaseGameMode);
            ReadScenarioCharacters(Child(root, "ScenarioCharacters"), definition.ScenarioCharacters);
            definition.ScenarioFlow = ReadScenarioFlow(Child(root, "ScenarioFlow"));
            definition.Conversations = ReadConversations(Child(root, "Conversations"));
            definition.VanillaSuppression = ReadVanillaSuppression(Child(root, "VanillaSuppression"));
            definition.FamilySetup = familySerializer.Read(Child(root, "FamilySetup"));
            definition.StartingInventory = inventorySerializer.Read(Child(root, "StartingInventory"));
            definition.BunkerEdits = bunkerEditsSerializer.Read(Child(root, "BunkerEdits"));
            definition.TriggersAndEvents = triggerSerializer.Read(Child(root, "TriggersAndEvents"));
            definition.Quests = questMapSerializer.ReadQuests(Child(root, "Quests"));
            definition.Map = questMapSerializer.ReadMap(Child(root, "Map"));
            definition.WinLossConditions = winLossSerializer.Read(Child(root, "WinLossConditions"));
            definition.Scoring = ReadScoring(Child(root, "Scoring"));
            definition.AssetReferences = assetSerializer.Read(Child(root, "AssetReferences"));
            definition.BunkerGrid = bunkerGridSerializer.Read(Child(root, "BunkerGrid"));
            XmlElement backendWorlds = Child(root, "BackendWorlds");
            definition.BackendWorlds = ReadBackendWorlds(backendWorlds);
            if (backendWorlds == null)
                ScenarioBackendWorldMaterializer.MigrateLegacyCurrentWorld(definition);
            else
                ScenarioBackendWorldMaterializer.MaterializeCurrentWorld(definition, definition.BaseGameMode);
            gateSerializer.Read(Child(root, "Gates"), definition.Gates);
            scheduledSerializer.Read(Child(root, "ScheduledActions"), definition.ScheduledActions);
            definition.Journal = ReadJournal(Child(root, "Journal"));
            return definition;
        }

        private static ScenarioSelectionRulesDefinition ReadSelectionRules(XmlElement element)
        {
            ScenarioSelectionRulesDefinition rules = new ScenarioSelectionRulesDefinition();
            if (element == null)
                return rules;

            rules.Weight = ReadFloatAttribute(element, "weight", rules.Weight);
            rules.StartDay = ReadIntAttribute(element, "startDay", rules.StartDay);
            rules.TimeoutDays = ReadIntAttribute(element, "timeoutDays", rules.TimeoutDays);
            rules.MaxSimultaneousInstances = ReadIntAttribute(element, "maxSimultaneousInstances", rules.MaxSimultaneousInstances);
            rules.OnceOnly = ReadBoolAttribute(element, "onceOnly", rules.OnceOnly);
            rules.DiscoverByRadio = ReadBoolAttribute(element, "discoverByRadio", rules.DiscoverByRadio);

            XmlElement availability = Child(element, "Availability");
            if (availability != null)
            {
                rules.Availability.Survival = ReadBoolAttribute(availability, "survival", rules.Availability.Survival);
                rules.Availability.Surrounded = ReadBoolAttribute(availability, "surrounded", rules.Availability.Surrounded);
                rules.Availability.Stasis = ReadBoolAttribute(availability, "stasis", rules.Availability.Stasis);
            }

            ReadStringList(Child(element, "PrerequisiteMilestones"), "Milestone", rules.PrerequisiteMilestones);
            return rules;
        }

        private static void ReadScenarioCharacters(XmlElement element, System.Collections.Generic.List<ScenarioNpcDefinition> target)
        {
            if (element == null || target == null)
                return;

            XmlNodeList nodes = element.GetElementsByTagName("Character");
            for (int i = 0; i < nodes.Count; i++)
            {
                XmlElement node = nodes[i] as XmlElement;
                if (node == null)
                    continue;

                ScenarioNpcDefinition character = new ScenarioNpcDefinition();
                character.CharacterId = AttributeOrChild(node, "id", "Id");
                character.DisplayName = AttributeOrChild(node, "displayName", "DisplayName");
                character.ActorRef = ScenarioActorXmlSerializer.ReadActorRef(node);
                ScenarioActorXmlSerializer.ReadActorComponents(node, character.ActorComponents);
                character.PresetId = AttributeOrChild(node, "presetId", "PresetId");
                character.WeaponItemId = AttributeOrChild(node, "weapon", "Weapon");
                character.EquippedItem1Id = AttributeOrChild(node, "equippedItem1", "EquippedItem1");
                character.EquippedItem2Id = AttributeOrChild(node, "equippedItem2", "EquippedItem2");
                character.Personality = AttributeOrChild(node, "personality", "Personality");
                character.NumRandomItems = ReadIntAttribute(node, "numRandomItems", 0);
                character.StatSetting = AttributeOrChild(node, "statSetting", "StatSetting");
                character.BackgroundNpc = ReadBoolAttribute(node, "backgroundNpc", false);
                character.FlipMesh = ReadBoolAttribute(node, "flipMesh", false);
                character.Species = AttributeOrChild(node, "species", "Species");
                character.AvatarOverrideSpriteId = AttributeOrChild(node, "avatarOverrideSpriteId", "AvatarOverrideSpriteId");
                character.Stats = ReadScenarioNpcStats(Child(node, "Stats"));
                ReadItemEntries(Child(node, "CarriedItems"), "Item", character.CarriedItems);
                target.Add(character);
            }
        }

        private static ScenarioNpcStatsDefinition ReadScenarioNpcStats(XmlElement element)
        {
            ScenarioNpcStatsDefinition stats = new ScenarioNpcStatsDefinition();
            if (element == null)
                return stats;

            stats.Strength = ReadIntAttribute(element, "strength", 0);
            stats.Dexterity = ReadIntAttribute(element, "dexterity", 0);
            stats.Charisma = ReadIntAttribute(element, "charisma", 0);
            stats.Perception = ReadIntAttribute(element, "perception", 0);
            stats.Intelligence = ReadIntAttribute(element, "intelligence", 0);
            return stats;
        }

        private static ScenarioFlowDefinition ReadScenarioFlow(XmlElement element)
        {
            ScenarioFlowDefinition flow = new ScenarioFlowDefinition();
            if (element == null)
                return flow;

            XmlElement stages = Child(element, "Stages") ?? element;
            XmlNodeList nodes = stages.GetElementsByTagName("Stage");
            for (int i = 0; i < nodes.Count; i++)
            {
                XmlElement stageNode = nodes[i] as XmlElement;
                if (stageNode == null)
                    continue;

                ScenarioFlowStageDefinition stage = new ScenarioFlowStageDefinition();
                stage.Id = AttributeOrChild(stageNode, "id", "Id");
                stage.UnansweredNextStage = AttributeOrChild(stageNode, "unansweredNextStage", "UnansweredNextStage");
                stage.UnansweredNextDays = ReadIntAttribute(stageNode, "unansweredNextDays", 1);
                stage.PunishOnUnanswered = ReadBoolAttribute(stageNode, "punishOnUnanswered", false);
                ReadStringList(Child(stageNode, "CharacterIds"), "CharacterId", stage.CharacterIds);
                ReadIntercomStages(Child(stageNode, "IntercomStages"), stage.IntercomStages);
                flow.Stages.Add(stage);
            }

            return flow;
        }

        private static void ReadIntercomStages(XmlElement element, System.Collections.Generic.List<ScenarioIntercomStageDefinition> target)
        {
            if (element == null || target == null)
                return;

            XmlNodeList nodes = element.GetElementsByTagName("IntercomStage");
            for (int i = 0; i < nodes.Count; i++)
            {
                XmlElement node = nodes[i] as XmlElement;
                if (node == null)
                    continue;

                ScenarioIntercomStageDefinition stage = new ScenarioIntercomStageDefinition();
                stage.Id = AttributeOrChild(node, "id", "Id");
                stage.Type = AttributeOrChild(node, "type", "Type");
                stage.NextId = AttributeOrChild(node, "nextId", "NextId");
                stage.AlternateNextId = AttributeOrChild(node, "alternateNextId", "AlternateNextId");
                ReadDialogueLines(Child(node, "Dialogue"), stage.Dialogue);
                ReadDialogueOptions(Child(node, "Options"), stage.Options);
                ReadStringList(Child(node, "RandomizedNextIds"), "NextId", stage.RandomizedNextIds);
                ReadItemEntries(Child(node, "Items"), "Item", stage.Items);
                ReadItemEntries(Child(node, "ItemsToRemove"), "Item", stage.ItemsToRemove);
                stage.EndOptions = ReadEndOptions(Child(node, "EndOptions"));
                ReadStringList(Child(node, "SubquestsToActivate"), "SubquestId", stage.SubquestsToActivate);
                stage.SubquestCheck = ReadSubquestCheck(Child(node, "SubquestCheck"));
                ReadMilestones(Child(node, "SetMilestones"), stage.SetMilestones);
                ReadMilestoneChecks(Child(node, "CheckMilestones"), stage.CheckMilestones);
                stage.StageChange = ReadStageChange(Child(node, "StageChange"));
                XmlElement description = Child(node, "StageDescription");
                stage.StageDescriptionKey = description != null ? AttributeOrChild(description, "key", "Key") : null;
                ReadStringList(Child(node, "CharacterIdsToRecruit"), "CharacterId", stage.CharacterIdsToRecruit);
                stage.RecruitAsFamily = ReadBoolAttribute(node, "recruitAsFamily", false);
                target.Add(stage);
            }
        }

        private static void ReadDialogueLines(XmlElement element, System.Collections.Generic.List<ScenarioDialogueLineDefinition> target)
        {
            if (element == null || target == null)
                return;

            XmlNodeList nodes = element.GetElementsByTagName("Line");
            for (int i = 0; i < nodes.Count; i++)
            {
                XmlElement node = nodes[i] as XmlElement;
                if (node == null)
                    continue;

                target.Add(new ScenarioDialogueLineDefinition
                {
                    Character = AttributeOrChild(node, "character", "Character"),
                    TextKey = AttributeOrChild(node, "textKey", "TextKey")
                });
            }
        }

        private static void ReadDialogueOptions(XmlElement element, System.Collections.Generic.List<ScenarioDialogueOptionDefinition> target)
        {
            if (element == null || target == null)
                return;

            XmlNodeList nodes = element.GetElementsByTagName("Option");
            for (int i = 0; i < nodes.Count; i++)
            {
                XmlElement node = nodes[i] as XmlElement;
                if (node == null)
                    continue;

                target.Add(new ScenarioDialogueOptionDefinition
                {
                    TextKey = AttributeOrChild(node, "textKey", "TextKey"),
                    NextId = AttributeOrChild(node, "nextId", "NextId")
                });
            }
        }

        private static ScenarioEncounterEndOptionsDefinition ReadEndOptions(XmlElement element)
        {
            ScenarioEncounterEndOptionsDefinition options = new ScenarioEncounterEndOptionsDefinition();
            if (element == null)
                return options;

            options.Type = AttributeOrChild(element, "type", "Type");
            options.CombatResult = AttributeOrChild(element, "combatResult", "CombatResult");
            options.CombatWinMilestone = AttributeOrChild(element, "combatWinMilestone", "CombatWinMilestone");
            options.CombatLossMilestone = AttributeOrChild(element, "combatLossMilestone", "CombatLossMilestone");
            options.AddVehicle = ReadBoolAttribute(element, "addVehicle", false);
            options.MoralOutcome = AttributeOrChild(element, "moralOutcome", "MoralOutcome");
            options.MoralOutcomeCombatWon = AttributeOrChild(element, "moralOutcomeCombatWon", "MoralOutcomeCombatWon");
            options.MoralOutcomeCombatLost = AttributeOrChild(element, "moralOutcomeCombatLost", "MoralOutcomeCombatLost");
            options.AddSurroundedCharacterOutcome = AttributeOrChild(element, "addSurroundedCharacterOutcome", "AddSurroundedCharacterOutcome");
            options.RevealSurroundedMapRegionOption = AttributeOrChild(element, "revealSurroundedMapRegionOption", "RevealSurroundedMapRegionOption");
            options.OverrideTradeItems = ReadBoolAttribute(element, "overrideTradeItems", false);
            options.MinRandomTradeItems = ReadIntAttribute(element, "minRandomTradeItems", 0);
            options.MaxRandomTradeItems = ReadIntAttribute(element, "maxRandomTradeItems", 0);
            options.CompleteQuest = ReadBoolAttribute(element, "completeQuest", false);
            options.CompleteParentScenario = ReadBoolAttribute(element, "completeParentScenario", false);
            ReadItemEntries(Child(element, "RewardItems"), "Item", options.RewardItems);
            ReadItemEntries(Child(element, "TradeItems"), "Item", options.TradeItems);
            ReadFloatingQuestTriggers(Child(element, "TriggerFloatingQuests"), options.TriggerFloatingQuests);
            ReadSpawnTriggers(Child(element, "SpawnScenarios"), options.SpawnScenarios);
            return options;
        }

        private static ScenarioSubquestCheckDefinition ReadSubquestCheck(XmlElement element)
        {
            ScenarioSubquestCheckDefinition check = new ScenarioSubquestCheckDefinition();
            if (element == null)
                return check;

            check.Check = AttributeOrChild(element, "check", "Check");
            ReadStringList(Child(element, "Subquests"), "SubquestId", check.Subquests);
            return check;
        }

        private static ScenarioStageChangeDefinition ReadStageChange(XmlElement element)
        {
            ScenarioStageChangeDefinition change = new ScenarioStageChangeDefinition();
            if (element == null)
                return change;

            change.Id = AttributeOrChild(element, "id", "Id");
            change.DelayDays = ReadIntAttribute(element, "delayDays", ReadIntAttribute(element, "delay", 0));
            return change;
        }

        private static void ReadMilestones(XmlElement element, System.Collections.Generic.List<ScenarioMilestoneDefinition> target)
        {
            if (element == null || target == null)
                return;

            XmlNodeList nodes = element.GetElementsByTagName("Milestone");
            for (int i = 0; i < nodes.Count; i++)
            {
                XmlElement node = nodes[i] as XmlElement;
                if (node == null)
                    continue;

                target.Add(new ScenarioMilestoneDefinition
                {
                    Name = AttributeOrChild(node, "name", "Name"),
                    Scope = AttributeOrChild(node, "scope", "Scope"),
                    Action = AttributeOrChild(node, "action", "Action")
                });
            }
        }

        private static void ReadMilestoneChecks(XmlElement element, System.Collections.Generic.List<ScenarioMilestoneCheckDefinition> target)
        {
            if (element == null || target == null)
                return;

            XmlNodeList nodes = element.GetElementsByTagName("Milestone");
            for (int i = 0; i < nodes.Count; i++)
            {
                XmlElement node = nodes[i] as XmlElement;
                if (node == null)
                    continue;

                target.Add(new ScenarioMilestoneCheckDefinition
                {
                    Name = AttributeOrChild(node, "name", "Name"),
                    Scope = AttributeOrChild(node, "scope", "Scope")
                });
            }
        }

        private static void ReadFloatingQuestTriggers(XmlElement element, System.Collections.Generic.List<ScenarioFloatingQuestTriggerDefinition> target)
        {
            if (element == null || target == null)
                return;

            XmlNodeList nodes = element.GetElementsByTagName("FloatingQuest");
            for (int i = 0; i < nodes.Count; i++)
            {
                XmlElement node = nodes[i] as XmlElement;
                if (node == null)
                    continue;

                target.Add(new ScenarioFloatingQuestTriggerDefinition
                {
                    Id = AttributeOrChild(node, "id", "Id"),
                    ActivationDelayDays = ReadFloatAttribute(node, "activationDelayDays", 2f),
                    DurationDays = ReadFloatAttribute(node, "durationDays", 5f)
                });
            }
        }

        private static void ReadSpawnTriggers(XmlElement element, System.Collections.Generic.List<ScenarioSpawnTriggerDefinition> target)
        {
            if (element == null || target == null)
                return;

            XmlNodeList nodes = element.GetElementsByTagName("Scenario");
            for (int i = 0; i < nodes.Count; i++)
            {
                XmlElement node = nodes[i] as XmlElement;
                if (node == null)
                    continue;

                target.Add(new ScenarioSpawnTriggerDefinition
                {
                    Id = AttributeOrChild(node, "id", "Id"),
                    SpawnChance = ReadFloatAttribute(node, "spawnChance", 100f),
                    DelayDays = ReadIntAttribute(node, "delayDays", 1)
                });
            }
        }

        private static void ReadItemEntries(XmlElement element, string childName, System.Collections.Generic.List<ItemEntry> target)
        {
            if (element == null || target == null)
                return;

            XmlNodeList nodes = element.GetElementsByTagName(childName);
            for (int i = 0; i < nodes.Count; i++)
            {
                XmlElement node = nodes[i] as XmlElement;
                if (node == null)
                    continue;

                target.Add(new ItemEntry
                {
                    ItemId = AttributeOrChild(node, "id", "Id"),
                    Quantity = ReadIntAttribute(node, "quantity", 0)
                });
            }
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
                            WireSpriteIndex = ReadNullableIntAttribute(roomElement, "wireSpriteIndex"),
                            WallRuntimeSpriteKey = AttributeOrChild(roomElement, "wallRuntimeSpriteKey", "WallRuntimeSpriteKey"),
                            WireRuntimeSpriteKey = AttributeOrChild(roomElement, "wireRuntimeSpriteKey", "WireRuntimeSpriteKey"),
                            WallCleared = ReadBoolAttribute(roomElement, "wallCleared", false),
                            WireCleared = ReadBoolAttribute(roomElement, "wireCleared", false)
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

        internal static ScenarioScoringDefinition ReadScoring(XmlElement element)
        {
            ScenarioScoringDefinition result = new ScenarioScoringDefinition();
            if (element == null)
                return result;

            result.Enabled = ReadBoolAttribute(element, "enabled", result.Enabled);
            result.ScoreLabel = AttributeOrChild(element, "scoreLabel", "ScoreLabel") ?? result.ScoreLabel;
            result.HigherIsBetter = ReadBoolAttribute(element, "higherIsBetter", result.HigherIsBetter);
            result.LeaderboardKey = AttributeOrChild(element, "leaderboardKey", "LeaderboardKey");

            XmlElement categories = Child(element, "Categories");
            if (categories != null)
            {
                XmlNodeList nodes = categories.GetElementsByTagName("Category");
                for (int i = 0; i < nodes.Count; i++)
                {
                    XmlElement categoryElement = nodes[i] as XmlElement;
                    if (categoryElement == null || categoryElement.ParentNode != categories)
                        continue;

                    result.Categories.Add(new ScenarioScoreCategoryDefinition
                    {
                        Id = AttributeOrChild(categoryElement, "id", "Id"),
                        DisplayName = AttributeOrChild(categoryElement, "displayName", "DisplayName"),
                        Description = AttributeOrChild(categoryElement, "description", "Description"),
                        SortOrder = ReadIntAttribute(categoryElement, "sortOrder", 0)
                    });
                }
            }

            XmlElement rules = Child(element, "Rules");
            if (rules != null)
            {
                XmlNodeList nodes = rules.GetElementsByTagName("Rule");
                for (int i = 0; i < nodes.Count; i++)
                {
                    XmlElement ruleElement = nodes[i] as XmlElement;
                    if (ruleElement == null || ruleElement.ParentNode != rules)
                        continue;

                    ScenarioScoreRuleDefinition rule = new ScenarioScoreRuleDefinition();
                    rule.Id = AttributeOrChild(ruleElement, "id", "Id");
                    rule.CategoryId = AttributeOrChild(ruleElement, "categoryId", "CategoryId");
                    rule.DisplayName = AttributeOrChild(ruleElement, "displayName", "DisplayName");
                    rule.Description = AttributeOrChild(ruleElement, "description", "Description");
                    rule.Source = AttributeOrChild(ruleElement, "source", "Source");
                    rule.Operation = AttributeOrChild(ruleElement, "operation", "Operation") ?? rule.Operation;
                    rule.OutcomeFilter = AttributeOrChild(ruleElement, "outcomeFilter", "OutcomeFilter") ?? rule.OutcomeFilter;
                    rule.Weight = ReadFloatAttribute(ruleElement, "weight", rule.Weight);
                    ReadProperties(Child(ruleElement, "Properties"), rule.Properties);
                    result.Rules.Add(rule);
                }
            }

            ReadProperties(Child(element, "Metadata"), result.Metadata);
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
                quest.ScheduledStart = ReadOptionalScheduleTime(Child(questElement, "ScheduledStart"));
                ReadProperties(Child(questElement, "Properties"), quest.Properties);
                result.Quests.Add(quest);
            }

            return result;
        }

        internal static MapAuthoringDefinition ReadMap(XmlElement element)
        {
            return new ScenarioMapXmlSerializer().Read(element);
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
                            PatchId = AttributeOrChild(spriteElement, "patchId", "PatchId"),
                            UserOwned = ReadBoolAttribute(spriteElement, "userOwned", false)
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
                        AnimationFrameIndex = ReadNullableIntAttribute(swapElement, "animationFrameIndex"),
                        AnimationFrameRuntimeSpriteKey = AttributeOrChild(swapElement, "animationFrameRuntimeSpriteKey", "AnimationFrameRuntimeSpriteKey"),
                        Day = ReadNullableIntAttribute(swapElement, "day"),
                        TargetComponent = ReadEnumAttribute(swapElement, "targetComponent", ScenarioSpriteTargetComponentKind.Auto)
                    });
                }
            }

            XmlElement scenePlacements = Child(element, "SceneSpritePlacements");
            ReadSceneSpritePlacements(scenePlacements, result.SceneSpritePlacements);

            XmlElement assetCredits = Child(element, "AssetCredits");
            if (assetCredits != null)
            {
                XmlNodeList creditNodes = assetCredits.GetElementsByTagName("Credit");
                for (int i = 0; i < creditNodes.Count; i++)
                {
                    XmlElement creditElement = creditNodes[i] as XmlElement;
                    if (creditElement != null)
                    {
                        result.AssetCredits.Add(new ScenarioAssetCreditDefinition
                        {
                            RelativePath = AttributeOrChild(creditElement, "path", "Path"),
                            Credit = AttributeOrChild(creditElement, "note", "Note")
                        });
                    }
                }
            }

            return result;
        }

        private static void ReadSceneSpritePlacements(XmlElement scenePlacements, System.Collections.Generic.List<SceneSpritePlacement> target)
        {
            if (scenePlacements == null || target == null)
                return;

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
                target.Add(placement);
            }
        }

        private static ScenarioBackendWorldsDefinition ReadBackendWorlds(XmlElement element)
        {
            ScenarioBackendWorldsDefinition backendWorlds = new ScenarioBackendWorldsDefinition();
            if (element == null)
                return backendWorlds;

            XmlNodeList nodes = element.GetElementsByTagName("BackendWorld");
            for (int i = 0; i < nodes.Count; i++)
            {
                XmlElement worldElement = nodes[i] as XmlElement;
                if (worldElement == null)
                    continue;

                ScenarioBackendWorldDefinition world = new ScenarioBackendWorldDefinition();
                world.BaseMode = ReadEnumAttribute(worldElement, "baseMode", ScenarioBaseGameMode.Survival);
                world.BunkerEdits = ReadBunkerEdits(Child(worldElement, "BunkerEdits"));
                world.BunkerGrid = ReadBunkerGrid(Child(worldElement, "BunkerGrid"));
                world.SceneSpritePlacements.Clear();
                ReadSceneSpritePlacements(Child(worldElement, "SceneSpritePlacements"), world.SceneSpritePlacements);

                ScenarioBackendWorldDefinition existing = backendWorlds.Find(world.BaseMode);
                if (existing == null)
                    backendWorlds.Worlds.Add(world);
                else
                {
                    existing.BunkerEdits = world.BunkerEdits;
                    existing.BunkerGrid = world.BunkerGrid;
                    existing.SceneSpritePlacements.Clear();
                    for (int placementIndex = 0; placementIndex < world.SceneSpritePlacements.Count; placementIndex++)
                        existing.SceneSpritePlacements.Add(world.SceneSpritePlacements[placementIndex]);
                }
            }

            return backendWorlds;
        }

        private static void WriteDocument(ScenarioDefinition definition, XmlWriter writer)
        {
            ScenarioBackendWorldMaterializer.StoreCurrentWorld(definition);

            writer.WriteStartDocument();
            writer.WriteStartElement("Scenario");
            IScenarioSectionSerializer<FamilySetupDefinition> familySerializer = new FamilyScenarioSectionSerializer();
            IScenarioSectionSerializer<StartingInventoryDefinition> inventorySerializer = new InventoryScenarioSectionSerializer();
            IScenarioSectionSerializer<BunkerEditsDefinition> bunkerEditsSerializer = new BunkerEditsScenarioSectionSerializer();
            IScenarioSectionSerializer<TriggersAndEventsDefinition> triggerSerializer = new TriggerEventScenarioSectionSerializer();
            QuestMapScenarioSectionSerializer questMapSerializer = new QuestMapScenarioSectionSerializer();
            IScenarioSectionSerializer<WinLossConditionsDefinition> winLossSerializer = new WinLossScenarioSectionSerializer();
            IScenarioSectionSerializer<AssetReferencesDefinition> assetSerializer = new AssetReferenceScenarioSectionSerializer();
            IScenarioSectionSerializer<ScenarioBunkerGridDefinition> bunkerGridSerializer = new BunkerGridScenarioSectionSerializer();
            GateConditionScenarioSectionSerializer gateSerializer = new GateConditionScenarioSectionSerializer();
            ScheduledActionScenarioSectionSerializer scheduledSerializer = new ScheduledActionScenarioSectionSerializer();

            writer.WriteStartElement("Meta");
            WriteElement(writer, "Id", definition.Id);
            WriteElement(writer, "DisplayName", definition.DisplayName);
            WriteElement(writer, "Description", definition.Description);
            if (!string.IsNullOrEmpty(definition.Goal))
                WriteElement(writer, "Goal", definition.Goal);
            WriteElement(writer, "Author", definition.Author);
            WriteElement(writer, "Version", definition.Version);
            WriteElement(writer, "Credits", definition.Credits);
            WriteStringList(writer, "Tags", "Tag", definition.Tags);
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
            if (!string.IsNullOrEmpty(definition.BaseFamilyChoice))
                WriteElement(writer, "BaseFamilyChoice", definition.BaseFamilyChoice.ToString());
            if (definition.SeedOverride.HasValue)
                WriteElement(writer, "SeedOverride", definition.SeedOverride.Value.ToString(CultureInfo.InvariantCulture));
            WriteSelectionRules(writer, definition.SelectionRules, definition.BaseGameMode);
            WriteScenarioCharacters(writer, definition.ScenarioCharacters);
            WriteScenarioFlow(writer, definition.ScenarioFlow);
            WriteConversations(writer, definition.Conversations);
            WriteVanillaSuppression(writer, definition.VanillaSuppression);

            familySerializer.Write(writer, definition.FamilySetup);
            inventorySerializer.Write(writer, definition.StartingInventory);
            bunkerEditsSerializer.Write(writer, definition.BunkerEdits);
            triggerSerializer.Write(writer, definition.TriggersAndEvents);
            questMapSerializer.WriteQuests(writer, definition.Quests);
            questMapSerializer.WriteMap(writer, definition.Map);
            winLossSerializer.Write(writer, definition.WinLossConditions);
            WriteScoring(writer, definition.Scoring);
            assetSerializer.Write(writer, definition.AssetReferences);
            bunkerGridSerializer.Write(writer, definition.BunkerGrid);
            WriteBackendWorlds(writer, definition.BackendWorlds);
            gateSerializer.Write(writer, definition.Gates);
            scheduledSerializer.Write(writer, definition.ScheduledActions);
            WriteJournal(writer, definition.Journal);

            writer.WriteEndElement();
            writer.WriteEndDocument();
        }

        private static ScenarioVanillaSuppressionDefinition ReadVanillaSuppression(XmlElement element)
        {
            ScenarioVanillaSuppressionDefinition suppression = new ScenarioVanillaSuppressionDefinition();
            if (element == null)
                return suppression;

            suppression.RandomVisitors = ReadBoolAttribute(element, "randomVisitors", false);
            suppression.Binman = ReadBoolAttribute(element, "binman", false);
            suppression.Raids = ReadBoolAttribute(element, "raids", false);
            suppression.StasisVisitors = ReadBoolAttribute(element, "stasisVisitors", false);
            suppression.RadioBroadcastOdds = ReadBoolAttribute(element, "radioBroadcastOdds", false);
            return suppression;
        }

        private static void WriteVanillaSuppression(XmlWriter writer, ScenarioVanillaSuppressionDefinition suppression)
        {
            if (suppression == null)
                suppression = new ScenarioVanillaSuppressionDefinition();

            writer.WriteStartElement("VanillaSuppression");
            writer.WriteAttributeString("randomVisitors", suppression.RandomVisitors ? "true" : "false");
            writer.WriteAttributeString("binman", suppression.Binman ? "true" : "false");
            writer.WriteAttributeString("raids", suppression.Raids ? "true" : "false");
            writer.WriteAttributeString("stasisVisitors", suppression.StasisVisitors ? "true" : "false");
            writer.WriteAttributeString("radioBroadcastOdds", suppression.RadioBroadcastOdds ? "true" : "false");
            writer.WriteEndElement();
        }

        private static void WriteSelectionRules(XmlWriter writer, ScenarioSelectionRulesDefinition rules, ScenarioBaseGameMode baseMode)
        {
            if (rules == null)
                rules = ScenarioSelectionRulesDefinition.ForBaseMode(baseMode);
            if (rules.Availability == null)
                rules.Availability = new ScenarioModeAvailabilityDefinition();

            writer.WriteStartElement("SelectionRules");
            writer.WriteAttributeString("weight", rules.Weight.ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("startDay", rules.StartDay.ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("timeoutDays", rules.TimeoutDays.ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("maxSimultaneousInstances", rules.MaxSimultaneousInstances.ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("onceOnly", rules.OnceOnly.ToString());
            writer.WriteAttributeString("discoverByRadio", rules.DiscoverByRadio.ToString());
            writer.WriteStartElement("Availability");
            writer.WriteAttributeString("survival", rules.Availability.Survival.ToString());
            writer.WriteAttributeString("surrounded", rules.Availability.Surrounded.ToString());
            writer.WriteAttributeString("stasis", rules.Availability.Stasis.ToString());
            writer.WriteEndElement();
            WriteStringList(writer, "PrerequisiteMilestones", "Milestone", rules.PrerequisiteMilestones);
            writer.WriteEndElement();
        }

        private static void WriteScenarioCharacters(XmlWriter writer, System.Collections.Generic.List<ScenarioNpcDefinition> characters)
        {
            writer.WriteStartElement("ScenarioCharacters");
            for (int i = 0; characters != null && i < characters.Count; i++)
            {
                ScenarioNpcDefinition character = characters[i];
                if (character == null)
                    continue;

                writer.WriteStartElement("Character");
                WriteAttribute(writer, "id", character.CharacterId);
                WriteAttribute(writer, "displayName", character.DisplayName);
                WriteAttribute(writer, "presetId", character.PresetId);
                WriteAttribute(writer, "weapon", character.WeaponItemId);
                WriteAttribute(writer, "equippedItem1", character.EquippedItem1Id);
                WriteAttribute(writer, "equippedItem2", character.EquippedItem2Id);
                WriteAttribute(writer, "personality", character.Personality);
                writer.WriteAttributeString("numRandomItems", character.NumRandomItems.ToString(CultureInfo.InvariantCulture));
                WriteAttribute(writer, "statSetting", character.StatSetting);
                writer.WriteAttributeString("backgroundNpc", character.BackgroundNpc.ToString());
                writer.WriteAttributeString("flipMesh", character.FlipMesh.ToString());
                WriteAttribute(writer, "species", character.Species);
                WriteAttribute(writer, "avatarOverrideSpriteId", character.AvatarOverrideSpriteId);
                ScenarioActorXmlSerializer.WriteActorRef(writer, character.ActorRef);
                ScenarioActorXmlSerializer.WriteActorComponents(writer, character.ActorComponents);
                WriteScenarioNpcStats(writer, character.Stats);
                WriteItemEntries(writer, "CarriedItems", "Item", character.CarriedItems);
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
        }

        private static void WriteScenarioNpcStats(XmlWriter writer, ScenarioNpcStatsDefinition stats)
        {
            if (stats == null)
                stats = new ScenarioNpcStatsDefinition();

            writer.WriteStartElement("Stats");
            writer.WriteAttributeString("strength", stats.Strength.ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("dexterity", stats.Dexterity.ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("charisma", stats.Charisma.ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("perception", stats.Perception.ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("intelligence", stats.Intelligence.ToString(CultureInfo.InvariantCulture));
            writer.WriteEndElement();
        }

        private static void WriteScenarioFlow(XmlWriter writer, ScenarioFlowDefinition flow)
        {
            if (flow == null)
                flow = new ScenarioFlowDefinition();

            writer.WriteStartElement("ScenarioFlow");
            writer.WriteStartElement("Stages");
            for (int i = 0; flow.Stages != null && i < flow.Stages.Count; i++)
            {
                ScenarioFlowStageDefinition stage = flow.Stages[i];
                if (stage == null)
                    continue;

                writer.WriteStartElement("Stage");
                WriteAttribute(writer, "id", stage.Id);
                WriteAttribute(writer, "unansweredNextStage", stage.UnansweredNextStage);
                writer.WriteAttributeString("unansweredNextDays", stage.UnansweredNextDays.ToString(CultureInfo.InvariantCulture));
                writer.WriteAttributeString("punishOnUnanswered", stage.PunishOnUnanswered.ToString());
                WriteStringList(writer, "CharacterIds", "CharacterId", stage.CharacterIds);
                WriteIntercomStages(writer, stage.IntercomStages);
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
            writer.WriteEndElement();
        }

        private static void WriteIntercomStages(XmlWriter writer, System.Collections.Generic.List<ScenarioIntercomStageDefinition> stages)
        {
            writer.WriteStartElement("IntercomStages");
            for (int i = 0; stages != null && i < stages.Count; i++)
            {
                ScenarioIntercomStageDefinition stage = stages[i];
                if (stage == null)
                    continue;

                writer.WriteStartElement("IntercomStage");
                WriteAttribute(writer, "id", stage.Id);
                WriteAttribute(writer, "type", stage.Type);
                WriteAttribute(writer, "nextId", stage.NextId);
                WriteAttribute(writer, "alternateNextId", stage.AlternateNextId);
                writer.WriteAttributeString("recruitAsFamily", stage.RecruitAsFamily.ToString());
                WriteDialogueLines(writer, stage.Dialogue);
                WriteDialogueOptions(writer, stage.Options);
                WriteStringList(writer, "RandomizedNextIds", "NextId", stage.RandomizedNextIds);
                WriteItemEntries(writer, "Items", "Item", stage.Items);
                WriteItemEntries(writer, "ItemsToRemove", "Item", stage.ItemsToRemove);
                WriteEndOptions(writer, stage.EndOptions);
                WriteStringList(writer, "SubquestsToActivate", "SubquestId", stage.SubquestsToActivate);
                WriteSubquestCheck(writer, stage.SubquestCheck);
                WriteMilestones(writer, "SetMilestones", stage.SetMilestones);
                WriteMilestoneChecks(writer, "CheckMilestones", stage.CheckMilestones);
                WriteStageChange(writer, stage.StageChange);
                writer.WriteStartElement("StageDescription");
                WriteAttribute(writer, "key", stage.StageDescriptionKey);
                writer.WriteEndElement();
                WriteStringList(writer, "CharacterIdsToRecruit", "CharacterId", stage.CharacterIdsToRecruit);
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
        }

        private static void WriteDialogueLines(XmlWriter writer, System.Collections.Generic.List<ScenarioDialogueLineDefinition> lines)
        {
            writer.WriteStartElement("Dialogue");
            for (int i = 0; lines != null && i < lines.Count; i++)
            {
                ScenarioDialogueLineDefinition line = lines[i];
                if (line == null)
                    continue;
                writer.WriteStartElement("Line");
                WriteAttribute(writer, "character", line.Character);
                WriteAttribute(writer, "textKey", line.TextKey);
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
        }

        private static void WriteDialogueOptions(XmlWriter writer, System.Collections.Generic.List<ScenarioDialogueOptionDefinition> options)
        {
            writer.WriteStartElement("Options");
            for (int i = 0; options != null && i < options.Count; i++)
            {
                ScenarioDialogueOptionDefinition option = options[i];
                if (option == null)
                    continue;
                writer.WriteStartElement("Option");
                WriteAttribute(writer, "textKey", option.TextKey);
                WriteAttribute(writer, "nextId", option.NextId);
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
        }

        private static void WriteEndOptions(XmlWriter writer, ScenarioEncounterEndOptionsDefinition options)
        {
            if (options == null)
                options = new ScenarioEncounterEndOptionsDefinition();

            writer.WriteStartElement("EndOptions");
            WriteAttribute(writer, "type", options.Type);
            WriteAttribute(writer, "combatResult", options.CombatResult);
            WriteAttribute(writer, "combatWinMilestone", options.CombatWinMilestone);
            WriteAttribute(writer, "combatLossMilestone", options.CombatLossMilestone);
            writer.WriteAttributeString("addVehicle", options.AddVehicle.ToString());
            WriteAttribute(writer, "moralOutcome", options.MoralOutcome);
            WriteAttribute(writer, "moralOutcomeCombatWon", options.MoralOutcomeCombatWon);
            WriteAttribute(writer, "moralOutcomeCombatLost", options.MoralOutcomeCombatLost);
            WriteAttribute(writer, "addSurroundedCharacterOutcome", options.AddSurroundedCharacterOutcome);
            WriteAttribute(writer, "revealSurroundedMapRegionOption", options.RevealSurroundedMapRegionOption);
            writer.WriteAttributeString("overrideTradeItems", options.OverrideTradeItems.ToString());
            writer.WriteAttributeString("minRandomTradeItems", options.MinRandomTradeItems.ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("maxRandomTradeItems", options.MaxRandomTradeItems.ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("completeQuest", options.CompleteQuest.ToString());
            writer.WriteAttributeString("completeParentScenario", options.CompleteParentScenario.ToString());
            WriteItemEntries(writer, "RewardItems", "Item", options.RewardItems);
            WriteItemEntries(writer, "TradeItems", "Item", options.TradeItems);
            WriteFloatingQuestTriggers(writer, options.TriggerFloatingQuests);
            WriteSpawnTriggers(writer, options.SpawnScenarios);
            writer.WriteEndElement();
        }

        private static void WriteSubquestCheck(XmlWriter writer, ScenarioSubquestCheckDefinition check)
        {
            if (check == null)
                check = new ScenarioSubquestCheckDefinition();

            writer.WriteStartElement("SubquestCheck");
            WriteAttribute(writer, "check", check.Check);
            WriteStringList(writer, "Subquests", "SubquestId", check.Subquests);
            writer.WriteEndElement();
        }

        private static void WriteStageChange(XmlWriter writer, ScenarioStageChangeDefinition change)
        {
            if (change == null)
                change = new ScenarioStageChangeDefinition();

            writer.WriteStartElement("StageChange");
            WriteAttribute(writer, "id", change.Id);
            writer.WriteAttributeString("delayDays", change.DelayDays.ToString(CultureInfo.InvariantCulture));
            writer.WriteEndElement();
        }

        private static void WriteMilestones(XmlWriter writer, string parentName, System.Collections.Generic.List<ScenarioMilestoneDefinition> milestones)
        {
            writer.WriteStartElement(parentName);
            for (int i = 0; milestones != null && i < milestones.Count; i++)
            {
                ScenarioMilestoneDefinition milestone = milestones[i];
                if (milestone == null)
                    continue;
                writer.WriteStartElement("Milestone");
                WriteAttribute(writer, "name", milestone.Name);
                WriteAttribute(writer, "scope", milestone.Scope);
                WriteAttribute(writer, "action", milestone.Action);
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
        }

        private static void WriteMilestoneChecks(XmlWriter writer, string parentName, System.Collections.Generic.List<ScenarioMilestoneCheckDefinition> milestones)
        {
            writer.WriteStartElement(parentName);
            for (int i = 0; milestones != null && i < milestones.Count; i++)
            {
                ScenarioMilestoneCheckDefinition milestone = milestones[i];
                if (milestone == null)
                    continue;
                writer.WriteStartElement("Milestone");
                WriteAttribute(writer, "name", milestone.Name);
                WriteAttribute(writer, "scope", milestone.Scope);
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
        }

        private static void WriteFloatingQuestTriggers(XmlWriter writer, System.Collections.Generic.List<ScenarioFloatingQuestTriggerDefinition> triggers)
        {
            writer.WriteStartElement("TriggerFloatingQuests");
            for (int i = 0; triggers != null && i < triggers.Count; i++)
            {
                ScenarioFloatingQuestTriggerDefinition trigger = triggers[i];
                if (trigger == null)
                    continue;
                writer.WriteStartElement("FloatingQuest");
                WriteAttribute(writer, "id", trigger.Id);
                writer.WriteAttributeString("activationDelayDays", trigger.ActivationDelayDays.ToString(CultureInfo.InvariantCulture));
                writer.WriteAttributeString("durationDays", trigger.DurationDays.ToString(CultureInfo.InvariantCulture));
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
        }

        private static void WriteSpawnTriggers(XmlWriter writer, System.Collections.Generic.List<ScenarioSpawnTriggerDefinition> triggers)
        {
            writer.WriteStartElement("SpawnScenarios");
            for (int i = 0; triggers != null && i < triggers.Count; i++)
            {
                ScenarioSpawnTriggerDefinition trigger = triggers[i];
                if (trigger == null)
                    continue;
                writer.WriteStartElement("Scenario");
                WriteAttribute(writer, "id", trigger.Id);
                writer.WriteAttributeString("spawnChance", trigger.SpawnChance.ToString(CultureInfo.InvariantCulture));
                writer.WriteAttributeString("delayDays", trigger.DelayDays.ToString(CultureInfo.InvariantCulture));
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
        }

        private static void WriteItemEntries(XmlWriter writer, string parentName, string itemName, System.Collections.Generic.List<ItemEntry> items)
        {
            writer.WriteStartElement(parentName);
            for (int i = 0; items != null && i < items.Count; i++)
            {
                ItemEntry item = items[i];
                if (item == null)
                    continue;
                writer.WriteStartElement(itemName);
                WriteAttribute(writer, "id", item.ItemId);
                writer.WriteAttributeString("quantity", item.Quantity.ToString(CultureInfo.InvariantCulture));
                writer.WriteEndElement();
            }
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
                WriteAttribute(writer, "wallRuntimeSpriteKey", room.WallRuntimeSpriteKey);
                WriteAttribute(writer, "wireRuntimeSpriteKey", room.WireRuntimeSpriteKey);
                if (room.WallCleared)
                    writer.WriteAttributeString("wallCleared", "true");
                if (room.WireCleared)
                    writer.WriteAttributeString("wireCleared", "true");
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

        internal static void WriteScoring(XmlWriter writer, ScenarioScoringDefinition value)
        {
            if (value == null)
                value = new ScenarioScoringDefinition();

            writer.WriteStartElement("Scoring");
            writer.WriteAttributeString("enabled", value.Enabled ? "true" : "false");
            WriteAttribute(writer, "scoreLabel", value.ScoreLabel);
            writer.WriteAttributeString("higherIsBetter", value.HigherIsBetter ? "true" : "false");
            WriteAttribute(writer, "leaderboardKey", value.LeaderboardKey);

            writer.WriteStartElement("Categories");
            for (int i = 0; value.Categories != null && i < value.Categories.Count; i++)
            {
                ScenarioScoreCategoryDefinition category = value.Categories[i];
                if (category == null)
                    continue;

                writer.WriteStartElement("Category");
                WriteAttribute(writer, "id", category.Id);
                WriteAttribute(writer, "displayName", category.DisplayName);
                WriteAttribute(writer, "description", category.Description);
                writer.WriteAttributeString("sortOrder", category.SortOrder.ToString(CultureInfo.InvariantCulture));
                writer.WriteEndElement();
            }
            writer.WriteEndElement();

            writer.WriteStartElement("Rules");
            for (int i = 0; value.Rules != null && i < value.Rules.Count; i++)
            {
                ScenarioScoreRuleDefinition rule = value.Rules[i];
                if (rule == null)
                    continue;

                writer.WriteStartElement("Rule");
                WriteAttribute(writer, "id", rule.Id);
                WriteAttribute(writer, "categoryId", rule.CategoryId);
                WriteAttribute(writer, "displayName", rule.DisplayName);
                WriteAttribute(writer, "description", rule.Description);
                WriteAttribute(writer, "source", rule.Source);
                WriteAttribute(writer, "operation", rule.Operation);
                WriteAttribute(writer, "outcomeFilter", rule.OutcomeFilter);
                writer.WriteAttributeString("weight", rule.Weight.ToString(CultureInfo.InvariantCulture));
                WriteProperties(writer, "Properties", rule.Properties);
                writer.WriteEndElement();
            }
            writer.WriteEndElement();

            WriteProperties(writer, "Metadata", value.Metadata);
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
                WriteOptionalScheduleTime(writer, "ScheduledStart", quest.ScheduledStart);
                WriteElement(writer, "Description", quest.Description);
                WriteProperties(writer, "Properties", quest.Properties);
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
        }

        internal static void WriteMap(XmlWriter writer, MapAuthoringDefinition value)
        {
            new ScenarioMapXmlSerializer().Write(writer, value);
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
                if (value.CustomSprites[i].UserOwned)
                    writer.WriteAttributeString("userOwned", "true");
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
                if (swap.AnimationFrameIndex.HasValue)
                    writer.WriteAttributeString("animationFrameIndex", swap.AnimationFrameIndex.Value.ToString(CultureInfo.InvariantCulture));
                WriteAttribute(writer, "animationFrameRuntimeSpriteKey", swap.AnimationFrameRuntimeSpriteKey);
                if (swap.Day.HasValue)
                    writer.WriteAttributeString("day", swap.Day.Value.ToString(CultureInfo.InvariantCulture));
                writer.WriteAttributeString("targetComponent", swap.TargetComponent.ToString());
                writer.WriteEndElement();
            }
            writer.WriteEndElement();

            WriteSceneSpritePlacements(writer, value.SceneSpritePlacements);
            writer.WriteStartElement("AssetCredits");
            for (int i = 0; value.AssetCredits != null && i < value.AssetCredits.Count; i++)
            {
                ScenarioAssetCreditDefinition credit = value.AssetCredits[i];
                if (credit == null || string.IsNullOrEmpty(credit.RelativePath) || string.IsNullOrEmpty(credit.Credit))
                    continue;
                writer.WriteStartElement("Credit");
                WriteAttribute(writer, "path", credit.RelativePath);
                WriteAttribute(writer, "note", credit.Credit);
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
            writer.WriteEndElement();
        }

        private static void WriteSceneSpritePlacements(XmlWriter writer, System.Collections.Generic.List<SceneSpritePlacement> placements)
        {
            writer.WriteStartElement("SceneSpritePlacements");
            for (int i = 0; placements != null && i < placements.Count; i++)
            {
                SceneSpritePlacement placement = placements[i];
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
        }

        private static void WriteBackendWorlds(XmlWriter writer, ScenarioBackendWorldsDefinition backendWorlds)
        {
            writer.WriteStartElement("BackendWorlds");
            for (int i = 0; backendWorlds != null && backendWorlds.Worlds != null && i < backendWorlds.Worlds.Count; i++)
            {
                ScenarioBackendWorldDefinition world = backendWorlds.Worlds[i];
                if (world == null)
                    continue;

                writer.WriteStartElement("BackendWorld");
                writer.WriteAttributeString("baseMode", world.BaseMode.ToString());
                WriteBunkerEdits(writer, world.BunkerEdits);
                WriteBunkerGrid(writer, world.BunkerGrid);
                WriteSceneSpritePlacements(writer, world.SceneSpritePlacements);
                writer.WriteEndElement();
            }
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

        private static JournalDefinition ReadJournal(XmlElement element)
        {
            JournalDefinition journal = new JournalDefinition();
            if (element == null)
                return journal;

            XmlElement entries = Child(element, "Entries");
            XmlNodeList entryNodes = entries != null ? entries.GetElementsByTagName("Entry") : element.GetElementsByTagName("Entry");
            for (int i = 0; i < entryNodes.Count; i++)
            {
                XmlElement node = entryNodes[i] as XmlElement;
                if (node == null)
                    continue;

                JournalEntryDefinition entry = new JournalEntryDefinition();
                entry.Id = AttributeOrChild(node, "id", "Id");
                entry.Text = AttributeOrChild(node, "text", "Text");
                entry.TriggerId = AttributeOrChild(node, "triggerId", "TriggerId");
                entry.GateId = AttributeOrChild(node, "gateId", "GateId");
                entry.Mode = ReadEnumAttribute(node, "mode", ScenarioJournalEntryMode.Once);
                entry.CooldownMinutes = ReadIntAttribute(node, "cooldownMinutes", 0);
                entry.DueTime = ReadOptionalScheduleTime(Child(node, "DueTime"));
                entry.Writer = ReadJournalWriter(node);
                ReadConditionRefs(Child(node, "Conditions"), entry.Conditions);
                journal.Entries.Add(entry);
            }

            XmlElement policy = Child(element, "VanillaPolicy");
            if (policy != null)
            {
                journal.VanillaPolicy.SuppressFirstEntry = ReadBoolAttribute(policy, "suppressFirstEntry", false);
                XmlNodeList suppressNodes = policy.GetElementsByTagName("Suppress");
                for (int i = 0; i < suppressNodes.Count; i++)
                {
                    XmlElement suppress = suppressNodes[i] as XmlElement;
                    if (suppress == null)
                        continue;

                    ScenarioJournalVanillaCategory category;
                    if (TryParseJournalCategory(AttributeOrChild(suppress, "category", "Category"), out category)
                        && !journal.VanillaPolicy.SuppressedCategories.Contains(category))
                    {
                        journal.VanillaPolicy.SuppressedCategories.Add(category);
                    }
                }
            }

            return journal;
        }

        private static void WriteJournal(XmlWriter writer, JournalDefinition journal)
        {
            if (journal == null)
                journal = new JournalDefinition();
            if (journal.VanillaPolicy == null)
                journal.VanillaPolicy = new JournalVanillaPolicyDefinition();

            writer.WriteStartElement("Journal");
            writer.WriteStartElement("Entries");
            for (int i = 0; journal.Entries != null && i < journal.Entries.Count; i++)
            {
                JournalEntryDefinition entry = journal.Entries[i];
                if (entry == null)
                    continue;

                writer.WriteStartElement("Entry");
                WriteAttribute(writer, "id", entry.Id);
                writer.WriteAttributeString("mode", entry.Mode.ToString());
                WriteAttribute(writer, "triggerId", entry.TriggerId);
                WriteAttribute(writer, "gateId", entry.GateId);
                writer.WriteAttributeString("cooldownMinutes", entry.CooldownMinutes.ToString(CultureInfo.InvariantCulture));
                WriteJournalWriter(writer, entry.Writer);
                WriteOptionalScheduleTime(writer, "DueTime", entry.DueTime);
                WriteElement(writer, "Text", entry.Text);
                WriteConditionRefs(writer, "Conditions", entry.Conditions);
                writer.WriteEndElement();
            }
            writer.WriteEndElement();

            writer.WriteStartElement("VanillaPolicy");
            writer.WriteAttributeString("suppressFirstEntry", journal.VanillaPolicy.SuppressFirstEntry ? "true" : "false");
            for (int i = 0; journal.VanillaPolicy.SuppressedCategories != null && i < journal.VanillaPolicy.SuppressedCategories.Count; i++)
            {
                writer.WriteStartElement("Suppress");
                writer.WriteAttributeString("category", journal.VanillaPolicy.SuppressedCategories[i].ToString());
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
            writer.WriteEndElement();
        }

        private static ScenarioActorRef ReadJournalWriter(XmlElement entry)
        {
            XmlElement writer = Child(entry, "Writer");
            if (writer == null)
                return null;

            ScenarioActorRef actorRef = new ScenarioActorRef();
            actorRef.Kind = AttributeOrChild(writer, "kind", "Kind");
            actorRef.LocalId = ReadIntAttribute(writer, "localId", 0);
            actorRef.Domain = AttributeOrChild(writer, "domain", "Domain");
            actorRef.BindingType = AttributeOrChild(writer, "bindingType", "BindingType");
            actorRef.BindingKey = AttributeOrChild(writer, "bindingKey", "BindingKey");
            actorRef.DisplayNameFallback = AttributeOrChild(writer, "displayNameFallback", "DisplayNameFallback");
            actorRef.RequiredModId = AttributeOrChild(writer, "requiredModId", "RequiredModId");
            return actorRef;
        }

        private static void WriteJournalWriter(XmlWriter writer, ScenarioActorRef actorRef)
        {
            if (actorRef == null)
                return;

            writer.WriteStartElement("Writer");
            WriteAttribute(writer, "kind", actorRef.Kind);
            writer.WriteAttributeString("localId", actorRef.LocalId.ToString(CultureInfo.InvariantCulture));
            WriteAttribute(writer, "domain", actorRef.Domain);
            WriteAttribute(writer, "bindingType", actorRef.BindingType);
            WriteAttribute(writer, "bindingKey", actorRef.BindingKey);
            WriteAttribute(writer, "displayNameFallback", actorRef.DisplayNameFallback);
            WriteAttribute(writer, "requiredModId", actorRef.RequiredModId);
            writer.WriteEndElement();
        }

        private static bool TryParseJournalCategory(string value, out ScenarioJournalVanillaCategory category)
        {
            category = ScenarioJournalVanillaCategory.Death;
            if (string.IsNullOrEmpty(value))
                return false;
            try
            {
                category = (ScenarioJournalVanillaCategory)Enum.Parse(typeof(ScenarioJournalVanillaCategory), value, true);
                return true;
            }
            catch
            {
                return false;
            }
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
                    action.Policy.WindowEndDay = ReadIntAttribute(policy, "windowEndDay", 0);
                    action.Policy.Chance = ReadFloatAttribute(policy, "chance", 1f);
                    action.Policy.JitterMinutes = ReadIntAttribute(policy, "jitterMinutes", 0);
                    action.Policy.MaxRuns = ReadIntAttribute(policy, "maxRuns", 0);
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
                    if (action.Policy != null && action.Policy.WindowEndDay > 0)
                        writer.WriteAttributeString("windowEndDay", action.Policy.WindowEndDay.ToString(CultureInfo.InvariantCulture));
                    if (action.Policy != null && action.Policy.Chance < 1f)
                        writer.WriteAttributeString("chance", action.Policy.Chance.ToString(CultureInfo.InvariantCulture));
                    if (action.Policy != null && action.Policy.JitterMinutes > 0)
                        writer.WriteAttributeString("jitterMinutes", action.Policy.JitterMinutes.ToString(CultureInfo.InvariantCulture));
                    if (action.Policy != null && action.Policy.MaxRuns > 0)
                        writer.WriteAttributeString("maxRuns", action.Policy.MaxRuns.ToString(CultureInfo.InvariantCulture));
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
                condition.ActorRef = ScenarioActorXmlSerializer.ReadActorRef(node);
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
                ScenarioActorXmlSerializer.WriteActorRef(writer, condition.ActorRef);
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
                effect.ActorRef = ScenarioActorXmlSerializer.ReadActorRef(node);
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
                effect.TriggerId = AttributeOrChild(node, "triggerId", "TriggerId");
                effect.ConversationId = AttributeOrChild(node, "conversationId", "ConversationId");
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
                WriteAttribute(writer, "triggerId", effect.TriggerId);
                WriteAttribute(writer, "conversationId", effect.ConversationId);
                ScenarioActorXmlSerializer.WriteActorRef(writer, effect.ActorRef);
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

        private static ScenarioConversationAuthoringDefinition ReadConversations(XmlElement element)
        {
            ScenarioConversationAuthoringDefinition authoring = new ScenarioConversationAuthoringDefinition();
            if (element == null)
                return authoring;

            XmlElement settings = Child(element, "Settings");
            if (settings != null)
            {
                authoring.Settings.SuppressVanillaRandomChatter = ReadBool(settings, "SuppressVanillaRandomChatter", authoring.Settings.SuppressVanillaRandomChatter);
                ReadStringList(Child(settings, "SuppressedVanillaCategories"), "string", authoring.Settings.SuppressedVanillaCategories);
                ReadStringList(Child(settings, "SuppressedVanillaTopicKeys"), "string", authoring.Settings.SuppressedVanillaTopicKeys);
            }

            XmlNodeList nodes = element.GetElementsByTagName("Conversation");
            for (int i = 0; i < nodes.Count; i++)
            {
                XmlElement conversationElement = nodes[i] as XmlElement;
                if (conversationElement == null)
                    continue;

                ScenarioConversationDefinition conversation = new ScenarioConversationDefinition();
                conversation.Id = AttributeOrChild(conversationElement, "id", "Id");
                conversation.Trigger = ReadConversationTrigger(Child(conversationElement, "Trigger"));
                ReadConversationParticipants(Child(conversationElement, "Participants"), conversation.Participants);
                ReadConditionRefs(Child(conversationElement, "Conditions"), conversation.Conditions);
                ReadConversationLines(Child(conversationElement, "Lines"), conversation.Lines);
                ReadStringList(Child(conversationElement, "Tags"), "Tag", conversation.Tags);
                authoring.Conversations.Add(conversation);
            }

            return authoring;
        }

        private static ScenarioConversationTriggerDefinition ReadConversationTrigger(XmlElement element)
        {
            ScenarioConversationTriggerDefinition trigger = new ScenarioConversationTriggerDefinition();
            if (element == null)
                return trigger;

            trigger.Source = ReadEnumAttribute(element, "source", trigger.Source);
            trigger.Weight = ReadFloatAttribute(element, "weight", trigger.Weight);
            trigger.TriggerId = AttributeOrChild(element, "triggerId", "TriggerId");
            trigger.CooldownDays = ReadFloatAttribute(element, "cooldownDays", trigger.CooldownDays);
            trigger.Once = ReadBoolAttribute(element, "once", trigger.Once);
            trigger.Time = ReadScheduleTime(Child(element, "Time"));
            return trigger;
        }

        private static void ReadConversationParticipants(XmlElement element, System.Collections.Generic.List<ScenarioConversationParticipantDefinition> target)
        {
            if (element == null || target == null)
                return;

            XmlNodeList nodes = element.GetElementsByTagName("Participant");
            for (int i = 0; i < nodes.Count; i++)
            {
                XmlElement participantElement = nodes[i] as XmlElement;
                if (participantElement == null)
                    continue;

                ScenarioConversationParticipantDefinition participant = new ScenarioConversationParticipantDefinition();
                participant.Slot = AttributeOrChild(participantElement, "slot", "Slot");
                participant.StoryCharacterId = AttributeOrChild(participantElement, "storyCharacterId", "StoryCharacterId");
                participant.ActorRef = ScenarioActorXmlSerializer.ReadActorRef(participantElement);
                participant.Fallback = ReadEnumAttribute(participantElement, "fallback", participant.Fallback);
                participant.Required = ReadBoolAttribute(participantElement, "required", participant.Required);
                target.Add(participant);
            }
        }

        private static void ReadConversationLines(XmlElement element, System.Collections.Generic.List<ScenarioConversationLineDefinition> target)
        {
            if (element == null || target == null)
                return;

            XmlNodeList nodes = element.GetElementsByTagName("Line");
            for (int i = 0; i < nodes.Count; i++)
            {
                XmlElement lineElement = nodes[i] as XmlElement;
                if (lineElement == null)
                    continue;

                ScenarioConversationLineDefinition line = new ScenarioConversationLineDefinition();
                line.SpeakerSlot = AttributeOrChild(lineElement, "speakerSlot", "SpeakerSlot");
                line.TextKey = AttributeOrChild(lineElement, "textKey", "TextKey");
                line.RawText = AttributeOrChild(lineElement, "rawText", "RawText");
                line.DelaySeconds = ReadFloatAttribute(lineElement, "delaySeconds", line.DelaySeconds);
                target.Add(line);
            }
        }

        private static void WriteConversations(XmlWriter writer, ScenarioConversationAuthoringDefinition authoring)
        {
            if (authoring == null)
                authoring = new ScenarioConversationAuthoringDefinition();

            writer.WriteStartElement("Conversations");
            ScenarioConversationSuppressionDefinition settings = authoring.Settings ?? new ScenarioConversationSuppressionDefinition();
            writer.WriteStartElement("Settings");
            WriteElement(writer, "SuppressVanillaRandomChatter", settings.SuppressVanillaRandomChatter ? "true" : "false");
            WriteStringList(writer, "SuppressedVanillaCategories", "string", settings.SuppressedVanillaCategories);
            WriteStringList(writer, "SuppressedVanillaTopicKeys", "string", settings.SuppressedVanillaTopicKeys);
            writer.WriteEndElement();

            for (int i = 0; authoring.Conversations != null && i < authoring.Conversations.Count; i++)
            {
                ScenarioConversationDefinition conversation = authoring.Conversations[i];
                if (conversation == null)
                    continue;

                writer.WriteStartElement("Conversation");
                WriteAttribute(writer, "id", conversation.Id);
                WriteConversationTrigger(writer, conversation.Trigger);
                WriteConversationParticipants(writer, conversation.Participants);
                WriteConditionRefs(writer, "Conditions", conversation.Conditions);
                WriteConversationLines(writer, conversation.Lines);
                WriteStringList(writer, "Tags", "Tag", conversation.Tags);
                writer.WriteEndElement();
            }

            writer.WriteEndElement();
        }

        private static void WriteConversationTrigger(XmlWriter writer, ScenarioConversationTriggerDefinition trigger)
        {
            if (trigger == null)
                trigger = new ScenarioConversationTriggerDefinition();

            writer.WriteStartElement("Trigger");
            writer.WriteAttributeString("source", trigger.Source.ToString());
            writer.WriteAttributeString("weight", trigger.Weight.ToString(CultureInfo.InvariantCulture));
            WriteAttribute(writer, "triggerId", trigger.TriggerId);
            writer.WriteAttributeString("cooldownDays", trigger.CooldownDays.ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("once", trigger.Once ? "true" : "false");
            WriteScheduleTime(writer, "Time", trigger.Time);
            writer.WriteEndElement();
        }

        private static void WriteConversationParticipants(XmlWriter writer, System.Collections.Generic.List<ScenarioConversationParticipantDefinition> participants)
        {
            writer.WriteStartElement("Participants");
            for (int i = 0; participants != null && i < participants.Count; i++)
            {
                ScenarioConversationParticipantDefinition participant = participants[i];
                if (participant == null)
                    continue;

                writer.WriteStartElement("Participant");
                WriteAttribute(writer, "slot", participant.Slot);
                WriteAttribute(writer, "storyCharacterId", participant.StoryCharacterId);
                writer.WriteAttributeString("fallback", participant.Fallback.ToString());
                writer.WriteAttributeString("required", participant.Required ? "true" : "false");
                ScenarioActorXmlSerializer.WriteActorRef(writer, participant.ActorRef);
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
        }

        private static void WriteConversationLines(XmlWriter writer, System.Collections.Generic.List<ScenarioConversationLineDefinition> lines)
        {
            writer.WriteStartElement("Lines");
            for (int i = 0; lines != null && i < lines.Count; i++)
            {
                ScenarioConversationLineDefinition line = lines[i];
                if (line == null)
                    continue;

                writer.WriteStartElement("Line");
                WriteAttribute(writer, "speakerSlot", line.SpeakerSlot);
                WriteAttribute(writer, "textKey", line.TextKey);
                WriteAttribute(writer, "rawText", line.RawText);
                writer.WriteAttributeString("delaySeconds", line.DelaySeconds.ToString(CultureInfo.InvariantCulture));
                writer.WriteEndElement();
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
