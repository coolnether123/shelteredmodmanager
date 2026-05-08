using System.Collections.Generic;
using HarmonyLib;
using ModAPI.Harmony;

namespace ShelteredAPI.Networking
{
    [PatchPolicy(PatchDomain.World, "ShelteredMultiplayerExpeditionRouteAnchor",
        TargetBehavior = "Multiplayer expedition route calculations read the active bunker map position instead of the default center shelter anchor.",
        FailureMode = "Expedition routes and open-ground distance checks can still measure from the center-map shelter.",
        RollbackStrategy = "Disable the World patch domain or remove the multiplayer expedition route-anchor patch host.",
        StartupTiming = PatchStartupTiming.GameplayDeferred)]
    [HarmonyPatch(typeof(ExplorationParty))]
    internal static class ShelteredMultiplayerExplorationPartyAnchorPatches
    {
        [HarmonyPatch("SetRoute")]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> SetRouteTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            return ShelteredMultiplayerShelterAnchorTranspiler.ReplaceShelterMapGetterPairs(instructions);
        }

        [HarmonyPatch("OpenGroundEncounterCheck")]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> OpenGroundEncounterCheckTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            return ShelteredMultiplayerShelterAnchorTranspiler.ReplaceShelterMapGetterPairs(instructions);
        }
    }
}
