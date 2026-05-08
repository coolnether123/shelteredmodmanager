using System;
using HarmonyLib;
using ModAPI.Harmony;

namespace ShelteredAPI.Networking.Travel
{
    [PatchPolicy(PatchDomain.World, "ShelteredExpeditionTravelEvents",
        TargetBehavior = "Expedition travel transitions publish deterministic started, corrected, and arrived events for multiplayer prediction.",
        FailureMode = "Remote peers cannot represent expedition movement from authoritative travel events.",
        RollbackStrategy = "Disable the World patch domain or remove the expedition travel event patch host.",
        StartupTiming = PatchStartupTiming.GameplayDeferred)]
    internal static class ShelteredExpeditionTravelPatches
    {
        [HarmonyPatch(typeof(ExplorationParty), "Begin_Traveling")]
        [HarmonyPostfix]
        private static void BeginTravelingPostfix(ExplorationParty __instance)
        {
            try
            {
                ShelteredExpeditionTravelHookService.Instance.OnTravelStarted(__instance);
            }
            catch (Exception ex)
            {
                ShelteredExpeditionTravelHookService.WarnHookFailed("Begin_Traveling", ex);
            }
        }

        [HarmonyPatch(typeof(ExplorationParty), "RecallToShelter")]
        [HarmonyPostfix]
        private static void RecallToShelterPostfix(ExplorationParty __instance)
        {
            try
            {
                ShelteredExpeditionTravelHookService.Instance.OnPartyRecalled(__instance);
            }
            catch (Exception ex)
            {
                ShelteredExpeditionTravelHookService.WarnHookFailed("RecallToShelter", ex);
            }
        }

        [HarmonyPatch(typeof(ExplorationManager), "DisbandExplorationParty")]
        [HarmonyPrefix]
        private static void DisbandExplorationPartyPrefix(int partyId, ref ExplorationParty __state)
        {
            __state = ExplorationManager.Instance != null ? ExplorationManager.Instance.GetParty(partyId) : null;
        }

        [HarmonyPatch(typeof(ExplorationManager), "DisbandExplorationParty")]
        [HarmonyPostfix]
        private static void DisbandExplorationPartyPostfix(bool __result, ExplorationParty __state)
        {
            if (!__result || __state == null)
                return;

            try
            {
                ShelteredExpeditionTravelHookService.Instance.OnPartyDisbanded(__state);
            }
            catch (Exception ex)
            {
                ShelteredExpeditionTravelHookService.WarnHookFailed("DisbandExplorationParty", ex);
            }
        }

        [HarmonyPatch(typeof(ExplorationParty), "PushState")]
        [HarmonyPostfix]
        private static void PushStatePostfix(ExplorationParty __instance, ExplorationParty.ePartyState stateType)
        {
            try
            {
                ShelteredExpeditionTravelHookService.Instance.OnPartyStatePushed(__instance, stateType);
            }
            catch (Exception ex)
            {
                ShelteredExpeditionTravelHookService.WarnHookFailed("PushState", ex);
            }
        }
    }
}
