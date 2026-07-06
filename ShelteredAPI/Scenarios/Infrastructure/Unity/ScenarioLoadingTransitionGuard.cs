using ModAPI.Core;

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
    }
}
