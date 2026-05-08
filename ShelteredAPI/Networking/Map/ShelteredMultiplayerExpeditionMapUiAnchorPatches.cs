using System;
using System.Collections.Generic;
using HarmonyLib;
using ModAPI.Core;
using ModAPI.Harmony;
using ShelteredAPI.Networking.Diagnostics;

namespace ShelteredAPI.Networking
{
    [PatchPolicy(PatchDomain.World, "ShelteredMultiplayerShelterMapAnchor",
        TargetBehavior = "Vanilla map UI and expedition code read the active multiplayer bunker position instead of the default center shelter anchor.",
        FailureMode = "Every peer sees and routes from a center-map shelter even after multiplayer bunker assignments are applied.",
        RollbackStrategy = "Disable the World patch domain or remove the multiplayer shelter map-anchor patch host.",
        StartupTiming = PatchStartupTiming.GameplayDeferred)]
    [HarmonyPatch(typeof(UI_ExpeditionMap))]
    internal static class ShelteredMultiplayerExpeditionMapUiAnchorPatches
    {
        [HarmonyPatch("UpdateMapSymbols")]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> UpdateMapSymbolsTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            return ShelteredMultiplayerShelterAnchorTranspiler.ReplaceShelterMapGetterPairs(instructions);
        }

        [HarmonyPatch("OnEnable")]
        [HarmonyPrefix]
        private static void ExpeditionMapOnEnablePrefix()
        {
            try
            {
                ShelteredMultiplayerBunkerAnchorRuntime.CacheActiveBunkerPosition("UI_ExpeditionMap.OnEnable");
                ShelteredMultiplayerMapAnchorDiagnostics.LogReport("UI_ExpeditionMap.OnEnable");
            }
            catch (Exception ex)
            {
                MMLog.WarnOnce("ShelteredMultiplayerShelterMapAnchor.OnEnable",
                    "Multiplayer shelter map-anchor UI hook failed: " + ex.Message);
            }
        }
    }
}
