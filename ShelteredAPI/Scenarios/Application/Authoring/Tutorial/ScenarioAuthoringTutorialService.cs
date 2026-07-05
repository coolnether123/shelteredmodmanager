using System;
using ModAPI.Scenarios;

using ShelteredAPI.Scenarios.Domain.Stages;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Presentation.Authoring.Shell;
using ShelteredAPI.Scenarios.Presentation.Authoring.Windows;

namespace ShelteredAPI.Scenarios.Application.Authoring.Tutorial{
    internal sealed class ScenarioAuthoringTutorialService
    {
        private readonly ScenarioAuthoringSettingsService _settingsService;

        public ScenarioAuthoringTutorialService(ScenarioAuthoringSettingsService settingsService)
        {
            _settingsService = settingsService;
        }

        public TutorialProgress Load(ScenarioAuthoringSettingsSnapshot settings)
        {
            TutorialProgress progress = new TutorialProgress();
            if (settings == null)
                return progress;

            progress.Completed = settings.GetBool(TutorialContent.CompletedKey, false);
            progress.Skipped = settings.GetBool(TutorialContent.SkippedKey, false);
            progress.Step = ClampStep(settings.GetInt(TutorialContent.StepKey, 0));
            progress.HelpPage = ClampHelpPage(settings.GetInt(TutorialContent.HelpPageKey, 0));
            return progress;
        }

        public TutorialStep GetActiveStep(ScenarioAuthoringState state)
        {
            TutorialProgress progress = Load(state != null ? state.Settings : null);
            if (!ShouldShowTour(state, progress))
                return null;

            TutorialStep[] steps = TutorialContent.GetSteps();
            int index = ClampStep(progress.Step);
            return index >= 0 && index < steps.Length ? steps[index] : null;
        }

        public bool ShouldShowTour(ScenarioAuthoringState state, TutorialProgress progress)
        {
            return state != null
                && state.IsActive
                && !string.IsNullOrEmpty(state.ActiveDraftId)
                && progress != null
                && !progress.Completed
                && !progress.Skipped;
        }

        public bool Synchronize(ScenarioAuthoringState state, ScenarioEditorSession editorSession, out string message)
        {
            message = null;
            TutorialStep step = GetActiveStep(state);
            if (step == null)
                return false;

            if (!IsStepSatisfied(state, editorSession, step))
                return false;

            return Advance(state, false, out message);
        }

        public bool HandleAction(ScenarioAuthoringState state, string actionId, out string message)
        {
            message = null;
            if (state == null || string.IsNullOrEmpty(actionId))
                return false;

            if (string.Equals(actionId, ScenarioAuthoringActionIds.ActionTutorialNext, StringComparison.Ordinal))
                return Advance(state, true, out message);

            if (string.Equals(actionId, ScenarioAuthoringActionIds.ActionTutorialSkip, StringComparison.Ordinal))
                return Skip(state, out message);

            if (string.Equals(actionId, ScenarioAuthoringActionIds.ActionTutorialReset, StringComparison.Ordinal))
                return Reset(state, out message);

            if (string.Equals(actionId, ScenarioAuthoringActionIds.ActionHelpPageNext, StringComparison.Ordinal))
                return StepHelpPage(state, 1, out message);

            if (string.Equals(actionId, ScenarioAuthoringActionIds.ActionHelpPagePrevious, StringComparison.Ordinal))
                return StepHelpPage(state, -1, out message);

            return false;
        }

        public bool IsStepSatisfied(ScenarioAuthoringState state, ScenarioEditorSession editorSession, TutorialStep step)
        {
            if (state == null || step == null)
                return false;

            if (string.Equals(step.TargetActionId, "playtest", StringComparison.Ordinal))
                return editorSession != null && editorSession.PlaytestState == ScenarioPlaytestState.Playtesting;

            if (string.Equals(step.Id, "supplies", StringComparison.OrdinalIgnoreCase)
                && HasStockpileEdited(editorSession))
                return true;

            if (!string.IsNullOrEmpty(step.TargetWindowId))
                return IsWindowVisible(state, step.TargetWindowId);

            if (step.TargetStage != ScenarioStageKind.None)
                return state.ActiveStage == step.TargetStage;

            return false;
        }

        private static bool HasStockpileEdited(ScenarioEditorSession editorSession)
        {
            ScenarioDefinition definition = editorSession != null ? editorSession.WorkingDefinition : null;
            StartingInventoryDefinition inventory = definition != null ? definition.StartingInventory : null;
            return inventory != null
                && (inventory.OverrideRandomStart
                    || (inventory.Items != null && inventory.Items.Count > 0)
                    || (inventory.ScheduledChanges != null && inventory.ScheduledChanges.Count > 0));
        }

