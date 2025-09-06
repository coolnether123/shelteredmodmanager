using System.Collections;
using UnityEngine;

/// <summary>
/// Simple global UI flow guard to prevent the save-slot panel from
/// consuming clicks while a mod is taking over the Scenario screen
/// (e.g., to show a custom scenario list).
/// </summary>
public static class UIFlowGuard
{
    // Volatile to ensure cross-thread visibility if anything weird happens.
    public static volatile bool BlockSlotClicks;

    /// <summary>
    /// Enables/disables the guard immediately.
    /// </summary>
    public static void BlockSlotClicksToggle(bool on)
    {
        BlockSlotClicks = on;
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

