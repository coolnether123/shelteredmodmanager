using HarmonyLib;
using ModAPI.Core;
using UnityEngine;

namespace ModAPI.Hooks
{
    /// <summary>
    /// Tracks if we're in a "Save and Exit" flow (returning to menu, NOT quitting app).
    /// </summary>
    public static class SaveExitState
    {
        public static bool IsSaveExiting = false;
        private static float _saveCompleteTime = 0f;
        private static bool _forceTransitionTriggered = false;

        public static void OnSaveComplete()
        {
            _saveCompleteTime = UnityEngine.Time.realtimeSinceStartup;
            _forceTransitionTriggered = false;
        }

        public static void Reset()
        {
            IsSaveExiting = false;
            _saveCompleteTime = 0f;
            _forceTransitionTriggered = false;
        }

        public static bool ShouldForceTransition()
        {
            if (_forceTransitionTriggered) return false;
            if (_saveCompleteTime == 0f) return false;
            
            float elapsed = UnityEngine.Time.realtimeSinceStartup - _saveCompleteTime;
            if (elapsed > 1.0f) // 1 second timeout
            {
                _forceTransitionTriggered = true;
                return true;
            }
            return false;
        }
    }

    [HarmonyPatch(typeof(MainMenuPanel), "OnMessageBoxClosed")]
    public static class MainMenuPanel_OnMessageBoxClosed_Patch
    {
        public static void Prefix(int response)
        {
            MMLog.WriteDebug($"[MainMenuPanel] OnMessageBoxClosed called with response: {response}");
            // Response 1 is "Yes" (Save and Exit)
            if (response == 1)
            {
                MMLog.WriteDebug("[MainMenuPanel] 'Save and Exit' confirmed.");
                
                // SAFETY: Pre-calculate mod data now, while the scene is healthy.
                // This ensures we don't access destroyed Unity objects during the scene transition.
                MMLog.WriteDebug("[MainMenuPanel] Starting PrecalculateShutdownData...");
                SaveSystemImpl.PrecalculateShutdownData();
                MMLog.WriteDebug("[MainMenuPanel] PrecalculateShutdownData finished.");
                
                SaveExitState.IsSaveExiting = true;
                MMLog.WriteDebug("[MainMenuPanel] SaveExitState.IsSaveExiting set to true.");
            }
        }
    }

    /// <summary>
    /// Verbose logging for MainMenuPanel.Update during save-exit flow.
    /// Forces scene transition if vanilla fails to do it.
    /// </summary>
    [HarmonyPatch(typeof(MainMenuPanel), "Update")]
    public static class MainMenuPanel_Update_Patch
    {
        public static void Prefix(MainMenuPanel __instance)
        {
            if (SaveExitState.IsSaveExiting)
            {
                bool saveComplete = SaveManager.instance == null || !SaveManager.instance.isSaving;
                bool wasSaveError = SaveManager.instance != null && SaveManager.instance.wasSaveError;

                if (saveComplete && !wasSaveError)
                {
                    // Check if vanilla is taking too long to transition
                    if (SaveExitState.ShouldForceTransition())
                    {
                        MMLog.WriteDebug("[MainMenuPanel.Update] TIMEOUT! Vanilla failed to show loading screen. Forcing scene transition.");
                        try
                        {
                            // Reset quitting flag so main menu can load properly
                            PluginRunner.IsQuitting = false;
                            SaveExitState.Reset();
                            
                            // Force load menu scene
                            UnityEngine.SceneManagement.SceneManager.LoadScene("MenuScene");
                            MMLog.WriteDebug("[MainMenuPanel.Update] Forced scene load initiated.");
                        }
                        catch (System.Exception ex)
                        {
                            MMLog.WriteDebug($"[MainMenuPanel.Update] ERROR forcing scene transition: {ex.Message}");
                        }
                    }
                }
            }
        }

        public static void Postfix(MainMenuPanel __instance)
        {
            if (SaveExitState.IsSaveExiting)
            {
                bool saveComplete = SaveManager.instance == null || !SaveManager.instance.isSaving;
                if (saveComplete)
                {
                    // Mark when save completed for timeout tracking
                    SaveExitState.OnSaveComplete();
                    MMLog.WriteDebug("[MainMenuPanel.Update] Save complete. Waiting for vanilla to show loading screen...");
                }
            }
        }
    }

    /// <summary>
    /// Verbose logging for SaveManager.Update to track state machine progression during saves.
    /// Uses counters to prevent log spam.
    /// </summary>
    [HarmonyPatch(typeof(SaveManager), "Update")]
    /// <summary>
    /// Verbose logging for SaveManager.Update to track state machine progression during saves.
    /// Uses counters to prevent log spam.
    /// BLOCCKS execution if PluginRunner.IsQuitting is true to prevent crashes during scene unload.
    /// </summary>
    [HarmonyPatch(typeof(SaveManager), "Update")]
    public static class SaveManager_Update_Verbose_Patch
    {
        private static int _updateCount = 0;
        private static int _loggedCount = 0;

