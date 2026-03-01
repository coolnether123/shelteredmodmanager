using System;
using HarmonyLib;
using ModAPI.Characters;

namespace ModAPI.Harmony
{
    [HarmonyPatch(typeof(ExplorationParty), "AddMember")]
    internal static class ExplorationParty_AddMember_Patch
    {
        private static void Postfix(ExplorationParty __instance)
        {
            PartyHelper.NotifyPartyChanged(__instance.id);
        }
    }

    [HarmonyPatch(typeof(ExplorationParty), "RemoveMember")]
    internal static class ExplorationParty_RemoveMember_Patch
    {
        private static void Postfix(ExplorationParty __instance)
        {
            PartyHelper.NotifyPartyChanged(__instance.id);
        }
    }

    [HarmonyPatch(typeof(ExplorationManager), "CreateParty")]
    internal static class ExplorationManager_CreateParty_Patch
    {
        private static void Postfix(int __result)
        {
            PartyHelper.NotifyPartyChanged(__result);
        }
    }

    [HarmonyPatch(typeof(ExplorationManager), "DisbandParty")]
    internal static class ExplorationManager_DisbandParty_Patch
    {
        private static void Postfix(int id)
        {
            PartyHelper.NotifyPartyChanged(id);
        }
    }
}
