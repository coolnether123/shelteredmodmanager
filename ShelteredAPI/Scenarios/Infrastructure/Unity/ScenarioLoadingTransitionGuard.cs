using ModAPI.Core;
using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShelteredAPI.Scenarios.Infrastructure.Unity
{
    internal static class ScenarioLoadingTransitionGuard
    {
        private static string _ownedDirectLaunchScene;
        private static string _ownedDirectLaunchTarget;
        private static string _ownedManagedLoadingScene;
        private static string _ownedManagedLoadingTarget;
        private static bool _managedHideSuppressionLogged;
        private static readonly FieldInfo LoadingScreenShowCountField = typeof(LoadingScreen).GetField("m_showCount", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo LoadingScreenShowScreenField = typeof(LoadingScreen).GetField("m_showScreen", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo LoadingScreenImageField = typeof(LoadingScreen).GetField("m_loadingImage", BindingFlags.Instance | BindingFlags.NonPublic);

        public static void PrepareForManagedTransition(string targetLabel)
        {
            ClearDirectLaunchOwnership();
            ClearManagedLoadingOwnership();

            if (LoadingScreen.Instance != null)
            {
                if (LoadingScreen.Instance.isShowing)
                {
                    HideManagedLoadingScreenImmediately(LoadingScreen.Instance);
                    MMLog.WriteInfo("[ScenarioLoadingTransitionGuard] Cleared stale loading screen before managed transition. target="
                        + (targetLabel ?? "<unknown>") + ".");
                }

                LoadingScreen.ClearNextLevel();
            }

            if (CutsceneManager.Instance != null && CutsceneManager.Instance.CutSceneActive)
            {
                CutsceneManager.Instance.pauseCutsceneManager = true;
                MMLog.WriteInfo("[ScenarioLoadingTransitionGuard] Paused active cutscene manager before managed transition. target="
                    + (targetLabel ?? "<unknown>") + ".");
            }
        }

        public static void OwnManagedLoadingTransition(string expectedSceneName, string targetLabel)
        {
            _ownedManagedLoadingScene = expectedSceneName ?? string.Empty;
            _ownedManagedLoadingTarget = targetLabel ?? string.Empty;
            _managedHideSuppressionLogged = false;
        }

        public static bool ShouldSuppressVanillaLoadingScreenHide()
        {
            if (string.IsNullOrEmpty(_ownedManagedLoadingScene))
                return false;

            if (!_managedHideSuppressionLogged)
            {
                _managedHideSuppressionLogged = true;
                MMLog.WriteInfo("[ScenarioLoadingTransitionGuard] Suppressed a premature vanilla loading-screen hide while waiting for scene="
                    + _ownedManagedLoadingScene + " target="
                    + (!string.IsNullOrEmpty(_ownedManagedLoadingTarget) ? _ownedManagedLoadingTarget : "<unknown>") + ".");
            }
            return true;
        }

        public static void OwnDirectLaunchTransition(string expectedSceneName, string targetLabel)
        {
            _ownedDirectLaunchScene = expectedSceneName ?? string.Empty;
            _ownedDirectLaunchTarget = targetLabel ?? string.Empty;
            MMLog.WriteInfo("[ScenarioLoadingTransitionGuard] Direct launch owns fade handoff. target="
                + (_ownedDirectLaunchTarget.Length > 0 ? _ownedDirectLaunchTarget : "<unknown>")
                + " scene=" + (_ownedDirectLaunchScene.Length > 0 ? _ownedDirectLaunchScene : "<unknown>") + ".");
        }

        public static bool TryCompleteManagedTransition(string expectedSceneName, string targetLabel)
        {
            if (!ScenarioWorldReady.IsShelterSceneActive())
                return false;

            string activeSceneName = SceneManager.GetActiveScene().name;
            if (!string.IsNullOrEmpty(expectedSceneName)
                && !string.Equals(activeSceneName, expectedSceneName, StringComparison.Ordinal))
            {
                return false;
            }

            if (!string.IsNullOrEmpty(LoadingScreen.nextLevel))
                return false;

            if (SaveManager.instance != null && (SaveManager.instance.isLoading || SaveManager.instance.isSaving))
                return false;

            ClearManagedLoadingOwnership();
            bool completed = false;
            if (LoadingScreen.Instance != null && LoadingScreen.Instance.isShowing)
            {
                HideManagedLoadingScreenImmediately(LoadingScreen.Instance);
                MMLog.WriteInfo("[ScenarioLoadingTransitionGuard] Completed managed shelter loading screen without a redundant post-load fade. target="
                    + (targetLabel ?? "<unknown>") + " scene=" + activeSceneName + ".");
                completed = true;
            }

            if (TryCompleteOwnedDirectLaunchFade(activeSceneName))
                completed = true;

            return completed;
        }

        internal static bool OwnsDirectLaunchFadeForScene(string activeSceneName)
        {
            return !string.IsNullOrEmpty(_ownedDirectLaunchScene)
                && string.Equals(_ownedDirectLaunchScene, activeSceneName, StringComparison.Ordinal);
        }

        private static bool TryCompleteOwnedDirectLaunchFade(string activeSceneName)
        {
            if (!OwnsDirectLaunchFadeForScene(activeSceneName))
                return false;

            FadeManager fade = FadeManager.Instance;
            if (fade == null || UIPanelManager.instance == null)
                return false;

            string targetLabel = _ownedDirectLaunchTarget;
            ClearDirectLaunchOwnership();
            fade.FadeFromBlack(true);
            MMLog.WriteInfo("[ScenarioLoadingTransitionGuard] Started vanilla direct-launch fade-in handoff. target="
                + (!string.IsNullOrEmpty(targetLabel) ? targetLabel : "<unknown>")
                + " scene=" + activeSceneName + ".");
            return true;
        }

        private static void HideManagedLoadingScreenImmediately(LoadingScreen loadingScreen)
        {
            if (loadingScreen == null)
                return;

            try
            {
                if (LoadingScreenShowCountField == null
                    || LoadingScreenShowScreenField == null
                    || LoadingScreenImageField == null)
                {
                    loadingScreen.HideLoadingScreen(true);
                    return;
                }

                LoadingScreenShowCountField.SetValue(loadingScreen, 0);
                LoadingScreenShowScreenField.SetValue(loadingScreen, false);
                GameObject loadingImage = LoadingScreenImageField.GetValue(loadingScreen) as GameObject;
                if (loadingImage != null)
                    loadingImage.SetActive(false);

                if (ScreenFade.instance != null)
                    ScreenFade.instance.ClearFade(0f, true);
                if (UIPanelManager.instance == null || !UIPanelManager.instance.timePaused)
                    Time.timeScale = 1f;
            }
            catch (Exception ex)
            {
                MMLog.WriteWarning("[ScenarioLoadingTransitionGuard] Immediate loading-screen teardown failed; using vanilla hide. " + ex.Message);
                loadingScreen.HideLoadingScreen(true);
            }
        }

        private static void ClearDirectLaunchOwnership()
        {
            _ownedDirectLaunchScene = null;
            _ownedDirectLaunchTarget = null;
        }

        private static void ClearManagedLoadingOwnership()
        {
            _ownedManagedLoadingScene = null;
            _ownedManagedLoadingTarget = null;
            _managedHideSuppressionLogged = false;
        }
    }
}
