using ShelteredScenarioEditor.Application.Commands;

namespace ShelteredScenarioEditor.Application.Authoring
{
    internal sealed class ScenarioAuthoringCommandService
    {
        private readonly ScenarioCommandDispatcher _dispatcher;
        private readonly ScenarioDraftSnapshotService _snapshots;

        public ScenarioAuthoringCommandService(
            ScenarioCommandDispatcher dispatcher,
            ScenarioDraftSnapshotService snapshots)
        {
            _dispatcher = dispatcher;
            _snapshots = snapshots;
        }

        public ScenarioCommandExecutionResult ExecuteWithResult(
            ScenarioAuthoringState state,
            ScenarioAuthoringCommand command)
        {
            string automationId = command != null ? command.AutomationId : null;
            if (state == null || command == null)
                return ScenarioCommandExecutionResult.Unavailable(automationId, "Scenario authoring is not active.");

            ScenarioGlobalSearchRouteCommand route = command as ScenarioGlobalSearchRouteCommand;
            if (route != null)
            {
                if (route.Steps == null || route.Steps.Length == 0)
                    return ScenarioCommandExecutionResult.Failure(automationId, "Search result route was empty.", state.StatusMessage);

                bool changed = false;
                string lastMessage = null;
                for (int i = 0; i < route.Steps.Length; i++)
                {
                    ScenarioGlobalSearchRouteStep step = route.Steps[i];
                    ScenarioCommandExecutionResult stepResult = ExecuteWithResult(state, step.Command);
                    if (!stepResult.Available || !stepResult.Changed)
                    {
                        string reason = !string.IsNullOrEmpty(stepResult.Reason)
                            ? stepResult.Reason
                            : "Search result route was not handled: " + step.AutomationId;
                        return ScenarioCommandExecutionResult.Failure(automationId, reason, state.StatusMessage);
                    }

                    changed = true;
                    if (!string.IsNullOrEmpty(stepResult.StatusMessage))
                        lastMessage = stepResult.StatusMessage;
                }

                state.GlobalSearchOpen = false;
                state.StatusMessage = string.IsNullOrEmpty(lastMessage) ? "Search result opened." : lastMessage;
                return ScenarioCommandExecutionResult.Success(automationId, changed, state.StatusMessage);
            }

            if (state.ReloadPending && !command.Policy.AllowedDuringReload)
            {
                string reason = string.IsNullOrEmpty(state.ReloadPendingReason)
                    ? "Scenario world is reloading; controls are disabled until the editor reconnects."
                    : state.ReloadPendingReason;
                return ScenarioCommandExecutionResult.Failure(automationId, reason, reason);
            }

            if (state.WorldLoading && command.Policy.RequiresWorld)
            {
                string reason = string.IsNullOrEmpty(state.WorldLoadingStatus)
                    ? "Loading game - world actions are disabled until the shelter is ready."
                    : state.WorldLoadingStatus;
                return ScenarioCommandExecutionResult.Failure(automationId, reason, reason);
            }

            if (_snapshots != null && command.Policy.CreatesSafetySnapshot)
            {
                string autosaveError;
                _snapshots.TryAutosaveCurrent("major editor action", out autosaveError);
            }

            string beforeStatus = state.StatusMessage;
            ScenarioCommandDispatchResult dispatch = _dispatcher.DispatchDetailed(state, command);
            if (!string.IsNullOrEmpty(dispatch.Message))
                state.StatusMessage = dispatch.Message;
            if (dispatch.Changed)
                return ScenarioCommandExecutionResult.Success(automationId, true, state.StatusMessage);

            string reasonMessage = !string.IsNullOrEmpty(dispatch.Message)
                ? dispatch.Message
                : (dispatch.Handled ? "Command was handled but made no change." : "Command was not handled.");
            return ScenarioCommandExecutionResult.Failure(automationId, reasonMessage, state.StatusMessage ?? beforeStatus);
        }
    }
}
