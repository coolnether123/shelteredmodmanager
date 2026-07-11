using ModAPI.Core;
using System;
using UnityEngine.SceneManagement;

namespace ShelteredAPI.Scenarios.Infrastructure.Unity
{
    internal static class ScenarioLoadingTransitionGuard
    {
        public static void PrepareForManagedTransition(string targetLabel)
        {
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

        public static bool TryCompleteManagedTransition(string expectedSceneName, string targetLabel)
        {
            if (LoadingScreen.Instance == null || !LoadingScreen.Instance.isShowing)
                return false;

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

            LoadingScreen.Instance.HideLoadingScreen();
            MMLog.WriteInfo("[ScenarioLoadingTransitionGuard] Completed managed shelter transition. target="
                + (targetLabel ?? "<unknown>") + " scene=" + activeSceneName + ".");
            return true;
        }
    }
}
