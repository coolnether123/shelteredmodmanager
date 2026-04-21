using UnityEngine.SceneManagement;
using UnityEngine;

namespace ShelteredAPI.Scenarios
{
    public static class ScenarioWorldReady
    {
        public static bool IsReady()
        {
            string reason;
            return Evaluate(out reason);
        }

        public static bool Evaluate(out string blockingReason)
        {
            Scene activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid())
            {
                blockingReason = "The active scene is not valid yet.";
                return false;
            }

            if (!IsShelterSceneName(activeScene.name))
            {
                blockingReason = "Authoring requires a shelter scene but the active scene is '" + activeScene.name + "'.";
                return false;
            }

            if (SaveManager.instance != null && SaveManager.instance.isLoading)
            {
                blockingReason = "SaveManager is still loading the world state.";
                return false;
            }

            if (LoadingScreen.Instance != null && LoadingScreen.Instance.isShowing)
            {
                blockingReason = "LoadingScreen is still visible.";
                return false;
            }

            if (CutsceneManager.Instance != null && CutsceneManager.Instance.CutSceneActive)
            {
                blockingReason = "A cutscene is still active.";
                return false;
            }

            if (RelocationManager.instance != null && RelocationManager.instance.isTransitioning)
            {
                blockingReason = "Relocation is still transitioning.";
                return false;
            }

            if (QuestManager.instance == null)
            {
                blockingReason = "QuestManager is not initialized yet.";
                return false;
            }

            if (QuestLibrary.instance == null)
            {
                blockingReason = "QuestLibrary is not initialized yet.";
                return false;
            }

            if (ExpeditionMap.Instance == null)
            {
                blockingReason = "ExpeditionMap instance is missing.";
                return false;
            }

            if (!ExpeditionMap.Instance.initialised)
            {
                blockingReason = "ExpeditionMap is not initialized yet.";
                return false;
            }

            if (ShelterRoomGrid.Instance == null)
            {
                blockingReason = "ShelterRoomGrid instance is missing.";
                return false;
            }

            if (!ShelterRoomGrid.Instance.isInitialized)
            {
                blockingReason = "ShelterRoomGrid is not initialized yet.";
                return false;
            }

            if (FamilySpawner.instance == null)
            {
                blockingReason = "FamilySpawner is not initialized yet.";
                return false;
            }

            if (InteractionManager.Instance == null)
            {
                blockingReason = "InteractionManager is not initialized yet.";
                return false;
            }

            if (InventoryManager.Instance == null)
            {
                blockingReason = "InventoryManager is not initialized yet.";
                return false;
            }

            if (ObjectManager.Instance == null)
            {
                blockingReason = "ObjectManager is not initialized yet.";
                return false;
            }

            if (UIPanelManager.instance == null)
            {
                blockingReason = "UIPanelManager is not initialized yet.";
                return false;
            }

            blockingReason = null;
            return true;
        }

        public static bool IsShelterSceneActive()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            return activeScene.IsValid() && IsShelterSceneName(activeScene.name);
        }

        private static bool IsShelterSceneName(string sceneName)
        {
            return sceneName == "ShelterScene"
                || sceneName == "ShelterScene_Surrounded"
                || sceneName == "ShelterScene_Stasis";
        }
    }
}
