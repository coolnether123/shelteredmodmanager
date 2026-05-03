namespace ShelteredAPI.UI.Internal
{
    internal static class RuntimeUiLifecycleService
    {
        public static void NotifyPanelOpened(BasePanel panel)
        {
            RuntimeUiRegistry.RequestRebindAll();
        }

        public static void NotifyPanelClosed(BasePanel panel)
        {
            RuntimeUiRegistry.RequestRebindAll();
        }

        public static void NotifyPanelResumed(BasePanel panel)
        {
            RuntimeUiRegistry.RequestRebindAll();
        }
    }
}
