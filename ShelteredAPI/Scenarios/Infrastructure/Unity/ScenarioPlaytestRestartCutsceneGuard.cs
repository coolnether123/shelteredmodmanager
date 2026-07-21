using System;
using ModAPI.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShelteredAPI.Scenarios.Infrastructure.Unity
{
    internal static class ScenarioPlaytestRestartCutsceneGuard
    {
        public static bool TryClearBlockingIntroCutscene(string draftId, out string blockingReason)
        {
            blockingReason = null;

            CutsceneManager manager = CutsceneManager.Instance;
            if (manager == null || !manager.CutSceneActive)
                return true;

            Scene activeScene = SceneManager.GetActiveScene();
            string sceneName = activeScene.IsValid() ? activeScene.name : "<invalid>";
            if (!IsIntroCutsceneScene(sceneName))
            {
                blockingReason = "Playtest restart is blocked by an active cutscene in '" + sceneName + "'.";
                return false;
            }

            Cutscene activeCutscene = manager.GetActiveCutscene;
            if (activeCutscene == null)
            {
                blockingReason = "Playtest restart is blocked by a cutscene with no active cutscene instance.";
                return false;
            }

            if (!activeCutscene.IsIntro)
            {
                blockingReason = "Playtest restart is blocked by a non-intro cutscene in '" + sceneName + "'.";
                return false;
            }

            try
            {
                manager.pauseCutsceneManager = false;
                activeCutscene.SkipCutscene();
                manager.DeactivateCutscene();

                if (manager.CutSceneActive)
                {
                    blockingReason = "The active intro cutscene could not be deactivated.";
                    return false;
                }

                MMLog.WriteInfo("[ScenarioPlaytestRestartCutsceneGuard] Cleared active intro cutscene before playtest restart. draftId="
                    + (draftId ?? "<none>") + ", scene=" + sceneName + ".");
                return true;
            }
            catch (Exception ex)
            {
                blockingReason = "The active intro cutscene could not be cleared: " + ex.Message;
                MMLog.WriteWarning("[ScenarioPlaytestRestartCutsceneGuard] Failed to clear active intro cutscene before playtest restart. draftId="
                    + (draftId ?? "<none>") + ", scene=" + sceneName + ", error=" + ex + ".");
                return false;
            }
        }

        private static bool IsIntroCutsceneScene(string sceneName)
        {
            return string.Equals(sceneName, "ShelterScene_Stasis", StringComparison.Ordinal)
                || string.Equals(sceneName, "ShelterScene_Surrounded", StringComparison.Ordinal);
        }
    }
}