        public bool OpenStepTarget(ScenarioAuthoringState state, TutorialStep step, ScenarioAuthoringLayoutService layoutService, out string message)
        {
            message = null;
            if (state == null || step == null)
                return false;

            if (!string.IsNullOrEmpty(step.TargetWindowId) && layoutService != null)
            {
                bool changed = layoutService.SetWindowOpen(state, step.TargetWindowId, true);
                message = changed ? "Tutorial panel opened." : "Tutorial panel is already open.";
                return true;
            }

            if (step.TargetStage != ScenarioStageKind.None && layoutService != null)
            {
                bool changed = layoutService.SelectStage(state, step.TargetStage);
                message = changed ? "Tutorial workspace opened." : "Tutorial workspace is already open.";
                return true;
            }

            message = "Follow the highlighted action to continue.";
            return true;
        }

        private bool Advance(ScenarioAuthoringState state, bool manual, out string message)
        {
            message = null;
            if (state == null || state.Settings == null)
                return false;

            TutorialProgress progress = Load(state.Settings);
            if (!ShouldShowTour(state, progress))
                return false;

            TutorialStep[] steps = TutorialContent.GetSteps();
            int next = ClampStep(progress.Step) + 1;
            if (next >= steps.Length)
            {
                progress.Completed = true;
                progress.Step = steps.Length - 1;
                Save(state, progress);
                message = manual ? "Tutorial complete." : "Tutorial completed.";
                return true;
            }

            progress.Step = next;
            Save(state, progress);
            message = manual ? "Tutorial advanced." : "Tutorial step complete.";
            return true;
        }

        private bool Skip(ScenarioAuthoringState state, out string message)
        {
            message = null;
            if (state == null || state.Settings == null)
                return false;

            TutorialProgress progress = Load(state.Settings);
            progress.Skipped = true;
            Save(state, progress);
            message = "Tutorial skipped. Help remains available.";
            return true;
        }

        private bool Reset(ScenarioAuthoringState state, out string message)
        {
            message = null;
            if (state == null || state.Settings == null)
                return false;

            TutorialProgress progress = Load(state.Settings);
            progress.Completed = false;
            progress.Skipped = false;
            progress.Step = 0;
            state.HelpWindowOpen = false;
            Save(state, progress);
            message = "Tutorial replay started.";
            return true;
        }

        private bool StepHelpPage(ScenarioAuthoringState state, int direction, out string message)
        {
            message = null;
            if (state == null || state.Settings == null)
                return false;

            TutorialProgress progress = Load(state.Settings);
            int current = ClampHelpPage(progress.HelpPage);
            int next = ClampHelpPage(current + direction);
            if (next == current)
                return false;

            progress.HelpPage = next;
            Save(state, progress);
            message = "Help page " + (next + 1) + " of " + TutorialContent.GetHelpPages().Length + ".";
            return true;
        }

        private void Save(ScenarioAuthoringState state, TutorialProgress progress)
        {
            state.Settings.Set(TutorialContent.CompletedKey, progress.Completed ? "true" : "false");
            state.Settings.Set(TutorialContent.SkippedKey, progress.Skipped ? "true" : "false");
            state.Settings.Set(TutorialContent.StepKey, progress.Step.ToString());
            state.Settings.Set(TutorialContent.HelpPageKey, progress.HelpPage.ToString());
            if (_settingsService != null)
                _settingsService.Save(state.Settings);
        }

        private static bool IsWindowVisible(ScenarioAuthoringState state, string windowId)
        {
            for (int i = 0; state != null && state.WindowStates != null && i < state.WindowStates.Count; i++)
            {
                ScenarioAuthoringWindowState window = state.WindowStates[i];
                if (window != null
                    && string.Equals(window.Id, windowId, StringComparison.OrdinalIgnoreCase)
                    && window.Visible
                    && !window.Collapsed)
                {
                    return true;
                }
            }

            return false;
        }

        private static int ClampStep(int step)
        {
            TutorialStep[] steps = TutorialContent.GetSteps();
            if (steps == null || steps.Length == 0)
                return 0;
            return Math.Max(0, Math.Min(steps.Length - 1, step));
        }

        private static int ClampHelpPage(int page)
        {
            ScenarioAuthoringHelpPage[] pages = TutorialContent.GetHelpPages();
            if (pages == null || pages.Length == 0)
                return 0;
            return Math.Max(0, Math.Min(pages.Length - 1, page));
        }
    }
}
