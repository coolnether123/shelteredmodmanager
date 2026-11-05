using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ModAPI.Core;
using ModAPI.Harmony;
using ShelteredAPI.UI.Internal;


using ShelteredAPI.Saves;
using ShelteredAPI.UI.Internal.Runtime;
namespace ShelteredAPI.UI.Compatibility
{
    /// <summary>
    /// Patches for various game panels that manually iterate through ItemType enum,
    /// causing them to skip modded items with high IDs.
    /// </summary>
    [PatchPolicy(PatchDomain.UI, "UIIntegration",
        TargetBehavior = "Panel item injection and UI lifecycle events",
        FailureMode = "Custom items or panel lifecycle events do not appear correctly in UI.",
        RollbackStrategy = "Disable the UI patch domain or isolate the affected panel patch host.",
        StartupTiming = PatchStartupTiming.MenuCritical)]
    internal static class UIPatches
    {
        static UIPatches()
        {
            MMLog.WriteDebug("Static constructor called - class is being initialized");
        }

        [HarmonyPatch(typeof(StoragePanel), "OnShow")]
        [HarmonyPostfix]
        static void Postfix_StoragePanel_OnShow(StoragePanel __instance)
        {
            UIItemPanelRuntimeService.AugmentStoragePanel(__instance);
        }

        [HarmonyPatch(typeof(RecyclingPanel), "OnShow")]
        [HarmonyPostfix]
        static void Postfix_RecyclingPanel_OnShow(RecyclingPanel __instance)
        {
            UIItemPanelRuntimeService.AugmentRecyclingPanel(__instance);
        }

        [HarmonyPatch(typeof(ItemFabricationPanel), "OnShow")]
        [HarmonyPostfix]
        static void Postfix_ItemFabricationPanel_OnShow(ItemFabricationPanel __instance)
        {
            UIItemPanelRuntimeService.AugmentItemFabricationPanel(__instance);
        }

        [HarmonyPatch(typeof(TradingPanel), "OnShow")]
        [HarmonyPostfix]
        static void Postfix_TradingPanel_OnShow(TradingPanel __instance)
        {
            UIItemPanelRuntimeService.AugmentTradingPanel(__instance);
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
            catch (Exception ex) { MMLog.WriteError($"ItemTransferPanel.OnShow failed: {ex}"); }
        }
        
        // ============================================================================
        // UI Lifecycle Event Patches (Phase 1: Event System Expansion)
        // ============================================================================
        
        /// <summary>
        /// Publish after the deferred push has initialized and shown the panel.
        /// </summary>
        [HarmonyPatch(typeof(UIPanelManager), "PushPanel_Delayed", new Type[] { typeof(BasePanel) })]
        [HarmonyPostfix]
        static void Postfix_UIPanelManager_PushPanel(BasePanel panel, List<BasePanel> ___m_panel_stack)
        {
            if (panel != null && ___m_panel_stack != null && ___m_panel_stack.Contains(panel))
                UIPanelLifecycleRuntimeService.RaisePanelOpened(panel);
        }
        
        /// <summary>
        /// Both deferred pops and PopTop close through this method.
        /// </summary>
        [HarmonyPatch(typeof(UIPanelManager), "ClosePanel", new Type[] { typeof(BasePanel) })]
        [HarmonyPostfix]
        static void Postfix_UIPanelManager_PopPanel(BasePanel panel)
        {
            UIPanelLifecycleRuntimeService.RaisePanelClosed(panel);
        }
        
        /// <summary>
        /// Hook BasePanel.OnResume to fire OnPanelResumed event.
        /// This fires when a panel returns to the top of the stack.
        /// </summary>
        [HarmonyPatch(typeof(BasePanel), "OnResume")]
        [HarmonyPostfix]
        static void Postfix_BasePanel_OnResume(BasePanel __instance)
        {
            UIPanelLifecycleRuntimeService.RaisePanelResumed(__instance);
        }

        // This override restores focus without calling BasePanel.OnResume.
        [HarmonyPatch(typeof(CustomisationPanel), "OnResume")]
        [HarmonyPostfix]
        static void Postfix_CustomisationPanel_OnResume(CustomisationPanel __instance)
        {
            UIPanelLifecycleRuntimeService.RaisePanelResumed(__instance);
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
                UiLifecycleEventSinks.RaisePanelPaused(__instance);
            }
            catch (Exception ex)
            {
                MMLog.WriteError($"[UIPatches] BasePanel.OnPause postfix failed: {ex}");
            }
        }
        */
    }
}
