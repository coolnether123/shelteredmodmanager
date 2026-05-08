using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using ModAPI.Core;
using ModAPI.Harmony;
using UnityEngine;

namespace ShelteredAPI.Networking
{
    [PatchPolicy(PatchDomain.World, "ShelteredMultiplayerFixedGameTime",
        TargetBehavior = "Active multiplayer sessions use the fixed network day length without allowing player fast/slow controls to change global time.",
        FailureMode = "Players can speed up or slow down GameTime locally and desynchronize the shared multiplayer calendar.",
        RollbackStrategy = "Disable the World patch domain or remove the multiplayer fixed-time patch host.",
        StartupTiming = PatchStartupTiming.GameplayDeferred)]
    [HarmonyPatch(typeof(GameTime))]
    internal static class ShelteredMultiplayerGameTimePolicyPatches
    {
        [HarmonyPatch("Awake")]
        [HarmonyPrefix]
        private static void AwakePrefix(GameTime __instance)
        {
            try
            {
                ShelteredMultiplayerTimePolicy.ApplyGameTimePolicy(__instance);
            }
            catch (Exception ex)
            {
                MMLog.WarnOnce("ShelteredMultiplayerGameTimePolicy.Awake",
                    "GameTime multiplayer day-length policy failed: " + ex.Message);
            }
        }
    }

    [PatchPolicy(PatchDomain.World, "ShelteredMultiplayerCameraSpeedControls",
        TargetBehavior = "Fast-forward and slow-down inputs record local bunker intensity, not Time.timeScale or shared world-clock speed.",
        FailureMode = "Camera speed buttons or hotkeys can still change Time.timeScale and desynchronize multiplayer time.",
        RollbackStrategy = "Disable the World patch domain or remove the multiplayer camera speed patch host.",
        StartupTiming = PatchStartupTiming.GameplayDeferred)]
    internal static class ShelteredMultiplayerCameraSpeedPatches
    {
        [HarmonyPatch(typeof(BasicCamera), "StartFastForward")]
        [HarmonyPrefix]
        private static bool StartFastForwardPrefix(BasicCamera __instance)
        {
            try
            {
                return !ShelteredMultiplayerTimePolicy.TryHandleFastForward(true, __instance, "BasicCamera.StartFastForward");
            }
            catch (Exception ex)
            {
                MMLog.WarnOnce("ShelteredMultiplayerCameraSpeed.StartFastForward",
                    "Fast-forward multiplayer speed hook failed: " + ex.Message);
                return true;
            }
        }

        [HarmonyPatch(typeof(BasicCamera), "EndFastForward")]
        [HarmonyPrefix]
        private static bool EndFastForwardPrefix(BasicCamera __instance)
        {
            try
            {
                return !ShelteredMultiplayerTimePolicy.TryHandleFastForward(false, __instance, "BasicCamera.EndFastForward");
            }
            catch (Exception ex)
            {
                MMLog.WarnOnce("ShelteredMultiplayerCameraSpeed.EndFastForward",
                    "Fast-forward multiplayer speed hook failed: " + ex.Message);
                return true;
            }
        }

        [HarmonyPatch(typeof(BasicCamera), "StartSlowDown")]
        [HarmonyPrefix]
        private static bool StartSlowDownPrefix(BasicCamera __instance)
        {
            try
            {
                return !ShelteredMultiplayerTimePolicy.TryHandleSlowDown(true, __instance, "BasicCamera.StartSlowDown");
            }
            catch (Exception ex)
            {
                MMLog.WarnOnce("ShelteredMultiplayerCameraSpeed.StartSlowDown",
                    "Slow-down multiplayer speed hook failed: " + ex.Message);
                return true;
            }
        }

