namespace ModAPI.Core
{
    /// <summary>
    /// Neutral sink for UI lifecycle notifications raised by legacy ModAPI patch hosts.
    /// Game-specific assemblies own the concrete panel/button types.
    /// </summary>
    public interface IUiLifecycleEventSink
    {
        void RaisePanelOpened(object panel);
        void RaisePanelClosed(object panel);
        void RaisePanelResumed(object panel);
        void RaisePanelPaused(object panel);
        void RaiseButtonClicked(object button, string buttonName);
    }

    public static class UiLifecycleEventSinks
    {
        private static readonly IUiLifecycleEventSink NullSink = new NullUiLifecycleEventSink();

        public static void RaisePanelOpened(object panel)
        {
            Current.RaisePanelOpened(panel);
        }

        public static void RaisePanelClosed(object panel)
        {
            Current.RaisePanelClosed(panel);
        }

        public static void RaisePanelResumed(object panel)
        {
            Current.RaisePanelResumed(panel);
        }

        public static void RaisePanelPaused(object panel)
        {
            Current.RaisePanelPaused(panel);
        }

        public static void RaiseButtonClicked(object button, string buttonName)
        {
            Current.RaiseButtonClicked(button, buttonName);
        }

        private static IUiLifecycleEventSink Current
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
