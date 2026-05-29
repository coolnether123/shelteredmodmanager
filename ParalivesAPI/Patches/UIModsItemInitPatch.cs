using HarmonyLib;
using ModAPI.Harmony;
using ParalivesAPI.Core;

namespace ParalivesAPI.Patches
{
    [HarmonyPatch(typeof(global::UIModsItem), "Init")]
    [PatchPolicy(
        PatchDomain.UI,
        "Paralives SMM Mods Screen",
        TargetBehavior = "Marks SMM shadow mod rows as managed entries and hides unsafe asset-pack actions.",
        FailureMode = "SMM mods may appear as ordinary local asset-pack mods in the Paralives mods screen.",
        RollbackStrategy = "Disable this optional UI patch; shadow entries can be recreated by the bridge.",
        IsOptional = true)]
    internal static class UIModsItemInitPatch
    {
        private static void Postfix(global::UIModsItem __instance)
        {
            if (__instance == null)
                return;

            AssetMod assetMod = __instance.AssetMod;
            string modId;
            if (!SmmModScreenBridge.TryGetSmmModId(assetMod, out modId))
                return;

            if (__instance.ButtonDeleteMod != null)
                __instance.ButtonDeleteMod.SetActive(false);
            if (__instance.ButtonRenameMod != null)
                __instance.ButtonRenameMod.SetActive(false);
            if (__instance.ButtonSetPreviewImage != null)
                __instance.ButtonSetPreviewImage.SetActive(false);
            if (__instance.UploadUpdateButtonObject != null)
                __instance.UploadUpdateButtonObject.SetActive(false);
            if (__instance.ButtonViewOnWorkshop != null)
                __instance.ButtonViewOnWorkshop.SetActive(false);
            if (__instance.TooltipExtraInfo != null)
                __instance.TooltipExtraInfo.TextToShow = "Managed by SMM. Enable changes take effect after restarting Paralives.";
        }
    }
}