        public static bool Prefix(SaveManager __instance)
        {
            if (PluginRunner.IsQuitting)
            {
                // CRITICAL: Stop SaveManager from running during scene unload/quit.
                // It often tries to access destroyed objects, causing crashes.
                return false; 
            }

            if (SaveExitState.IsSaveExiting)
            {
                _updateCount++;
                
                // Log every 50 updates to avoid spam
                if (_updateCount - _loggedCount >= 50)
                {
                    bool isSaving = __instance.isSaving;
                    bool isLoading = __instance.isLoading;
                    MMLog.WriteDebug($"[SaveManager.Update] Updates: {_updateCount} (isSaving={isSaving}, isLoading={isLoading})");
                    _loggedCount = _updateCount;
                }
            }
            return true;
        }

        public static void Postfix()
        {
            // Reset counter when save-exit completes
            if (!SaveExitState.IsSaveExiting && _updateCount > 0)
            {
                MMLog.WriteDebug($"[SaveManager.Update] Total updates: {_updateCount}");
                _updateCount = 0;
                _loggedCount = 0;
            }
        }
    }

    /// <summary>
    /// Verbose logging for SaveManager.SetState to track state transitions.
    /// </summary>
    [HarmonyPatch(typeof(SaveManager), "SetState")]
    public static class SaveManager_SetState_Verbose_Patch
    {
        public static void Prefix(object state)
        {
            if (SaveExitState.IsSaveExiting || PluginRunner.IsQuitting)
            {
                MMLog.WriteDebug($"[SaveManager.SetState] Transitioning to state: {state}");
            }
        }
    }

    // ==================== POST-SAVE FLOW LOGGING ====================

    /// <summary>
    /// Log when PauseManager.Resume is called (happens after save completes).
    /// </summary>
    [HarmonyPatch(typeof(PauseManager), "Resume")]
    public static class PauseManager_Resume_Verbose_Patch
    {
        public static void Prefix()
        {
            if (SaveExitState.IsSaveExiting)
            {
                MMLog.WriteDebug("[PauseManager.Resume] Called during save-exit flow.");
            }
        }

        public static void Postfix()
        {
            if (SaveExitState.IsSaveExiting)
            {
                MMLog.WriteDebug("[PauseManager.Resume] Completed.");
            }
        }
    }

    /// <summary>
    /// CRITICAL: Set IsQuitting when scene transition starts.
    /// This enables mod safety checks during scene unload.
    /// </summary>
    [HarmonyPatch(typeof(LoadingScreen), "ShowLoadingScreen")]
    public static class LoadingScreen_ShowLoadingScreen_Patch
    {
        public static void Prefix(string levelToLoad)
        {
            if (SaveExitState.IsSaveExiting && levelToLoad == "MenuScene")
            {
                MMLog.WriteDebug($"[LoadingScreen.ShowLoadingScreen] Scene transition to '{levelToLoad}' starting.");
                MMLog.WriteDebug($"[LoadingScreen.ShowLoadingScreen] Setting IsQuitting=true for mod safety during scene unload.");
                PluginRunner.IsQuitting = true;
                SaveExitState.Reset();
            }
        }
    }

    /// <summary>
    /// Log when LeaderboardMan.PostScores is called (optional, happens in survival mode).
    /// </summary>
    [HarmonyPatch(typeof(LeaderboardMan), "PostScores")]
    public static class LeaderboardMan_PostScores_Verbose_Patch
    {
        public static void Prefix()
        {
            if (SaveExitState.IsSaveExiting)
            {
                MMLog.WriteDebug("[LeaderboardMan.PostScores] Called during save-exit flow.");
            }
        }

        public static void Postfix()
        {
            if (SaveExitState.IsSaveExiting)
            {
                MMLog.WriteDebug("[LeaderboardMan.PostScores] Completed.");
            }
        }
    }

    /// <summary>
    /// Log when AchievementManager.SubmitStats is called (happens in SaveToCurrentSlot).
    /// </summary>
    [HarmonyPatch(typeof(AchievementManager), "SubmitStats")]
    public static class AchievementManager_SubmitStats_Verbose_Patch
    {
        public static void Prefix()
        {
            if (SaveExitState.IsSaveExiting)
            {
                MMLog.WriteDebug("[AchievementManager.SubmitStats] Called during save-exit flow.");
            }
        }

        public static void Postfix()
        {
            if (SaveExitState.IsSaveExiting)
            {
                MMLog.WriteDebug("[AchievementManager.SubmitStats] Completed.");
            }
        }
    }
}
