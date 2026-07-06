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
        private readonly ScenarioAuthoringSetupStateService _setupStateService;
        private string _activationDraftId;
        private int _activationStep = -1;
        private bool _lastPredicateSatisfied;
        private bool _activationRendered;
        private string _activeTourId;
        private int _activeTourStep;

        public ScenarioAuthoringTutorialService(
            ScenarioAuthoringSettingsService settingsService,
            ScenarioAuthoringSetupStateService setupStateService)
        {
            _settingsService = settingsService;
            _setupStateService = setupStateService;
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
            progress.HelpTopicId = settings.Get(TutorialContent.HelpTopicKey, ResolveHelpTopicId(progress.HelpPage));
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
            if (state != null && state.HelpWindowOpen)
                return false;

            TutorialStep step = GetActiveStep(state);
            if (step == null)
            {
                ClearActivation();
                return false;
            }

            bool satisfied = IsStepSatisfied(state, editorSession, step);
            EnsureActivation(state, step, satisfied);
            bool changedAfterActivation = !_lastPredicateSatisfied && satisfied;
            _lastPredicateSatisfied = satisfied;

            if (!_activationRendered || !changedAfterActivation)
                return false;

            return Advance(state, false, out message);
        }

        public void MarkStepRendered(ScenarioAuthoringState state, ScenarioEditorSession editorSession, TutorialStep step)
        {
            if (state == null || step == null || state.HelpWindowOpen)
                return;

            bool satisfied = IsStepSatisfied(state, editorSession, step);
            EnsureActivation(state, step, satisfied);
            _activationRendered = true;
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

        public bool OpenHelpTopic(
            ScenarioAuthoringState state,
            string topicId,
            ScenarioAuthoringLayoutService layoutService,
            out string message)
        {
            message = null;
            if (state == null || state.Settings == null)
                return false;

            int pageIndex = TutorialContent.FindHelpPageIndex(topicId);
            if (pageIndex < 0)
            {
                message = "Help topic is not available.";
                return true;
            }

            ScenarioAuthoringHelpPage page = TutorialContent.GetHelpPages()[pageIndex];
            TutorialProgress progress = Load(state.Settings);
            progress.HelpPage = pageIndex;
            progress.HelpTopicId = page != null ? page.Id : topicId;
            Save(state, progress);
            state.HelpWindowOpen = true;
            ApplyHelpNavigation(state, page, layoutService);
            message = page != null ? "Help opened: " + page.Title + "." : "Workshop help opened.";
            return true;
        }

        public bool StartTour(
            ScenarioAuthoringState state,
            string tourId,
            ScenarioAuthoringLayoutService layoutService,
            out string message)
        {
            message = null;
            ScenarioAuthoringTourDefinition tour = TutorialContent.FindTour(tourId);
            if (state == null || tour == null || tour.Steps == null || tour.Steps.Length == 0)
            {
                message = "Tour is not available.";
                return true;
            }

            _activeTourId = tour.Id;
            _activeTourStep = 0;
            state.HelpWindowOpen = false;
            ApplyTourOpenAction(state, CurrentTourStep(), layoutService);
            message = "Tour started: " + tour.Title + ".";
            return true;
        }

        public bool StepTour(
            ScenarioAuthoringState state,
            int direction,
            ScenarioAuthoringLayoutService layoutService,
            out string message)
        {
            message = null;
            ScenarioAuthoringTourDefinition tour = CurrentTour();
            if (tour == null)
                return false;

            int next = _activeTourStep + direction;
            if (next < 0)
                next = 0;

            if (next >= tour.Steps.Length)
                return CompleteTour(state, out message);

            _activeTourStep = next;
            ApplyTourOpenAction(state, CurrentTourStep(), layoutService);
            message = "Tour step " + (_activeTourStep + 1) + " of " + tour.Steps.Length + ".";
            return true;
        }

        public bool ExitTour(ScenarioAuthoringState state, out string message)
        {
            message = null;
            if (CurrentTour() == null)
                return false;

            string title = CurrentTour().Title;
            ClearTour();
            message = "Tour closed: " + title + ".";
            return true;
        }

        public ScenarioAuthoringTourDefinition CurrentTour()
        {
            return TutorialContent.FindTour(_activeTourId);
        }

        public ScenarioAuthoringTourStep CurrentTourStep()
        {
            ScenarioAuthoringTourDefinition tour = CurrentTour();
            if (tour == null || tour.Steps == null || tour.Steps.Length == 0)
                return null;

            int index = Math.Max(0, Math.Min(tour.Steps.Length - 1, _activeTourStep));
            return tour.Steps[index];
        }

        public int CurrentTourStepIndex
        {
            get { return _activeTourStep; }
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
                ClearActivation();
                message = manual ? "Tutorial complete." : "Tutorial completed.";
                return true;
            }

            progress.Step = next;
            Save(state, progress);
            ClearActivation();
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
            ClearActivation();
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
            ClearActivation();
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
            progress.HelpTopicId = ResolveHelpTopicId(next);
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
            state.Settings.Set(TutorialContent.HelpTopicKey, progress.HelpTopicId ?? ResolveHelpTopicId(progress.HelpPage));
            if (_settingsService != null)
                _settingsService.Save(state.Settings);
        }

        private bool CompleteTour(ScenarioAuthoringState state, out string message)
        {
            ScenarioAuthoringTourDefinition tour = CurrentTour();
            message = null;
            if (tour == null)
                return false;

            if (state != null && state.SetupState != null)
            {
                state.SetupState.AddCompletedTour(tour.Id);
                if (_setupStateService != null)
                    _setupStateService.SaveActive(state);
            }

            ClearTour();
            message = "Tour complete: " + tour.Title + ".";
            return true;
        }

        private void ClearTour()
        {
            _activeTourId = null;
            _activeTourStep = 0;
        }

        private static void ApplyHelpNavigation(
            ScenarioAuthoringState state,
            ScenarioAuthoringHelpPage page,
            ScenarioAuthoringLayoutService layoutService)
        {
            if (state == null || page == null || layoutService == null)
                return;

            if (page.Stage != ScenarioStageKind.None)
                layoutService.SelectStage(state, page.Stage);
            if (!string.IsNullOrEmpty(page.WindowId))
                layoutService.SetWindowOpen(state, page.WindowId, true);
        }

        private static void ApplyTourOpenAction(
            ScenarioAuthoringState state,
            ScenarioAuthoringTourStep step,
            ScenarioAuthoringLayoutService layoutService)
        {
            if (state == null || step == null || layoutService == null || string.IsNullOrEmpty(step.OpenAction))
                return;

            if (step.OpenAction.StartsWith(ScenarioAuthoringActionIds.ActionStageSelectPrefix, StringComparison.Ordinal))
            {
                string token = step.OpenAction.Substring(ScenarioAuthoringActionIds.ActionStageSelectPrefix.Length);
                ScenarioStageKind stage;
                if (TryParseStageKind(token, out stage))
                    layoutService.SelectStage(state, stage);
                return;
            }

            if (step.OpenAction.StartsWith(ScenarioAuthoringActionIds.ActionHelpOpenTopicPrefix, StringComparison.Ordinal))
            {
                string topicId = step.OpenAction.Substring(ScenarioAuthoringActionIds.ActionHelpOpenTopicPrefix.Length);
                ApplyHelpNavigation(state, TutorialContent.FindHelpPage(topicId), layoutService);
                return;
            }

            if (step.OpenAction.StartsWith(ScenarioAuthoringActionIds.ActionWindowTogglePrefix, StringComparison.Ordinal))
            {
                layoutService.SetWindowOpen(state, step.OpenAction.Substring(ScenarioAuthoringActionIds.ActionWindowTogglePrefix.Length), true);
                return;
            }

            if (string.Equals(step.OpenAction, ScenarioAuthoringActionIds.ActionToolSelect, StringComparison.Ordinal))
                layoutService.SelectTool(state, ScenarioAuthoringTool.Select);
            else if (string.Equals(step.OpenAction, ScenarioAuthoringActionIds.ActionToolObjects, StringComparison.Ordinal))
                layoutService.SelectTool(state, ScenarioAuthoringTool.Objects);
            else if (string.Equals(step.OpenAction, ScenarioAuthoringActionIds.ActionToolAssets, StringComparison.Ordinal))
                layoutService.SelectTool(state, ScenarioAuthoringTool.Assets);
        }

        private static bool TryParseStageKind(string token, out ScenarioStageKind stage)
        {
            stage = ScenarioStageKind.None;
            if (string.IsNullOrEmpty(token))
                return false;

            try
            {
                object parsed = Enum.Parse(typeof(ScenarioStageKind), token, true);
                if (parsed != null && Enum.IsDefined(typeof(ScenarioStageKind), parsed))
                {
                    stage = (ScenarioStageKind)parsed;
                    return true;
                }
            }
            catch
            {
            }

            return false;
        }

        private static string ResolveHelpTopicId(int pageIndex)
        {
            ScenarioAuthoringHelpPage[] pages = TutorialContent.GetHelpPages();
            if (pages == null || pages.Length == 0)
                return null;

            int index = Math.Max(0, Math.Min(pages.Length - 1, pageIndex));
            ScenarioAuthoringHelpPage page = pages[index];
            return page != null ? page.Id : null;
        }

        private void EnsureActivation(ScenarioAuthoringState state, TutorialStep step, bool satisfied)
        {
            string draftId = state != null ? state.ActiveDraftId : null;
            int stepIndex = step != null ? step.Index : -1;
            if (_activationStep == stepIndex && string.Equals(_activationDraftId, draftId, StringComparison.Ordinal))
                return;

            _activationDraftId = draftId;
            _activationStep = stepIndex;
            _lastPredicateSatisfied = satisfied;
            _activationRendered = false;
        }

        private void ClearActivation()
        {
            _activationDraftId = null;
            _activationStep = -1;
            _lastPredicateSatisfied = false;
            _activationRendered = false;
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
