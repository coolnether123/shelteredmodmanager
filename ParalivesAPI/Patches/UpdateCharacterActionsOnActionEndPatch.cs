using HarmonyLib;
using ModAPI.Harmony;
using ParalivesAPI.Core;

namespace ParalivesAPI.Patches
{
    [HarmonyPatch(typeof(global::UpdateCharacterActions), "OnActionEnd")]
    [PatchPolicy(
        PatchDomain.Interactions,
        "Paralives Native Action Completion",
        TargetBehavior = "Publishes completed native Paralives actions to registered SMM runtime integrations.",
        FailureMode = "Mods can add native interactions, but cannot reliably grant rewards or persist progress when actions finish.",
        RollbackStrategy = "Disable the native interaction feature or remove the dependent mod content.",
        IsOptional = false)]
    internal static class UpdateCharacterActionsOnActionEndPatch
    {
        private static void Postfix(
            global::AssetCharacter characterAsset,
            global::AssetCharacterDataInteraction interaction,
            global::CurrentAction currentAction,
            global::Setting.ActionUnit actionUnit)
        {
            ParalivesRuntimeInfo.Current.ActionCompletions.Raise(characterAsset, interaction, currentAction, actionUnit);
        }
    }
}
