using ModAPI.Core;
using System;
using UnityEngine.SceneManagement;

namespace ShelteredAPI.Scenarios.Infrastructure.Unity
{
    internal static class ScenarioLoadingTransitionGuard
    {
        private static string _ownedDirectLaunchScene;
        private static string _ownedDirectLaunchTarget;

        public static void PrepareForManagedTransition(string targetLabel)
        {
            ClearDirectLaunchOwnership();

            if (LoadingScreen.Instance != null)
            {
                if (LoadingScreen.Instance.isShowing)
                {
                    LoadingScreen.Instance.HideLoadingScreen(true);
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

            bool completed = false;
            if (LoadingScreen.Instance != null && LoadingScreen.Instance.isShowing)
            {
                LoadingScreen.Instance.HideLoadingScreen();
                MMLog.WriteInfo("[ScenarioLoadingTransitionGuard] Completed managed shelter loading screen. target="
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

        private static void ClearDirectLaunchOwnership()
        {
            _ownedDirectLaunchScene = null;
            _ownedDirectLaunchTarget = null;
        }
    }
}
