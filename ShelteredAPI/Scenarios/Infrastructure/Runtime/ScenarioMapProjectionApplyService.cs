using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using ModAPI.Core;
using ShelteredAPI.Content.Compatibility;
using ShelteredAPI.Content;
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
            if (definition == null || definition.Map == null || definition.Map.Locations == null || definition.Map.Locations.Count == 0)
                return;

            ExpeditionMap map = ExpeditionMap.Instance;
            if (map == null)
            {
                AddMessage(result, "ExpeditionMap is not ready; authored map locations skipped.");
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
                result.MapChanges += projected;
                result.AddMessage("Projected " + projected.ToString(CultureInfo.InvariantCulture)
                    + " authored map location(s) onto the generated expedition map; authored loot items added="
                    + lootItems.ToString(CultureInfo.InvariantCulture) + ".");
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
            List<MapLootProjectionEntry> result = new List<MapLootProjectionEntry>();
            List<MapLootEntryDefinition> candidates = new List<MapLootEntryDefinition>();
            if (table == null || table.Entries == null)
                return result;

            int seed = BuildLootSeed(definition, location, table);
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
            if (table != null && table.SearchNpcRevealChance >= 0)
                region.chanceThatSearchRevealsNpcs = ClampPercent(table.SearchNpcRevealChance);
            if (table != null && table.AnimalEncounterChance >= 0)
                region.chanceThatEncounterIsAnimal = ClampPercent(table.AnimalEncounterChance);
            if (table != null && table.FactionEncounterChance >= 0 && MapRegionFactionEncounterChanceField != null)
                MapRegionFactionEncounterChanceField.SetValue(region, ClampPercent(table.FactionEncounterChance));

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

        private static int BuildLootSeed(ScenarioDefinition definition, MapLocationDefinition location, MapLootTableDefinition table)
        {
            int masterSeed = ModRandom.CurrentSeed;
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
}
