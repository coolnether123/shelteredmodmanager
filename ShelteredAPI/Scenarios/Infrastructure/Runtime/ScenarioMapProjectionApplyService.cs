using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using ModAPI.Core;
using ShelteredAPI.Content.Compatibility;
using ShelteredAPI.Content;
using ShelteredAPI.Infrastructure;
using ShelteredAPI.Scenarios.Application.Runtime;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Domain.Map;
using UnityEngine;

namespace ShelteredAPI.Scenarios.Infrastructure.Runtime{
    internal sealed class ScenarioMapProjectionApplyService
    {
        private static readonly BindingFlags InstanceAny = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly FieldInfo MapRegionTopographyField = typeof(MapRegion).GetField("m_topography", InstanceAny);
        private static readonly FieldInfo MapRegionItemsField = typeof(MapRegion).GetField("m_items", InstanceAny);
        private static readonly FieldInfo MapRegionLocationItemsCountField = typeof(MapRegion).GetField("m_locationItemsCount", InstanceAny);
        private static readonly FieldInfo MapRegionCommonItemsCountField = typeof(MapRegion).GetField("m_commonItemsCount", InstanceAny);
        private static readonly FieldInfo MapRegionHiddenItemTypesField = typeof(MapRegion).GetField("m_hiddenItemTypes", InstanceAny);
        private static readonly FieldInfo MapRegionRequiredHiddenItemTypesField = typeof(MapRegion).GetField("m_requiredForHiddenItemTypes", InstanceAny);
        private static readonly FieldInfo MapRegionWeightedHiddenItemsField = typeof(MapRegion).GetField("m_weightedHiddenItems", InstanceAny);
        private static readonly FieldInfo MapRegionHiddenItemsUnlockedField = typeof(MapRegion).GetField("m_hiddenItemsUnlocked", InstanceAny);
        private static readonly FieldInfo MapRegionFactionEncounterChanceField = typeof(MapRegion).GetField("m_chanceOfOpenGroundFactionEncounter", InstanceAny);
        private static readonly int DefaultHiddenUnlockItem = (int)ItemManager.ItemType.LockpickSet;

        public void Apply(ScenarioDefinition definition, ScenarioApplyResult result)
        {
            string message;
            if (!SeamGuard.Run(
                "scenario.map.projection",
                SeamRecoveryPolicy.DisableSeamAndDegrade,
                delegate { ApplyCore(definition, result); },
                "Map projection unavailable - scenario still playable.",
                null,
                out message))
            {
                AddMessage(result, message);
            }
        }

        private void ApplyCore(ScenarioDefinition definition, ScenarioApplyResult result)
        {
            if (definition == null || definition.Map == null)
                return;

            ExpeditionMap map = ExpeditionMap.Instance;
            if (map == null)
            {
                AddMessage(result, "ExpeditionMap is not ready; authored map locations skipped.");
                return;
            }

            int terrainChanges = ScenarioMapTerrainProjection.Apply(definition.Map, map, result);
            if (definition.Map.Locations == null || definition.Map.Locations.Count == 0)
            {
                if (terrainChanges > 0 && result != null)
                    result.MapChanges += terrainChanges;
                return;
            }

            bool isLoading = SaveManager.instance != null && SaveManager.instance.isLoading && !SaveManager.instance.isRelocating;
            MapLocationDefinition startLocation = FindLocation(definition.Map, definition.Map.StartLocationId);
            int projected = 0;
            int lootItems = 0;

            for (int i = 0; i < definition.Map.Locations.Count; i++)
            {
                MapLocationDefinition location = definition.Map.Locations[i];
                if (location == null)
                    continue;

                ExpeditionMap.GridRef gridRef = new ExpeditionMap.GridRef(location.GridX, location.GridY);
                MapRegion region = map.GetRegionOnMap(gridRef);
                if (region == null)
                {
                    AddMessage(result, "Map location '" + Safe(location.Id) + "' skipped because no generated region exists at grid "
                        + location.GridX.ToString(CultureInfo.InvariantCulture) + "," + location.GridY.ToString(CultureInfo.InvariantCulture)
                        + ". Runtime region injection is deferred to avoid bypassing ExpeditionMap.CreateRegion prefab setup.");
                    continue;
                }

                if (region.topography == MapRegion.Topography.NowhereSpecial)
                {
                    AddMessage(result, "Map location '" + Safe(location.Id) + "' skipped because grid "
                        + location.GridX.ToString(CultureInfo.InvariantCulture) + "," + location.GridY.ToString(CultureInfo.InvariantCulture)
                        + " is an empty NowhereSpecial cell. Empty-grid region creation is blocked because ExpeditionMap.CreateRegion is private and wires prefab/icon/fog state from private map scratchpad data.");
                    continue;
                }

                bool forceStartVisible = startLocation != null && string.Equals(startLocation.Id, location.Id, StringComparison.OrdinalIgnoreCase);
                ProjectLocation(region, location, forceStartVisible, isLoading);
                ProjectEncounter(region, definition.Map, location, result);
                lootItems += ProjectLoot(region, definition, location, isLoading, result);
                projected++;
            }

            if (projected > 0 && result != null)
            {
                result.MapChanges += projected + terrainChanges;
                result.AddMessage("Projected " + projected.ToString(CultureInfo.InvariantCulture)
                    + " authored map location(s) onto the generated expedition map; authored loot items added="
                    + lootItems.ToString(CultureInfo.InvariantCulture) + ".");
            }
            else if (terrainChanges > 0 && result != null)
            {
                result.MapChanges += terrainChanges;
            }
        }

