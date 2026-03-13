using ModAPI.InputServices;

namespace ModAPI.UI
{
    /// <summary>
    /// Compatibility wrapper retained for older call sites while scroll input is sourced through ModAPI services.
    /// </summary>
    public static class ScrollInputBridge
    {
        public static bool TryGetVerticalScroll(float minUiX, float maxUiX, out float scroll)
        {
            return ScrollInputService.TryGetVerticalScroll(ScrollInputQuery.ForUiRange(minUiX, maxUiX), out scroll);
        }

        public static bool TryGetVerticalScrollAnywhere(out float scroll)
        {
            return ScrollInputService.TryGetVerticalScroll(ScrollInputQuery.Anywhere(false), out scroll);
        }

        public static bool TryGetVerticalScrollAnywhereRaw(out float scroll)
        {
            return ScrollInputService.TryGetVerticalScroll(ScrollInputQuery.Anywhere(true), out scroll);
        }
    }
}
