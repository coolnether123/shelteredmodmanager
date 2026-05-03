using ModAPI.Core;
using System;
using System.Reflection;
using UnityEngine;

namespace ShelteredAPI.Scenarios
{
    internal sealed class ScenarioAuthoringPauseService : IScenarioPauseService
    {
        private static readonly FieldInfo PauseCountField = typeof(PauseManager).GetField("m_pauseCount", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo TimePausedField = typeof(PauseManager).GetField("m_timePaused", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo PreviousTimescaleField = typeof(PauseManager).GetField("m_previousTimescale", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo OnPauseField = typeof(PauseManager).GetField("OnPause", BindingFlags.Static | BindingFlags.NonPublic);
        private static readonly FieldInfo OnResumeField = typeof(PauseManager).GetField("OnResume", BindingFlags.Static | BindingFlags.NonPublic);
        private bool _ownsPause;
        private bool _ownsInjectedPauseDepth;
        private float _authoringPauseStartedAt;
        private string _lastOwnerReason;

        public static ScenarioAuthoringPauseService Instance
        {
            get { return ScenarioCompositionRoot.Resolve<ScenarioAuthoringPauseService>(); }
        }

        public bool OwnsPause
        {
            get { return _ownsPause; }
        }

        internal ScenarioAuthoringPauseService()
        {
        }

        public bool EnsurePaused(string reason)
        {
            PauseManager pauseManager = ResolvePauseManager();
            if (pauseManager == null)
                return false;

            SuppressPauseMenu(pauseManager, "EnsurePaused");
            if (_ownsPause)
            {
                return MaintainOwnedPause(pauseManager);
            }

            _authoringPauseStartedAt = RealTime.time;
            _lastOwnerReason = reason;
            _ownsInjectedPauseDepth = !PauseManager.isPaused;
            if (_ownsInjectedPauseDepth)
            {
                if (!TryEnterAuthoringPause(pauseManager))
                {
                    _ownsInjectedPauseDepth = false;
                    _authoringPauseStartedAt = 0f;
                    _lastOwnerReason = null;
                    MMLog.WriteWarning("[ScenarioAuthoringPause] Failed to engage authoring pause. Reason="
                        + (reason ?? "unspecified") + ".");
                    return false;
                }
            }
            else if (Time.timeScale != 0f)
            {
                Time.timeScale = 0f;
            }

            _ownsPause = true;
            MMLog.WriteInfo("[ScenarioAuthoringPause] Authoring pause engaged without opening the vanilla pause menu. Reason="
                + (reason ?? "unspecified") + ", injectedPauseDepth=" + _ownsInjectedPauseDepth + ".");
            return true;
        }

        public void ReleasePause(string reason)
        {
            if (!_ownsPause)
                return;

            PauseManager pauseManager = ResolvePauseManager();
            if (pauseManager == null)
            {
                _ownsPause = false;
                _ownsInjectedPauseDepth = false;
                _authoringPauseStartedAt = 0f;
                _lastOwnerReason = null;
                return;
            }

            SuppressPauseMenu(pauseManager, "ReleasePause");
            bool remainingPauseState = PauseManager.isPaused;
            if (_ownsInjectedPauseDepth)
            {
                remainingPauseState = TryExitAuthoringPause(pauseManager);
            }

            MMLog.WriteInfo("[ScenarioAuthoringPause] Authoring pause released. Reason="
                + (reason ?? "unspecified") + ", previousReason=" + (_lastOwnerReason ?? "unspecified")
                + ", remainingPauseState=" + remainingPauseState + ".");
            _ownsPause = false;
            _ownsInjectedPauseDepth = false;
            _authoringPauseStartedAt = 0f;
            _lastOwnerReason = null;
        }

        public bool ShouldSuppressPauseMenu()
        {
            return _ownsPause && ScenarioAuthoringRuntimeGuards.ShouldMaintainPausedSimulation();
        }

        public bool IsPauseMenuPanel(BasePanel panel)
        {
            PauseManager pauseManager = ResolvePauseManager();
            return pauseManager != null && panel != null && ReferenceEquals(panel, pauseManager.pauseMenuPanel);
        }

        private static PauseManager ResolvePauseManager()
        {
            PauseManager manager = UnityEngine.Object.FindObjectOfType<PauseManager>();
            if (manager == null)
                MMLog.WarnOnce("ScenarioAuthoringPause.ResolvePauseManager", "PauseManager was not available when authoring pause was requested.");
            return manager;
        }

        private bool MaintainOwnedPause(PauseManager pauseManager)
        {
            SuppressPauseMenu(pauseManager, "MaintainOwnedPause");
            if (!PauseManager.isPaused && _ownsInjectedPauseDepth)
            {
                if (!TryEnterAuthoringPause(pauseManager))
                    return false;
            }

            if (Time.timeScale != 0f)
            {
                Time.timeScale = 0f;
                MMLog.WriteInfo("[ScenarioAuthoringPause] Restored frozen simulation while authoring remained active.");
            }

            return true;
        }

        private static bool TryEnterAuthoringPause(PauseManager pauseManager)
        {
            if (pauseManager == null || PauseCountField == null || TimePausedField == null || PreviousTimescaleField == null)
                return false;

            int pauseCount = GetPauseCount(pauseManager);
            if (pauseCount == 0)
                PreviousTimescaleField.SetValue(pauseManager, Time.timeScale);

            PauseCountField.SetValue(pauseManager, pauseCount + 1);
            TimePausedField.SetValue(pauseManager, RealTime.time);
            Time.timeScale = 0f;

            PauseManager.PauseEvent onPause = OnPauseField != null
                ? OnPauseField.GetValue(null) as PauseManager.PauseEvent
                : null;
            if (onPause != null)
                onPause();

            return PauseManager.isPaused;
        }

        private bool TryExitAuthoringPause(PauseManager pauseManager)
        {
            if (pauseManager == null || PauseCountField == null || TimePausedField == null || PreviousTimescaleField == null)
                return PauseManager.isPaused;

            int pauseCount = Math.Max(0, GetPauseCount(pauseManager) - 1);
            PauseCountField.SetValue(pauseManager, pauseCount);
            if (pauseCount > 0)
            {
                Time.timeScale = 0f;
                return true;
            }

            float previousTimescale = GetPreviousTimescale(pauseManager);
            Time.timeScale = previousTimescale;
            PreviousTimescaleField.SetValue(pauseManager, 1f);

            PauseManager.ResumeEvent onResume = OnResumeField != null
                ? OnResumeField.GetValue(null) as PauseManager.ResumeEvent
                : null;
            if (onResume != null)
            {
                float timePaused = RealTime.time - GetPausedAt(pauseManager);
                onResume(timePaused);
            }

            TimePausedField.SetValue(pauseManager, 0f);
            return PauseManager.isPaused;
        }

        private static void SuppressPauseMenu(PauseManager pauseManager, string context)
        {
            if (pauseManager == null)
                return;

            UIPanelManager panelManager = UIPanelManager.instance;
            BasePanel pauseMenu = pauseManager.pauseMenuPanel;
            if (pauseMenu != null && panelManager != null && panelManager.IsPanelOnStack(pauseMenu))
            {
                panelManager.PopPanel(pauseMenu);
                MMLog.WriteInfo("[ScenarioAuthoringPause] Removed vanilla pause menu panel during authoring. Context=" + context + ".");
            }

            if (pauseMenu != null && pauseMenu.IsShowing())
                pauseMenu.gameObject.SetActive(false);
        }

        private static int GetPauseCount(PauseManager pauseManager)
        {
            return PauseCountField != null
                ? (int)PauseCountField.GetValue(pauseManager)
                : 0;
        }

        private static float GetPausedAt(PauseManager pauseManager)
        {
            return TimePausedField != null
                ? Convert.ToSingle(TimePausedField.GetValue(pauseManager))
                : RealTime.time;
        }

        private static float GetPreviousTimescale(PauseManager pauseManager)
        {
            return PreviousTimescaleField != null
                ? Convert.ToSingle(PreviousTimescaleField.GetValue(pauseManager))
                : 1f;
        }
    }
}
