using System.Collections.Generic;
using HarmonyLib;
using ModAPI.Harmony;
using ParalivesAPI.Core;

namespace ParalivesAPI.Patches
{
    [HarmonyPatch]
    [PatchPolicy(
        PatchDomain.Interactions,
        "Paralives Together Card Hooks",
        TargetBehavior = "Lets mods register social cards and contribute/card-used hooks through ParalivesAPI.Together.",
        FailureMode = "Mods can still mutate Together settings directly, but cannot hook card offering and usage predictably.",
        RollbackStrategy = "Unsubscribe together hooks or remove dependent Together card content.",
        IsOptional = true)]
    internal static class TogetherManagerHooksPatch
    {
        [HarmonyPostfix]
        [HarmonyPatch(typeof(global::TogetherManager), "PickCharacterCards")]
        private static void PickCharacterCardsPostfix(
            global::SocialGroup group,
            ref Dictionary<ulong, List<global::TogetherCardChoice>> __result)
        {
            if (__result == null)
                __result = new Dictionary<ulong, List<global::TogetherCardChoice>>();

            ParalivesRuntimeInfo.Current.Together.PublishChoicesBuilding(group, __result);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(global::TogetherManager), "ProcessOutcomes")]
        private static void ProcessOutcomesPostfix(
            bool isSuccess,
            ulong cardGUID,
            ulong characterUsingCardGUID,
            global::SocialGroup socialGroup,
            ulong targetGUID,
            List<ulong> charactersFromCard,
            ulong skinGUID,
            ulong initiativeReplyGUID,
            ulong requestGUID,
            global::TogetherCardOutcomeData __result)
        {
            ParalivesRuntimeInfo.Current.Together.PublishCardUsed(new ParalivesTogetherCardUsedEvent
            {
                IsSuccess = isSuccess,
                CardGuid = cardGUID,
                ActorCharacterGuid = characterUsingCardGUID,
                TargetCharacterGuid = targetGUID,
                CharactersFromCard = charactersFromCard == null ? new ulong[0] : charactersFromCard.ToArray(),
                SkinGuid = skinGUID,
                InitiativeReplyGuid = initiativeReplyGUID,
                RequestGuid = requestGUID,
                SocialGroupGuid = socialGroup == null ? 0UL : socialGroup.GUID,
                OutcomeData = __result
            });
        }
    }
}
