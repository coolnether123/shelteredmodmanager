using System;
using ModAPI.Content;
using ModAPI.Core;
using ModAPI.Events;

namespace ModAPI.UI
{
    internal static class UIPatchCoordinator
    {
        internal static void AugmentStoragePanel(StoragePanel panel)
        {
            Run("StoragePanel.OnShow", delegate
            {
                if (panel == null || InventoryManager.Instance == null)
                    return;

                int itemsAdded = 0;
                foreach (var type in ContentInjector.RegisteredTypes)
                {
                    int count = InventoryManager.Instance.GetNumItemsOfType(type);
                    if (count > 0 && panel.m_items.AddItem(type, count))
                        itemsAdded++;
                }

                if (itemsAdded > 0)
                    MMLog.Write("StoragePanel.OnShow: Added " + itemsAdded + " custom item types from player inventory.");
            });
        }

        internal static void AugmentRecyclingPanel(RecyclingPanel panel)
        {
            Run("RecyclingPanel.OnShow", delegate
            {
                if (panel == null || ItemManager.Instance == null || InventoryManager.Instance == null)
                    return;

                foreach (var type in ContentInjector.RegisteredTypes)
                {
                    ItemDefinition itemDefinition = ItemManager.Instance.GetItemDefinition(type);
                    if (itemDefinition == null || itemDefinition.ItemBaseParts.Count == 0)
                        continue;

                    int count = InventoryManager.Instance.GetNumItemsOfType(type);
                    if (count > 0)
                        panel.m_items.AddItem(type, count);
                }
            });
        }

        internal static void AugmentItemFabricationPanel(ItemFabricationPanel panel)
        {
            Run("ItemFabricationPanel.OnShow", delegate
            {
                if (panel == null || ItemManager.Instance == null || InventoryManager.Instance == null)
                    return;

                foreach (var type in ContentInjector.RegisteredTypes)
                {
                    ItemDefinition def = ItemManager.Instance.GetItemDefinition(type);
                    if (def == null) continue;

                    if (def.ScrapValue > 0)
                    {
                        int count = InventoryManager.Instance.GetNumItemsOfType(type);
                        if (count > 0) panel.m_storageItems.AddItem(type, count);
                    }

                    if (def.Category == ItemManager.ItemCategory.Normal && def.BaseFabricationTime > 0 && def.FabricationCost > 0)
                    {
                        panel.m_selectionItems.AddItem(type, 1);
                    }
                }
            });
        }

        internal static void AugmentTradingPanel(TradingPanel panel)
        {
            Run("TradingPanel.OnShow", delegate
            {
                if (panel == null || InventoryManager.Instance == null)
                    return;

                foreach (var type in ContentInjector.RegisteredTypes)
                {
                    int count = InventoryManager.Instance.GetNumItemsOfType(type);
                    if (count > 0)
                        panel.m_playerItems.AddItem(type, count);
                }
            });
        }

        internal static void RaisePanelOpened(BasePanel panel)
        {
            Run("UIPanelManager.PushPanel", delegate
            {
                UIEvents.RaisePanelOpened(panel);
            });
        }

        internal static void RaisePanelClosed(BasePanel panel)
        {
            Run("UIPanelManager.PopPanel", delegate
            {
                if (panel != null)
                    UIEvents.RaisePanelClosed(panel);
            });
        }

        internal static void RaisePanelResumed(BasePanel panel)
        {
            Run("BasePanel.OnResume", delegate
            {
                UIEvents.RaisePanelResumed(panel);
            });
        }

        private static void Run(string operation, Action action)
        {
            try
            {
                if (action != null) action();
            }
            catch (Exception ex)
            {
                MMLog.Write("ERROR in " + operation + ": " + ex);
            }
        }
    }
}
