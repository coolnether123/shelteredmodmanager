using UnityEngine;

namespace ShelteredAPI.Scenarios
{
    public static class ScenarioWorldReady
    {
        public static bool IsReady()
        {
            if (SaveManager.instance != null && SaveManager.instance.isLoading)
                return false;
            if (CutsceneManager.Instance != null && CutsceneManager.Instance.CutSceneActive)
                return false;
            if (QuestManager.instance == null || QuestLibrary.instance == null)
                return false;
            if (ExpeditionMap.Instance == null || !ExpeditionMap.Instance.initialised)
                return false;
            if (ShelterRoomGrid.Instance == null || !ShelterRoomGrid.Instance.isInitialized)
                return false;
            if (FamilySpawner.instance == null)
                return false;

            return true;
        }
    }
}
