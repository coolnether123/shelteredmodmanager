using HarmonyLib;
using ModAPI.Reflection;
using ModAPI.Core;
using System;
using System.Collections.Generic;

namespace ModAPI.Content
{
    /// <summary>
    /// Patches InventoryManager to ensure custom items get inventory slots.
    /// Fixes the race condition where InventoryManager.InitialiseInventory() runs
    /// before ContentInjector adds custom items to ItemManager.
    /// </summary>
    [HarmonyPatch(typeof(InventoryManager), "InitialiseInventory")]
    internal static class InventoryIntegrationPatch
    {
        /// <summary>
        /// After InventoryManager builds its m_Inventory dictionary, add slots for any
        /// custom items that were injected by ContentInjector.
        /// </summary>
        static void Postfix(InventoryManager __instance)
        {
            MMLog.Write("[InventoryIntegration] Postfix patch called!");
            
            // CRITICAL: Ensure ContentInjector has run before we try to add slots
            // The RuntimeInitializeOnLoadMethod trigger sometimes doesn't fire in time,
            // so we manually trigger it here as a fallback
            try
            {
                ContentInjector.NotifyManagerReady("InventoryManager");
            }
            catch (Exception ex)
            {
                MMLog.Write($"[InventoryIntegration] Failed to trigger ContentInjector: {ex.Message}");
            }
            
            try
            {
                // Get the private m_Inventory field using reflection
                // Use non-generic reflection since InventoryManager.Items is a private nested class
                var inventoryField = typeof(InventoryManager).GetField("m_Inventory", 
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                
                if (inventoryField == null)
                {
                    MMLog.Write("[InventoryIntegration] Failed to find m_Inventory field");
                    return;
                }

                var inventoryObj = inventoryField.GetValue(__instance);
                if (inventoryObj == null)
                {
                    MMLog.Write("[InventoryIntegration] m_Inventory is null");
                    return;
                }

                // Cast to IDictionary since we can't get the exact generic type
                var inventory = inventoryObj as System.Collections.IDictionary;
                if (inventory == null)
                {
                    MMLog.Write("[InventoryIntegration] m_Inventory is not an IDictionary");
                    return;
                }

                // Get all defined items (including custom items added by ContentInjector)
                var allItems = ItemManager.Instance?.GetAllDefinedItems();
                if (allItems == null || allItems.Count == 0)
                {
                    MMLog.Write("[InventoryIntegration] No items found in ItemManager");
                    return;
                }

                // Get the nested Items type for creating new instances
                var itemsType = typeof(InventoryManager).GetNestedType("Items", 
                    System.Reflection.BindingFlags.NonPublic);
                
                if (itemsType == null)
                {
                    MMLog.Write("[InventoryIntegration] Failed to find InventoryManager.Items type");
                    return;
                }

                // Add inventory slots for any items that don't have one yet
                int added = 0;
                int skipped = 0;
                
                foreach (var itemType in allItems)
                {
                    if (!inventory.Contains(itemType))
                    {
                        var itemsInstance = Activator.CreateInstance(itemsType);
                        inventory.Add(itemType, itemsInstance);
                        added++;

                        // Log custom items (ID >= 10000)
                        if ((int)itemType >= 10000)
                        {
                            MMLog.Write($"[InventoryIntegration] Added slot for custom item: {itemType} (ID: {(int)itemType})");
                        }
                    }
                    else
                    {
                        skipped++;
                    }
                }

                if (added > 0)
                {
                    MMLog.Write($"[InventoryIntegration] Added {added} new item slots (total items: {allItems.Count}, already existed: {skipped})");
                }
            }
            catch (Exception ex)
            {
                MMLog.Write($"[InventoryIntegration] Error patching InventoryManager: {ex.GetType().Name}: {ex.Message}");
                MMLog.Write($"[InventoryIntegration] Stack trace: {ex.StackTrace}");
            }
        }
    }

    /// <summary>
    /// Debug patches to verify item addition flow for custom items.
    /// </summary>
    [HarmonyPatch]
    internal static class InventoryDebugPatches
    {
        [HarmonyPatch(typeof(InventoryManager), "AddNewItem")]
        [HarmonyPostfix]
        static void Postfix_AddNewItem(ItemManager.ItemType item)
        {
            if ((int)item >= 10000)
            {
                MMLog.Write($"[InventoryDebug] AddNewItem called for custom item: {item} ({(int)item})");
                VerifyItemInInventory(item);
            }
        }

        [HarmonyPatch(typeof(InventoryManager), "AddExistingItem", new[] { typeof(ItemInstance) })]
        [HarmonyPostfix]
        static void Postfix_AddExistingItem(ItemInstance item)
        {
            if (item != null && (int)item.Type >= 10000)
            {
                MMLog.Write($"[InventoryDebug] AddExistingItem called for custom item: {item.Type} ({(int)item.Type})");
                VerifyItemInInventory(item.Type);
            }
        }

        static void VerifyItemInInventory(ItemManager.ItemType type)
        {
            if (InventoryManager.Instance != null)
            {
                int current = InventoryManager.Instance.GetItemCountInStorage(type, true);
                int total = InventoryManager.Instance.GetNumItemsOfType(type);
                MMLog.Write($"[InventoryDebug] Verification for {type}: Storage={current}, Total={total}");
            }
        }
    }
}
