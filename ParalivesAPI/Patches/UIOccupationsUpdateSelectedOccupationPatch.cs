using HarmonyLib;
using ModAPI.Harmony;
using ParalivesAPI.Core;

namespace ParalivesAPI.Patches
{
    [HarmonyPatch(typeof(global::UIOccupations), "UpdateSelectedOccupation")]
    [PatchPolicy(
        PatchDomain.UI,
        "Paralives Occupation Panel Providers",
        TargetBehavior = "Lets registered UI providers append simple rows to the native occupation panel after vanilla refreshes it.",
        FailureMode = "Mods can still open native occupation windows, but provider rows will not appear in UIOccupations.",
        RollbackStrategy = "Unregister occupation panel providers or disable this optional UI patch.",
        IsOptional = true)]
    internal static class UIOccupationsUpdateSelectedOccupationPatch
    {
        private static void Postfix(global::UIOccupations __instance)
        {
            ParalivesRuntimeInfo.Current.Windows.Extensions.TryApplyOccupationPanelProviders(__instance);
        }
    }
}
