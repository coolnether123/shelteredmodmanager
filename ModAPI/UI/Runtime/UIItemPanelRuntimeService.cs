using ModAPI.Content;
using ModAPI.Core;

namespace ModAPI.Internal.UI
{
    internal static class UIItemPanelRuntimeService
    {
        internal static void AugmentStoragePanel(StoragePanel panel)
        {
            UIRuntimeServiceHelper.Run("StoragePanel.OnShow", delegate
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
            UIRuntimeServiceHelper.Run("RecyclingPanel.OnShow", delegate
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
            UIRuntimeServiceHelper.Run("ItemFabricationPanel.OnShow", delegate
            {
                if (panel == null || ItemManager.Instance == null || InventoryManager.Instance == null)
                    return;

                foreach (var type in ContentInjector.RegisteredTypes)
                {
                    ItemDefinition def = ItemManager.Instance.GetItemDefinition(type);
                    if (def == null)
                        continue;

                    if (def.ScrapValue > 0)
                    {
                        int count = InventoryManager.Instance.GetNumItemsOfType(type);
                        if (count > 0)
                            panel.m_storageItems.AddItem(type, count);
                    }

                    if (def.Category == ItemManager.ItemCategory.Normal && def.BaseFabricationTime > 0 && def.FabricationCost > 0)
                        panel.m_selectionItems.AddItem(type, 1);
                }
            });
        }

        internal static void AugmentTradingPanel(TradingPanel panel)
        {
            UIRuntimeServiceHelper.Run("TradingPanel.OnShow", delegate
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
    }
}
