using System;
using System.Collections.Generic;
using System.Text;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;
using HarmonyLib;
using ModAPI.Core;
using ModAPI.Harmony;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ModAPI.Debugging
{
    internal static class CrashCorridorMapDiagnostics
    {
        private static readonly FieldInfo LoadingLevelLoadTimeField =
            AccessTools.Field(typeof(LoadingLevel), "m_loadTime");
        private static readonly HashSet<int> DisabledUiMapInstanceIds = new HashSet<int>();
        private static readonly FieldInfo LoadingManagerStateField =
            AccessTools.Field(typeof(LoadingManager), "mc_LoadingState");
        private static readonly FieldInfo LoadingManagersWaitingField =
            AccessTools.Field(typeof(LoadingManager), "mc_ManagersWaitingToStart");
        private static readonly FieldInfo LoadingScreenInitializedField =
            AccessTools.Field(typeof(LoadingScreen), "m_initialized");
        private static readonly FieldInfo LoadingScreenShowCountField =
            AccessTools.Field(typeof(LoadingScreen), "m_showCount");
        private static readonly FieldInfo LoadingScreenShowScreenField =
            AccessTools.Field(typeof(LoadingScreen), "m_showScreen");
        private static float _nextLoadingLevelTickLogAt;
        private static float _nextLoadingManagerUpdateLogAt;
        private static string _lastLoadingManagerState = string.Empty;
        private static int _lastLoadingManagerWaiting = int.MinValue;
        private static float _nextLoadingScreenUpdateLogAt;
        private static float _nextUiPanelUpdateLogAt;
        private static readonly FieldInfo LoadingLevelLoadTimeCompareField =
            AccessTools.Field(typeof(LoadingLevel), "m_loadTime");

        private static void Mark(string step, string detail = null)
        {
            if (!PluginRunner.IsQuitting) return;
            SaveExitTracker.Mark(step, detail);
        }

        private static Exception LogException(string step, Exception ex)
        {
            if (ex == null) return null;
            MMLog.WriteWithSource(
                MMLog.LogLevel.Error,
                MMLog.LogCategory.General,
                "SaveExitCheckpoint",
                "[MapDiag] " + step + " threw " + ex.GetType().Name + ": " + ex.Message);
            MMLog.Flush();
            return ex;
        }

        [HarmonyPatch(typeof(UI_ExpeditionMap), "OnEnable")]
        private static class UIExpeditionMap_OnEnable_Patch
        {
            private static void Prefix()
            {
                Mark("UI_ExpeditionMap.OnEnable");
            }
        }

        [HarmonyPatch(typeof(UI_ExpeditionMap), "OnDisable")]
        private static class UIExpeditionMap_OnDisable_Patch
        {
            private static void Prefix(UI_ExpeditionMap __instance)
            {
                if (__instance != null)
                {
                    DisabledUiMapInstanceIds.Add(__instance.GetInstanceID());
                }
                Mark("UI_ExpeditionMap.OnDisable.Begin");
            }

            private static void Postfix()
            {
                Mark("UI_ExpeditionMap.OnDisable.End");
            }

            private static Exception Finalizer(Exception __exception)
            {
                return LogException("UI_ExpeditionMap.OnDisable", __exception);
            }
        }

        [HarmonyPatch(typeof(UI_ExpeditionMap), "OnDestroy")]
        private static class UIExpeditionMap_OnDestroy_Patch
        {
            private static void Prefix(UI_ExpeditionMap __instance)
            {
                if (__instance == null)
                {
                    Mark("UI_ExpeditionMap.OnDestroy");
                    return;
                }

                bool sawDisable = DisabledUiMapInstanceIds.Contains(__instance.GetInstanceID());
                if (sawDisable)
                {
                    DisabledUiMapInstanceIds.Remove(__instance.GetInstanceID());
                }

                Mark(
                    "UI_ExpeditionMap.OnDestroy",
                    "sawOnDisable=" + sawDisable + ", components=" + DescribeComponents(__instance.gameObject));
            }

            private static Exception Finalizer(Exception __exception)
            {
                return LogException("UI_ExpeditionMap.OnDestroy", __exception);
            }
        }

        [HarmonyPatch(typeof(UI_ExpeditionMap), "RemoveAllMapSymbols")]
        private static class UIExpeditionMap_RemoveAllMapSymbols_Patch
        {
            private static void Prefix()
            {
                Mark("UI_ExpeditionMap.RemoveAllMapSymbols.Begin");
            }

            private static void Postfix()
            {
                Mark("UI_ExpeditionMap.RemoveAllMapSymbols.End");
            }

            private static Exception Finalizer(Exception __exception)
            {
                return LogException("UI_ExpeditionMap.RemoveAllMapSymbols", __exception);
            }
        }

        [HarmonyPatch(typeof(PartyMapPanel), "OnClose")]
        private static class PartyMapPanel_OnClose_Patch
        {
            private static void Prefix()
            {
                Mark("PartyMapPanel.OnClose.Begin");
            }

            private static void Postfix()
            {
                Mark("PartyMapPanel.OnClose.End");
            }

            private static Exception Finalizer(Exception __exception)
            {
                return LogException("PartyMapPanel.OnClose", __exception);
            }
        }

        [HarmonyPatch(typeof(UIPanelManager), "OnLevelFinishedLoading")]
        private static class UIPanelManager_OnLevelFinishedLoading_Patch
        {
            private static void Prefix(object scene, object mode)
            {
                if (!PluginRunner.IsQuitting) return;
                Mark("UIPanelManager.OnLevelFinishedLoading.Begin", "scene=" + TrySceneName(scene));
            }

            private static void Postfix(object scene, object mode)
            {
                if (!PluginRunner.IsQuitting) return;
                Mark("UIPanelManager.OnLevelFinishedLoading.End", "scene=" + TrySceneName(scene));
            }

            private static Exception Finalizer(Exception __exception)
            {
                return LogException("UIPanelManager.OnLevelFinishedLoading", __exception);
            }
        }

        [HarmonyPatch(typeof(UIPanelManager), "Update")]
        private static class UIPanelManager_Update_Patch
        {
            private static void Prefix(UIPanelManager __instance)
            {
                if (!PluginRunner.IsQuitting) return;
                if (__instance == null) return;
                if (Time.realtimeSinceStartup < _nextUiPanelUpdateLogAt) return;
                _nextUiPanelUpdateLogAt = Time.realtimeSinceStartup + 0.1f;

                BasePanel top = null;
                int count = -1;
                try
                {
                    top = __instance.GetTopPanel();
                    count = __instance.GetStackCount();
                }
                catch { }

                Mark(
                    "UIPanelManager.Update",
                    "scene=" + GetActiveSceneSafe() +
                    ", stack=" + count +
                    ", top=" + (top != null ? top.GetType().Name : "<null>") +
                    ", inputActive=" + __instance.IsGameInputActive() +
                    ", timePaused=" + __instance.timePaused);
            }

            private static Exception Finalizer(Exception __exception)
            {
                return LogException("UIPanelManager.Update", __exception);
            }
        }

        [HarmonyPatch(typeof(LoadingScreen), "OnClearComplete")]
        private static class LoadingScreen_OnClearComplete_Patch
        {
            private static void Prefix()
            {
                if (!PluginRunner.IsQuitting) return;
                Mark("LoadingScreen.OnClearComplete", "nextLevel=" + (LoadingScreen.nextLevel ?? string.Empty));
            }

            private static Exception Finalizer(Exception __exception)
            {
                return LogException("LoadingScreen.OnClearComplete", __exception);
            }
        }

        [HarmonyPatch(typeof(LoadingScreen), "Update")]
        private static class LoadingScreen_Update_Patch
        {
            private static void Prefix(LoadingScreen __instance)
            {
                if (!PluginRunner.IsQuitting) return;
                if (__instance == null) return;
                if (Time.realtimeSinceStartup < _nextLoadingScreenUpdateLogAt) return;
                _nextLoadingScreenUpdateLogAt = Time.realtimeSinceStartup + 0.1f;

                Mark(
                    "LoadingScreen.Update",
                    "scene=" + GetActiveSceneSafe() +
                    ", initialized=" + ReadBoolField(__instance, LoadingScreenInitializedField) +
                    ", showCount=" + ReadIntField(__instance, LoadingScreenShowCountField) +
                    ", showScreen=" + ReadBoolField(__instance, LoadingScreenShowScreenField) +
                    ", isShowing=" + __instance.isShowing +
                    ", nextLevel=" + (LoadingScreen.nextLevel ?? string.Empty));
            }

            private static Exception Finalizer(Exception __exception)
            {
                return LogException("LoadingScreen.Update", __exception);
            }
        }

        [HarmonyPatch(typeof(LoadingLevel), "Update")]
        private static class LoadingLevel_Update_Patch
        {
            public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase original)
            {
                return FluentTranspiler.For(instructions, original)
                    .MatchCall(typeof(SaveManager), "ContinueTransition", parameterTypes: Type.EmptyTypes)
                    .ReplaceWithCall(typeof(CrashCorridorMapDiagnostics), nameof(ContinueTransition_WithLog), new[] { typeof(SaveManager) })
                    .MatchCall(typeof(SceneManager), "LoadSceneAsync", parameterTypes: new[] { typeof(string) })
                    .ReplaceWithCall(typeof(CrashCorridorMapDiagnostics), nameof(LoadSceneAsync_WithLog), new[] { typeof(string) })
                    .MatchCall(typeof(LoadingScreen), "ClearNextLevel", parameterTypes: Type.EmptyTypes)
                    .ReplaceWithCall(typeof(CrashCorridorMapDiagnostics), nameof(ClearNextLevel_WithLog), Type.EmptyTypes)
                    .Build(strict: false, validateStack: true);
            }

            private static void Prefix(LoadingLevel __instance, out bool __state)
            {
                __state = false;
                if (!PluginRunner.IsQuitting) return;

                if (__instance == null || LoadingLevelLoadTimeField == null) return;
                try
                {
                    object value = LoadingLevelLoadTimeField.GetValue(__instance);
                    float loadTime = value is float ? (float)value : 0f;
                    float now = Time.realtimeSinceStartup;
                    float delta = loadTime - now;

                    if (Time.realtimeSinceStartup >= _nextLoadingLevelTickLogAt)
                    {
                        _nextLoadingLevelTickLogAt = Time.realtimeSinceStartup + 1.0f; 
                        Mark(
                            "LoadingLevel.Update.Tick",
                            "now=" + Time.realtimeSinceStartup + ", m_loadTime=" + loadTime + ", nextLevel=" + (LoadingScreen.nextLevel ?? string.Empty));
                    }

                    if (loadTime > 0f && now >= loadTime)
                    {
                        __state = true;
                        string next = LoadingScreen.nextLevel;
                        if (string.IsNullOrEmpty(next)) next = "MenuScene";
                        Mark("LoadingLevel.Update.Trigger", "next=" + next + ", delta=" + delta);
                    }
                }
                catch (Exception ex)
                {
                    MMLog.WriteDebug("[MapDiag] LoadingLevel.Update probe failed: " + ex.Message);
                }
            }

            private static void Postfix(bool __state)
            {
                if (!PluginRunner.IsQuitting) return;
                if (!__state) return;

                // If this appears, LoadingLevel.Update returned after trigger path.
                Mark("LoadingLevel.Update.AfterTrigger", "nextLevel=" + (LoadingScreen.nextLevel ?? string.Empty));
            }

            private static Exception Finalizer(Exception __exception)
            {
                return LogException("LoadingLevel.Update", __exception);
            }

        }

        [HarmonyPatch(typeof(LoadingManager), "Awake")]
        private static class LoadingManager_Awake_Patch
        {
            private static void Prefix()
            {
                if (!PluginRunner.IsQuitting) return;
                Mark("LoadingManager.Awake.Begin", "state=" + GetLoadingManagerState() + ", waiting=" + GetWaitingManagerCount());
            }

            private static void Postfix()
            {
                if (!PluginRunner.IsQuitting) return;
                Mark("LoadingManager.Awake.End", "state=" + GetLoadingManagerState() + ", waiting=" + GetWaitingManagerCount());
            }

            private static Exception Finalizer(Exception __exception)
            {
                return LogException("LoadingManager.Awake", __exception);
            }
        }

        [HarmonyPatch(typeof(LoadingManager), "Update")]
        private static class LoadingManager_Update_Patch
        {
            private static void Prefix(out string __state)
            {
                __state = null;
                if (!PluginRunner.IsQuitting) return;

                string state = GetLoadingManagerState();
                int waiting = GetWaitingManagerCount();
                if (!ShouldLogLoadingManagerUpdate(state, waiting)) return;

                __state = "state=" + state + ", waiting=" + waiting;
            }

            private static void Postfix(string __state)
            {
                if (!PluginRunner.IsQuitting) return;
                if (string.IsNullOrEmpty(__state)) return;
                Mark("LoadingManager.Update", __state);
            }

            private static Exception Finalizer(Exception __exception)
            {
                return LogException("LoadingManager.Update", __exception);
            }
        }


        [HarmonyPatch(typeof(LoadingManager), "SetLoadingFinished")]
        private static class LoadingManager_SetLoadingFinished_Patch
        {
            private static void Prefix()
            {
                if (!PluginRunner.IsQuitting) return;
                Mark("LoadingManager.SetLoadingFinished.Begin", "state=" + GetLoadingManagerState() + ", waiting=" + GetWaitingManagerCount());
            }

            private static void Postfix()
            {
                if (!PluginRunner.IsQuitting) return;
                Mark("LoadingManager.SetLoadingFinished.End", "state=" + GetLoadingManagerState() + ", waiting=" + GetWaitingManagerCount());
            }

            private static Exception Finalizer(Exception __exception)
            {
                return LogException("LoadingManager.SetLoadingFinished", __exception);
            }
        }

        [HarmonyPatch(typeof(LoadingManager), "OnDestroy")]
        private static class LoadingManager_OnDestroy_Patch
        {
            private static void Prefix()
            {
                if (!PluginRunner.IsQuitting) return;
                Mark("LoadingManager.OnDestroy.Begin", "state=" + GetLoadingManagerState() + ", waiting=" + GetWaitingManagerCount());
            }

            private static void Postfix()
            {
                if (!PluginRunner.IsQuitting) return;
                Mark("LoadingManager.OnDestroy.End", "state=" + GetLoadingManagerState() + ", waiting=" + GetWaitingManagerCount());
            }

            private static Exception Finalizer(Exception __exception)
            {
                return LogException("LoadingManager.OnDestroy", __exception);
            }
        }

        [HarmonyPatch(typeof(LoadingLevel), "Awake")]
        private static class LoadingLevel_Awake_Patch
        {
            private static void Postfix(LoadingLevel __instance)
            {
                if (!PluginRunner.IsQuitting) return;
                if (__instance == null || LoadingLevelLoadTimeField == null)
                {
                    Mark("LoadingLevel.Awake");
                    return;
                }

                try
                {
                    object value = LoadingLevelLoadTimeField.GetValue(__instance);
                    float loadTime = value is float ? (float)value : 0f;
                    Mark("LoadingLevel.Awake", "m_loadTime=" + loadTime);
                }
                catch (Exception ex)
                {
                    MMLog.WriteDebug("[MapDiag] LoadingLevel.Awake probe failed: " + ex.Message);
                    Mark("LoadingLevel.Awake");
                }
            }

            private static Exception Finalizer(Exception __exception)
            {
                return LogException("LoadingLevel.Awake", __exception);
            }
        }

        private static string TrySceneName(object scene)
        {
            if (scene == null) return "<null>";
            try
            {
                var nameProp = scene.GetType().GetProperty("name", BindingFlags.Public | BindingFlags.Instance);
                if (nameProp == null) return scene.ToString();
                var value = nameProp.GetValue(scene, null) as string;
                return string.IsNullOrEmpty(value) ? "<empty>" : value;
            }
            catch
            {
                return scene.ToString();
            }
        }

        private static string DescribeComponents(GameObject go)
        {
            if (go == null) return "<null-go>";
            try
            {
                var comps = go.GetComponents<Component>();
                if (comps == null || comps.Length == 0) return "<none>";

                var sb = new StringBuilder();
                for (int i = 0; i < comps.Length; i++)
                {
                    if (i > 0) sb.Append(",");
                    var c = comps[i];
                    if (c == null)
                    {
                        sb.Append("<missing-script>");
                    }
                    else
                    {
                        var t = c.GetType();
                        sb.Append(t != null ? t.FullName : "<unknown>");
                    }
                }
                return sb.ToString();
            }
            catch (Exception ex)
            {
                return "<component-enum-error:" + ex.GetType().Name + ">";
            }
        }

        private static string GetLoadingManagerState()
        {
            try
            {
                if (LoadingManagerStateField == null) return "<no-state-field>";
                object state = LoadingManagerStateField.GetValue(null);
                return state != null ? state.ToString() : "<null>";
            }
            catch (Exception ex)
            {
                return "<state-error:" + ex.GetType().Name + ">";
            }
        }

        private static int GetWaitingManagerCount()
        {
            try
            {
                if (LoadingManagersWaitingField == null) return -1;
                var list = LoadingManagersWaitingField.GetValue(null) as System.Collections.ICollection;
                return list != null ? list.Count : -1;
            }
            catch
            {
                return -1;
            }
        }

        private static bool ShouldLogLoadingManagerUpdate(string state, int waiting)
        {
            bool changed = !string.Equals(_lastLoadingManagerState, state, StringComparison.Ordinal) ||
                           _lastLoadingManagerWaiting != waiting;
            if (changed)
            {
                _lastLoadingManagerState = state ?? string.Empty;
                _lastLoadingManagerWaiting = waiting;
                _nextLoadingManagerUpdateLogAt = Time.realtimeSinceStartup + 0.5f;
                return true;
            }

            if (Time.realtimeSinceStartup < _nextLoadingManagerUpdateLogAt) return false;
            _nextLoadingManagerUpdateLogAt = Time.realtimeSinceStartup + 0.5f;
            return true;
        }

        private static string GetActiveSceneSafe()
        {
            try
            {
                var s = SceneManager.GetActiveScene();
                return string.IsNullOrEmpty(s.name) ? "<empty>" : s.name;
            }
            catch
            {
                return "<scene-error>";
            }
        }

        private static bool ReadBoolField(object instance, FieldInfo field)
        {
            try
            {
                if (instance == null || field == null) return false;
                object value = field.GetValue(instance);
                return value is bool && (bool)value;
            }
            catch
            {
                return false;
            }
        }

        private static int ReadIntField(object instance, FieldInfo field)
        {
            try
            {
                if (instance == null || field == null) return -1;
                object value = field.GetValue(instance);
                return value is int ? (int)value : -1;
            }
            catch
            {
                return -1;
            }
        }

        // Transpiler wrappers: mid-method probes with exact exception capture.
        private static void ContinueTransition_WithLog(SaveManager manager)
        {
            if (PluginRunner.IsQuitting)
            {
                Mark("LoadingLevel.Call.ContinueTransition.Begin");
            }
            try
            {
                if (manager != null) manager.ContinueTransition();
                if (PluginRunner.IsQuitting)
                {
                    Mark("LoadingLevel.Call.ContinueTransition.End");
                }
            }
            catch (Exception ex)
            {
                MMLog.WriteWithSource(
                    MMLog.LogLevel.Error,
                    MMLog.LogCategory.General,
                    "SaveExitCheckpoint",
                    "[MapDiag] ContinueTransition exception: " + ex.GetType().Name + ": " + ex.Message + "\n" + ex.StackTrace);
                MMLog.Flush();
                throw;
            }
        }

        private static AsyncOperation LoadSceneAsync_WithLog(string sceneName)
        {
            if (PluginRunner.IsQuitting)
            {
                Mark("LoadingLevel.Call.LoadSceneAsync.Begin", "scene=" + sceneName);
            }
            try
            {
                var op = SceneManager.LoadSceneAsync(sceneName);
                if (PluginRunner.IsQuitting)
                {
                    Mark("LoadingLevel.Call.LoadSceneAsync.End", "opNull=" + (op == null));
                }
                return op;
            }
            catch (Exception ex)
            {
                MMLog.WriteWithSource(
                    MMLog.LogLevel.Error,
                    MMLog.LogCategory.General,
                    "SaveExitCheckpoint",
                    "[MapDiag] LoadSceneAsync exception: " + ex.GetType().Name + ": " + ex.Message + "\n" + ex.StackTrace);
                MMLog.Flush();
                throw;
            }
        }

        private static void ClearNextLevel_WithLog()
        {
            if (PluginRunner.IsQuitting)
            {
                Mark("LoadingLevel.Call.ClearNextLevel.Begin");
            }
            try
            {
                LoadingScreen.ClearNextLevel();
                if (PluginRunner.IsQuitting)
                {
                    Mark("LoadingLevel.Call.ClearNextLevel.End", "nextLevel=" + (LoadingScreen.nextLevel ?? string.Empty));
                }
            }
            catch (Exception ex)
            {
                MMLog.WriteWithSource(
                    MMLog.LogLevel.Error,
                    MMLog.LogCategory.General,
                    "SaveExitCheckpoint",
                    "[MapDiag] ClearNextLevel exception: " + ex.GetType().Name + ": " + ex.Message + "\n" + ex.StackTrace);
                MMLog.Flush();
                throw;
            }
        }


    }
}