        private static void ProjectLocation(MapRegion region, MapLocationDefinition location, bool forceStartVisible, bool isLoading)
        {
            string displayName = TrimToNull(location.DisplayName);
            if (displayName != null)
                region.regionName = displayName;

            string kind = TrimToNull(location.Kind);
            if (kind != null)
            {
                region.category = kind;
                MapRegion.Topography topography;
                if (TryParseTopography(kind, out topography))
                    SetTopography(region, topography);
            }

            region.isSearchable = location.Searchable;
            region.isHiddenUntilDiscovered = location.HiddenUntilDiscovered && !forceStartVisible;
            if (!string.IsNullOrEmpty(location.IconId))
                region.ChangeIcon(location.IconId);
            if (location.Danger >= 0)
                region.chanceOfOpenGroundEncounter = ClampPercent(location.Danger);

            ApplyVisibility(region, location, forceStartVisible, isLoading);
        }

        private static void ApplyVisibility(MapRegion region, MapLocationDefinition location, bool forceStartVisible, bool isLoading)
        {
            bool startsDiscovered = forceStartVisible || location.DiscoveredAtStart;
            bool startsVisible = forceStartVisible || location.VisibleAtStart;

            if (startsDiscovered)
            {
                region.discovered = true;
                region.SetShownOnMap(true);
                return;
            }

            if (isLoading)
            {
                if (region.discovered)
                    region.SetShownOnMap(true);
                else if (region.isVisibleOnMap || (startsVisible && !region.isHiddenUntilDiscovered))
                    region.SetShownOnMap(true);
                return;
            }

            region.SetShownOnMap(startsVisible && !region.isHiddenUntilDiscovered);
        }

        private static int ProjectLoot(
            MapRegion region,
            ScenarioDefinition definition,
            MapLocationDefinition location,
            bool isLoading,
            ScenarioApplyResult result)
        {
            string lootTableId = TrimToNull(location.LootTableId);
            if (lootTableId == null)
                return 0;

            if (isLoading)
                return 0;

            MapAuthoringDefinition map = definition != null ? definition.Map : null;
            MapLootTableDefinition table = FindLootTable(map, lootTableId);
            if (table == null)
            {
                AddMessage(result, "Map location '" + Safe(location.Id) + "' references missing loot table '" + lootTableId + "' at runtime.");
                return 0;
            }

            if (location.ReplaceGeneratedLoot)
                ClearRegionLoot(region);

            List<MapLootProjectionEntry> rolled = PlanLootRolls(definition, location, table);
            int added = 0;
            for (int i = 0; rolled != null && i < rolled.Count; i++)
            {
                MapLootProjectionEntry entry = rolled[i];
                if (entry == null || entry.Quantity <= 0 || string.IsNullOrEmpty(entry.ItemId))
                    continue;

                ItemManager.ItemType type;
                if (!InventoryHelper.ResolveItemType(entry.ItemId, out type))
                {
                    AddMessage(result, "Unknown map loot item id skipped: " + entry.ItemId);
                    continue;
                }

                if (entry.Hidden)
                {
                    ItemManager.ItemType unlockType;
                    if (!ResolveHiddenUnlockItem(entry.HiddenUnlockItemId, out unlockType))
                    {
                        AddMessage(result, "Map location '" + Safe(location.Id) + "' hidden loot item '" + entry.ItemId
                            + "' skipped because hiddenUnlockItemId '" + Safe(entry.HiddenUnlockItemId) + "' is not a known item id.");
                        continue;
                    }

                    int missing = entry.Quantity - CountHiddenRegionItems(region, type);
                    if (missing > 0 && region.AddHiddenItem(type, missing, unlockType))
                        added += missing;
                    continue;
                }

                int visibleMissing = entry.Quantity - CountRegionItems(region, type);
                for (int addIndex = 0; addIndex < visibleMissing; addIndex++)
                {
                    if (region.AddItem(type, false))
                        added++;
                    else
                    {
                        AddMessage(result, "Map location '" + Safe(location.Id) + "' could not accept authored loot item '"
                            + type + "'; MapRegion.AddItem rejected it, likely because the vanilla item cap is full.");
                        break;
                    }
                }
            }

            return added;
        }

