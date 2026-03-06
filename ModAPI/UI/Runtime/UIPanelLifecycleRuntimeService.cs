using ModAPI.Events;

namespace ModAPI.Internal.UI
{
    internal static class UIPanelLifecycleRuntimeService
    {
        internal static void RaisePanelOpened(BasePanel panel)
        {
            UIRuntimeServiceHelper.Run("UIPanelManager.PushPanel", delegate
            {
                UIEvents.RaisePanelOpened(panel);
            });
        }

        internal static void RaisePanelClosed(BasePanel panel)
        {
            UIRuntimeServiceHelper.Run("UIPanelManager.PopPanel", delegate
            {
                if (panel != null)
                    UIEvents.RaisePanelClosed(panel);
            });
        }

        internal static void RaisePanelResumed(BasePanel panel)
        {
            UIRuntimeServiceHelper.Run("BasePanel.OnResume", delegate
            {
                UIEvents.RaisePanelResumed(panel);
            });
        }
    }
}
