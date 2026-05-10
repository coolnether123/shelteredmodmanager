using System;
using HarmonyLib;
using ModAPI.Core;
using ModAPI.Harmony;
using UnityEngine;

namespace ShelteredAPI.Networking
{
    [PatchPolicy(PatchDomain.World, "ShelteredMultiplayerMapShelterOrigin",
        TargetBehavior = "Vanilla expedition map generation treats the active multiplayer bunker as the shelter origin.",
        FailureMode = "Map generation paths hardcoded to world origin can still build shelter-adjacent regions around the center of the map.",
        RollbackStrategy = "Disable the World patch domain or remove the multiplayer shelter-origin patch host.",
        StartupTiming = PatchStartupTiming.GameplayDeferred)]
    [HarmonyPatch(typeof(ExpeditionMap))]
    internal static class ShelteredMultiplayerMapShelterOriginPatches
    {
        [HarmonyPatch("WorldPosToGridRef")]
        [HarmonyPrefix]
        private static void WorldPosToGridRefPrefix(ref Vector2 worldPos)
        {
            try
            {
                ShelteredMultiplayerBunkerAnchorRuntime.RedirectShelterOriginWorldPosition(ref worldPos);
            }
            catch (Exception ex)
            {
                MMLog.WarnOnce("ShelteredMultiplayerMapShelterOrigin.WorldPosToGridRef",
                    "Multiplayer shelter-origin redirect failed: " + ex.Message);
            }
        }

        [HarmonyPatch("PlaceShelters")]
        [HarmonyPostfix]
        private static void PlaceSheltersPostfix(ExpeditionMap __instance)
        {
            try
            {
                ShelteredMultiplayerShelterCellRuntime.ForceMapGenerationBunkerShelterCell(__instance);
            }
            catch (Exception ex)
            {
                MMLog.WarnOnce("ShelteredMultiplayerMapShelterOrigin.PlaceShelters",
                    "Multiplayer shelter-cell placement failed: " + ex.Message);
            }
        }
    }
}
