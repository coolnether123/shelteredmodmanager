using System;

using ShelteredAPI.Scenarios.Application.Authoring;
using ShelteredAPI.Scenarios.Application.Authoring.Tutorial;
using ShelteredAPI.Scenarios.Presentation.Authoring.Shell;

namespace ShelteredAPI.Scenarios.Application.Commands{
    internal sealed class ScenarioHelpCommandHandler : IScenarioCommandHandler
    {
        private readonly ScenarioAuthoringTutorialService _tutorialService;
        private readonly ScenarioAuthoringLayoutService _layoutService;
        private readonly ScenarioAuthoringSetupStateService _setupStateService;

        public ScenarioHelpCommandHandler(
            ScenarioAuthoringTutorialService tutorialService,
            ScenarioAuthoringLayoutService layoutService,
            ScenarioAuthoringSetupStateService setupStateService)
        {
            _tutorialService = tutorialService;
            _layoutService = layoutService;
            _setupStateService = setupStateService;
        }

        public bool TryHandle(ScenarioAuthoringState state, string actionId, out bool handled, out string message)
        {
            handled = false;
            message = null;
            if (state == null || string.IsNullOrEmpty(actionId))
                return false;

            if (actionId.StartsWith(ScenarioAuthoringActionIds.ActionHelpOpenTopicPrefix, StringComparison.Ordinal))
            {
                handled = true;
                string topicId = actionId.Substring(ScenarioAuthoringActionIds.ActionHelpOpenTopicPrefix.Length);
                return _tutorialService != null
                    && _tutorialService.OpenHelpTopic(state, topicId, _layoutService, out message);
            }

            if (actionId.StartsWith(ScenarioAuthoringActionIds.ActionTourStartPrefix, StringComparison.Ordinal))
            {
                handled = true;
                string tourId = actionId.Substring(ScenarioAuthoringActionIds.ActionTourStartPrefix.Length);
                return _tutorialService != null
                    && _tutorialService.StartTour(state, tourId, _layoutService, out message);
            }

            if (string.Equals(actionId, ScenarioAuthoringActionIds.ActionTourNext, StringComparison.Ordinal))
            {
                handled = true;
                return _tutorialService != null
                    && _tutorialService.StepTour(state, 1, _layoutService, out message);
            }

            if (string.Equals(actionId, ScenarioAuthoringActionIds.ActionTourBack, StringComparison.Ordinal))
            {
                handled = true;
                return _tutorialService != null
                    && _tutorialService.StepTour(state, -1, _layoutService, out message);
            }

            if (string.Equals(actionId, ScenarioAuthoringActionIds.ActionTourExit, StringComparison.Ordinal))
            {
                handled = true;
                return _tutorialService != null
                    && _tutorialService.ExitTour(state, out message);
            }

            if (string.Equals(actionId, ScenarioAuthoringActionIds.ActionSetupDismiss, StringComparison.Ordinal))
            {
                handled = true;
                if (state.SetupState == null)
                    state.SetupState = new ScenarioAuthoringSetupState();

                state.SetupState.ChecklistDismissed = true;
                if (_setupStateService != null)
                    _setupStateService.SaveActive(state);
                message = "Scenario setup checklist dismissed.";
                return true;
            }

            return false;
        }
    }
}
