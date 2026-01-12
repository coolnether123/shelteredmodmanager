using HarmonyLib;
using ModAPI.Core;

namespace ModAPI.Hooks
{
    [HarmonyPatch(typeof(SaveExitPanel), "OnCancel")]
    internal static class SaveExitPanelOnCancelPatch
    {
        static void Prefix()
        {
            MMLog.Write("[SaveExitPanelOnCancelPatch] SaveExitPanel.OnCancel() called.");
        }
    }
}
