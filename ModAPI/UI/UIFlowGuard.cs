using System.Collections;
using UnityEngine;

namespace ModAPI.UI
{
    /// <summary>
    /// Blocks underlying UI controls while a mod owns the current screen.
    /// </summary>
    public static class UIFlowGuard
    {
        // Volatile keeps the guard visible if a caller changes it off the main thread.
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
        /// Enables or disables the guard immediately.
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
        /// Start the coroutine with <c>StartCoroutine(UIFlowGuard.ReleaseGuardNextFrame())</c>.
        /// </summary>
        public static IEnumerator ReleaseGuardNextFrame()
        {
            yield return null; // one frame
            BlockSlotClicks = false;
        }
    }
}
