using System;
using HarmonyLib;
using ModAPI.Core;
using ModAPI.Harmony;

namespace ShelteredAPI.Networking
{
    [PatchPolicy(PatchDomain.World, "ShelteredMultiplayerGameTimeHooks",
        TargetBehavior = "GameTime.Update publishes multiplayer hook events and allows remote-authoritative clients to suppress local clock advancement.",
        FailureMode = "Multiplayer cannot centralize world tick ownership and clients may drift from the host clock.",
        RollbackStrategy = "Disable the World patch domain or remove the multiplayer hook patch host.",
        StartupTiming = PatchStartupTiming.GameplayDeferred)]
    [HarmonyPatch(typeof(GameTime), "Update")]
    internal static class ShelteredMultiplayerGameTimePatches
    {
        private static bool Prefix(GameTime __instance)
        {
            try
            {
                return ShelteredMultiplayerHookService.Instance.BeginGameTimeUpdate(__instance);
            }
            catch (Exception ex)
            {
                MMLog.WarnOnce("ShelteredMultiplayerGameTime.Prefix", "GameTime multiplayer hook failed: " + ex.Message);
                return true;
            }
        }

        private static void Postfix(GameTime __instance)
        {
            try
            {
                ShelteredMultiplayerHookService.Instance.EndGameTimeUpdate(__instance);
            }
            catch (Exception ex)
            {
                MMLog.WarnOnce("ShelteredMultiplayerGameTime.Postfix", "GameTime multiplayer hook failed: " + ex.Message);
            }
        }
    }

    [PatchPolicy(PatchDomain.World, "ShelteredMultiplayerPauseHooks",
        TargetBehavior = "PauseManager requests are routed through the multiplayer hook service so active network sessions can keep simulation time moving.",
        FailureMode = "Opening a menu can pause Time.timeScale on one peer and immediately desync multiplayer sessions.",
        RollbackStrategy = "Disable the World patch domain or remove the multiplayer pause hook patch host.",
        StartupTiming = PatchStartupTiming.GameplayDeferred)]
    internal static class ShelteredMultiplayerPausePatches
    {
        [HarmonyPatch(typeof(PauseManager), "Pause")]
        [HarmonyPrefix]
        private static bool PausePrefix(PauseManager __instance)
        {
            try
            {
                return ShelteredMultiplayerHookService.Instance.HandlePauseRequest("PauseManager.Pause", __instance);
            }
            catch (Exception ex)
            {
                MMLog.WarnOnce("ShelteredMultiplayerPause.Pause", "Pause multiplayer hook failed: " + ex.Message);
                return true;
            }
        }

        [HarmonyPatch(typeof(PauseManager), "Resume")]
        [HarmonyPostfix]
        private static void ResumePostfix(PauseManager __instance)
        {
            try
            {
                ShelteredMultiplayerHookService.Instance.HandleResumeRequest("PauseManager.Resume", __instance);
            }
            catch (Exception ex)
            {
                MMLog.WarnOnce("ShelteredMultiplayerPause.Resume", "Resume multiplayer hook failed: " + ex.Message);
            }
        }

        [HarmonyPatch(typeof(UIPanelManager), nameof(UIPanelManager.timePaused), MethodType.Getter)]
        [HarmonyPostfix]
        private static void TimePausedGetterPostfix(ref bool __result)
        {
            try
            {
                if (ShelteredMultiplayerHookService.Instance.IsMultiplayerActive)
                    __result = false;
            }
            catch (Exception ex)
            {
                MMLog.WarnOnce("ShelteredMultiplayerPause.TimePaused", "timePaused multiplayer hook failed: " + ex.Message);
            }
        }
    }
}