        internal static List<MapLootProjectionEntry> PlanLootRolls(
            ScenarioDefinition definition,
            MapLocationDefinition location,
            MapLootTableDefinition table)
        {
            return PlanLootRolls(definition, location, table, ModRandom.CurrentSeed);
        }

        internal static List<MapLootProjectionEntry> PlanLootRolls(
            ScenarioDefinition definition,
            MapLocationDefinition location,
            MapLootTableDefinition table,
            int masterSeed)
        {
            List<MapLootProjectionEntry> result = new List<MapLootProjectionEntry>();
            List<MapLootEntryDefinition> candidates = new List<MapLootEntryDefinition>();
            if (table == null || table.Entries == null)
                return result;

            int seed = BuildLootSeed(definition, location, table, masterSeed);
            ModRandomStream random = new ModRandomStream(seed);
            bool weighted = HasWeightedEntries(table);
            int totalWeightedPicks = 0;

            for (int i = 0; table != null && table.Entries != null && i < table.Entries.Count; i++)
            {
                MapLootEntryDefinition entry = table.Entries[i];
                if (entry == null || string.IsNullOrEmpty(entry.ItemId))
                    continue;

                float chance = Clamp01(entry.Chance);
                if (chance < 1f && random.Value() > chance)
                    continue;

                int quantity = RollQuantity(entry, random);
                if (quantity <= 0)
                    continue;

                if (weighted)
                {
                    candidates.Add(entry);
                    totalWeightedPicks += quantity;
                    continue;
                }

                AddRolledEntry(result, entry, quantity);
            }

            if (weighted)
            {
                for (int i = 0; i < totalWeightedPicks; i++)
                {
                    MapLootEntryDefinition selected = PickWeighted(candidates, random);
                    AddRolledEntry(result, selected, 1);
                }
            }

            return result;
        }

        internal static string BuildLootRollSignature(List<MapLootProjectionEntry> entries)
        {
            List<string> parts = new List<string>();
            for (int i = 0; entries != null && i < entries.Count; i++)
            {
                MapLootProjectionEntry entry = entries[i];
                if (entry == null)
                    continue;
                parts.Add((entry.Hidden ? "hidden:" : "visible:") + Safe(entry.ItemId) + ":"
                    + entry.Quantity.ToString(CultureInfo.InvariantCulture) + ":" + Safe(entry.HiddenUnlockItemId));
            }

            parts.Sort(StringComparer.OrdinalIgnoreCase);
            return string.Join("|", parts.ToArray());
        }

        private static void ProjectEncounter(
            MapRegion region,
            MapAuthoringDefinition map,
            MapLocationDefinition location,
            ScenarioApplyResult result)
        {
            MapEncounterTableDefinition table = FindEncounterTable(map, location != null ? location.EncounterTableId : null);
            if (!string.IsNullOrEmpty(location != null ? location.EncounterTableId : null) && table == null)
            {
                AddMessage(result, "Map location '" + Safe(location.Id) + "' references missing encounter table '" + location.EncounterTableId + "' at runtime.");
                return;
            }

            int openGroundChance = table != null && table.OpenGroundChance >= 0 ? table.OpenGroundChance : (location != null ? location.Danger : -1);
            if (openGroundChance >= 0)
                region.chanceOfOpenGroundEncounter = ClampPercent(openGroundChance);
            ScenarioMapProjectionFieldCatalog.ApplyEncounterFields(region, table, MapRegionFactionEncounterChanceField);

            if (table != null && table.Entries != null && table.Entries.Count > 0)
                AddMessage(result, "Map location '" + Safe(location.Id) + "' projected encounter chance fields from table '"
                    + Safe(table.Id) + "'. Encounter table entries are retained in scenario data but are not projected because vanilla EncounterGenerator does not read MapRegion-authored tables.");
        }

