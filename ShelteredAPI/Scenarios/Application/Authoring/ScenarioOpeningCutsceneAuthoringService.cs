using System;
using System.Collections.Generic;
using System.Reflection;

using ModAPI.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

using ShelteredAPI.Scenarios.Infrastructure.Unity;
using ShelteredAPI.Scenarios.Definitions;

namespace ShelteredAPI.Scenarios.Application.Authoring
{
    internal sealed class ScenarioOpeningCutsceneAuthoringService
    {
        private static readonly BindingFlags InstancePrivate = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly BindingFlags InstanceAny = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static PreviewContext _activePreview;

        public static bool IsPreviewActive
        {
            get { return _activePreview != null; }
        }

        public static void UpdateActivePreview()
        {
            PreviewContext preview = _activePreview;
            if (preview == null)
                return;

            CutsceneManager manager = preview.Manager;
            if (manager == null || preview.State == null || !preview.State.IsActive)
            {
                RestorePreview(preview, "Opening cutscene preview stopped because authoring is no longer active.");
                return;
            }

            Cutscene active = manager.GetActiveCutscene;
            if (manager.CutSceneActive && active != null && active.IsIntro && !active.IsFinished)
                return;

            if (manager.CutSceneActive && active != null && ReferenceEquals(active, preview.Cutscene))
                return;

            RestorePreview(preview, "Opening cutscene preview finished; editor restored.");
        }

        public bool TryWatchOpeningCutscene(ScenarioEditorSession session, ScenarioAuthoringState state, out string message)
        {
            message = null;
            ScenarioDefinition definition = session != null ? session.WorkingDefinition : null;
            if (definition == null)
            {
                message = "No active scenario draft is available.";
                return true;
            }

            CutsceneManager manager = CutsceneManager.Instance;
            if (manager == null)
            {
                message = BuildNoCutsceneManagerMessage(definition);
                return true;
            }

            Cutscene active = manager.GetActiveCutscene;
            if (manager.CutSceneActive)
            {
                if (active != null && active.IsIntro)
                {
                    BeginPreview(state, manager, active);
                    message = "Playing " + ScenarioAuthoringBaseModeReloadService.FormatBaseMode(definition.BaseGameMode) + " opening cutscene.";
                }
                else
                {
                    message = "Opening cutscene is unavailable while another cutscene is active.";
                }

                return true;
            }

            Cutscene intro = FindIntroCutscene(manager);
            if (intro == null)
            {
                message = "Opening cutscene is unavailable because this backend scene does not expose an intro cutscene.";
                return true;
            }

            try
            {
                BeginPreview(state, manager, intro);
                ResetCutsceneForReplay(intro);
                manager.pauseCutsceneManager = false;
                bool started = intro.CheckEntryCondition();
                if (!started)
                {
                    manager.PlayCutscene(intro);
                    started = intro.CheckEntryCondition();
                }

                if (started || manager.CutSceneActive)
                {
                    message = "Playing " + ScenarioAuthoringBaseModeReloadService.FormatBaseMode(definition.BaseGameMode) + " opening cutscene.";
                    return true;
                }

                RestorePreview(_activePreview, null);
                message = BuildStartFailureMessage();
                return true;
            }
            catch (Exception ex)
            {
                RestorePreview(_activePreview, null);
                message = "Opening cutscene could not start: " + ex.Message;
                MMLog.WriteWarning("[ScenarioOpeningCutsceneAuthoring] Failed to play opening cutscene: " + ex + ".");
                return true;
            }
        }

        private static Cutscene FindIntroCutscene(CutsceneManager manager)
        {
            FieldInfo field = typeof(CutsceneManager).GetField("cutscenes", InstancePrivate);
            List<Cutscene> cutscenes = field != null ? field.GetValue(manager) as List<Cutscene> : null;
            for (int i = 0; cutscenes != null && i < cutscenes.Count; i++)
            {
                Cutscene cutscene = cutscenes[i];
                if (cutscene != null && cutscene.IsIntro)
                    return cutscene;
            }

            return null;
        }

