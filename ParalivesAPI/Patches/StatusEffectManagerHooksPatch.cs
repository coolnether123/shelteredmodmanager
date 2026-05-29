using HarmonyLib;
using ModAPI.Harmony;
using ParalivesAPI.Core;

namespace ParalivesAPI.Patches
{
    [HarmonyPatch]
    [PatchPolicy(
        PatchDomain.Characters,
        "Paralives Status Effect Hooks",
        TargetBehavior = "Publishes native status effect add/remove changes through ParalivesAPI.Status.",
        FailureMode = "Mods can still poll status effects, but cannot react consistently to native status changes.",
        RollbackStrategy = "Unsubscribe status hooks or disable mods depending on reactive status updates.",
        IsOptional = true)]
    internal static class StatusEffectManagerHooksPatch
    {
        [HarmonyPostfix]
        [HarmonyPatch(typeof(global::StatusEffectManager), "AddStatusEffectToCharacter")]
        private static void AddStatusPostfix(
            global::AssetCharacter character,
            ulong statusEffectGUID,
            global::AssetCharacterStatusEffectSaveData __result)
        {
            if (character == null || statusEffectGUID == 0UL)
                return;

            ParalivesRuntimeInfo.Current.Status.PublishChanged(new ParalivesStatusEffectChangedEvent
            {
                ChangeType = ParalivesStatusEffectChangeType.Added,
                CharacterGuid = character.GUID,
                StatusEffectGuid = statusEffectGUID,
                OccupationIndex = __result == null ? -1 : __result.OccupationIndex,
                SkillGuid = __result == null ? 0UL : __result.SkillGUID,
                CharacterWhoGaveIt = __result == null ? 0UL : __result.CharacterWhoGaveIt
            });
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(global::StatusEffectManager), "RemoveStatusEffectFromCharacter")]
        private static void RemoveStatusPrefix(
            global::AssetCharacter character,
            ulong statusEffectGUID,
            ref bool __state)
        {
            __state = false;
            if (character == null || statusEffectGUID == 0UL)
                return;

            try
            {
                __state = global::StatusEffectManager.Instance.HasStatusEffect(statusEffectGUID, character);
            }
            catch
            {
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(global::StatusEffectManager), "RemoveStatusEffectFromCharacter")]
        private static void RemoveStatusPostfix(
            global::AssetCharacter character,
            ulong statusEffectGUID,
            bool __state)
        {
            if (!__state || character == null || statusEffectGUID == 0UL)
                return;

            ParalivesRuntimeInfo.Current.Status.PublishChanged(new ParalivesStatusEffectChangedEvent
            {
                ChangeType = ParalivesStatusEffectChangeType.Removed,
                CharacterGuid = character.GUID,
                StatusEffectGuid = statusEffectGUID,
                OccupationIndex = -1
            });
        }
    }
}
