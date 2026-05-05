using System;
using System.Reflection;
using HarmonyLib;
using ModAPI.Harmony;
using ShelteredAPI.Core;
using UnityEngine;

namespace ShelteredAPI.Harmony
{
    [PatchPolicy(PatchDomain.SaveFlow, "LoadingTransitionRecovery",
        TargetBehavior = "Detect stalled Sheltered loading transitions, return to main menu, and surface captured error details.",
        FailureMode = "A failed loading transition can leave the player on a transition/loading screen without actionable feedback.",
        RollbackStrategy = "Disable the SaveFlow patch domain or remove the loading transition recovery patch.",
        StartupTiming = PatchStartupTiming.SaveFlowCritical)]
    internal static class LoadingTransitionRecoveryPatches
    {
        private static readonly FieldInfo LoadingLevelLoadTimeField =
            AccessTools.Field(typeof(LoadingLevel), "m_loadTime");

        [PatchPolicy(PatchDomain.SaveFlow, "LoadingScreenRecoveryMonitor",
            TargetBehavior = "Start transition recovery monitoring when Sheltered shows its loading screen.",
            FailureMode = "ShelteredAPI cannot identify transitions that stall before LoadingScene.",
            RollbackStrategy = "Disable the SaveFlow patch domain or remove the loading transition recovery patch.",
            StartupTiming = PatchStartupTiming.SaveFlowCritical)]
        [HarmonyPatch(typeof(LoadingScreen), "ShowLoadingScreen")]
        private static class LoadingScreen_ShowLoadingScreen_Patch
        {
            private static void Prefix(string levelToLoad)
            {
                LoadingTransitionRecoveryService.NotifyLoadingScreenRequested(levelToLoad);
            }

            private static Exception Finalizer(string levelToLoad, Exception __exception)
            {
                if (__exception == null)
                    return null;

                LoadingTransitionRecoveryService.NotifyLoadingScreenRequested(levelToLoad);
                LoadingTransitionRecoveryService.ReportTransitionException("LoadingScreen.ShowLoadingScreen", __exception);
                return null;
            }
        }

        [PatchPolicy(PatchDomain.SaveFlow, "LoadingLevelAwakeRecoveryMonitor",
            TargetBehavior = "Mark LoadingScene entry for failed-load recovery diagnostics.",
            FailureMode = "ShelteredAPI recovery details omit LoadingScene entry breadcrumbs.",
            RollbackStrategy = "Disable the SaveFlow patch domain or remove the loading transition recovery patch.",
            StartupTiming = PatchStartupTiming.SaveFlowCritical)]
        [HarmonyPatch(typeof(LoadingLevel), "Awake")]
        private static class LoadingLevel_Awake_Patch
        {
            private static void Postfix()
            {
                LoadingTransitionRecoveryService.NotifyLoadingLevelAwake();
            }

            private static Exception Finalizer(Exception __exception)
            {
                if (__exception == null)
                    return null;

                LoadingTransitionRecoveryService.ReportTransitionException("LoadingLevel.Awake", __exception);
                return null;
            }
        }

        [PatchPolicy(PatchDomain.SaveFlow, "LoadingLevelUpdateRecoveryMonitor",
            TargetBehavior = "Capture LoadingLevel scene-load attempts and recover from managed transition exceptions.",
            FailureMode = "ShelteredAPI cannot identify failures inside LoadingLevel.Update.",
            RollbackStrategy = "Disable the SaveFlow patch domain or remove the loading transition recovery patch.",
            StartupTiming = PatchStartupTiming.SaveFlowCritical)]
        [HarmonyPatch(typeof(LoadingLevel), "Update")]
        private static class LoadingLevel_Update_Patch
        {
            private static void Prefix(LoadingLevel __instance, out string __state)
            {
                __state = null;

                if (__instance == null || LoadingLevelLoadTimeField == null)
                    return;

                try
                {
                    object value = LoadingLevelLoadTimeField.GetValue(__instance);
                    float loadTime = value is float ? (float)value : 0f;
                    if (loadTime <= 0f || Time.realtimeSinceStartup < loadTime)
                        return;

                    string target = LoadingScreen.nextLevel;
                    if (string.IsNullOrEmpty(target))
                        target = "MenuScene";

                    __state = target;
                    LoadingTransitionRecoveryService.NotifyLoadingLevelTriggered(target);
                }
                catch (Exception ex)
                {
                    LoadingTransitionRecoveryService.ReportTransitionException("LoadingLevel.Update probe", ex);
                }
            }

            private static void Postfix(string __state)
            {
                if (string.IsNullOrEmpty(__state))
                    return;

                bool opMissing = false;
                try
                {
                    opMissing = SaveManager.instance == null || SaveManager.instance.SceneLoadAsyncOp == null;
                }
                catch
                {
                    opMissing = true;
                }

                LoadingTransitionRecoveryService.NotifyLoadingLevelSceneLoadIssued(__state, opMissing);
            }

            private static Exception Finalizer(Exception __exception)
            {
                if (__exception == null)
                    return null;

                LoadingTransitionRecoveryService.ReportTransitionException("LoadingLevel.Update", __exception);
                return null;
            }
        }
    }
}
