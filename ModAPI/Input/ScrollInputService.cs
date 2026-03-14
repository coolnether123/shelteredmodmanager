namespace ModAPI.InputServices
{
    /// <summary>
    /// Registry for the active runtime scroll input source.
    /// </summary>
    public static class ScrollInputService
    {
        private static readonly object Sync = new object();
        private static IScrollInputSource _source;

        public static void RegisterSource(IScrollInputSource source)
        {
            lock (Sync)
            {
                _source = source;
            }
        }

        public static bool TryGetVerticalScroll(ScrollInputQuery query, out float scroll)
        {
            IScrollInputSource source;
            lock (Sync)
            {
                source = _source;
            }

            if (source == null)
            {
                scroll = 0f;
                return false;
            }

            return source.TryGetVerticalScroll(query, out scroll);
        }

        public static bool IsIndirectScrollActive()
        {
            IScrollInputSource source;
            lock (Sync)
            {
                source = _source;
            }

            return source != null && source.IsIndirectScrollActive();
        }
    }
}
