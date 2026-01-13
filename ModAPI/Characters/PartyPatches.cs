using HarmonyLib;
using ModAPI.Characters;
using UnityEngine;

namespace ModAPI.Characters.Internal
{
    [HarmonyPatch]
    internal static class PartyPatches
    {
        [HarmonyPatch(typeof(ExplorationManager), "CreateExplorationParty")]
        [HarmonyPostfix]
        static void Postfix_CreateParty(bool __result, ref int partyId)
        {
            if (__result) PartyHelper.NotifyPartyChanged(partyId);
        }

        [HarmonyPatch(typeof(ExplorationManager), "DisbandExplorationParty")]
        [HarmonyPostfix]
        static void Postfix_DisbandParty(bool __result, int partyId)
        {
            if (__result) PartyHelper.NotifyPartyChanged(partyId);
        }

        [HarmonyPatch(typeof(ExplorationParty), "AddMember")]
        [HarmonyPostfix]
        static void Postfix_AddMember(ExplorationParty __instance, bool __result)
        {
            if (__result) PartyHelper.NotifyPartyChanged(__instance.id);
        }

        [HarmonyPatch(typeof(ExplorationParty), "RemoveMember")]
        [HarmonyPostfix]
        static void Postfix_RemoveMember(ExplorationParty __instance, bool __result)
        {
            if (__result) PartyHelper.NotifyPartyChanged(__instance.id);
        }
        
        [HarmonyPatch(typeof(ExplorationManager), "PartyHasReturned")]
        [HarmonyPostfix]
        static void Postfix_PartyReturned(int partyId)
        {
            // Disband is called inside PartyHasReturned, but notifying here is also good
            PartyHelper.NotifyPartyChanged(partyId);
        }
    }
}
