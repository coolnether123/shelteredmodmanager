using HarmonyLib;
using ModAPI.Core;
using UnityEngine;

namespace ModAPI.Hooks
{
    // =========================================================================================
    // MANAGED SHUTDOWN SYSTEM
    // 
    // Problem: Vanilla "Save & Exit" destroys scene objects immediately after triggering a save,
    // causing crashes if saving takes more than one frame (which it always does).
    //
    // Solution: We intercept the "Yes" response in the Quit dialog, blocking the vanilla logic.
    // We then hand control to PluginRunner.StartManagedShutdown(), which runs a coroutine to:
    // 1. Pause game
    // 2. Save synchronously(ish)
    // 3. Wait for SaveManager to finish
    // 4. Set IsQuitting flag
    // 5. Load MenuScene
    // =========================================================================================

    /// <summary>
    /// Intercepts the "Are you sure you want to Save & Exit?" dialog response.
    /// </summary>
    [HarmonyPatch(typeof(MainMenuPanel), "OnMessageBoxClosed")]
    public static class ManagedShutdown_Interceptor
    {
        public static bool Prefix(MainMenuPanel __instance, int response)
        {
            // Response 1 is "Yes" (Save and Exit)
            if (response == 1)
            {
                MMLog.WriteInfo("[ManagedShutdown] 'Save & Exit' confirmed. Letting vanilla logic run.");
                CrashCorridorTracer.Mark("OnMessageBoxClosed(response=1)", "User confirmed Save & Exit");
                
                // Signal to mods that we are intentionaly shutting down.
                PluginRunner.IsQuitting = true;
                CrashCorridorTracer.Mark("IsQuitting set true");
                
                // We no longer block vanilla logic, as requested.
                // return false; 
            }
            return true;
        }
    }

    /// <summary>
    /// Prevents SaveManager from updating its state machine if we are in the "Teardown" phase.
    /// This effectively freezes the SaveManager once we decide it's time to quit,
    /// preventing it from accessing destroyed objects.
    /// </summary>
    [HarmonyPatch(typeof(SaveManager), "Update")]
    public static class SaveManager_Update_Patch
    {
        private static float _nextPassthroughLogAt = 0f;

        public static bool Prefix(SaveManager __instance)
        {
            // Do NOT hard-block SaveManager.Update during quit.
            // Corridor traces showed this can deadlock the shutdown path after a successful save.
            if (PluginRunner.IsQuitting && !__instance.isSaving && !__instance.isLoading)
            {
                if (Time.realtimeSinceStartup >= _nextPassthroughLogAt)
                {
                    _nextPassthroughLogAt = Time.realtimeSinceStartup + 0.75f;
                    CrashCorridorTracer.Mark("SaveManager.Update passthrough", "isSaving=false, isLoading=false");
                }
                return true;
            }
            return true;
        }
    }
}