        private static int CountRegionItems(MapRegion region, ItemManager.ItemType type)
        {
            IList items = MapRegionItemsField != null ? MapRegionItemsField.GetValue(region) as IList : null;
            if (items == null)
                return 0;

            int count = 0;
            for (int i = 0; i < items.Count; i++)
            {
                object item = items[i];
                if (item == null)
                    continue;

                FieldInfo typeField = item.GetType().GetField("m_type", InstanceAny);
                object value = typeField != null ? typeField.GetValue(item) : null;
                if (value is ItemManager.ItemType && (ItemManager.ItemType)value == type)
                    count++;
            }

            return count;
        }

        private static int CountHiddenRegionItems(MapRegion region, ItemManager.ItemType type)
        {
            IList hiddenItems = MapRegionHiddenItemTypesField != null ? MapRegionHiddenItemTypesField.GetValue(region) as IList : null;
            if (hiddenItems == null)
                return 0;

            int count = 0;
            for (int i = 0; i < hiddenItems.Count; i++)
            {
                object item = hiddenItems[i];
                if (item == null)
                    continue;

                FieldInfo typeField = item.GetType().GetField("itemType", InstanceAny);
                FieldInfo biasField = item.GetType().GetField("bias", InstanceAny);
                object value = typeField != null ? typeField.GetValue(item) : null;
                object bias = biasField != null ? biasField.GetValue(item) : null;
                if (value is ItemManager.ItemType && (ItemManager.ItemType)value == type && bias is int)
                    count += Math.Max(0, (int)bias);
            }

            return count;
        }

        private static void ClearRegionLoot(MapRegion region)
        {
            IList items = MapRegionItemsField != null ? MapRegionItemsField.GetValue(region) as IList : null;
            if (items != null)
                items.Clear();
            if (MapRegionLocationItemsCountField != null)
                MapRegionLocationItemsCountField.SetValue(region, 0);
            if (MapRegionCommonItemsCountField != null)
                MapRegionCommonItemsCountField.SetValue(region, 0);

            IList hiddenItems = MapRegionHiddenItemTypesField != null ? MapRegionHiddenItemTypesField.GetValue(region) as IList : null;
            if (hiddenItems != null)
                hiddenItems.Clear();
            IList required = MapRegionRequiredHiddenItemTypesField != null ? MapRegionRequiredHiddenItemTypesField.GetValue(region) as IList : null;
            if (required != null)
                required.Clear();
            IList weighted = MapRegionWeightedHiddenItemsField != null ? MapRegionWeightedHiddenItemsField.GetValue(region) as IList : null;
            if (weighted != null)
                weighted.Clear();
            if (MapRegionHiddenItemsUnlockedField != null)
                MapRegionHiddenItemsUnlockedField.SetValue(region, false);
        }

        private static MapLocationDefinition FindLocation(MapAuthoringDefinition map, string id)
        {
            string target = TrimToNull(id);
            if (map == null || map.Locations == null || target == null)
                return null;

            for (int i = 0; i < map.Locations.Count; i++)
            {
                MapLocationDefinition location = map.Locations[i];
                if (location != null && string.Equals(location.Id, target, StringComparison.OrdinalIgnoreCase))
                    return location;
            }

            return null;
        }

        private static MapLootTableDefinition FindLootTable(MapAuthoringDefinition map, string id)
        {
            string target = TrimToNull(id);
            if (map == null || map.LootTables == null || target == null)
                return null;

            for (int i = 0; i < map.LootTables.Count; i++)
            {
                MapLootTableDefinition table = map.LootTables[i];
                if (table != null && string.Equals(table.Id, target, StringComparison.OrdinalIgnoreCase))
                    return table;
            }

            return null;
        }

        private static MapEncounterTableDefinition FindEncounterTable(MapAuthoringDefinition map, string id)
        {
            string target = TrimToNull(id);
            if (map == null || map.EncounterTables == null || target == null)
                return null;

            for (int i = 0; i < map.EncounterTables.Count; i++)
            {
                MapEncounterTableDefinition table = map.EncounterTables[i];
                if (table != null && string.Equals(table.Id, target, StringComparison.OrdinalIgnoreCase))
                    return table;
            }

            return null;
        }

