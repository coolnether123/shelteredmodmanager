using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ModAPI.Content;
using ModAPI.Core;
using ModAPI.Events;

namespace ModAPI.UI
{
    /// <summary>
    /// Patches for various game panels that manually iterate through ItemType enum,
    /// causing them to skip modded items with high IDs.
    /// </summary>
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

                int itemsAdded = 0;
                foreach (var type in ContentInjector.RegisteredTypes)
                {
                    int count = InventoryManager.Instance.GetNumItemsOfType(type);
                    if (count > 0)
                    {
                        if (__instance.m_items.AddItem(type, count))
                            itemsAdded++;
                    }
                }
                
                if (itemsAdded > 0)
                    MMLog.Write($"[UIPatches] StoragePanel.OnShow: Added {itemsAdded} custom item types from player inventory.");
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
                if (ItemManager.Instance == null || InventoryManager.Instance == null) return;

                foreach (var type in ContentInjector.RegisteredTypes)
                {
                    ItemDefinition def = ItemManager.Instance.GetItemDefinition(type);
                    if (def == null) continue;

                    if (def.ScrapValue > 0)
                    {
                        int count = InventoryManager.Instance.GetNumItemsOfType(type);
                        if (count > 0) __instance.m_storageItems.AddItem(type, count);
                    }

                    if (def.Category == ItemManager.ItemCategory.Normal && def.BaseFabricationTime > 0 && def.FabricationCost > 0)
                    {
                        __instance.m_selectionItems.AddItem(type, 1);
                    }
                }
            }
            catch (Exception ex) { MMLog.Write($"[UIPatches] ERROR in ItemFabricationPanel.OnShow: {ex}"); }
        }

        [HarmonyPatch(typeof(TradingPanel), "OnShow")]
        [HarmonyPostfix]
        static void Postfix_TradingPanel_OnShow(TradingPanel __instance)
        {
            try
            {
                if (InventoryManager.Instance == null) return;

                foreach (var type in ContentInjector.RegisteredTypes)
                {
                    int count = InventoryManager.Instance.GetNumItemsOfType(type);
                    if (count > 0) __instance.m_playerItems.AddItem(type, count);
                    
                    // Note: NPC items in TradingPanel are usually populated via MapRegion or Encounter, 
                    // which is handled by our Loot Injection patches.
                }
            }
            catch (Exception ex) { MMLog.Write($"[UIPatches] ERROR in TradingPanel.OnShow: {ex}"); }
        }

        [HarmonyPatch(typeof(ItemTransferPanel), "OnShow")]
        [HarmonyPostfix]
        static void Postfix_ItemTransferPanel_OnShow(ItemTransferPanel __instance)
        {
            try
            {
                // ItemTransferPanel is complex as it can show two arbitrary inventories.
                // However, the common case is Shelter vs Region.
                // Our other patches ensure Region and Shelter inventories contain the items,
                // but if this panel still uses Enum.GetValues in OnShow to filter/re-add, we need this.
                // Upon review of decompiled code, it uses GetLeftSideItems callback.
                // If the callback returns our items, ItemGrid.UpdateItems -> AddItem_Stacked will handle it.
            }
            catch (Exception ex) { MMLog.Write($"[UIPatches] ERROR in ItemTransferPanel.OnShow: {ex}"); }
        }
        
        // ============================================================================
        // UI Lifecycle Event Patches (Phase 1: Event System Expansion)
        // ============================================================================
        
        /// <summary>
        /// Hook UIPanelManager.PushPanel to fire OnPanelOpened event.
        /// This is a public method so we can safely patch it.
        /// </summary>
        [HarmonyPatch(typeof(UIPanelManager), "PushPanel")]
        [HarmonyPostfix]
        static void Postfix_UIPanelManager_PushPanel(BasePanel panel)
        {
            try
            {
                UIEvents.RaisePanelOpened(panel);
            }
            catch (Exception ex)
            {
                MMLog.Write($"[UIPatches] ERROR in UIPanelManager.PushPanel postfix: {ex}");
            }
        }
        
        /// <summary>
        /// Hook UIPanelManager.PopPanel(BasePanel) to fire OnPanelClosed event.
        /// This is the public overload that takes a panel parameter.
        /// </summary>
        [HarmonyPatch(typeof(UIPanelManager), "PopPanel", new Type[] { typeof(BasePanel) })]
        [HarmonyPostfix]
        static void Postfix_UIPanelManager_PopPanel(BasePanel panel)
        {
            try
            {
                if (panel != null)
                {
                    UIEvents.RaisePanelClosed(panel);
                }
            }
            catch (Exception ex)
            {
                MMLog.Write($"[UIPatches] ERROR in UIPanelManager.PopPanel postfix: {ex}");
            }
        }
        
        /// <summary>
        /// Hook BasePanel.OnResume to fire OnPanelResumed event.
        /// This fires when a panel returns to the top of the stack.
        /// </summary>
        [HarmonyPatch(typeof(BasePanel), "OnResume")]
        [HarmonyPostfix]
        static void Postfix_BasePanel_OnResume(BasePanel __instance)
        {
            try
            {
                UIEvents.RaisePanelResumed(__instance);
            }
            catch (Exception ex)
            {
                MMLog.Write($"[UIPatches] ERROR in BasePanel.OnResume postfix: {ex}");
            }
        }
        
        /* 
        /// <summary>
        /// Hook BasePanel.OnPause to fire OnPanelPaused event.
        /// This fires when another panel is pushed above this one.
        /// </summary>
        [HarmonyPatch(typeof(BasePanel), "OnPause")]
        [HarmonyPostfix]
        static void Postfix_BasePanel_OnPause(BasePanel __instance)
        {
            try
            {
                UIEvents.RaisePanelPaused(__instance);
            }
            catch (Exception ex)
            {
                MMLog.Write($"[UIPatches] ERROR in BasePanel.OnPause postfix: {ex}");
            }
        }
        */
    }
}
