using ModAPI.Core;

namespace ModAPI.Internal.UI
{
    internal static class UIPanelLifecycleRuntimeService
    {
        internal static void RaisePanelOpened(BasePanel panel)
        {
            UIRuntimeServiceHelper.Run("UIPanelManager.PushPanel", delegate
            {
                UiLifecycleEventSinks.RaisePanelOpened(panel);
            });
        }

        internal static void RaisePanelClosed(BasePanel panel)
        {
            UIRuntimeServiceHelper.Run("UIPanelManager.PopPanel", delegate
            {
                if (panel != null)
                    UiLifecycleEventSinks.RaisePanelClosed(panel);
            });
        }

        internal static void RaisePanelResumed(BasePanel panel)
        {
            UIRuntimeServiceHelper.Run("BasePanel.OnResume", delegate
            {
                UiLifecycleEventSinks.RaisePanelResumed(panel);
            });
        }
    }
}