        private static bool ResolveHiddenUnlockItem(string itemId, out ItemManager.ItemType type)
        {
            type = (ItemManager.ItemType)DefaultHiddenUnlockItem;
            string id = TrimToNull(itemId);
            return id != null && InventoryHelper.ResolveItemType(id, out type);
        }

        private static int BuildLootSeed(ScenarioDefinition definition, MapLocationDefinition location, MapLootTableDefinition table, int masterSeed)
        {
            string key = "map-loot-v1|"
                + masterSeed.ToString(CultureInfo.InvariantCulture) + "|"
                + Safe(definition != null ? definition.Id : null) + "|"
                + Safe(table != null ? table.Id : null) + "|"
                + Safe(location != null ? location.Id : null) + "|"
                + (location != null ? location.GridX.ToString(CultureInfo.InvariantCulture) : "0") + ","
                + (location != null ? location.GridY.ToString(CultureInfo.InvariantCulture) : "0") + "|"
                + BuildLootTableSignature(table);
            int seed = ContentRegistry.StableContentIdHash(key);
            return seed == 0 ? 1 : seed;
        }

        private static string BuildLootTableSignature(MapLootTableDefinition table)
        {
            List<string> parts = new List<string>();
            for (int i = 0; table != null && table.Entries != null && i < table.Entries.Count; i++)
            {
                MapLootEntryDefinition entry = table.Entries[i];
                if (entry == null)
                    continue;
                parts.Add(Safe(entry.ItemId) + ":" + entry.MinQuantity.ToString(CultureInfo.InvariantCulture)
                    + ":" + entry.MaxQuantity.ToString(CultureInfo.InvariantCulture)
                    + ":" + entry.Weight.ToString(CultureInfo.InvariantCulture)
                    + ":" + entry.Chance.ToString(CultureInfo.InvariantCulture)
                    + ":" + (entry.Hidden ? "H" : "V")
                    + ":" + Safe(entry.HiddenUnlockItemId));
            }

            return string.Join(";", parts.ToArray());
        }

        private static bool HasWeightedEntries(MapLootTableDefinition table)
        {
            for (int i = 0; table != null && table.Entries != null && i < table.Entries.Count; i++)
            {
                MapLootEntryDefinition entry = table.Entries[i];
                if (entry != null && entry.Weight != 1)
                    return true;
            }

            return false;
        }

        private static int RollQuantity(MapLootEntryDefinition entry, ModRandomStream random)
        {
            if (entry == null)
                return 0;
            int min = Math.Max(0, entry.MinQuantity);
            int max = Math.Max(min, entry.MaxQuantity);
            if (max <= min)
                return min;
            return random.Range(min, max + 1);
        }

        private static MapLootEntryDefinition PickWeighted(List<MapLootEntryDefinition> entries, ModRandomStream random)
        {
            if (entries == null || entries.Count == 0)
                return null;

            int total = 0;
            for (int i = 0; i < entries.Count; i++)
                total += Math.Max(1, entries[i] != null ? entries[i].Weight : 1);

            int roll = random.Range(0, total);
            int cursor = 0;
            for (int i = 0; i < entries.Count; i++)
            {
                cursor += Math.Max(1, entries[i] != null ? entries[i].Weight : 1);
                if (roll < cursor)
                    return entries[i];
            }

            return entries[entries.Count - 1];
        }

