using HarmonyLib;
using ModAPI.Core;
using ModAPI.Harmony;

namespace ModAPI.Hooks
{
    [PatchPolicy(PatchDomain.SaveFlow, "SaveExitCancelTrace",
        TargetBehavior = "Save-exit cancel path logging",
        FailureMode = "Cancel-path diagnostics are incomplete.",
        RollbackStrategy = "Disable the SaveFlow patch domain or remove the cancel trace patch.",
        IsOptional = true)]
    [HarmonyPatch(typeof(SaveExitPanel), "OnCancel")]
    internal static class SaveExitPanelOnCancelPatch
    {
        static void Prefix()
        {
            MMLog.Write("[SaveExitPanelOnCancelPatch] SaveExitPanel.OnCancel() called.");
        }
    }
}
