using System;
using System.Reflection;
using HarmonyLib;
using ModAPI.Harmony;

using ShelteredAPI.Scenarios.Infrastructure.Runtime;
namespace ShelteredAPI.Scenarios.Infrastructure.Harmony{
    [PatchPolicy(PatchDomain.Scenarios, "ScenarioWorldEventSuppression",
        TargetBehavior = "Scenario world-event suppression flags disable selected vanilla visitor, binman, raid, and radio-broadcast odds paths while leaving scripted events active.",
        FailureMode = "Authored scenarios cannot reliably replace vanilla world-event timing.",
        RollbackStrategy = "Disable the Scenarios patch domain or remove the world-event suppression patch host.",
        StartupTiming = PatchStartupTiming.GameplayDeferred)]
    internal static class ScenarioWorldEventSuppressionPatches
    {
        private static readonly MethodInfo UpdateBinManSpawnMethod = typeof(NpcVisitManager).GetMethod("UpdateBinManSpawn", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo BreachNextSpawnTimeField = typeof(BreachMan).GetField("m_nextSpawnTime", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo RadioBroadcastingField = typeof(Obj_Radio).GetField("m_broadcasting", BindingFlags.NonPublic | BindingFlags.Instance);

        [HarmonyPatch(typeof(NpcVisitManager), "UpdateSurvivial")]
        [HarmonyPrefix]
        private static bool UpdateSurvivialPrefix(NpcVisitManager __instance)
        {
            if (!ScenarioWorldEventRuntimeState.SuppressRandomVisitors)
                return true;

            if (!ScenarioWorldEventRuntimeState.SuppressBinman)
                InvokeBinmanUpdate(__instance);
            return false;
        }

        [HarmonyPatch(typeof(NpcVisitManager), "UpdateStasis")]
        [HarmonyPrefix]
        private static bool UpdateStasisPrefix(NpcVisitManager __instance)
        {
            if (!ScenarioWorldEventRuntimeState.SuppressStasisVisitors)
                return true;

            if (!ScenarioWorldEventRuntimeState.SuppressBinman)
                InvokeBinmanUpdate(__instance);
            return false;
        }

        [HarmonyPatch(typeof(NpcVisitManager), "UpdateBinManSpawn")]
        [HarmonyPrefix]
        private static bool UpdateBinManSpawnPrefix()
        {
            return !ScenarioWorldEventRuntimeState.SuppressBinman;
        }

        [HarmonyPatch(typeof(BreachMan), "UpdateManager")]
        [HarmonyPrefix]
        private static bool BreachUpdateManagerPrefix(BreachMan __instance)
        {
            if (!ScenarioWorldEventRuntimeState.SuppressRaids || __instance == null)
                return true;
            if (__instance.currentStage != BreachMan.BreachStage.Finished || __instance.inProgress)
                return true;

            if (BreachNextSpawnTimeField != null)
                BreachNextSpawnTimeField.SetValue(__instance, UnityEngine.Time.time + GameTime.RealSecondsPerDay);
            return false;
        }

        [HarmonyPatch(typeof(Obj_Radio), "StartBroadcastingForTraders")]
        [HarmonyPrefix]
        private static bool StartBroadcastingForTradersPrefix(Obj_Radio __instance)
        {
            return StartBroadcastingPrefix(__instance);
        }

        [HarmonyPatch(typeof(Obj_Radio), "StartBroadcastingForRecruits")]
        [HarmonyPrefix]
        private static bool StartBroadcastingForRecruitsPrefix(Obj_Radio __instance)
        {
            return StartBroadcastingPrefix(__instance);
        }

        private static bool StartBroadcastingPrefix(Obj_Radio radio)
        {
            if (ScenarioWorldEventRuntimeState.IsDispatchingAuthoredRadioBroadcast)
                return true;

            if (!ScenarioWorldEventRuntimeState.SuppressRadioBroadcastOdds)
                return true;

            if (radio != null && RadioBroadcastingField != null)
                RadioBroadcastingField.SetValue(radio, true);
            return false;
        }

        private static void InvokeBinmanUpdate(NpcVisitManager manager)
        {
            if (manager == null || UpdateBinManSpawnMethod == null)
                return;
            try { UpdateBinManSpawnMethod.Invoke(manager, new object[0]); }
            catch { }
        }
    }
}
