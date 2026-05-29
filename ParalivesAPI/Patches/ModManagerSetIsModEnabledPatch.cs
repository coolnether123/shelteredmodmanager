using HarmonyLib;
using ModAPI.Harmony;
using ParalivesAPI.Core;

namespace ParalivesAPI.Patches
{
    [HarmonyPatch(typeof(global::ModManager), "SetIsModEnabled")]
    [PatchPolicy(
        PatchDomain.UI,
        "Paralives SMM Mods Screen",
        TargetBehavior = "Maps enable/disable changes on SMM shadow mod rows back to SMM loadorder.json.",
        FailureMode = "The Paralives mods screen can show SMM mods, but its toggle will not affect the SMM load order.",
        RollbackStrategy = "Disable this optional UI patch; SMM mods still load from the external manager and loadorder.json.",
        IsOptional = true)]
    internal static class ModManagerSetIsModEnabledPatch
    {
        private static void Postfix(ulong guid, bool isEnabled)
        {
            AssetMod assetMod = global::AssetManager.Instance != null
                ? global::AssetManager.Instance.GetAsset(guid) as AssetMod
                : null;

            if (assetMod == null)
                return;

            SmmModScreenBridge.TrySetSmmModEnabled(assetMod, isEnabled);
        }
    }
}
