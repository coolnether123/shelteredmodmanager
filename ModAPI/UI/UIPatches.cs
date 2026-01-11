using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ModAPI.Content;
using ModAPI.Core;

namespace ModAPI.UI
{
    /// <summary>
    /// Patches for various game panels that manually iterate through ItemType enum,
    /// causing them to skip modded items with high IDs.
    /// </summary>
    [HarmonyPatch]
    internal static class UIPatches
    {
        static UIPatches()
        {
            MMLog.Write("[UIPatches] Static constructor called - class is being initialized");
        }

        [HarmonyPatch(typeof(StoragePanel), "OnShow")]
        [HarmonyPostfix]
        static void Postfix_StoragePanel_OnShow(StoragePanel __instance)
        {
            try
            {
                MMLog.Write("[UIPatches] StoragePanel.OnShow postfix called!");
                
                if (InventoryManager.Instance == null)
                {
                    MMLog.Write("[UIPatches] InventoryManager.Instance is null, skipping");
                    return;
                }

                MMLog.Write($"[UIPatches] About to check {ContentInjector.RegisteredTypes.Count()} registered types");

                int itemsAdded = 0;
                // The vanilla code loops over Enum.GetValues(ItemType), missing high IDs.
                // We append any custom items found in player inventory.
                foreach (var type in ContentInjector.RegisteredTypes)
                {
                    MMLog.Write($"[UIPatches] Checking registered type: {type} ({(int)type})");
                    int count = InventoryManager.Instance.GetNumItemsOfType(type);
                    MMLog.Write($"[UIPatches] - Inventory count for {type}: {count}");
                    
                    if (count > 0)
                    {
                        bool added = __instance.m_items.AddItem(type, count);
                        MMLog.Write($"[UIPatches] - AddItem result for {type}: {added}");
                        if (added)
                        {
                            itemsAdded++;
                            MMLog.Write($"[UIPatches] Successfully added custom item {type} to StoragePanel (count: {count})");
                        }
                    }
                }
                
                MMLog.Write($"[UIPatches] StoragePanel.OnShow complete: {itemsAdded} custom items added");
            }
            catch (Exception ex)
            {
                MMLog.Write($"[UIPatches] ERROR in StoragePanel.OnShow: {ex}");
            }
        }

        [HarmonyPatch(typeof(RecyclingPanel), "OnShow")]
        [HarmonyPostfix]
        static void Postfix_RecyclingPanel_OnShow(RecyclingPanel __instance)
        {
            try
            {
                MMLog.Write("[UIPatches] RecyclingPanel.OnShow postfix called!");
                
                if (ItemManager.Instance == null || InventoryManager.Instance == null)
                {
                    MMLog.Write("[UIPatches] ItemManager or InventoryManager is null, skipping");
                    return;
                }

                foreach (var type in ContentInjector.RegisteredTypes)
                {
                    ItemDefinition itemDefinition = ItemManager.Instance.GetItemDefinition(type);
                    if (itemDefinition != null && itemDefinition.ItemBaseParts.Count != 0)
                    {
                        int count = InventoryManager.Instance.GetNumItemsOfType(type);
                        if (count > 0)
                        {
                            __instance.m_items.AddItem(type, count);
                            MMLog.Write($"[UIPatches] Added custom item {type} to RecyclingPanel (count: {count})");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MMLog.Write($"[UIPatches] ERROR in RecyclingPanel.OnShow: {ex}");
            }
        }

        [HarmonyPatch(typeof(ItemFabricationPanel), "OnShow")]
        [HarmonyPostfix]
        static void Postfix_ItemFabricationPanel_OnShow(ItemFabricationPanel __instance)
        {
            try
            {
                MMLog.Write("[UIPatches] ItemFabricationPanel.OnShow postfix called!");
                
                if (ItemManager.Instance == null || InventoryManager.Instance == null)
                {
                    MMLog.Write("[UIPatches] ItemManager or InventoryManager is null, skipping");
                    return;
                }

                foreach (var type in ContentInjector.RegisteredTypes)
                {
                    ItemDefinition def = ItemManager.Instance.GetItemDefinition(type);
                    if (def == null) continue;

                    // Storage Items (items to salvage)
                    if (def.ScrapValue > 0)
                    {
                        int count = InventoryManager.Instance.GetNumItemsOfType(type);
                        if (count > 0)
                        {
                            __instance.m_storageItems.AddItem(type, count);
                            MMLog.Write($"[UIPatches] Added custom item {type} to ItemFabricationPanel storage (count: {count})");
                        }
                    }

                    // Selection Items (items that can be fabricated)
                    // Note: Mostly for 'Normal' category items with costs defined
                    if (def.Category == ItemManager.ItemCategory.Normal && def.BaseFabricationTime > 0 && def.FabricationCost > 0)
                    {
                        __instance.m_selectionItems.AddItem(type, 1);
                    }
                }
            }
            catch (Exception ex)
            {
                MMLog.Write($"[UIPatches] ERROR in ItemFabricationPanel.OnShow: {ex}");
            }
        }
    }
}
