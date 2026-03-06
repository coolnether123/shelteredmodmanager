using System;
using HarmonyLib;
using ModAPI.Characters;
using ModAPI.Harmony;

namespace ModAPI.Harmony
{
    [PatchPolicy(PatchDomain.Characters, "ShelteredPartyEvents",
        TargetBehavior = "Sheltered expedition party membership change notifications",
        FailureMode = "Sheltered party callbacks stop reflecting party membership changes.",
        RollbackStrategy = "Disable the Characters patch domain or remove the Sheltered party patch host.")]
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
