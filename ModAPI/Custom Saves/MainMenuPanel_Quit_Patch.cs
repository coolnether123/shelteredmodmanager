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
                
                // Signal to mods that we are intentionaly shutting down.
                PluginRunner.IsQuitting = true;
                
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
        public static bool Prefix(SaveManager __instance)
        {
            // If we are quitting, only block updates IF we are not saving or loading.
            // This prevents deadlocking the SaveManager during the final save.
            if (PluginRunner.IsQuitting && !__instance.isSaving && !__instance.isLoading)
            {
                return false; 
            }
            return true;
        }
    }
}