        [HarmonyPatch(typeof(BasicCamera), "EndSlowDown")]
        [HarmonyPrefix]
        private static bool EndSlowDownPrefix(BasicCamera __instance)
        {
            try
            {
                return !ShelteredMultiplayerTimePolicy.TryHandleSlowDown(false, __instance, "BasicCamera.EndSlowDown");
            }
            catch (Exception ex)
            {
                MMLog.WarnOnce("ShelteredMultiplayerCameraSpeed.EndSlowDown",
                    "Slow-down multiplayer speed hook failed: " + ex.Message);
                return true;
            }
        }

        [HarmonyPatch(typeof(BasicCamera), "get_isSpedUp")]
        [HarmonyPostfix]
        private static void IsSpedUpPostfix(ref bool __result)
        {
            try
            {
                if (ShelteredMultiplayerHookService.Instance.IsMultiplayerActive)
                    __result = ShelteredMultiplayerTimePolicy.IsFastModeActive();
            }
            catch (Exception ex)
            {
                MMLog.WarnOnce("ShelteredMultiplayerCameraSpeed.IsSpedUp",
                    "Fast-forward UI state hook failed: " + ex.Message);
            }
        }

        [HarmonyPatch(typeof(BasicCamera), "get_isSlowedDown")]
        [HarmonyPostfix]
        private static void IsSlowedDownPostfix(ref bool __result)
        {
            try
            {
                if (ShelteredMultiplayerHookService.Instance.IsMultiplayerActive)
                    __result = ShelteredMultiplayerTimePolicy.IsSlowModeActive();
            }
            catch (Exception ex)
            {
                MMLog.WarnOnce("ShelteredMultiplayerCameraSpeed.IsSlowedDown",
                    "Slow-down UI state hook failed: " + ex.Message);
            }
        }

        [HarmonyPatch(typeof(BasicCamera), "Update")]
        [HarmonyPostfix]
        private static void UpdatePostfix()
        {
            try
            {
                ShelteredMultiplayerTimePolicy.ForceRealtimeTimescale();
            }
            catch (Exception ex)
            {
                MMLog.WarnOnce("ShelteredMultiplayerCameraSpeed.Update",
                    "Realtime timescale guard failed: " + ex.Message);
            }
        }
    }

    [PatchPolicy(PatchDomain.World, "ShelteredMultiplayerExpeditionTravelCompensation",
        TargetBehavior = "Expedition travel compensates for the shorter multiplayer day without applying local fast/slow input.",
        FailureMode = "Faster multiplayer days make expeditions move faster than intended or let local time-scale controls affect shared travel.",
        RollbackStrategy = "Disable the World patch domain or remove the multiplayer expedition speed patch host.",
        StartupTiming = PatchStartupTiming.GameplayDeferred)]
    [HarmonyPatch(typeof(ExplorationParty), "Update_Traveling")]
    internal static class ShelteredMultiplayerExpeditionTravelSpeedPatch
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo realSecondsToGameSeconds = AccessTools.Method(typeof(GameTime), "RealSecondsToGameSeconds", new Type[] { typeof(float) });
            MethodInfo applyTravelDistance = AccessTools.Method(typeof(ShelteredMultiplayerTimePolicy), "ApplyTravelDistance", new Type[] { typeof(float) });
            bool afterGameTimeConversion = false;
            bool patched = false;

            foreach (CodeInstruction instruction in instructions)
            {
                yield return instruction;

                if (instruction.Calls(realSecondsToGameSeconds))
                {
                    afterGameTimeConversion = true;
                    continue;
                }

                if (afterGameTimeConversion && instruction.opcode == OpCodes.Mul)
                {
                    yield return new CodeInstruction(OpCodes.Call, applyTravelDistance);
                    afterGameTimeConversion = false;
                    patched = true;
                }
            }

            if (!patched)
            {
                MMLog.WarnOnce("ShelteredMultiplayerExpeditionTravelSpeed.Transpiler",
                    "Could not find ExplorationParty.Update_Traveling travel-distance calculation.");
            }
        }
    }
}
