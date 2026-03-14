namespace ModAPI.UI
{
    /// <summary>
    /// Compatibility shim retained so older binaries do not hard-fail if they still resolve the legacy type.
    /// </summary>
    public static class TouchInputBridge
    {
        public static bool IsTouchDragDown(float minUiX, float maxUiX)
        {
            return false;
        }

        public static bool IsTouchDragHeld(float minUiX, float maxUiX)
        {
            return false;
        }

        public static bool IsTouchDragUp(float minUiX, float maxUiX)
        {
            return false;
        }

        public static bool TryGetTouchScroll(float minUiX, float maxUiX, out float scroll)
        {
            scroll = 0f;
            return false;
        }
    }
}
