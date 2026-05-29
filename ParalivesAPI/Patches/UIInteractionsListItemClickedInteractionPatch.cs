using HarmonyLib;
using ModAPI.Harmony;
using ParalivesAPI.Core;

namespace ParalivesAPI.Patches
{
    [HarmonyPatch(typeof(global::UIInteractionsListItem), "ClickedInteraction")]
    [PatchPolicy(
        PatchDomain.Interactions,
        "Paralives Native Interaction Selection",
        TargetBehavior = "Publishes native interaction menu selections immediately when the player clicks an interaction.",
        FailureMode = "Mods can react to completed actions, but cannot open immediate UI such as stat panels from interaction menu clicks.",
        RollbackStrategy = "Disable the native interaction feature or remove the dependent immediate-click menu item.",
        IsOptional = false)]
    internal static class UIInteractionsListItemClickedInteractionPatch
    {
        private static void Postfix(global::UIInteractionsListItem __instance, ulong skinGUID)
        {
            ParalivesRuntimeInfo.Current.InteractionSelections.Raise(__instance, skinGUID);
        }
    }
}
