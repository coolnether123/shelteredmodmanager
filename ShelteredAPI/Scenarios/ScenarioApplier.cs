using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
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
        public int TriggerChanges { get; set; }
        public int ConditionChanges { get; set; }
        public int SpriteSwapChanges { get; set; }

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

    public sealed class ScenarioApplier : IScenarioApplier
    {
        private readonly IScenarioSpriteAssetResolver _assetResolver;
        private readonly IScenarioSpriteSwapEngine _spriteSwapEngine;
        private readonly IScenarioSceneSpritePlacementEngine _sceneSpritePlacementEngine;
        private readonly ScenarioCharacterAppearanceService _characterAppearanceService;

        private static readonly FieldInfo BaseCharacterFirstNameField = typeof(BaseCharacter).GetField("m_firstName", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo BaseCharacterMaleField = typeof(BaseCharacter).GetField("m_male", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo InventoryRandomStartCountField = typeof(InventoryManager).GetField("numberOfRandomStartingItems", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo InventoryRandomStartItemsField = typeof(InventoryManager).GetField("listOfRandomStartingItems", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo ShelterRoomGridWiresSpritesField = typeof(ShelterRoomGrid).GetField("wiresSprites", BindingFlags.NonPublic | BindingFlags.Instance);

        internal ScenarioApplier(
            IScenarioSpriteAssetResolver assetResolver,
            IScenarioSpriteSwapEngine spriteSwapEngine,
            IScenarioSceneSpritePlacementEngine sceneSpritePlacementEngine,
            ScenarioCharacterAppearanceService characterAppearanceService)
        {
            _assetResolver = assetResolver;
            _spriteSwapEngine = spriteSwapEngine;
            _sceneSpritePlacementEngine = sceneSpritePlacementEngine;
            _characterAppearanceService = characterAppearanceService;
        }

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
            FamilyApply(definition, scenarioFilePath, result);
            InventoryApply(definition, result);
            BunkerVisualApply(definition, result);
            TriggerApply(definition, result);
            WinLossConditionApply(definition, result);
            _spriteSwapEngine.Activate(definition, scenarioFilePath, result);
            _sceneSpritePlacementEngine.Activate(definition, scenarioFilePath, result);

            LogResult(definition, result);
            return result;
        }

        private void PreloadAssets(ScenarioDefinition definition, string scenarioFilePath, ScenarioApplyResult result)
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
                    if (_assetResolver.ResolveSprite(definition, packRoot, sprite.Id, sprite.RelativePath, null, "preload sprite '" + sprite.RelativePath + "'") == null)
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
                    if (_assetResolver.ResolveSprite(definition, packRoot, icon.Id, icon.RelativePath, null, "preload icon '" + icon.RelativePath + "'") == null)
                        result.AddMessage("Icon asset failed to load: " + icon.RelativePath);
                }
                catch (Exception ex)
                {
                    result.AddMessage("Icon asset failed to load: " + icon.RelativePath + " (" + ex.Message + ")");
                }
            }
        }

        public void FamilyApply(ScenarioDefinition definition, string scenarioFilePath, ScenarioApplyResult result)
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

                ApplyStats(member, config, result);
                ApplyTraits(member, config, result);
                ApplySkills(member, config, result);
                ApplyAppearance(definition, scenarioFilePath, member, config, result);
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
            if (definition == null || definition.BunkerEdits == null)
                return;

            bool hasRoomChanges = definition.BunkerEdits.RoomChanges != null && definition.BunkerEdits.RoomChanges.Count > 0;
            bool hasObjectPlacements = definition.BunkerEdits.ObjectPlacements != null && definition.BunkerEdits.ObjectPlacements.Count > 0;
            if (!hasRoomChanges && !hasObjectPlacements)
                return;

            ShelterRoomGrid grid = ShelterRoomGrid.Instance;
            if (grid == null || !grid.isInitialized)
            {
                result.AddMessage("ShelterRoomGrid is not ready; bunker changes skipped.");
                return;
            }

            ObjectPlacementApply(definition, result);

            List<Sprite> wires = ShelterRoomGridWiresSpritesField != null ? ShelterRoomGridWiresSpritesField.GetValue(grid) as List<Sprite> : null;
            for (int i = 0; definition.BunkerEdits.RoomChanges != null && i < definition.BunkerEdits.RoomChanges.Count; i++)
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
        }

        public void ObjectPlacementApply(ScenarioDefinition definition, ScenarioApplyResult result)
        {
            if (definition == null
                || definition.BunkerEdits == null
                || definition.BunkerEdits.ObjectPlacements == null
                || definition.BunkerEdits.ObjectPlacements.Count == 0)
            {
                return;
            }

            ApplyStructurePlacements(definition.BunkerEdits.ObjectPlacements, ScenarioPlacementDefinitionKind.Room, result);
            ApplyStructurePlacements(definition.BunkerEdits.ObjectPlacements, ScenarioPlacementDefinitionKind.Ladder, result);
            ApplyStructurePlacements(definition.BunkerEdits.ObjectPlacements, ScenarioPlacementDefinitionKind.RoomLight, result);
            ApplyObjectPlacements(definition.BunkerEdits.ObjectPlacements, result);
        }

        private void ApplyStructurePlacements(
            List<ObjectPlacement> placements,
            ScenarioPlacementDefinitionKind targetKind,
            ScenarioApplyResult result)
        {
            ShelterRoomGrid grid = ShelterRoomGrid.Instance;
            if (grid == null || !grid.isInitialized || placements == null)
                return;

            for (int i = 0; i < placements.Count; i++)
            {
                ObjectPlacement placement = placements[i];
                ScenarioPlacementDefinitionKind kind;
                if (placement == null
                    || !ScenarioPlacementDefinitions.TryParseSpecialKind(placement.DefinitionReference, out kind)
                    || kind != targetKind)
                {
                    continue;
                }

                switch (kind)
                {
                    case ScenarioPlacementDefinitionKind.Room:
                        ApplyRoomPlacement(grid, placement, i, result);
                        break;

                    case ScenarioPlacementDefinitionKind.Ladder:
                        ApplyLadderPlacement(grid, placement, i, result);
                        break;

                    case ScenarioPlacementDefinitionKind.RoomLight:
                        ApplyRoomLightPlacement(grid, placement, i, result);
                        break;
                }
            }
        }

        private void ApplyObjectPlacements(List<ObjectPlacement> placements, ScenarioApplyResult result)
        {
            if (placements == null || placements.Count == 0)
                return;

            bool hasStandardPlacements = false;
            for (int i = 0; i < placements.Count; i++)
            {
                ObjectPlacement placement = placements[i];
                if (placement != null && !ScenarioPlacementDefinitions.IsSpecialDefinition(placement.DefinitionReference))
                {
                    hasStandardPlacements = true;
                    break;
                }
            }

            if (!hasStandardPlacements)
                return;

            ObjectManager manager = ObjectManager.Instance;
            if (manager == null)
            {
                result.AddMessage("ObjectManager is not ready; standard object placements skipped.");
                return;
            }

            for (int i = 0; i < placements.Count; i++)
            {
                ObjectPlacement placement = placements[i];
                if (placement == null)
                    continue;

                if (ScenarioPlacementDefinitions.IsSpecialDefinition(placement.DefinitionReference))
                    continue;

                if (!string.IsNullOrEmpty(placement.PrefabReference))
                {
                    result.AddMessage("Object placement #" + i + " uses PrefabReference '" + placement.PrefabReference
                        + "' and is deferred because direct prefab-path instantiation is not safe for live saves.");
                    continue;
                }

                ObjectManager.ObjectType objectType;
                if (!TryParseObjectType(placement.DefinitionReference, out objectType))
                {
                    result.AddMessage("Object placement #" + i + " has unknown DefinitionReference: " + (placement.DefinitionReference ?? string.Empty));
                    continue;
                }

                if (!manager.HasPrefab(objectType))
                {
                    result.AddMessage("Object placement #" + i + " skipped because ObjectManager has no prefab for " + objectType + ".");
                    continue;
                }

                int level = GetIntProperty(placement.CustomProperties, "level", 1);
                bool lockDeconstruct = GetBoolProperty(placement.CustomProperties, "lockDeconstruct", false);
                bool movable = GetBoolProperty(placement.CustomProperties, "movable", true);
                Vector2 position = new Vector2(
                    placement.Position != null ? placement.Position.X : 0f,
                    placement.Position != null ? placement.Position.Y : 0f);

                Obj_Base spawned = manager.SpawnObject(objectType, level, position, lockDeconstruct, movable);
                if (spawned == null)
                {
                    result.AddMessage("Object placement #" + i + " failed to spawn " + objectType + " at " + position.x + "," + position.y + ".");
                    continue;
                }

                if (placement.Rotation != null)
                    spawned.transform.eulerAngles = new Vector3(placement.Rotation.X, placement.Rotation.Y, placement.Rotation.Z);

                result.BunkerChanges++;
            }
        }

        private static void ApplyRoomPlacement(
            ShelterRoomGrid grid,
            ObjectPlacement placement,
            int index,
            ScenarioApplyResult result)
        {
            int gridX;
            int gridY;
            if (!TryResolveGridCoordinates(grid, placement, out gridX, out gridY))
            {
                result.AddMessage("Room placement #" + index + " could not resolve a shelter cell.");
                return;
            }
            if (!IsValidCell(grid, gridX, gridY))
            {
                result.AddMessage("Room placement #" + index + " is outside the shelter grid at " + gridX + "," + gridY + ".");
                return;
            }

            ShelterRoomGrid.GridCell cell = grid.GetCell(gridX, gridY);
            ShelterRoomGrid.CellType cellType = string.Equals(placement.DefinitionReference, ScenarioPlacementDefinitions.RoomTop, StringComparison.OrdinalIgnoreCase)
                ? ShelterRoomGrid.CellType.RoomTop
                : ShelterRoomGrid.CellType.Room;
            if (cell != null && cell.type == cellType)
                return;

            if (grid.SetCellType(gridX, gridY, cellType))
                result.BunkerChanges++;
            else
                result.AddMessage("Room placement #" + index + " failed at " + gridX + "," + gridY + ".");
        }

        private static void ApplyLadderPlacement(
            ShelterRoomGrid grid,
            ObjectPlacement placement,
            int index,
            ScenarioApplyResult result)
        {
            int gridX;
            int gridY;
            if (!TryResolveGridCoordinates(grid, placement, out gridX, out gridY))
            {
                result.AddMessage("Ladder placement #" + index + " could not resolve a shelter cell.");
                return;
            }
            if (!IsValidCell(grid, gridX, gridY))
            {
                result.AddMessage("Ladder placement #" + index + " is outside the shelter grid at " + gridX + "," + gridY + ".");
                return;
            }

            if (grid.HasLadder(gridX, gridY))
                return;

            float horizontalPos = ResolveHorizontalPosition(grid, placement, gridX);
            if (grid.AddLadder(gridX, gridY, horizontalPos) != null)
                result.BunkerChanges++;
            else
                result.AddMessage("Ladder placement #" + index + " failed at " + gridX + "," + gridY + ".");
        }

        private static void ApplyRoomLightPlacement(
            ShelterRoomGrid grid,
            ObjectPlacement placement,
            int index,
            ScenarioApplyResult result)
        {
            int gridX;
            int gridY;
            if (!TryResolveGridCoordinates(grid, placement, out gridX, out gridY))
            {
                result.AddMessage("Room light placement #" + index + " could not resolve a shelter cell.");
                return;
            }
            if (!IsValidCell(grid, gridX, gridY))
            {
                result.AddMessage("Room light placement #" + index + " is outside the shelter grid at " + gridX + "," + gridY + ".");
                return;
            }

            ShelterRoomGrid.GridCell cell = grid.GetCell(gridX, gridY);
            if (cell != null && (UnityEngine.Object)cell.lightObject != (UnityEngine.Object)null)
                return;

            if (grid.AddLight(gridX, gridY))
                result.BunkerChanges++;
            else
                result.AddMessage("Room light placement #" + index + " failed at " + gridX + "," + gridY + ".");
        }

        private static bool TryResolveGridCoordinates(
            ShelterRoomGrid grid,
            ObjectPlacement placement,
            out int gridX,
            out int gridY)
        {
            gridX = GetIntProperty(placement != null ? placement.CustomProperties : null, ScenarioPlacementDefinitions.PropertyGridX, int.MinValue);
            gridY = GetIntProperty(placement != null ? placement.CustomProperties : null, ScenarioPlacementDefinitions.PropertyGridY, int.MinValue);
            if (gridX != int.MinValue && gridY != int.MinValue)
                return true;

            Vector3 worldPosition = new Vector3(
                placement != null && placement.Position != null ? placement.Position.X : 0f,
                placement != null && placement.Position != null ? placement.Position.Y : 0f,
                placement != null && placement.Position != null ? placement.Position.Z : 0f);
            return grid != null && grid.WorldCoordsToCellCoords(worldPosition, out gridX, out gridY);
        }

        private static bool IsValidCell(ShelterRoomGrid grid, int gridX, int gridY)
        {
            return grid != null
                && gridX >= 0
                && gridX < grid.grid_width
                && gridY >= 0
                && gridY < grid.grid_height;
        }

        private static float ResolveHorizontalPosition(ShelterRoomGrid grid, ObjectPlacement placement, int gridX)
        {
            string storedValue = GetProperty(placement != null ? placement.CustomProperties : null, ScenarioPlacementDefinitions.PropertyHorizontalPos);
            float parsedValue;
            if (!string.IsNullOrEmpty(storedValue)
                && float.TryParse(storedValue, NumberStyles.Float, CultureInfo.InvariantCulture, out parsedValue))
            {
                return Mathf.Clamp01(parsedValue);
            }

            float cellLeft = gridX * grid.grid_cell_width;
            float cellRight = cellLeft + grid.grid_cell_width;
            float width = cellRight - cellLeft;
            if (width <= 0f || placement == null || placement.Position == null)
                return 0.5f;

            return Mathf.Clamp01((placement.Position.X - cellLeft) / width);
        }

        public void TriggerApply(ScenarioDefinition definition, ScenarioApplyResult result)
        {
            if (definition == null || definition.TriggersAndEvents == null || definition.TriggersAndEvents.Triggers.Count == 0)
                return;

            for (int i = 0; i < definition.TriggersAndEvents.Triggers.Count; i++)
            {
                TriggerDef trigger = definition.TriggersAndEvents.Triggers[i];
                if (trigger == null)
                    continue;

                string type = trigger.Type ?? string.Empty;
                result.AddMessage("Trigger '" + (trigger.Id ?? ("#" + i)) + "' of type '" + type
                    + "' is deferred because the XML schema does not yet define a safe runtime action target to invoke.");
            }
        }

        public void WinLossConditionApply(ScenarioDefinition definition, ScenarioApplyResult result)
        {
            if (definition == null || definition.WinLossConditions == null)
                return;

            ApplyConditionList(definition.WinLossConditions.WinConditions, "win", result);
            ApplyConditionList(definition.WinLossConditions.LossConditions, "loss", result);
        }

        private static void ApplyStats(FamilyMember member, FamilyMemberConfig config, ScenarioApplyResult result)
        {
            if (member == null || config == null || config.Stats.Count == 0 || member.BaseStats == null)
                return;

            for (int i = 0; i < config.Stats.Count; i++)
            {
                StatOverride stat = config.Stats[i];
                if (stat == null)
                    continue;

                BaseStats.StatType statType;
                if (!TryParseStatType(stat.StatId, out statType))
                {
                    result.AddMessage("Unknown stat id skipped for '" + (config.Name ?? member.firstName) + "': " + (stat.StatId ?? string.Empty));
                    continue;
                }

                BaseStat target = member.BaseStats.GetStatByEnum(statType);
                if (target == null)
                {
                    result.AddMessage("Stat target was unavailable for '" + (config.Name ?? member.firstName) + "': " + statType + ".");
                    continue;
                }

                int level = Mathf.Clamp(stat.Value, 0, 20);
                target.SetInitialLevel(level, 20);
                result.FamilyChanges++;
            }
        }

        private static void ApplyTraits(FamilyMember member, FamilyMemberConfig config, ScenarioApplyResult result)
        {
            if (member == null || config == null || config.Traits.Count == 0 || member.traits == null)
                return;

            for (int i = 0; i < config.Traits.Count; i++)
            {
                string traitId = config.Traits[i];
                Traits.Strength strength;
                if (TryParseStrengthTrait(traitId, out strength))
                {
                    if (member.traits.AddStrength(strength))
                        result.FamilyChanges++;
                    else
                        result.AddMessage("Strength trait was already active or blocked by its paired weakness: " + traitId);
                    continue;
                }

                Traits.Weakness weakness;
                if (TryParseWeaknessTrait(traitId, out weakness))
                {
                    if (member.traits.AddWeakness(weakness, true))
                        result.FamilyChanges++;
                    else
                        result.AddMessage("Weakness trait was already active or blocked by its paired strength: " + traitId);
                    continue;
                }

                result.AddMessage("Unknown trait id skipped for '" + (config.Name ?? member.firstName) + "': " + (traitId ?? string.Empty));
            }
        }

        private static void ApplySkills(FamilyMember member, FamilyMemberConfig config, ScenarioApplyResult result)
        {
            if (member == null || config == null || config.Skills.Count == 0)
                return;

            for (int i = 0; i < config.Skills.Count; i++)
            {
                SkillOverride skill = config.Skills[i];
                if (skill == null)
                    continue;

                result.AddMessage("Skill '" + (skill.SkillId ?? string.Empty) + "' level " + skill.Level
                    + " for '" + (config.Name ?? member.firstName)
                    + "' is deferred because Sheltered exposes no stable runtime skill/save API comparable to BaseStats or Traits.");
            }
        }

        private void ApplyAppearance(
            ScenarioDefinition definition,
            string scenarioFilePath,
            FamilyMember member,
            FamilyMemberConfig config,
            ScenarioApplyResult result)
        {
            if (member == null || config == null || config.Appearance == null)
                return;

            string message;
            if (_characterAppearanceService.ApplyConfiguredAppearance(definition, scenarioFilePath, config, member, out message))
                result.FamilyChanges++;
            else if (!string.IsNullOrEmpty(message))
                result.AddMessage(message);
        }

        private static void ApplyConditionList(List<ConditionDef> conditions, string outcome, ScenarioApplyResult result)
        {
            if (conditions == null || conditions.Count == 0)
                return;

            for (int i = 0; i < conditions.Count; i++)
            {
                ConditionDef condition = conditions[i];
                if (condition == null)
                    continue;

                result.AddMessage(outcome + " condition '" + (condition.Id ?? ("#" + i)) + "' of type '" + (condition.Type ?? string.Empty)
                    + "' is deferred because active scenario bindings do not yet persist the spawned QuestInstance id needed to complete/fail the scenario safely.");
            }
        }

        private static bool TryParseStatType(string value, out BaseStats.StatType statType)
        {
            statType = BaseStats.StatType.Max;
            string trimmed = TrimToNull(value);
            if (trimmed == null)
                return false;

            try
            {
                object parsed = Enum.Parse(typeof(BaseStats.StatType), trimmed, true);
                statType = (BaseStats.StatType)parsed;
                return statType != BaseStats.StatType.Max;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryParseStrengthTrait(string value, out Traits.Strength strength)
        {
            strength = Traits.Strength.Max;
            string trimmed = TrimTraitPrefix(value, "Strength:");
            if (trimmed == null)
                return false;

            try
            {
                object parsed = Enum.Parse(typeof(Traits.Strength), trimmed, true);
                strength = (Traits.Strength)parsed;
                return strength != Traits.Strength.Max;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryParseWeaknessTrait(string value, out Traits.Weakness weakness)
        {
            weakness = Traits.Weakness.Max;
            string trimmed = TrimTraitPrefix(value, "Weakness:");
            if (trimmed == null)
                return false;

            try
            {
                object parsed = Enum.Parse(typeof(Traits.Weakness), trimmed, true);
                weakness = (Traits.Weakness)parsed;
                return weakness != Traits.Weakness.Max;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryParseObjectType(string value, out ObjectManager.ObjectType objectType)
        {
            objectType = ObjectManager.ObjectType.Undefined;
            string trimmed = TrimToNull(value);
            if (trimmed == null)
                return false;

            try
            {
                object parsed = Enum.Parse(typeof(ObjectManager.ObjectType), trimmed, true);
                objectType = (ObjectManager.ObjectType)parsed;
                return objectType != ObjectManager.ObjectType.Undefined && objectType != ObjectManager.ObjectType.Max;
            }
            catch
            {
                return false;
            }
        }

        private static string TrimTraitPrefix(string value, string prefix)
        {
            string trimmed = TrimToNull(value);
            if (trimmed == null)
                return null;

            if (trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return TrimToNull(trimmed.Substring(prefix.Length));

            return trimmed.IndexOf(':') >= 0 ? null : trimmed;
        }

        private static int GetIntProperty(List<ScenarioProperty> properties, string key, int fallback)
        {
            string value = GetProperty(properties, key);
            int parsed;
            return !string.IsNullOrEmpty(value) && int.TryParse(value, out parsed) ? parsed : fallback;
        }

        private static bool GetBoolProperty(List<ScenarioProperty> properties, string key, bool fallback)
        {
            string value = GetProperty(properties, key);
            bool parsed;
            return !string.IsNullOrEmpty(value) && bool.TryParse(value, out parsed) ? parsed : fallback;
        }

        private static string GetProperty(List<ScenarioProperty> properties, string key)
        {
            if (properties == null || string.IsNullOrEmpty(key))
                return null;

            for (int i = 0; i < properties.Count; i++)
            {
                ScenarioProperty property = properties[i];
                if (property != null && string.Equals(property.Key, key, StringComparison.OrdinalIgnoreCase))
                    return property.Value;
            }

            return null;
        }

        private static string TrimToNull(string value)
        {
            if (string.IsNullOrEmpty(value))
                return null;

            string trimmed = value.Trim();
            return trimmed.Length == 0 ? null : trimmed;
        }

        private static void LogResult(ScenarioDefinition definition, ScenarioApplyResult result)
        {
            MMLog.WriteInfo("[ShelteredScenarioApplier] Applied scenario '" + definition.Id + "': familyChanges="
                + result.FamilyChanges + ", inventoryChanges=" + result.InventoryChanges + ", bunkerChanges=" + result.BunkerChanges
                + ", triggerChanges=" + result.TriggerChanges + ", conditionChanges=" + result.ConditionChanges
                + ", spriteSwapChanges=" + result.SpriteSwapChanges + ".");

            string[] messages = result.Messages;
            for (int i = 0; i < messages.Length; i++)
                MMLog.WriteInfo("[ShelteredScenarioApplier] " + messages[i]);
        }
    }
}
