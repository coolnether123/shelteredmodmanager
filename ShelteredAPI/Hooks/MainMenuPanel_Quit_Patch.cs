using HarmonyLib;
using ModAPI.Core;
using ModAPI.Harmony;
using UnityEngine;
using ShelteredAPI.Saves.Runtime;

using ShelteredAPI.UI.FieldManual.Tooltips;
namespace ShelteredAPI.Hooks
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
    /// Intercepts the "Are you sure you want to Save &amp; Exit?" dialog response.
    /// </summary>
    [PatchPolicy(PatchDomain.SaveFlow, "ManagedShutdownQuitFlow",
        TargetBehavior = "Managed Save & Exit flow interception and quit-state tracing",
        FailureMode = "Quit/save sequencing falls back to vanilla timing and becomes harder to diagnose.",
        RollbackStrategy = "Disable the SaveFlow patch domain or remove the managed shutdown patch host.",
        StartupTiming = PatchStartupTiming.SaveFlowCritical)]
    [HarmonyPatch(typeof(MainMenuPanel), "OnMessageBoxClosed")]
    internal static class ManagedShutdown_Interceptor
    {
        public static bool Prefix(MainMenuPanel __instance, int response)
        {
            // Response 1 is "Yes" (Save and Exit)
            if (response == 1)
            {
                MMLog.WriteInfo("[ManagedShutdown] 'Save & Exit' confirmed. Letting vanilla logic run.");
                ModRuntime.MarkSaveExit("OnMessageBoxClosed(response=1)", "User confirmed Save & Exit");

                // Reset save-completion flag for new quit sequence
                SaveRuntimeStatus.ResetQuitSaveCompleted();
                ModRuntime.MarkSaveExit("IsQuitting set true");

                // We no longer block vanilla logic, as requested.
                // return false; 
            }
            return true;
        }

        /// <summary>
        /// Prevents SaveManager from updating its state machine if we are in the "Teardown" phase.
        /// This effectively freezes the SaveManager once we decide it's time to quit,
        /// preventing it from accessing destroyed objects.
        /// </summary>
        [HarmonyPatch(typeof(SaveManager), "Update")]
        internal static class SaveManager_Update_Patch
        {
            private static float _nextPassthroughLogAt = 0f;

            public static bool Prefix(SaveManager __instance)
            {
                // Do NOT hard-block SaveManager.Update during quit.
                // Corridor traces showed this can deadlock the shutdown path after a successful save.
                if (ModRuntime.IsQuitting && !__instance.isSaving && !__instance.isLoading)
                {
                    if (Time.realtimeSinceStartup >= _nextPassthroughLogAt)
                    {
                        _nextPassthroughLogAt = Time.realtimeSinceStartup + 0.75f;
                        ModRuntime.MarkSaveExit("SaveManager.Update passthrough", "isSaving=false, isLoading=false");
                    }
                    return true;
                }
                return true;
            }
        }
    } 
}
