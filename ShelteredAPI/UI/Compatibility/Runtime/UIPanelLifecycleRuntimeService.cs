using ModAPI.Core;

namespace ShelteredAPI.UI.Internal
{
    internal static class UIPanelLifecycleRuntimeService
    {
        private static readonly IUiLifecycleEventSink NullSink = new NullUiLifecycleEventSink();

        internal static void RaisePanelOpened(BasePanel panel)
        {
            UIRuntimeServiceHelper.Run("UIPanelManager.PushPanel", delegate
            {
                CurrentSink.RaisePanelOpened(panel);
                RuntimeUiLifecycleService.NotifyPanelOpened(panel);
            });
        }

        internal static void RaisePanelClosed(BasePanel panel)
        {
            UIRuntimeServiceHelper.Run("UIPanelManager.PopPanel", delegate
            {
                if (panel != null)
                    CurrentSink.RaisePanelClosed(panel);
                RuntimeUiLifecycleService.NotifyPanelClosed(panel);
            });
        }

        internal static void RaisePanelResumed(BasePanel panel)
        {
            UIRuntimeServiceHelper.Run("BasePanel.OnResume", delegate
            {
                CurrentSink.RaisePanelResumed(panel);
                RuntimeUiLifecycleService.NotifyPanelResumed(panel);
            });
        }

        private static IUiLifecycleEventSink CurrentSink
        {
            get
            {
                if (!ModAPIRegistry.IsAPIRegistered(GameRuntimeApiIds.UiLifecycleEvents))
                    return NullSink;

                IUiLifecycleEventSink sink = ModAPIRegistry.GetAPI<IUiLifecycleEventSink>(GameRuntimeApiIds.UiLifecycleEvents);
                return sink ?? NullSink;
            }
        }

        private sealed class NullUiLifecycleEventSink : IUiLifecycleEventSink
        {
            public void RaisePanelOpened(object panel) { }
            public void RaisePanelClosed(object panel) { }
            public void RaisePanelResumed(object panel) { }
            public void RaisePanelPaused(object panel) { }
            public void RaiseButtonClicked(object button, string buttonName) { }
        }
    }
}
