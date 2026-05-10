using System;
using HarmonyLib;
using ModAPI.Core;
using ModAPI.Harmony;
using ShelteredAPI.Networking.Diagnostics;

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
                ShelteredMultiplayerBunkerAnchorRuntime.ResetValidatedAnchor("ExpeditionMap.CreateMap");
                ShelteredMultiplayerBunkerAnchorRuntime.BeginMapGeneration("ExpeditionMap.CreateMap");
                ShelteredMultiplayerMapSeedRuntime.ApplyMapSeed(__instance, "ExpeditionMap.CreateMap");
            }
            catch (Exception ex)
            {
                MMLog.WarnOnce("ShelteredMultiplayerMapSeed.CreateMap",
                    "Multiplayer map seed hook failed: " + ex.Message);
            }
        }

        [HarmonyPatch(typeof(ExpeditionMap), "CreateMap")]
        [HarmonyPostfix]
        private static void CreateMapPostfix()
        {
            try
            {
                ShelteredMultiplayerBunkerAnchorRuntime.CacheActiveBunkerPosition("ExpeditionMap.CreateMap.Postfix");
                ShelteredMultiplayerMapAnchorDiagnostics.LogReport("ExpeditionMap.CreateMap.Postfix");
            }
            catch (Exception ex)
            {
                MMLog.WarnOnce("ShelteredMultiplayerMapSeed.CreateMap.Postfix",
                    "Multiplayer map seed post-hook failed: " + ex.Message);
            }
            finally
            {
                ShelteredMultiplayerBunkerAnchorRuntime.EndMapGeneration("ExpeditionMap.CreateMap.Postfix");
            }
        }

        [HarmonyPatch(typeof(ExpeditionMap), "CreateStasisMap")]
        [HarmonyPrefix]
        private static void CreateStasisMapPrefix(ExpeditionMap __instance)
        {
            try
            {
                ShelteredMultiplayerBunkerAnchorRuntime.ResetValidatedAnchor("ExpeditionMap.CreateStasisMap");
                ShelteredMultiplayerBunkerAnchorRuntime.BeginMapGeneration("ExpeditionMap.CreateStasisMap");
                ShelteredMultiplayerMapSeedRuntime.ApplyMapSeed(__instance, "ExpeditionMap.CreateStasisMap");
            }
            catch (Exception ex)
            {
                MMLog.WarnOnce("ShelteredMultiplayerMapSeed.CreateStasisMap",
                    "Multiplayer stasis map seed hook failed: " + ex.Message);
            }
        }

        [HarmonyPatch(typeof(ExpeditionMap), "CreateStasisMap")]
        [HarmonyPostfix]
        private static void CreateStasisMapPostfix()
        {
            try
            {
                ShelteredMultiplayerBunkerAnchorRuntime.CacheActiveBunkerPosition("ExpeditionMap.CreateStasisMap.Postfix");
                ShelteredMultiplayerMapAnchorDiagnostics.LogReport("ExpeditionMap.CreateStasisMap.Postfix");
            }
            catch (Exception ex)
            {
                MMLog.WarnOnce("ShelteredMultiplayerMapSeed.CreateStasisMap.Postfix",
                    "Multiplayer stasis map seed post-hook failed: " + ex.Message);
            }
            finally
            {
                ShelteredMultiplayerBunkerAnchorRuntime.EndMapGeneration("ExpeditionMap.CreateStasisMap.Postfix");
            }
        }
    }
}
