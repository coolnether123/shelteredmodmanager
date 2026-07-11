using System;
using HarmonyLib;
using ModAPI.Core;
using ShelteredAPI.Scenarios.Application.Authoring;
using ShelteredAPI.Scenarios.Application.Runtime;
using UnityEngine;

namespace ShelteredAPI.Scenarios.Infrastructure.Runtime
{
    internal interface IScenarioEndGamePresentationTarget
    {
        bool TryPresent(bool success, out string reason);
    }

    internal sealed class ScenarioAuthoringSessionContext : IScenarioAuthoringSessionContext
    {
        public bool HasActiveSession
        {
            get
            {
                ScenarioEditorController editor = ScenarioEditorController.Instance;
                return editor != null && editor.CurrentSession != null;
            }
        }
    }

    internal sealed class ScenarioEndGamePresenter : IScenarioEndGamePresenter
    {
        private readonly IScenarioAuthoringSessionContext _context;
        private readonly IScenarioEndGamePresentationTarget _playtestPresenter;
        private readonly IScenarioEndGamePresentationTarget _installedPresenter;

        public ScenarioEndGamePresenter(
            IScenarioAuthoringSessionContext context,
            IScenarioEndGamePresentationTarget playtestPresenter,
            IScenarioEndGamePresentationTarget installedPresenter)
        {
            _context = context;
            _playtestPresenter = playtestPresenter;
            _installedPresenter = installedPresenter;
        }

        public bool TryPresent(bool success, out string reason)
        {
            return _context != null && _context.HasActiveSession
                ? _playtestPresenter.TryPresent(success, out reason)
                : _installedPresenter.TryPresent(success, out reason);
        }
    }

    internal sealed class ScenarioPlaytestEndGamePresenter : IScenarioEndGamePresentationTarget
    {
        public bool TryPresent(bool success, out string reason)
        {
            try
            {
                ScenarioEditorController editor = ScenarioEditorController.Instance;
                if (editor == null || editor.CurrentSession == null)
                {
                    reason = "No active authoring session was available for the playtest ending.";
                    return false;
                }

                editor.EndPlaytest();
                reason = null;
                return true;
            }
            catch (Exception ex)
            {
                reason = "Authoring return could not be completed: " + ex.Message;
                return false;
            }
        }
    }

    internal sealed class ScenarioInstalledEndGamePresenter : IScenarioEndGamePresentationTarget
    {
        public bool TryPresent(bool success, out string reason)
        {
            try
            {
                FamilyManager family = FamilyManager.Instance;
                GameModeManager mode = GameModeManager.instance;
                if (family == null || mode == null)
                {
                    reason = "Vanilla end-game managers are not ready (FamilyManager="
                        + (family != null ? "ready" : "missing") + ", GameModeManager="
                        + (mode != null ? "ready" : "missing") + ").";
                    return false;
                }

                GameModeManager.ModeResult result = success
                    ? GameModeManager.ModeResult.Success
                    : GameModeManager.ModeResult.Failure;
                mode.UpdateModeResult(result);

                // Use the vanilla latch and OnGameOver path instead of pushing a panel
                // directly. This preserves score/death finalization, audio, camera/input
                // locking, tooltip cleanup, and fade sequencing. Stasis/Surrounded may
                // derive their own result inside OnGameOver, so restore the authored
                // scenario result afterwards before UpdateManager presents the panel.
                if (!family.isGameOver)
                {
                    Traverse familyTraverse = Traverse.Create(family);
                    Traverse gameOverField = familyTraverse.Field("game_over");
                    Traverse onGameOver = familyTraverse.Method("OnGameOver");
                    if (!gameOverField.FieldExists() || !onGameOver.MethodExists())
                    {
                        reason = "FamilyManager vanilla game-over latch members were not available.";
                        return false;
                    }
                    gameOverField.SetValue(true);
                    onGameOver.GetValue();
                }
                mode.UpdateModeResult(result);

                // Repair only an inconsistent unowned zero timescale. A real vanilla
                // pause remains intact; the game-over panel owns its normal pause/input
                // state once FamilyManager.UpdateManager pushes it.
                if (Time.timeScale == 0f && !PauseManager.isPaused)
                    Time.timeScale = 1f;

                reason = null;
                MMLog.WriteInfo("[ScenarioWinLoss] Armed vanilla FamilyManager game-over flow with result " + result + ".");
                return true;
            }
            catch (Exception ex)
            {
                reason = "Vanilla end-game flow could not be armed: " + ex.Message;
                return false;
            }
        }
    }
}
