using HarmonyLib;
using ModAPI.Core;
using ModAPI.Harmony;

namespace ShelteredAPI.Hooks
{
    [PatchPolicy(PatchDomain.SaveFlow, "SaveExitCancelTrace",
        TargetBehavior = "Save-exit cancel path logging",
        FailureMode = "Cancel-path diagnostics are incomplete.",
        RollbackStrategy = "Disable the SaveFlow patch domain or remove the cancel trace patch.",
        IsOptional = true,
        StartupTiming = PatchStartupTiming.DebugDeferred)]
    [HarmonyPatch(typeof(SaveExitPanel), "OnCancel")]
    internal static class SaveExitPanelOnCancelPatch
    {
        static void Prefix()
        {
            MMLog.Write("[SaveExitPanelOnCancelPatch] SaveExitPanel.OnCancel() called.");
        }
    }
}
