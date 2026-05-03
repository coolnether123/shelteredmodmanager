using System.Collections;
using UnityEngine;

namespace ModAPI.UI
{
    /// <summary>
    /// Simple global UI flow guard to prevent UI elements from
    /// consuming clicks while a mod is taking over a screen
    /// (e.g., to show a custom UI).
    /// </summary>
    public static class UIFlowGuard
    {
        // Volatile to ensure cross-thread visibility if anything weird happens.
        public static volatile bool BlockSlotClicks;
        private static int _blockSlotClicksUntilFrame;

        public static int BlockSlotClicksUntilFrame
        {
            get { return _blockSlotClicksUntilFrame; }
        }

        public static bool IsSlotClickBlocked
        {
            get { return BlockSlotClicks || Time.frameCount <= _blockSlotClicksUntilFrame; }
        }

        /// <summary>
        /// Enables/disables the guard immediately.
        /// </summary>
        public static void BlockSlotClicksToggle(bool on)
        {
            BlockSlotClicks = on;
        }

        /// <summary>
        /// Enables the guard through the current frame plus the requested number of future frames.
        /// Useful when a custom overlay click must not fall through to an underlying vanilla control.
        /// </summary>
        public static void BlockSlotClicksForFrames(int frameCount)
        {
            if (frameCount < 0)
                frameCount = 0;

            int targetFrame = Time.frameCount + frameCount;
            if (targetFrame > _blockSlotClicksUntilFrame)
                _blockSlotClicksUntilFrame = targetFrame;
        }

        /// <summary>
        /// Enables the guard for one frame. Requires a MonoBehaviour host to start the coroutine.
        /// </summary>
        public static void BlockSlotClicksOnce(MonoBehaviour host)
        {
            if (host == null) { BlockSlotClicks = false; return; }
            BlockSlotClicks = true;
            host.StartCoroutine(ReleaseGuardNextFrame());
        }

        /// <summary>
        /// Coroutine that releases the guard on the next frame.
        /// Usage: StartCoroutine(UIFlowGuard.ReleaseGuardNextFrame())
        /// </summary>
        public static IEnumerator ReleaseGuardNextFrame()
        {
            yield return null; // one frame
            BlockSlotClicks = false;
        }
    }
}
