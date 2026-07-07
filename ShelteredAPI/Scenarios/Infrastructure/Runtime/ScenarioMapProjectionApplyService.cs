using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using ModAPI.Core;
using ShelteredAPI.Content.Compatibility;
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

                bool forceStartVisible = startLocation != null && string.Equals(startLocation.Id, location.Id, StringComparison.OrdinalIgnoreCase);
                ProjectLocation(region, location, forceStartVisible, isLoading);
                if (!string.IsNullOrEmpty(location.EncounterTableId))
                    AddMessage(result, "Map location '" + Safe(location.Id) + "' keeps encounter table reference '"
                        + location.EncounterTableId + "' in scenario data; direct encounter-table runtime projection is deferred.");
                lootItems += ProjectLoot(region, definition.Map, location, isLoading, result);
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
            MapAuthoringDefinition map,
            MapLocationDefinition location,
            bool isLoading,
            ScenarioApplyResult result)
        {
            string lootTableId = TrimToNull(location.LootTableId);
            if (lootTableId == null)
                return 0;

            if (isLoading)
                return 0;

            MapLootTableDefinition table = FindLootTable(map, lootTableId);
            if (table == null)
            {
                AddMessage(result, "Map location '" + Safe(location.Id) + "' references missing loot table '" + lootTableId + "' at runtime.");
                return 0;
            }

            Dictionary<ItemManager.ItemType, int> desired = BuildDesiredLootCounts(table, result);
            int added = 0;
            foreach (KeyValuePair<ItemManager.ItemType, int> pair in desired)
            {
                int missing = pair.Value - CountRegionItems(region, pair.Key);
                for (int i = 0; i < missing; i++)
                {
                    if (region.AddItem(pair.Key, false))
                        added++;
                    else
                    {
                        AddMessage(result, "Map location '" + Safe(location.Id) + "' could not accept authored loot item '"
                            + pair.Key + "'; MapRegion.AddItem rejected it, likely because the vanilla item cap is full.");
                        break;
                    }
                }
            }

            return added;
        }

        private static Dictionary<ItemManager.ItemType, int> BuildDesiredLootCounts(MapLootTableDefinition table, ScenarioApplyResult result)
        {
            Dictionary<ItemManager.ItemType, int> desired = new Dictionary<ItemManager.ItemType, int>();
            for (int i = 0; table != null && table.Entries != null && i < table.Entries.Count; i++)
            {
                MapLootEntryDefinition entry = table.Entries[i];
                if (entry == null || string.IsNullOrEmpty(entry.ItemId))
                    continue;

                ItemManager.ItemType type;
                if (!InventoryHelper.ResolveItemType(entry.ItemId, out type))
                {
                    AddMessage(result, "Unknown map loot item id skipped: " + entry.ItemId);
                    continue;
                }

                int count = Math.Max(1, entry.MinQuantity);
                if (desired.ContainsKey(type))
                    desired[type] += count;
                else
                    desired[type] = count;
            }

            return desired;
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
}
