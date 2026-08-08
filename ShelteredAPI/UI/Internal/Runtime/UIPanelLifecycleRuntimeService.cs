using ModAPI.Core;
using ShelteredAPI.Content;
namespace ShelteredAPI.UI.Internal.Runtime{
    internal static class UIPanelLifecycleRuntimeService
    {
        private static readonly IUiLifecycleEventSink NullSink = new NullUiLifecycleEventSink();

        internal static void RaisePanelOpened(BasePanel panel)
        {
            UIRuntimeServiceHelper.Run("UIPanelManager.PushPanel", delegate
            {
                CurrentSink.RaisePanelOpened(panel);
                RuntimeUiRegistry.RequestRebindAll();
            });
        }

        internal static void RaisePanelClosed(BasePanel panel)
        {
            UIRuntimeServiceHelper.Run("UIPanelManager.PopPanel", delegate
            {
                if (panel != null)
                    CurrentSink.RaisePanelClosed(panel);
                RuntimeUiRegistry.RequestRebindAll();
            });
        }

        internal static void RaisePanelResumed(BasePanel panel)
        {
            UIRuntimeServiceHelper.Run("BasePanel.OnResume", delegate
            {
                CurrentSink.RaisePanelResumed(panel);
                RuntimeUiRegistry.RequestRebindAll();
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
