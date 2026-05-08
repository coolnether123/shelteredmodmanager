using System;
using System.Collections.Generic;
using HarmonyLib;
using ModAPI.Core;
using ModAPI.Harmony;

namespace ShelteredAPI.Networking.Locations
{
    [PatchPolicy(PatchDomain.World, "ShelteredLocationLoot",
        TargetBehavior = "Observe vanilla map-region generation, search, and item transfer callbacks for host-authoritative multiplayer location and loot state.",
        FailureMode = "Location and loot multiplayer registries may miss vanilla changes; vanilla exploration and inventory transfer behavior is unchanged.",
        RollbackStrategy = "Disable the ShelteredLocationLoot patch group.",
        StartupTiming = PatchStartupTiming.GameplayDeferred,
        IsOptional = true)]
    [HarmonyPatch]
    internal static class ShelteredLocationLootPatches
    {
        [HarmonyPatch(typeof(MapRegion), "GenerateRandomItems")]
        [HarmonyPostfix]
        private static void GenerateRandomItemsPostfix(MapRegion __instance)
        {
            try
            {
                ShelteredLocationLootService.Instance.RecordGenerated(__instance);
                ShelteredLocationLootService.Instance.RecordLootGenerated(__instance);
            }
            catch (Exception ex)
            {
                MMLog.WarnOnce("ShelteredLocationLoot.GenerateRandomItems",
                    "Location loot generation observer failed: " + ex.Message);
            }
        }

        [HarmonyPatch(typeof(MapRegion), "AddItem")]
        [HarmonyPostfix]
        private static void AddItemPostfix(MapRegion __instance, bool __result)
        {
            if (!__result)
                return;

            try
            {
                ShelteredLocationLootService.Instance.RecordGenerated(__instance);
                ShelteredLocationLootService.Instance.RecordLootGenerated(__instance);
            }
            catch (Exception ex)
            {
                MMLog.WarnOnce("ShelteredLocationLoot.AddItem",
                    "Location loot add observer failed: " + ex.Message);
            }
        }

        [HarmonyPatch(typeof(MapRegion), "OnItemsChanged")]
        [HarmonyPostfix]
        private static void OnItemsChangedPostfix(
            MapRegion __instance,
            int inventoryIndex,
            List<ItemStack> itemsAdded,
            List<ItemStack> itemsRemoved)
        {
            try
            {
                IList<LootItemRecord> taken = ToLootRecords(itemsRemoved, "Vanilla.MapRegion.OnItemsChanged");
                if (taken.Count > 0)
                    ShelteredLocationLootService.Instance.RecordLootTaken(__instance, taken);

                ShelteredLocationLootService.Instance.RecordLootGenerated(__instance);
                if (__instance != null && !__instance.hasItems && !__instance.AreThereHiddenItems())
                    ShelteredLocationLootService.Instance.RecordDepleted(__instance);
            }
            catch (Exception ex)
            {
                MMLog.WarnOnce("ShelteredLocationLoot.OnItemsChanged",
                    "Location loot transfer observer failed: " + ex.Message);
            }
        }

        [HarmonyPatch(typeof(ExplorationParty), "DiscoverLocationItems")]
        [HarmonyPostfix]
        private static void DiscoverLocationItemsPostfix(ExplorationParty __instance)
        {
            try
            {
                MapRegion region = __instance != null ? __instance.currentRegion : null;
                ShelteredLocationLootService.Instance.RecordLootGenerated(region);
            }
            catch (Exception ex)
            {
                MMLog.WarnOnce("ShelteredLocationLoot.DiscoverLocationItems",
                    "Location loot discovery observer failed: " + ex.Message);
            }
        }

        [HarmonyPatch(typeof(ExplorationParty), "Begin_SearchingLocation")]
        [HarmonyPostfix]
        private static void BeginSearchingLocationPostfix(ExplorationParty __instance)
        {
            try
            {
                MapRegion region = __instance != null ? __instance.currentRegion : null;
                ShelteredLocationLootService.Instance.RecordDiscovered(region);
            }
            catch (Exception ex)
            {
                MMLog.WarnOnce("ShelteredLocationLoot.BeginSearchingLocation",
                    "Location search observer failed: " + ex.Message);
            }
        }

        private static IList<LootItemRecord> ToLootRecords(IList<ItemStack> stacks, string source)
        {
            List<LootItemRecord> records = new List<LootItemRecord>();
            for (int i = 0; stacks != null && i < stacks.Count; i++)
            {
                ItemStack stack = stacks[i];
                if (stack == null || stack.m_count <= 0 || stack.m_type == ItemManager.ItemType.Undefined)
                    continue;

                records.Add(new LootItemRecord
                {
                    VanillaItemTypeInt = (int)stack.m_type,
                    Count = stack.m_count,
                    Source = source
                });
            }

            return records;
        }
    }
}
