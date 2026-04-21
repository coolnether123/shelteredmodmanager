using ModAPI.Core;
using UnityEngine;

namespace ShelteredAPI.Scenarios
{
    internal sealed class ScenarioAuthoringPauseService
    {
        private static readonly ScenarioAuthoringPauseService _instance = new ScenarioAuthoringPauseService();
        private bool _ownsPause;
        private string _lastOwnerReason;

        public static ScenarioAuthoringPauseService Instance
        {
            get { return _instance; }
        }

        public bool OwnsPause
        {
            get { return _ownsPause; }
        }

        private ScenarioAuthoringPauseService()
        {
        }

        public bool EnsurePaused(string reason)
        {
            PauseManager pauseManager = ResolvePauseManager();
            if (pauseManager == null)
                return false;

            if (_ownsPause)
            {
                if (Time.timeScale != 0f)
                {
                    Time.timeScale = 0f;
                    MMLog.WriteInfo("[ScenarioAuthoringPause] Restored frozen simulation while authoring remained active.");
                }

                return true;
            }

            BasePanel originalPauseMenu = pauseManager.pauseMenuPanel;
            try
            {
                pauseManager.pauseMenuPanel = null;
                PauseManager.Pause();
            }
            finally
            {
                pauseManager.pauseMenuPanel = originalPauseMenu;
            }

            _ownsPause = PauseManager.isPaused;
            _lastOwnerReason = reason;
            if (_ownsPause)
            {
                MMLog.WriteInfo("[ScenarioAuthoringPause] Authoring pause engaged without opening the vanilla pause menu. Reason="
                    + (reason ?? "unspecified") + ".");
            }
            else
            {
                MMLog.WriteWarning("[ScenarioAuthoringPause] Failed to engage authoring pause. Reason="
                    + (reason ?? "unspecified") + ".");
            }

            return _ownsPause;
        }

        public void ReleasePause(string reason)
        {
            if (!_ownsPause)
                return;

            PauseManager pauseManager = ResolvePauseManager();
            if (pauseManager == null)
            {
                _ownsPause = false;
                _lastOwnerReason = null;
                return;
            }

            BasePanel originalPauseMenu = pauseManager.pauseMenuPanel;
            try
            {
                pauseManager.pauseMenuPanel = null;
                PauseManager.Resume();
            }
            finally
            {
                pauseManager.pauseMenuPanel = originalPauseMenu;
            }

            // We called Resume() so we no longer own this pause regardless of whether
            // another system re-paused concurrently. Reading isPaused here would incorrectly
            // keep _ownsPause=true and prevent a clean future EnsurePaused engagement.
            MMLog.WriteInfo("[ScenarioAuthoringPause] Authoring pause released. Reason="
                + (reason ?? "unspecified") + ", previousReason=" + (_lastOwnerReason ?? "unspecified")
                + ", remainingPauseState=" + PauseManager.isPaused + ".");
            _ownsPause = false;
            _lastOwnerReason = null;
        }

        private static PauseManager ResolvePauseManager()
        {
            PauseManager manager = UnityEngine.Object.FindObjectOfType<PauseManager>();
            if (manager == null)
                MMLog.WarnOnce("ScenarioAuthoringPause.ResolvePauseManager", "PauseManager was not available when authoring pause was requested.");
            return manager;
        }
    }
}
