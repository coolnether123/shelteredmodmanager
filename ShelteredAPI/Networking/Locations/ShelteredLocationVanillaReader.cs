using System.Collections.Generic;
using System.Collections;
using System.Reflection;
using System.Text;

namespace ShelteredAPI.Networking.Locations
{
    internal static class ShelteredLocationVanillaReader
    {
        private static readonly FieldInfo MapItemsField = typeof(MapRegion).GetField("m_items", BindingFlags.Instance | BindingFlags.NonPublic);

        public static LocationState Read(MapRegion region)
        {
            if (region == null)
                return null;

            LocationState state = new LocationState();
            state.GridX = region.gridReference != null ? region.gridReference.x : 0;
            state.GridY = region.gridReference != null ? region.gridReference.y : 0;
            state.MapIdentity = ResolveMapIdentity();
            state.LocationKind = region.topography.ToString();
            state.LocationId = ShelteredLocationStateRegistry.BuildLocationId(
                state.MapIdentity,
                state.GridX,
                state.GridY,
                state.LocationKind);
            state.GeneratedSeedStream = ShelteredLocationLootService.CreateLocationSeedStreamName(state.LocationId);
            state.IsGenerated = true;
            state.DiscoveredByPlayerId = ResolveLocalPlayerId();
            state.IsSearched = region.discovered && region.lastVisited > 0;
            state.IsDepleted = !region.hasItems && !region.AreThereHiddenItems();
            state.RemainingLootSummaryJson = BuildLootSummaryJson(region);
            state.LastUpdatedTick = ResolveWorldTick();
            if (state.GeneratedWorldTick <= 0)
                state.GeneratedWorldTick = state.LastUpdatedTick;
            return state;
        }

        public static IList<LootItemRecord> ReadDiscoveredLoot(MapRegion region, string source)
        {
            List<LootItemRecord> records = new List<LootItemRecord>();
            if (region == null)
                return records;

            if (!AppendPrivateMapItems(records, region, source))
                AppendStacks(records, region.GetDiscoveredItems(0), source);
            return records;
        }

        public static string BuildLootSummaryJson(MapRegion region)
        {
            List<LootItemRecord> records = new List<LootItemRecord>();
            if (region != null)
            {
                if (!AppendPrivateMapItems(records, region, "Remaining"))
                    AppendStacks(records, region.GetDiscoveredItems(0), "Discovered");
                AppendStacks(records, region.GetHiddenItems(0), "Hidden");
            }

            return ShelteredLocationLootDiagnostics.ToLootSummaryJson(records);
        }

        private static void AppendStacks(IList<LootItemRecord> records, IList<ItemStack> stacks, string source)
        {
            for (int i = 0; stacks != null && i < stacks.Count; i++)
            {
                ItemStack stack = stacks[i];
                if (stack == null || stack.m_count <= 0 || stack.m_type == ItemManager.ItemType.Undefined)
                    continue;

                records.Add(new LootItemRecord
                {
                    VanillaItemTypeInt = (int)stack.m_type,
                    Count = stack.m_count,
                    Source = source ?? string.Empty
                });
            }
        }

        private static bool AppendPrivateMapItems(IList<LootItemRecord> records, MapRegion region, string source)
        {
            if (records == null || region == null || MapItemsField == null)
                return false;

            IList items = MapItemsField.GetValue(region) as IList;
            if (items == null)
                return false;

            for (int i = 0; i < items.Count; i++)
            {
                object mapItem = items[i];
                if (mapItem == null)
                    continue;

                PropertyInfo itemTypeProperty = mapItem.GetType().GetProperty("itemType", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (itemTypeProperty == null)
                    continue;

                object itemTypeValue = itemTypeProperty.GetValue(mapItem, null);
                if (!(itemTypeValue is ItemManager.ItemType))
                    continue;

                ItemManager.ItemType itemType = (ItemManager.ItemType)itemTypeValue;
                if (itemType == ItemManager.ItemType.Undefined)
                    continue;

                AddOrIncrement(records, (int)itemType, source);
            }

            return true;
        }

        private static void AddOrIncrement(IList<LootItemRecord> records, int vanillaItemTypeInt, string source)
        {
            for (int i = 0; i < records.Count; i++)
            {
                LootItemRecord existing = records[i];
                if (existing != null && existing.VanillaItemTypeInt.HasValue && existing.VanillaItemTypeInt.Value == vanillaItemTypeInt)
                {
                    existing.Count++;
                    return;
                }
            }

            records.Add(new LootItemRecord
            {
                VanillaItemTypeInt = vanillaItemTypeInt,
                Count = 1,
                Source = source ?? string.Empty
            });
        }

        private static int ResolveLocalPlayerId()
        {
            ShelteredMultiplayerSessionContext context = ShelteredMultiplayerSessionCoordinator.Instance.Context;
            return context != null ? context.LocalPlayerId : 0;
        }

        private static long ResolveWorldTick()
        {
            ShelteredMultiplayerSessionContext context = ShelteredMultiplayerSessionCoordinator.Instance.Context;
            return context != null ? context.WorldTick : 0;
        }

        private static string ResolveMapIdentity()
        {
            ExpeditionMap map = ExpeditionMap.Instance;
            if (map == null)
                return string.Empty;

            return "map-" + map.randomSeed.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + "-" + map.width.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + "x" + map.height.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
