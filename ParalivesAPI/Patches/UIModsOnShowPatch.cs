using HarmonyLib;
using ModAPI.Harmony;
using ParalivesAPI.Core;

namespace ParalivesAPI.Patches
{
    [HarmonyPatch(typeof(global::UIMods), "OnShow")]
    [PatchPolicy(
        PatchDomain.UI,
        "Paralives SMM Mods Screen",
        TargetBehavior = "Refreshes SMM shadow mod rows when the built-in Paralives mods screen opens.",
        FailureMode = "The mods screen may require another refresh before showing newly installed SMM mods.",
        RollbackStrategy = "Disable this optional UI patch; the startup runner still performs a best-effort sync.",
        IsOptional = true)]
    internal static class UIModsOnShowPatch
    {
        private static void Postfix()
        {
            SmmModScreenBridge.SyncNow(force: true);
        }
    }
}
