using ModAPI.Internal.UI;

namespace ModAPI.Internal.UI
{
    internal static class UIPatchCoordinator
    {
        internal static void AugmentStoragePanel(StoragePanel panel)
        {
            UIItemPanelRuntimeService.AugmentStoragePanel(panel);
        }

        internal static void AugmentRecyclingPanel(RecyclingPanel panel)
        {
            UIItemPanelRuntimeService.AugmentRecyclingPanel(panel);
        }

        internal static void AugmentItemFabricationPanel(ItemFabricationPanel panel)
        {
            UIItemPanelRuntimeService.AugmentItemFabricationPanel(panel);
        }

        internal static void AugmentTradingPanel(TradingPanel panel)
        {
            UIItemPanelRuntimeService.AugmentTradingPanel(panel);
        }

        internal static void RaisePanelOpened(BasePanel panel)
        {
            UIPanelLifecycleRuntimeService.RaisePanelOpened(panel);
        }

        internal static void RaisePanelClosed(BasePanel panel)
        {
            UIPanelLifecycleRuntimeService.RaisePanelClosed(panel);
        }

        internal static void RaisePanelResumed(BasePanel panel)
        {
            UIPanelLifecycleRuntimeService.RaisePanelResumed(panel);
        }
    }
}
