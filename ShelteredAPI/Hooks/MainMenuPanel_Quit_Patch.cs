using HarmonyLib;
using ModAPI.Core;
using ModAPI.Harmony;
using UnityEngine;
using ShelteredAPI.Saves.Runtime;

using ShelteredAPI.UI.FieldManual.Tooltips;
namespace ShelteredAPI.Hooks
{
    /// <summary>
    /// Records confirmed Save &amp; Exit requests before allowing the vanilla handler to continue.
    /// </summary>
    [PatchPolicy(PatchDomain.SaveFlow, "QuitFlowTracing",
        TargetBehavior = "Trace confirmed Save & Exit requests and reset quit-save completion state",
        FailureMode = "Save & Exit diagnostics and status reset are unavailable.",
        RollbackStrategy = "Disable the SaveFlow patch domain or remove the quit tracing patches.",
        StartupTiming = PatchStartupTiming.SaveFlowCritical)]
    [HarmonyPatch(typeof(MainMenuPanel), "OnMessageBoxClosed")]
    internal static class QuitFlowTracing_Patch
    {
        public static bool Prefix(MainMenuPanel __instance, int response)
        {
            // Response 1 confirms Save & Exit.
            if (response == 1)
            {
                MMLog.WriteInfo("[QuitFlow] Save & Exit confirmed; continuing with the vanilla handler.");
                ModRuntime.MarkSaveExit("OnMessageBoxClosed(response=1)", "User confirmed Save & Exit");

                // Reset completion state for the new quit sequence.
                SaveRuntimeStatus.ResetQuitSaveCompleted();
            }
            return true;
        }

        /// <summary>
        /// Records idle <see cref="SaveManager.Update"/> calls during shutdown and allows every update to continue.
        /// </summary>
        [HarmonyPatch(typeof(SaveManager), "Update")]
        internal static class SaveManager_Update_Patch
        {
            private static float _nextPassthroughLogAt = 0f;

            public static bool Prefix(SaveManager __instance)
            {
                // Blocking this update can deadlock shutdown after a successful save.
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
