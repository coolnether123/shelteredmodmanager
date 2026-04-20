using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using ModAPI.Core;
using ModAPI.Items;
using ModAPI.Scenarios;
using ShelteredAPI.Content;
using UnityEngine;

namespace ShelteredAPI.Scenarios
{
    public sealed class ScenarioApplyResult
    {
        private readonly List<string> _messages = new List<string>();

        public int FamilyChanges { get; set; }
        public int InventoryChanges { get; set; }
        public int BunkerChanges { get; set; }

        public string[] Messages
        {
            get { return _messages.ToArray(); }
        }

        public void AddMessage(string message)
        {
            if (!string.IsNullOrEmpty(message))
                _messages.Add(message);
        }
    }

    public sealed class ScenarioApplier
    {
        private static readonly FieldInfo BaseCharacterFirstNameField = typeof(BaseCharacter).GetField("m_firstName", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo BaseCharacterMaleField = typeof(BaseCharacter).GetField("m_male", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo InventoryRandomStartCountField = typeof(InventoryManager).GetField("numberOfRandomStartingItems", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo InventoryRandomStartItemsField = typeof(InventoryManager).GetField("listOfRandomStartingItems", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo ShelterRoomGridWiresSpritesField = typeof(ShelterRoomGrid).GetField("wiresSprites", BindingFlags.NonPublic | BindingFlags.Instance);

        public ScenarioApplyResult ApplyAll(ScenarioDefinition definition)
        {
            return ApplyAll(definition, null);
        }

        public ScenarioApplyResult ApplyAll(ScenarioDefinition definition, string scenarioFilePath)
        {
            ScenarioApplyResult result = new ScenarioApplyResult();
            if (definition == null)
            {
                result.AddMessage("Scenario definition is null; nothing applied.");
                return result;
            }

            ModRandom.GetStream("scenario_" + (definition.Id ?? "unknown"));
            PreloadAssets(definition, scenarioFilePath, result);
            FamilyApply(definition, result);
            InventoryApply(definition, result);
            BunkerVisualApply(definition, result);

            if (definition.TriggersAndEvents != null && definition.TriggersAndEvents.Triggers.Count > 0)
            {
                result.AddMessage("Triggers are parsed but not registered yet in this phase.");
            }

            if (definition.WinLossConditions != null
                && (definition.WinLossConditions.WinConditions.Count > 0 || definition.WinLossConditions.LossConditions.Count > 0))
            {
                result.AddMessage("Win/loss conditions are parsed but not watched yet in this phase.");
            }

            LogResult(definition, result);
            return result;
        }

        private static void PreloadAssets(ScenarioDefinition definition, string scenarioFilePath, ScenarioApplyResult result)
        {
            if (definition == null || definition.AssetReferences == null || string.IsNullOrEmpty(scenarioFilePath))
                return;

            string packRoot = Path.GetDirectoryName(scenarioFilePath);
            if (string.IsNullOrEmpty(packRoot))
                return;

            for (int i = 0; i < definition.AssetReferences.CustomSprites.Count; i++)
            {
                SpriteRef sprite = definition.AssetReferences.CustomSprites[i];
                if (sprite == null || string.IsNullOrEmpty(sprite.RelativePath))
                    continue;

                try
                {
                    if (AssetLoader.LoadSprite(packRoot, sprite.RelativePath, 100f) == null)
                        result.AddMessage("Sprite asset failed to load: " + sprite.RelativePath);
                }
                catch (Exception ex)
                {
                    result.AddMessage("Sprite asset failed to load: " + sprite.RelativePath + " (" + ex.Message + ")");
                }
            }

            for (int i = 0; i < definition.AssetReferences.CustomIcons.Count; i++)
            {
                IconRef icon = definition.AssetReferences.CustomIcons[i];
                if (icon == null || string.IsNullOrEmpty(icon.RelativePath))
                    continue;

                try
                {
                    if (AssetLoader.LoadSprite(packRoot, icon.RelativePath, 100f) == null)
                        result.AddMessage("Icon asset failed to load: " + icon.RelativePath);
                }
                catch (Exception ex)
                {
                    result.AddMessage("Icon asset failed to load: " + icon.RelativePath + " (" + ex.Message + ")");
                }
            }
        }

        public void FamilyApply(ScenarioDefinition definition, ScenarioApplyResult result)
        {
            if (definition == null || definition.FamilySetup == null || definition.FamilySetup.Members.Count == 0)
                return;

            if (FamilyManager.Instance == null)
            {
                result.AddMessage("FamilyManager is not ready; family changes skipped.");
                return;
            }

            List<FamilyMember> members = FamilyManager.Instance.GetAllFamilyMembers();
            if (members == null || members.Count == 0)
            {
                result.AddMessage("No spawned family members found; family changes skipped.");
                return;
            }

            int limit = Math.Min(members.Count, definition.FamilySetup.Members.Count);
            for (int i = 0; i < limit; i++)
            {
                FamilyMember member = members[i];
                FamilyMemberConfig config = definition.FamilySetup.Members[i];
                if (member == null || config == null)
                    continue;

                if (!string.IsNullOrEmpty(config.Name) && BaseCharacterFirstNameField != null)
                {
                    BaseCharacterFirstNameField.SetValue(member, config.Name);
                    member.name = config.Name;
                    result.FamilyChanges++;
                }

                if (config.Gender != ScenarioGender.Any && BaseCharacterMaleField != null)
                {
                    BaseCharacterMaleField.SetValue(member, config.Gender == ScenarioGender.Male);
                    result.FamilyChanges++;
                }

                if (config.Stats.Count > 0 || config.Traits.Count > 0 || config.Skills.Count > 0)
                {
                    // The v1 data model captures stats/traits/skills, but Sheltered applies
                    // most of those through BaseStats and Traits internals that also drive
                    // UI and save data. Renaming/gender changes are safe post-spawn; deeper
                    // mutation needs a dedicated adapter so we do not corrupt live saves.
                    result.AddMessage("Family stats/traits/skills parsed for '" + (config.Name ?? string.Empty) + "' but deferred to a dedicated character adapter.");
                }
            }

            if (definition.FamilySetup.OverrideVanillaFamily && definition.FamilySetup.Members.Count > members.Count)
            {
                result.AddMessage("OverrideVanillaFamily requested more members than currently spawned. Creating/removing family members is deferred until a safe spawn adapter is added.");
            }
        }

        public void InventoryApply(ScenarioDefinition definition, ScenarioApplyResult result)
        {
            if (definition == null || definition.StartingInventory == null || definition.StartingInventory.Items.Count == 0)
                return;

            InventoryManager manager = InventoryManager.Instance;
            if (manager == null)
            {
                result.AddMessage("InventoryManager is not ready; inventory changes skipped.");
                return;
            }

            if (definition.StartingInventory.OverrideRandomStart)
            {
                if (InventoryRandomStartCountField != null)
                    InventoryRandomStartCountField.SetValue(manager, 0);

                IList randomItems = InventoryRandomStartItemsField != null ? InventoryRandomStartItemsField.GetValue(manager) as IList : null;
                if (randomItems != null)
                    randomItems.Clear();
            }

            ContentInjector.NotifyManagerReady("ScenarioApplier");
            for (int i = 0; i < definition.StartingInventory.Items.Count; i++)
            {
                ItemEntry entry = definition.StartingInventory.Items[i];
                if (entry == null || string.IsNullOrEmpty(entry.ItemId) || entry.Quantity <= 0)
                    continue;

                ItemManager.ItemType type;
                if (!InventoryHelper.ResolveItemType(entry.ItemId, out type))
                {
                    result.AddMessage("Unknown item id skipped: " + entry.ItemId);
                    continue;
                }

                if (manager.AddNewItems(type, entry.Quantity))
                    result.InventoryChanges += entry.Quantity;
                else
                    result.AddMessage("InventoryManager rejected item '" + entry.ItemId + "' quantity " + entry.Quantity + ".");
            }
        }

        public void BunkerVisualApply(ScenarioDefinition definition, ScenarioApplyResult result)
        {
            if (definition == null || definition.BunkerEdits == null || definition.BunkerEdits.RoomChanges.Count == 0)
                return;

            ShelterRoomGrid grid = ShelterRoomGrid.Instance;
            if (grid == null || !grid.isInitialized)
            {
                result.AddMessage("ShelterRoomGrid is not ready; bunker changes skipped.");
                return;
            }

            List<Sprite> wires = ShelterRoomGridWiresSpritesField != null ? ShelterRoomGridWiresSpritesField.GetValue(grid) as List<Sprite> : null;
            for (int i = 0; i < definition.BunkerEdits.RoomChanges.Count; i++)
            {
                RoomEdit room = definition.BunkerEdits.RoomChanges[i];
                if (room == null)
                    continue;

                if (room.WallSpriteIndex.HasValue)
                {
                    if (grid.SetWall(room.GridX, room.GridY, room.WallSpriteIndex.Value))
                        result.BunkerChanges++;
                    else
                        result.AddMessage("Failed to set wall sprite at " + room.GridX + "," + room.GridY + ".");
                }

                if (room.WireSpriteIndex.HasValue)
                {
                    if (wires != null && room.WireSpriteIndex.Value >= 0 && room.WireSpriteIndex.Value < wires.Count
                        && grid.SetWiring(room.GridX, room.GridY, wires[room.WireSpriteIndex.Value]))
                    {
                        result.BunkerChanges++;
                    }
                    else
                    {
                        result.AddMessage("Failed to set wiring sprite at " + room.GridX + "," + room.GridY + ".");
                    }
                }
            }

            if (definition.BunkerEdits.ObjectPlacements.Count > 0)
                result.AddMessage("Object placements are parsed but deferred until prefab resolution is wired to ContentRegistry/ObjectManager.");
        }

        private static void LogResult(ScenarioDefinition definition, ScenarioApplyResult result)
        {
            MMLog.WriteInfo("[ShelteredScenarioApplier] Applied scenario '" + definition.Id + "': familyChanges="
                + result.FamilyChanges + ", inventoryChanges=" + result.InventoryChanges + ", bunkerChanges=" + result.BunkerChanges + ".");

            string[] messages = result.Messages;
            for (int i = 0; i < messages.Length; i++)
                MMLog.WriteInfo("[ShelteredScenarioApplier] " + messages[i]);
        }
    }
}
