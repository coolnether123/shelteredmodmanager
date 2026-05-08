using System;
using HarmonyLib;
using ModAPI.Core;
using ModAPI.Harmony;

namespace ShelteredAPI.Networking
{
    [PatchPolicy(PatchDomain.World, "ShelteredMultiplayerMapSeed",
        TargetBehavior = "Active multiplayer sessions seed vanilla expedition map generation from the shared master seed.",
        FailureMode = "Peers can create maps from local or clock-derived random seeds before the session seed is active.",
        RollbackStrategy = "Disable the World patch domain or remove the multiplayer map seed patch host.",
        StartupTiming = PatchStartupTiming.GameplayDeferred)]
    internal static class ShelteredMultiplayerMapSeedPatches
    {
        [HarmonyPatch(typeof(ExpeditionMap), "CreateMap")]
        [HarmonyPrefix]
        private static void CreateMapPrefix(ExpeditionMap __instance)
        {
            try
            {
                ShelteredMultiplayerMapSeedRuntime.ApplyMapSeed(__instance, "ExpeditionMap.CreateMap");
            }
            catch (Exception ex)
            {
                MMLog.WarnOnce("ShelteredMultiplayerMapSeed.CreateMap",
                    "Multiplayer map seed hook failed: " + ex.Message);
            }
        }

        [HarmonyPatch(typeof(ExpeditionMap), "CreateStasisMap")]
        [HarmonyPrefix]
        private static void CreateStasisMapPrefix(ExpeditionMap __instance)
        {
            try
            {
                ShelteredMultiplayerMapSeedRuntime.ApplyMapSeed(__instance, "ExpeditionMap.CreateStasisMap");
            }
            catch (Exception ex)
            {
                MMLog.WarnOnce("ShelteredMultiplayerMapSeed.CreateStasisMap",
                    "Multiplayer stasis map seed hook failed: " + ex.Message);
            }
        }
    }
}
