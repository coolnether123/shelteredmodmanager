using HarmonyLib;
using ModAPI.Harmony;
using ParalivesAPI.Core;

namespace ParalivesAPI.Patches
{
    [HarmonyPatch]
    [PatchPolicy(
        PatchDomain.Characters,
        "Paralives Want Hooks",
        TargetBehavior = "Publishes want add, offer, and status changes through ParalivesAPI.Wants.",
        FailureMode = "Mods can still read wants, but cannot react consistently to native want lifecycle changes.",
        RollbackStrategy = "Unsubscribe want hooks or disable mods depending on reactive want updates.",
        IsOptional = true)]
    internal static class WantsManagerHooksPatch
    {
        [HarmonyPostfix]
        [HarmonyPatch(typeof(global::WantsManager), "AddWant")]
        private static void AddWantPostfix(
            global::AssetCharacter character,
            ulong wantGUID,
            ulong brainLogicGUID,
            ulong characterTargetGUID,
            ulong skillGUID)
        {
            if (character == null)
                return;

            ParalivesRuntimeInfo.Current.Wants.PublishChanged(new ParalivesWantChangedEvent
            {
                ChangeType = ParalivesWantChangeType.Added,
                CharacterGuid = character.GUID,
                WantIndex = character.Data == null || character.Data.Wants == null ? -1 : character.Data.Wants.Count - 1,
                WantGuid = wantGUID,
                BrainLogicGuid = brainLogicGUID,
                CharacterTargetGuid = characterTargetGUID,
                SkillGuid = skillGUID,
                Status = global::AssetCharacterWantStatus.Active
            });
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(global::WantsManager), "AddOfferedWant")]
        private static void AddOfferedWantPostfix(
            global::AssetCharacter character,
            ulong wantGUID,
            ulong brainLogicGUID,
            ulong skillGUID,
            ulong otherCharacter)
        {
            if (character == null)
                return;

            ParalivesRuntimeInfo.Current.Wants.PublishChanged(new ParalivesWantChangedEvent
            {
                ChangeType = ParalivesWantChangeType.Offered,
                CharacterGuid = character.GUID,
                WantIndex = character.Data == null || character.Data.OfferedWants == null ? -1 : character.Data.OfferedWants.Count - 1,
                WantGuid = wantGUID,
                BrainLogicGuid = brainLogicGUID,
                CharacterTargetGuid = otherCharacter,
                SkillGuid = skillGUID,
                Status = global::AssetCharacterWantStatus.Active
            });
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(global::WantsManager), "SetWantStatus")]
        private static void SetWantStatusPostfix(
            global::AssetCharacter characterAsset,
            global::AssetCharacterWantData wantData,
            global::AssetCharacterWantStatus status)
        {
            if (characterAsset == null || wantData == null)
                return;

            ParalivesRuntimeInfo.Current.Wants.PublishChanged(new ParalivesWantChangedEvent
            {
                ChangeType = ParalivesWantChangeType.StatusChanged,
                CharacterGuid = characterAsset.GUID,
                WantIndex = ParalivesRuntimeInfo.Current.Wants.FindWantIndex(characterAsset, wantData),
                WantGuid = wantData.WantGUID,
                BrainLogicGuid = wantData.BrainLogicGUID,
                CharacterTargetGuid = wantData.CharacterTargetGUID,
                OccupationGuid = wantData.OccupationGUID,
                SkillGuid = wantData.SkillGUID,
                Status = status
            });
        }
    }
}