        private static void BeginPreview(ScenarioAuthoringState state, CutsceneManager manager, Cutscene cutscene)
        {
            if (_activePreview != null)
                RestorePreview(_activePreview, null);

            bool previousShellVisible = state != null && state.ShellVisible;
            if (state != null)
            {
                state.ShellVisible = false;
                state.WindowMenuOpen = false;
                state.SettingsWindowOpen = false;
                state.HelpWindowOpen = false;
                state.FocusedEditorKind = null;
                state.StatusMessage = "Playing opening cutscene. Press Escape to skip and return to the editor.";
            }

            _activePreview = new PreviewContext(state, manager, cutscene, previousShellVisible);
            ScenarioAuthoringPauseService.Instance.ReleasePause("Opening cutscene preview started.");
            if (Time.timeScale == 0f)
                Time.timeScale = 1f;

            manager.pauseCutsceneManager = false;
            MMLog.WriteInfo("[ScenarioOpeningCutsceneAuthoring] Opening cutscene preview started. scene="
                + SceneManager.GetActiveScene().name + ", cutscene=" + (cutscene != null ? cutscene.name : "<none>") + ".");
        }

        private static void RestorePreview(PreviewContext preview, string statusMessage)
        {
            if (preview == null)
                return;

            if (ReferenceEquals(_activePreview, preview))
                _activePreview = null;

            if (preview.State != null && preview.State.IsActive)
            {
                preview.State.ShellVisible = preview.PreviousShellVisible;
                if (!string.IsNullOrEmpty(statusMessage))
                    preview.State.StatusMessage = statusMessage;
            }

            ScenarioAuthoringPauseService.Instance.EnsurePaused("Opening cutscene preview finished.");
            MMLog.WriteInfo("[ScenarioOpeningCutsceneAuthoring] Opening cutscene preview restored authoring pause. scene="
                + SceneManager.GetActiveScene().name + ".");
        }

        private static void ResetCutsceneForReplay(Cutscene cutscene)
        {
            SetBoolField(cutscene, "finished", false);
            SetBoolField(cutscene, "isActive", false);
            SetIntField(cutscene, "stageNumber", 0);
            cutscene.cutsceneWaiting = true;
        }

        private static string BuildNoCutsceneManagerMessage(ScenarioDefinition definition)
        {
            if (definition != null && definition.BaseGameMode == ScenarioBaseGameMode.Survival)
                return "Standard mode has no vanilla opening cutscene asset to replay; Sheltered starts Standard games directly after family setup.";

            return "Opening cutscene is unavailable because this backend scene has not created CutsceneManager yet.";
        }

        private static string BuildStartFailureMessage()
        {
            string inputState = UIPanelManager.instance != null && UIPanelManager.instance.IsGameInputActive()
                ? "game input is active"
                : "game input is blocked";
            string saveState = SaveManager.instance != null && (SaveManager.instance.isSaving || SaveManager.instance.isLoading)
                ? "save/load is busy"
                : "save/load is idle";
            return "Opening cutscene could not start from the editor context (" + inputState + ", "
                + saveState + ", timeScale=" + Time.timeScale + ").";
        }

        private static void SetBoolField(object target, string fieldName, bool value)
        {
            if (target == null || string.IsNullOrEmpty(fieldName))
                return;

            FieldInfo field = target.GetType().GetField(fieldName, InstanceAny);
            if (field != null && field.FieldType == typeof(bool))
                field.SetValue(target, value);
        }

        private static void SetIntField(object target, string fieldName, int value)
        {
            if (target == null || string.IsNullOrEmpty(fieldName))
                return;

            FieldInfo field = target.GetType().GetField(fieldName, InstanceAny);
            if (field != null && field.FieldType == typeof(int))
                field.SetValue(target, value);
        }

        private sealed class PreviewContext
        {
            public readonly ScenarioAuthoringState State;
            public readonly CutsceneManager Manager;
            public readonly Cutscene Cutscene;
            public readonly bool PreviousShellVisible;

            public PreviewContext(
                ScenarioAuthoringState state,
                CutsceneManager manager,
                Cutscene cutscene,
                bool previousShellVisible)
            {
                State = state;
                Manager = manager;
                Cutscene = cutscene;
                PreviousShellVisible = previousShellVisible;
            }
        }
    }
}