        private static void AddRolledEntry(List<MapLootProjectionEntry> entries, MapLootEntryDefinition entry, int quantity)
        {
            if (entries == null || entry == null || quantity <= 0 || string.IsNullOrEmpty(entry.ItemId))
                return;

            for (int i = 0; i < entries.Count; i++)
            {
                MapLootProjectionEntry existing = entries[i];
                if (existing != null
                    && existing.Hidden == entry.Hidden
                    && string.Equals(existing.ItemId, entry.ItemId, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(existing.HiddenUnlockItemId, entry.HiddenUnlockItemId, StringComparison.OrdinalIgnoreCase))
                {
                    existing.Quantity += quantity;
                    return;
                }
            }

            entries.Add(new MapLootProjectionEntry
            {
                ItemId = entry.ItemId,
                Quantity = quantity,
                Hidden = entry.Hidden,
                HiddenUnlockItemId = entry.HiddenUnlockItemId
            });
        }

        private static float Clamp01(float value)
        {
            if (value < 0f)
                return 0f;
            if (value > 1f)
                return 1f;
            return value;
        }

        private static bool TryParseTopography(string value, out MapRegion.Topography topography)
        {
            topography = MapRegion.Topography.NowhereSpecial;
            try
            {
                topography = (MapRegion.Topography)Enum.Parse(typeof(MapRegion.Topography), value, true);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static void SetTopography(MapRegion region, MapRegion.Topography topography)
        {
            if (MapRegionTopographyField != null)
                MapRegionTopographyField.SetValue(region, topography);
        }

        private static int ClampPercent(int value)
        {
            if (value < 0)
                return 0;
            if (value > 100)
                return 100;
            return value;
        }

        private static string TrimToNull(string value)
        {
            if (string.IsNullOrEmpty(value))
                return null;
            string trimmed = value.Trim();
            return trimmed.Length == 0 ? null : trimmed;
        }

        private static string Safe(string value)
        {
            return value ?? string.Empty;
        }

        private static void AddMessage(ScenarioApplyResult result, string message)
        {
            if (result != null)
                result.AddMessage(message);
            else
                MMLog.WriteWarning("[ScenarioMapProjection] " + message);
        }
    }

    internal sealed class MapLootProjectionEntry
    {
        public string ItemId { get; set; }
        public int Quantity { get; set; }
        public bool Hidden { get; set; }
        public string HiddenUnlockItemId { get; set; }
    }

    internal static class ScenarioMapTerrainProjection
    {
        private static readonly BindingFlags InstanceAny = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly FieldInfo MapRegionTopographyField = typeof(MapRegion).GetField("m_topography", InstanceAny);
        private static int BaseTerrainMapId = int.MinValue;
        private static BaseTerrainSnapshot BaseTerrain;

        public static int Apply(MapAuthoringDefinition definition, ExpeditionMap map, ScenarioApplyResult result)
        {
            if (definition == null || map == null)
                return 0;

            BaseTerrainSnapshot baseTerrain = GetBaseTerrainSnapshot(map);
            int changed = 0;
            for (int x = 0; x < map.width; x++)
            {
                for (int y = 0; y < map.height; y++)
                {
                    string terrainId = ResolveTerrainAtCell(definition, map, baseTerrain, x, y);
                    if (string.IsNullOrEmpty(terrainId))
                        continue;

                    string reason;
                    if (TryApplyCell(map, x, y, terrainId, out reason))
                        changed++;
                    else if (result != null)
                        result.AddMessage("Map terrain at " + x.ToString(CultureInfo.InvariantCulture) + ","
                            + y.ToString(CultureInfo.InvariantCulture) + " was skipped: " + reason);
                }
            }

            if (changed > 0 && result != null)
                result.AddMessage("Projected " + changed.ToString(CultureInfo.InvariantCulture) + " authored terrain cell(s) onto the expedition map.");
            return changed;
        }

        public static bool TryApplyCell(ExpeditionMap map, int gridX, int gridY, string terrainId, out string reason)
        {
            reason = null;
            if (map == null)
            {
                reason = "The expedition map is unavailable.";
                return false;
            }

            MapRegion.Topography topography;
            if (!TryParseTerrain(terrainId, out topography))
            {
                reason = "Unknown terrain id '" + (terrainId ?? string.Empty) + "'.";
                return false;
            }

            MapRegion region = map.GetRegionOnMap(new ExpeditionMap.GridRef(gridX, gridY));
            if (region == null)
            {
                reason = "No generated MapRegion exists at that cell.";
                return false;
            }

            string iconId = FindTerrainIcon(map, topography);
            if (MapRegionTopographyField == null)
            {
                reason = "MapRegion terrain reflection is unavailable on this game build.";
                return false;
            }

            MapRegionTopographyField.SetValue(region, topography);
            region.category = topography.ToString();
            region.SetGridReference(new ExpeditionMap.GridRef(gridX, gridY));
            if (!string.IsNullOrEmpty(iconId))
                region.ChangeIcon(iconId);
            if (topography == MapRegion.Topography.Woodland || topography == MapRegion.Topography.Mountains)
                region.SetShownOnMap(true);
            else if (topography == MapRegion.Topography.NowhereSpecial)
                region.SetShownOnMap(false);
            return true;
        }

        public static bool TryParseTerrain(string terrainId, out MapRegion.Topography topography)
        {
            topography = MapRegion.Topography.NowhereSpecial;
            if (string.IsNullOrEmpty(terrainId))
                return false;

            try
            {
                topography = (MapRegion.Topography)Enum.Parse(typeof(MapRegion.Topography), terrainId, true);
                return topography == MapRegion.Topography.NowhereSpecial
                    || topography == MapRegion.Topography.Woodland
                    || topography == MapRegion.Topography.Mountains;
            }
            catch
            {
                return false;
            }
        }

        private static string ResolveTerrainAtCell(
            MapAuthoringDefinition definition,
            ExpeditionMap map,
            BaseTerrainSnapshot baseTerrain,
            int gridX,
            int gridY)
        {
            string terrainId = definition.DefaultTerrainId;
            int priority = int.MinValue;
            MapTerrainPatchDefinition winner = null;
            for (int i = 0; definition.TerrainPatches != null && i < definition.TerrainPatches.Count; i++)
            {
                MapTerrainPatchDefinition patch = definition.TerrainPatches[i];
                if (patch == null || !ContainsCell(patch, gridX, gridY) || patch.Priority < priority)
                    continue;

                terrainId = patch.TerrainId;
                priority = patch.Priority;
                winner = patch;
            }

            if (string.Equals(terrainId, ScenarioMapTerrainModes.GeneratedBlend, StringComparison.OrdinalIgnoreCase))
                return ResolveGeneratedBlend(definition, map, baseTerrain, winner, gridX, gridY);
            return terrainId;
        }

        private static string ResolveGeneratedBlend(
            MapAuthoringDefinition definition,
            ExpeditionMap map,
            BaseTerrainSnapshot baseTerrain,
            MapTerrainPatchDefinition patch,
            int gridX,
            int gridY)
        {
            if (baseTerrain == null || baseTerrain.Terrain == null)
                return null;

            MapRegion.Topography baseValue = baseTerrain.Terrain[gridX, gridY];
            if (!IsTerrainTopography(baseValue))
                return null;

            List<MapRegion.Topography> candidates = new List<MapRegion.Topography>();
            AddCandidate(candidates, baseValue, 4);
            int[] dx = new int[] { -1, 1, 0, 0 };
            int[] dy = new int[] { 0, 0, -1, 1 };
            for (int i = 0; i < dx.Length; i++)
            {
                int x = gridX + dx[i];
                int y = gridY + dy[i];
                if (x < 0 || y < 0 || x >= map.width || y >= map.height)
                    continue;

                AddCandidate(candidates, baseTerrain.Terrain[x, y], 1);
                MapRegion.Topography manual;
                if (TryResolveManualTerrain(definition, x, y, out manual))
                    AddCandidate(candidates, manual, 4);
            }

            if (candidates.Count == 0)
                return baseValue.ToString();

            string key = (map != null ? map.randomSeed.ToString(CultureInfo.InvariantCulture) : "0") + "|"
                + (patch != null ? patch.Id : "generated-blend") + "|"
                + gridX.ToString(CultureInfo.InvariantCulture) + "|" + gridY.ToString(CultureInfo.InvariantCulture);
            int hash = ContentRegistry.StableContentIdHash(key);
            int index = (int)((uint)hash % (uint)candidates.Count);
            return candidates[index].ToString();
        }

        private static bool TryResolveManualTerrain(MapAuthoringDefinition definition, int gridX, int gridY, out MapRegion.Topography topography)
        {
            topography = MapRegion.Topography.NowhereSpecial;
            string terrainId = definition != null ? definition.DefaultTerrainId : null;
            int priority = int.MinValue;
            for (int i = 0; definition != null && definition.TerrainPatches != null && i < definition.TerrainPatches.Count; i++)
            {
                MapTerrainPatchDefinition patch = definition.TerrainPatches[i];
                if (patch == null || !ContainsCell(patch, gridX, gridY) || patch.Priority < priority)
                    continue;
                if (string.Equals(patch.TerrainId, ScenarioMapTerrainModes.GeneratedBlend, StringComparison.OrdinalIgnoreCase))
                    continue;

                terrainId = patch.TerrainId;
                priority = patch.Priority;
            }
            return TryParseTerrain(terrainId, out topography);
        }

        private static void AddCandidate(List<MapRegion.Topography> candidates, MapRegion.Topography topography, int weight)
        {
            if (candidates == null || !IsTerrainTopography(topography))
                return;
            for (int i = 0; i < weight; i++)
                candidates.Add(topography);
        }

        private static bool IsTerrainTopography(MapRegion.Topography topography)
        {
            return topography == MapRegion.Topography.NowhereSpecial
                || topography == MapRegion.Topography.Woodland
                || topography == MapRegion.Topography.Mountains;
        }

        private static bool ContainsCell(MapTerrainPatchDefinition patch, int gridX, int gridY)
        {
            float x = gridX + 0.5f;
            float y = gridY + 0.5f;
            if (patch.Shape == MapTerrainBrushShape.Circle)
            {
                float dx = x - patch.X;
                float dy = y - patch.Y;
                return dx * dx + dy * dy <= patch.Radius * patch.Radius;
            }

            if (patch.Shape == MapTerrainBrushShape.Polygon)
                return ContainsPolygonPoint(patch.Points, x, y);

            return x >= patch.X && y >= patch.Y && x < patch.X + patch.Width && y < patch.Y + patch.Height;
        }

        private static bool ContainsPolygonPoint(List<MapPointDefinition> points, float x, float y)
        {
            if (points == null || points.Count < 3)
                return false;

            bool inside = false;
            int previous = points.Count - 1;
            for (int current = 0; current < points.Count; current++)
            {
                MapPointDefinition a = points[current];
                MapPointDefinition b = points[previous];
                if (a != null && b != null
                    && ((a.Y > y) != (b.Y > y))
                    && x < (b.X - a.X) * (y - a.Y) / (b.Y - a.Y) + a.X)
                {
                    inside = !inside;
                }
                previous = current;
            }
            return inside;
        }

        private static string FindTerrainIcon(ExpeditionMap map, MapRegion.Topography topography)
        {
            BaseTerrainSnapshot snapshot = GetBaseTerrainSnapshot(map);
            string cached;
            if (snapshot != null && snapshot.Icons.TryGetValue(topography, out cached))
                return cached;

            for (int x = 0; map != null && x < map.width; x++)
            {
                for (int y = 0; y < map.height; y++)
                {
                    MapRegion sample = map.GetRegionOnMap(new ExpeditionMap.GridRef(x, y));
                    if (sample == null || sample.topography != topography)
                        continue;

                    UISprite sprite = sample.GetComponent<UISprite>();
                    if (sprite != null && !string.IsNullOrEmpty(sprite.spriteName))
                    {
                        if (snapshot != null)
                            snapshot.Icons[topography] = sprite.spriteName;
                        return sprite.spriteName;
                    }
                }
            }
            return null;
        }

        private static BaseTerrainSnapshot GetBaseTerrainSnapshot(ExpeditionMap map)
        {
            if (map == null)
                return null;

            int id = map.GetInstanceID();
            BaseTerrainSnapshot snapshot = BaseTerrain;
            if (BaseTerrainMapId == id
                && snapshot != null
                && snapshot.Width == map.width
                && snapshot.Height == map.height)
            {
                return snapshot;
            }

            snapshot = new BaseTerrainSnapshot(map.width, map.height);
            for (int x = 0; x < map.width; x++)
            {
                for (int y = 0; y < map.height; y++)
                {
                    MapRegion region = map.GetRegionOnMap(new ExpeditionMap.GridRef(x, y));
                    if (region == null)
                        continue;
                    snapshot.Terrain[x, y] = region.topography;
                    UISprite sprite = region.GetComponent<UISprite>();
                    if (sprite != null && !string.IsNullOrEmpty(sprite.spriteName) && !snapshot.Icons.ContainsKey(region.topography))
                        snapshot.Icons.Add(region.topography, sprite.spriteName);
                }
            }
            // Sheltered exposes one active ExpeditionMap singleton. Retaining only
            // its snapshot avoids leaking destroyed Unity map instances across
            // editor reloads while preserving the generated base for live previews.
            BaseTerrainMapId = id;
            BaseTerrain = snapshot;
            return snapshot;
        }

        private sealed class BaseTerrainSnapshot
        {
            public BaseTerrainSnapshot(int width, int height)
            {
                Width = width;
                Height = height;
                Terrain = new MapRegion.Topography[width, height];
                Icons = new Dictionary<MapRegion.Topography, string>();
            }

            public int Width { get; private set; }
            public int Height { get; private set; }
            public MapRegion.Topography[,] Terrain { get; private set; }
            public Dictionary<MapRegion.Topography, string> Icons { get; private set; }
        }
    }
}
