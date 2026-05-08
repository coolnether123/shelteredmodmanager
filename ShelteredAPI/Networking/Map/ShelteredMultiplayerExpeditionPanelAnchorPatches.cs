using System.Collections.Generic;
using HarmonyLib;
using ModAPI.Harmony;

namespace ShelteredAPI.Networking
{
    [PatchPolicy(PatchDomain.World, "ShelteredMultiplayerExpeditionPanelAnchor",
        TargetBehavior = "Multiplayer expedition route-distance UI reads the active bunker map position instead of the default center shelter anchor.",
        FailureMode = "The expedition setup panel can report distances from the center-map shelter.",
        RollbackStrategy = "Disable the World patch domain or remove the multiplayer expedition panel-anchor patch host.",
        StartupTiming = PatchStartupTiming.GameplayDeferred)]
    [HarmonyPatch(typeof(ExpeditionMainPanelNew))]
    internal static class ShelteredMultiplayerExpeditionPanelAnchorPatches
    {
        [HarmonyPatch("CalculateRouteDistance")]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> CalculateRouteDistanceTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            return ShelteredMultiplayerShelterAnchorTranspiler.ReplaceShelterMapGetterPairs(instructions);
        }
    }
}
